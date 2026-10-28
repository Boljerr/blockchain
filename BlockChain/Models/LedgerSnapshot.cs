using System.Collections.Frozen;

namespace BlockChain.Models;

public sealed class LedgerSnapshot
{
    public IReadOnlyDictionary<string, long> Balances { get; }
    
    public IReadOnlySet<string> ConfirmedTransactionIds { get; }

    public LedgerSnapshot(
        IReadOnlyDictionary<string, long> balances,
        IReadOnlySet<string> confirmedTransactionIds)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(confirmedTransactionIds);
        
        Balances = balances.ToFrozenDictionary(StringComparer.Ordinal);
        
        ConfirmedTransactionIds = confirmedTransactionIds.ToFrozenSet(StringComparer.Ordinal);
        
    }
}