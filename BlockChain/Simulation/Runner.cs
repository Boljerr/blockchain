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
            CandidateSelection selection = _service.SelectCandidate(session, selectionRandom, config.CandidateLimit);
            if (selection.SelectedTransactions.Count == 0)
                break;

            int height = chain.Height + 1;
            var template = new BlockHeader(
                PrevHash: chain.TipHash,
                Timestamp: config.TimestampBase + height,
                Version: BlockValidator.SupportedVersion,
                TxRoot: TxRoot.Compute(selection.SelectedTransactions),
                Nonce: 0,
                Difficulty: config.Difficulty);

            MineResult mined = Miner.Mine(template, selection.SelectedTransactions);

            AcceptResult accepted = chain.TryAccept(mined.Block, session);
            if (!accepted.Accepted)
                throw new InvalidOperationException(
                    $"Own block at height {height} was rejected " +
                    $"({accepted.BlockReason}, {accepted.TransactionReason}): {accepted.Explanation}");

            blocks.Add(new BlockReport(height, mined.Block, mined.Attempts, mined.Elapsed, selection.DeferredTransactions));
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