namespace BlockChain.Models;

public sealed record BlockReport
{
    public int Height { get; }

    public Block Block { get; }

    public ulong Attempts { get; }

    public TimeSpan MiningTime { get; }

    public IReadOnlyList<RemainingTransaction> Deferred { get; }

    public BlockReport(
        int height,
        Block block,
        ulong attempts,
        TimeSpan miningTime,
        IReadOnlyList<RemainingTransaction> deferred)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(deferred);

        Height = height;
        Block = block;
        Attempts = attempts;
        MiningTime = miningTime;
        Deferred = Array.AsReadOnly(deferred.ToArray());
    }
}

public sealed record SubmissionOutcome(
    Transaction Transaction,
    AdmissionResult Result);

public sealed record RunReport
{
    public SimulationConfig Configuration { get; }

    public int UserCount { get; }

    public int GeneratedCount { get; }

    public int SubmissionCount { get; }

    public IReadOnlyList<BlockReport> Blocks { get; }

    public IReadOnlyList<RejectedSubmission> Rejections { get; }

    public IReadOnlyList<RemainingTransaction> Remaining { get; }

    public IReadOnlyList<SubmissionOutcome> PostRunOutcomes { get; }

    public long InitialTotal { get; }

    public LedgerSnapshot FinalLedger { get; }

    public TimeSpan TotalTime { get; }

    public long FinalTotal => FinalLedger.Balances.Values.Sum();

    public bool FundsConserved => FinalTotal == InitialTotal;

    public int IncludedCount => Blocks.Sum(b => b.Block.Transactions.Count);

    public bool AccountingHolds => GeneratedCount == IncludedCount + Rejections.Count + Remaining.Count;

    public TimeSpan MiningTime => TimeSpan.FromTicks(Blocks.Sum(b => b.MiningTime.Ticks));

    public RunReport(
        SimulationConfig configuration,
        int userCount,
        int generatedCount,
        int submissionCount,
        IReadOnlyList<BlockReport> blocks,
        IReadOnlyList<RejectedSubmission> rejections,
        IReadOnlyList<RemainingTransaction> remaining,
        IReadOnlyList<SubmissionOutcome> postRunOutcomes,
        long initialTotal,
        LedgerSnapshot finalLedger,
        TimeSpan totalTime)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(rejections);
        ArgumentNullException.ThrowIfNull(remaining);
        ArgumentNullException.ThrowIfNull(postRunOutcomes);
        ArgumentNullException.ThrowIfNull(finalLedger);

        Configuration = configuration;
        UserCount = userCount;
        GeneratedCount = generatedCount;
        SubmissionCount = submissionCount;
        Blocks = Array.AsReadOnly(blocks.ToArray());
        Rejections = Array.AsReadOnly(rejections.ToArray());
        Remaining = Array.AsReadOnly(remaining.ToArray());
        PostRunOutcomes = Array.AsReadOnly(postRunOutcomes.ToArray());
        InitialTotal = initialTotal;
        FinalLedger = finalLedger;
        TotalTime = totalTime;
    }
}