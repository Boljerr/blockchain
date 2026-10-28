using System.Diagnostics;
using BlockChain.Models;

namespace BlockChain.Blocks;

public static class Miner
{
    public const ulong DefaultMaxAttempts = 50_000_000;

    public static MineResult Mine(
        BlockHeader template,
        IReadOnlyList<Transaction> transactions,
        ulong maxAttempts = DefaultMaxAttempts)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(transactions);

        var prefix = new string('0', template.Difficulty);
        var stopwatch = Stopwatch.StartNew();

        for (ulong nonce = 0; nonce < maxAttempts; nonce++)
        {
            var header = template with { Nonce = nonce };
            var hash = BlockHasher.ComputeHash(header);

            if (hash.StartsWith(prefix, StringComparison.Ordinal))
                return new MineResult(new Block(header, transactions, hash), nonce + 1, stopwatch.Elapsed);
        }

        throw new InvalidOperationException($"No nonce found in {maxAttempts} attempts");
    }
}