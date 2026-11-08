using System.Diagnostics;
using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Simulation;

public sealed class Runner
{
    private readonly ITransactionService _service;

    public Runner(ITransactionService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    public RunReport Run(SimulationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        SimulationConfig config = input.Configuration;
        var stopwatch = Stopwatch.StartNew();

        TransactionSession session = _service.CreateSession(input.Users);
        foreach (Transaction transaction in input.Transactions)
            _service.AddToPool(session, transaction);

        var chain = new Chain(_service, config.Difficulty, config.CandidateLimit);
        var selectionRandom = new Random(config.SelectionSeed);
        var blocks = new List<BlockReport>();

        while (true)
        {
            int height = chain.Height + 1;

            var candidates = new List<MiningCandidate>(
                MiningCoordinator.CandidateCount);

            // Build all five before mining or accepting anything.
            for (int number = 1;
                 number <= MiningCoordinator.CandidateCount;
                 number++)
            {
                CandidateSelection selection = _service.SelectCandidate(
                    session,
                    selectionRandom,
                    config.CandidateLimit);

                var header = new BlockHeader(
                    PrevHash: chain.TipHash,
                    Timestamp: checked(config.TimestampBase + height),
                    Version: BlockValidator.SupportedVersion,
                    TxRoot: TxRoot.Compute(selection.SelectedTransactions),
                    Nonce: 0,
                    Difficulty: config.Difficulty);

                candidates.Add(new MiningCandidate(
                    number,
                    header,
                    selection.SelectedTransactions,
                    selection.DeferredTransactions));
            }

            if (candidates.All(candidate => candidate.Transactions.Count == 0))
                break;

            MiningCompetitionResult competition = MiningCoordinator.Mine(
                candidates,
                initialAttempts: config.InitialMiningAttempts,
                maxAttemptsPerCandidate: config.MaxMiningAttemptsPerCandidate);

            if (competition.Winner is not { } winner ||
                competition.Block is not { } minedBlock)
            {
                string details = string.Join(
                    Environment.NewLine,
                    competition.Candidates.Select(candidate =>
                        $"  Candidate {candidate.CandidateNumber}: " +
                        $"{candidate.Attempts} attempts, " +
                        $"{candidate.MiningTime.TotalMilliseconds:F1} ms, " +
                        $"last status {candidate.LastStatus}, " +
                        $"attempt limit reached {candidate.AttemptLimitReached}"));

                throw new InvalidOperationException(
                    $"Mining stopped at height {height}. " +
                    "All candidates exhausted their attempt or nonce limits." +
                    Environment.NewLine +
                    details);
            }

            AcceptResult accepted = chain.TryAccept(minedBlock, session);

            if (!accepted.Accepted)
            {
                throw new InvalidOperationException(
                    $"Own block at height {height} was rejected " +
                    $"({accepted.BlockReason}, {accepted.TransactionReason}): " +
                    accepted.Explanation);
            }

            CandidateMiningReport winnerReport = competition.Candidates
                .Single(candidate => candidate.CandidateNumber == winner.Number);

            blocks.Add(new BlockReport(
                height,
                minedBlock,
                winnerReport.Attempts,
                competition.Elapsed,
                winner.DeferredTransactions)
            {
                Competition = competition
            });
        }

        IReadOnlyList<RemainingTransaction> remaining = _service.ExplainRemaining(session);
        RejectedSubmission[] rejections = session.RejectedSubmissions.ToArray();
        LedgerSnapshot finalLedger = session.Ledger;

        SubmissionOutcome[] postRunOutcomes = input.PostRunSubmissions
            .Select(transaction => new SubmissionOutcome(transaction, _service.AddToPool(session, transaction)))
            .ToArray();

        stopwatch.Stop();

        return new RunReport(
            config,
            input.Users.Count,
            input.Transactions.Count,
            input.Transactions.Count + input.PostRunSubmissions.Count,
            blocks,
            rejections,
            remaining,
            postRunOutcomes,
            input.Users.Sum(u => u.InitialBalance),
            finalLedger,
            stopwatch.Elapsed);
    }
}