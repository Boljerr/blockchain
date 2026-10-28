using System.Text;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Blocks;

public static class TxRoot
{
    private static readonly HashFunc HashFunc = new();

    public static string Compute(IReadOnlyList<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        string concatenatedIds = string.Concat(transactions.Select(t => t.TransactionId));
        return HashFunc.ComputeHash(Encoding.UTF8.GetBytes(concatenatedIds));
    }
}