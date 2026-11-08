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

        if (candidates.Count != CandidateCount ||
            candidates.Any(c => c.Transactions.Count is < 1 or > 100))
        {
            throw new ArgumentException(
                "Provide five candidates with 1 to 100 transactions each.",
                nameof(candidates));
        }

        if (initialAttempts == 0)
            throw new ArgumentOutOfRangeException(nameof(initialAttempts));

        if (maxAttemptsPerCandidate == 0 ||
            maxAttemptsPerCandidate > ulong.MaxValue / CandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttemptsPerCandidate));
        }

        var stopwatch = Stopwatch.StartNew();

        // One progress record for each candidate.
        CandidateMiningReport[] progress = candidates
            .Select(candidate => new CandidateMiningReport(
                CandidateNumber: candidate.Number,
                Attempts: 0,
                NextNonce: candidate.Header.Nonce,
                MiningTime: TimeSpan.Zero,
                LastStatus: null,
                AttemptLimitReached: false))
            .ToArray();

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

        ulong budget = Math.Min(initialAttempts, maxAttemptsPerCandidate);

        while (progress.Any(p =>
                   p.NextNonce.HasValue && !p.AttemptLimitReached))
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                CandidateMiningReport current = progress[index];

                if (current.NextNonce is not { } nextNonce ||
                    current.AttemptLimitReached)
                {
                    continue;
                }

                MiningCandidate candidate = candidates[index];

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

            // Increase the budget safely for the next round.
            budget = budget > maxAttemptsPerCandidate / 2
                ? maxAttemptsPerCandidate
                : budget * 2;
        }

        return Finish(winner: null, block: null);
    }
}