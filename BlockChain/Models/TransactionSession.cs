using System.Collections.ObjectModel;

namespace BlockChain.Models;

public sealed class TransactionSession
{
    private readonly Dictionary<string, Transaction> _pending = new(StringComparer.Ordinal);
    private Transaction[]? _sortedPending;

    internal IReadOnlyDictionary<string, Transaction> Pending => PendingTransactions;

    internal List<RejectedSubmission> Rejections { get; } = [];
    
    public LedgerSnapshot Ledger { get; internal set; }
    
    public long InitialTotal { get; }
    
    public long SubmissionCount { get; internal set; }

    public IReadOnlyDictionary<string, Transaction> PendingTransactions
    {
        get;
    }
    public IReadOnlyList<RejectedSubmission> RejectedSubmissions
    {
        get;
    }

    internal TransactionSession(
        LedgerSnapshot ledger,
        long initialTotal)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        
        Ledger = ledger;
        InitialTotal = initialTotal;
        
        PendingTransactions = new ReadOnlyDictionary<string, Transaction>(_pending);
        
        RejectedSubmissions = Rejections.AsReadOnly();
    }

    internal void AddPending(Transaction transaction)
    {
        _pending.Add(transaction.TransactionId, transaction);
        _sortedPending = null;
    }

    internal bool RemovePending(string transactionId)
    {
        if (!_pending.Remove(transactionId))
            return false;

        _sortedPending = null;
        return true;
    }

    internal Transaction[] CopySortedPending()
    {
        _sortedPending ??= _pending.Values
            .OrderBy(transaction => transaction.TransactionId, StringComparer.Ordinal)
            .ToArray();

        // Each candidate shuffles its own array.
        return (Transaction[])_sortedPending.Clone();
    }
}
