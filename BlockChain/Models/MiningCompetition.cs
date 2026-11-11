namespace BlockChain.Models;

public sealed class MiningCandidate
{
    public int Number { get; }
    public BlockHeader Header { get; }
    public IReadOnlyList<Transaction> Transactions { get; }
    public IReadOnlyList<RemainingTransaction> DeferredTransactions { get; }

    public MiningCandidate(
        int number,
        BlockHeader header,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<RemainingTransaction> deferredTransactions)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(deferredTransactions);

        Number = number;
        Header = header;

        Transactions = Array.AsReadOnly(transactions.ToArray());
        DeferredTransactions =
            Array.AsReadOnly(deferredTransactions.ToArray());
    }
}

public sealed record CandidateMiningReport(
    int CandidateNumber,
    ulong Attempts,
    ulong? NextNonce,
    TimeSpan MiningTime,
    MiningSearchStatus? LastStatus,
    bool AttemptLimitReached);

public sealed record MiningCompetitionResult(
    MiningCandidate? Winner,
    Block? Block,
    IReadOnlyList<CandidateMiningReport> Candidates,
    TimeSpan Elapsed)
{
    public bool Success => Winner is not null && Block is not null;

    public ulong TotalAttempts => Candidates.Aggregate(0UL,
        (total, candidate) => checked(total + candidate.Attempts));
}