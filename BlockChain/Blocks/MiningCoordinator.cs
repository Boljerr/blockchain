using System.Diagnostics;
using BlockChain.Models;

namespace BlockChain.Blocks;

public static class MiningCoordinator
{
    public const int CandidateCount = 5;

    public static MiningCompetitionResult Mine(
        IReadOnlyList<MiningCandidate> candidates,
        ulong initialAttempts = 1_000,
        ulong maxAttemptsPerCandidate = Miner.DefaultMaxAttempts)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        MiningCandidate[] snapshot = candidates.ToArray();

        ValidateInputs(
            snapshot,
            initialAttempts,
            maxAttemptsPerCandidate);

        CandidateMiningReport[] progress = snapshot
            .Select(candidate => new CandidateMiningReport(
                candidate.Number,
                Attempts: 0,
                NextNonce: candidate.Header.Nonce,
                MiningTime: TimeSpan.Zero,
                LastStatus: null,
                AttemptLimitReached: false))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();

        MiningCompetitionResult Finish(
            MiningCandidate? winner,
            Block? block)
        {
            stopwatch.Stop();

            return new MiningCompetitionResult(
                winner,
                block,
                Array.AsReadOnly(progress),
                stopwatch.Elapsed);
        }

        ulong budget = Math.Min(
            initialAttempts,
            maxAttemptsPerCandidate);

        while (progress.Any(candidate =>
                   candidate.NextNonce.HasValue &&
                   !candidate.AttemptLimitReached))
        {
            for (int index = 0; index < snapshot.Length; index++)
            {
                CandidateMiningReport current = progress[index];

                if (current.NextNonce is not { } nextNonce ||
                    current.AttemptLimitReached)
                {
                    continue;
                }

                MiningCandidate candidate = snapshot[index];

                ulong remaining =
                    maxAttemptsPerCandidate - current.Attempts;

                MiningSearchResult search = Miner.SearchRange(
                    candidate.Header,
                    candidate.Transactions,
                    nextNonce,
                    Math.Min(budget, remaining));

                ulong attempts = current.Attempts + search.Attempts;

                progress[index] = current with
                {
                    Attempts = attempts,
                    NextNonce = search.NextNonce,
                    MiningTime = current.MiningTime + search.Elapsed,
                    LastStatus = search.Status,
                    AttemptLimitReached =
                        attempts >= maxAttemptsPerCandidate &&
                        search.Block is null
                };

                if (search.Block is { } block)
                    return Finish(candidate, block);
            }

            budget = budget > maxAttemptsPerCandidate / 2
                ? maxAttemptsPerCandidate
                : budget * 2;
        }

        return Finish(winner: null, block: null);
    }

    public static async Task<MiningCompetitionResult> MineParallelAsync(
        IReadOnlyList<MiningCandidate> candidates,
        ulong initialAttempts = 1_000,
        ulong maxAttemptsPerCandidate = Miner.DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        MiningCandidate[] snapshot = candidates.ToArray();

        ValidateInputs(
            snapshot,
            initialAttempts,
            maxAttemptsPerCandidate);

        cancellationToken.ThrowIfCancellationRequested();

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var winnerLock = new object();

        MiningCandidate? winner = null;
        Block? winningBlock = null;

        CandidateMiningReport SearchCandidate(MiningCandidate candidate)
        {
            var progress = new CandidateMiningReport(
                candidate.Number,
                Attempts: 0,
                NextNonce: candidate.Header.Nonce,
                MiningTime: TimeSpan.Zero,
                LastStatus: null,
                AttemptLimitReached: false);

            ulong budget = Math.Min(
                initialAttempts,
                maxAttemptsPerCandidate);

            try
            {
                while (progress.NextNonce is { } nextNonce &&
                       !progress.AttemptLimitReached)
                {
                    ulong remaining =
                        maxAttemptsPerCandidate - progress.Attempts;

                    MiningSearchResult search = Miner.SearchRange(
                        candidate.Header,
                        candidate.Transactions,
                        nextNonce,
                        Math.Min(budget, remaining),
                        stop.Token);

                    ulong attempts = progress.Attempts + search.Attempts;

                    progress = progress with
                    {
                        Attempts = attempts,
                        NextNonce = search.NextNonce,
                        MiningTime = progress.MiningTime + search.Elapsed,
                        LastStatus = search.Status,
                        AttemptLimitReached =
                            attempts >= maxAttemptsPerCandidate &&
                            search.Block is null
                    };

                    if (search.Block is { } block)
                    {
                        // Only one worker can register the winner.
                        lock (winnerLock)
                        {
                            if (winner is null &&
                                !stop.IsCancellationRequested)
                            {
                                winner = candidate;
                                winningBlock = block;
                                stop.Cancel();
                            }
                        }

                        break;
                    }

                    if (search.Status == MiningSearchStatus.Cancelled)
                        break;

                    budget = budget > maxAttemptsPerCandidate / 2
                        ? maxAttemptsPerCandidate
                        : budget * 2;
                }

                return progress;
            }
            catch
            {
                // Stop competing searches if a worker fails
                stop.Cancel();
                throw;
            }
        }

        Task<CandidateMiningReport>[] workers = snapshot
            .Select(candidate =>
                Task.Run(() => SearchCandidate(candidate)))
            .ToArray();

        // Collect reports after every worker has stopped.
        CandidateMiningReport[] reports =
            await Task.WhenAll(workers).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();

        return new MiningCompetitionResult(
            winner,
            winningBlock,
            Array.AsReadOnly(reports),
            stopwatch.Elapsed);
    }

    private static void ValidateInputs(
        IReadOnlyList<MiningCandidate> candidates,
        ulong initialAttempts,
        ulong maxAttemptsPerCandidate)
    {
        if (candidates.Count != CandidateCount ||
            candidates.Any(candidate => candidate is null ||
                candidate.Transactions.Count is < 1 or > 100))
        {
            throw new ArgumentException(
                $"Provide {CandidateCount} candidates with 1 to 100 transactions each.",
                nameof(candidates));
        }

        if (initialAttempts == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialAttempts),
                "The initial attempt budget must be positive.");
        }

        if (maxAttemptsPerCandidate == 0 ||
            maxAttemptsPerCandidate > ulong.MaxValue / CandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttemptsPerCandidate),
                "The attempt limit must be positive and allow a safe total.");
        }
    }
}