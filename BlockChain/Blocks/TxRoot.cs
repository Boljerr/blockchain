using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Blocks;

public static class TxRoot
{
    private static readonly HashFunc HashFunc = new();

    public static string Compute(IReadOnlyList<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        if (transactions.Count == 0)
            return HashFunc.ComputeHash([]);

        List<byte[]> level = transactions.Select(t => DecodeId(t.TransactionId)).ToList();

        while (level.Count > 1)
        {
            var parents = new List<byte[]>((level.Count + 1) / 2);

            for (int i = 0; i < level.Count; i += 2)
            {
                byte[] left = level[i];
                byte[] right = i + 1 < level.Count ? level[i + 1] : left;
                parents.Add(Convert.FromHexString(HashFunc.ComputeHash([.. left, .. right])));
            }

            level = parents;
        }

        return Convert.ToHexStringLower(level[0]);
    }

    public static bool IsWellFormedId(string? id)
        => id is { Length: 64 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static byte[] DecodeId(string id)
    {
        if (!IsWellFormedId(id))
            throw new ArgumentException($"Transaction ID '{id}' is not 64 lowercase hex characters.", nameof(id));

        return Convert.FromHexString(id);
    }
}