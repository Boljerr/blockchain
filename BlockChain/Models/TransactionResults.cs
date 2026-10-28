namespace BlockChain.Models;

public sealed record AdmissionResult(
    bool Accepted,
    TransactionReason ReasonCode,
    string Explanation);

public sealed record RemainingTransaction(
    Transaction Transaction,
    TransactionReason ReasonCode,
    string Explanation);

public sealed record CandidateSelection(
    IReadOnlyList<Transaction> SelectedTransactions,
    IReadOnlyList<RemainingTransaction> DeferredTransactions);

public sealed class ApplyResult
{
    public bool Success { get; }
    public TransactionReason ReasonCode { get; }
    public string Explanation { get; }

    public LedgerSnapshot OriginalLedger { get; }
    public LedgerSnapshot? ProposedLedger { get; }
    public IReadOnlyList<string> IncludedIds { get; }

    internal ApplyResult(
        bool success,
        TransactionReason reasonCode,
        string explanation,
        LedgerSnapshot originalLedger,
        LedgerSnapshot? proposedLedger,
        IReadOnlyList<string> includedIds)
    {
        Success = success;
        ReasonCode = reasonCode;
        Explanation = explanation;
        OriginalLedger = originalLedger;
        ProposedLedger = proposedLedger;

        IncludedIds = Array.AsReadOnly(includedIds.ToArray());
    }
}