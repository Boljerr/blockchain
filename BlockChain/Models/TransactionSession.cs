using System.Collections.ObjectModel;

namespace BlockChain.Models;

public sealed class TransactionSession
{
    internal Dictionary<string, Transaction> Pending { get; } = new(StringComparer.Ordinal);

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
        
        PendingTransactions = new ReadOnlyDictionary<string, Transaction>(Pending);
        
        RejectedSubmissions = Rejections.AsReadOnly();
    }
}