using BlockChain.Models;

namespace BlockChain.Blocks;

public static class BlockValidator
{
    public const int SupportedVersion = 1;

    private static readonly BlockValidationResult Accepted = new(true, BlockReason.None, "Block is valid.");

    public static BlockValidationResult Validate(
        Block block,
        string expectedPrevHash,
        int requiredDifficulty,
        int maxTxCount)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(expectedPrevHash);

        BlockHeader header = block.Header;

        if (!string.Equals(header.PrevHash, expectedPrevHash, StringComparison.Ordinal))
            return Reject(BlockReason.PrevHashMismatch,
                $"prev_hash {header.PrevHash} does not match the previous block hash {expectedPrevHash}.");

        if (header.Version != SupportedVersion)
            return Reject(BlockReason.UnsupportedVersion,
                $"Version {header.Version} is not supported; expected {SupportedVersion}.");

        int txCount = block.Transactions.Count;
        if (txCount < 1 || txCount > maxTxCount)
            return Reject(BlockReason.InvalidTransactionCount,
                $"Block has {txCount} transactions; allowed range is 1 to {maxTxCount}.");

        string recomputedRoot = TxRoot.Compute(block.Transactions);
        if (!string.Equals(recomputedRoot, header.TxRoot, StringComparison.Ordinal))
            return Reject(BlockReason.TxRootMismatch,
                $"tx_root {header.TxRoot} does not match the recomputed root {recomputedRoot}.");

        if (header.Difficulty != requiredDifficulty)
            return Reject(BlockReason.DifficultyMismatch,
                $"Header difficulty {header.Difficulty} does not match the chain difficulty {requiredDifficulty}.");

        string recomputedHash = BlockHasher.ComputeHash(header);
        if (!string.Equals(recomputedHash, block.DeclaredHash, StringComparison.Ordinal))
            return Reject(BlockReason.HashMismatch,
                $"Declared hash {block.DeclaredHash} does not match the recomputed hash {recomputedHash}.");

        if (!recomputedHash.StartsWith(new string('0', requiredDifficulty), StringComparison.Ordinal))
            return Reject(BlockReason.InsufficientProofOfWork,
                $"Hash {recomputedHash} does not start with {requiredDifficulty} zeros.");

        return Accepted;
    }

    private static BlockValidationResult Reject(BlockReason reason, string explanation)
        => new(false, reason, explanation);
}