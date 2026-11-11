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
        MiningSearchResult result = SearchRange(
            template,
            transactions,
            startNonce: 0,
            maxAttempts: maxAttempts);

        if (result.Block is not { } block)
        {
            throw new InvalidOperationException(
                $"No nonce found in {result.Attempts} attempts.");
        }

        return new MineResult(
            block,
            result.Attempts,
            result.Elapsed);
    }

    public static MiningSearchResult SearchRange(
        BlockHeader template,
        IReadOnlyList<Transaction> transactions,
        ulong startNonce,
        ulong maxAttempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(transactions);

        if (template.Difficulty is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(template),
                "Difficulty must be between 0 and 64.");
        }

        string prefix = new('0', template.Difficulty);

        var stopwatch = Stopwatch.StartNew();

        ulong attempts = 0;
        ulong nonce = startNonce;

        MiningSearchResult Finish(
            MiningSearchStatus status,
            Block? block,
            ulong? nextNonce)
        {
            stopwatch.Stop();

            return new MiningSearchResult(
                status,
                block,
                attempts,
                nextNonce,
                stopwatch.Elapsed);
        }

        while (attempts < maxAttempts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Finish(
                    MiningSearchStatus.Cancelled,
                    block: null,
                    nextNonce: nonce);
            }

            BlockHeader header = template with { Nonce = nonce };
            string hash = BlockHasher.ComputeHash(header);

            attempts++;

            ulong? nextNonce = nonce == ulong.MaxValue
                ? null
                : nonce + 1;

            if (hash.StartsWith(prefix, StringComparison.Ordinal))
            {
                var block = new Block(header, transactions, hash);

                return Finish(
                    MiningSearchStatus.Found,
                    block,
                    nextNonce);
            }

            if (nextNonce is null)
            {
                return Finish(
                    MiningSearchStatus.NonceExhausted,
                    block: null,
                    nextNonce: null);
            }

            nonce = nextNonce.Value;
        }

        return Finish(
            MiningSearchStatus.BudgetExhausted,
            block: null,
            nextNonce: nonce);
    }
}