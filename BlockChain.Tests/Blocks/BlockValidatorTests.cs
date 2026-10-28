using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class BlockValidatorTests
{
    private const int ChainDifficulty = 3;
    private const int MaxTxCount = 100;

    private static readonly string PrevHash = Genesis.Create(ChainDifficulty).DeclaredHash;

    private static readonly Transaction[] Transactions =
    [
        TestTransactions.Create("a12f", "b34e", 100, 1),
        TestTransactions.Create("b34e", "c56d", 50, 2),
        TestTransactions.Create("c56d", "a12f", 25, 3)
    ];

    [Fact]
    public void Validate_MinedBlock_IsValid()
    {
        BlockValidationResult result = Validate(MineBlock(Transactions));

        Assert.True(result.Valid);
        Assert.Equal(BlockReason.None, result.ReasonCode);
    }

    [Fact]
    public void Validate_WrongPrevHash_IsRejected()
    {
        Block block = MineBlock(Transactions, header => header with { PrevHash = new string('f', 64) });

        AssertRejected(Validate(block), BlockReason.PrevHashMismatch);
    }

    [Fact]
    public void Validate_Version2_IsRejected()
    {
        Block block = MineBlock(Transactions, header => header with { Version = 2 });

        AssertRejected(Validate(block), BlockReason.UnsupportedVersion);
    }

    [Fact]
    public void Validate_NoTransactions_IsRejected()
    {
        Block block = MineBlock([]);

        AssertRejected(Validate(block), BlockReason.InvalidTransactionCount);
    }

    [Fact]
    public void Validate_MoreTransactionsThanLimit_IsRejected()
    {
        Transaction[] tooMany = Enumerable.Range(1, MaxTxCount + 1)
            .Select(sequence => TestTransactions.Create("a12f", "b34e", 1, sequence))
            .ToArray();

        Block block = MineBlock(tooMany);

        AssertRejected(Validate(block), BlockReason.InvalidTransactionCount);
    }

    [Fact]
    public void Validate_SwappedTransactions_IsRejected()
    {
        Block block = MineBlock(Transactions);
        Transaction[] swapped = [block.Transactions[1], block.Transactions[0], block.Transactions[2]];

        var tampered = new Block(block.Header, swapped, block.DeclaredHash);

        AssertRejected(Validate(tampered), BlockReason.TxRootMismatch);
    }

    [Fact]
    public void Validate_LoweredDifficulty_IsRejected()
    {
        Block block = MineBlock(Transactions, header => header with { Difficulty = 2 });

        AssertRejected(Validate(block), BlockReason.DifficultyMismatch);
    }

    [Fact]
    public void Validate_AlteredDeclaredHash_IsRejected()
    {
        Block block = MineBlock(Transactions);
        string hash = block.DeclaredHash;
        string altered = hash[..^1] + (hash[^1] == 'a' ? 'b' : 'a');

        var tampered = new Block(block.Header, block.Transactions, altered);

        AssertRejected(Validate(tampered), BlockReason.HashMismatch);
    }

    [Fact]
    public void Validate_HashWithoutProofOfWork_IsRejected()
    {
        string prefix = new string('0', ChainDifficulty);
        BlockHeader template = CreateTemplate(Transactions);
        BlockHeader unsolved = Enumerable.Range(0, 100)
            .Select(nonce => template with { Nonce = (ulong)nonce })
            .First(header => !BlockHasher.ComputeHash(header).StartsWith(prefix, StringComparison.Ordinal));

        var block = new Block(unsolved, Transactions, BlockHasher.ComputeHash(unsolved));

        AssertRejected(Validate(block), BlockReason.InsufficientProofOfWork);
    }

    private static BlockHeader CreateTemplate(IReadOnlyList<Transaction> transactions)
        => new(PrevHash, 1_793_206_327, BlockValidator.SupportedVersion, TxRoot.Compute(transactions), 0, ChainDifficulty);

    private static Block MineBlock(
        IReadOnlyList<Transaction> transactions,
        Func<BlockHeader, BlockHeader>? change = null)
    {
        BlockHeader template = CreateTemplate(transactions);
        return Miner.Mine(change?.Invoke(template) ?? template, transactions).Block;
    }

    private static BlockValidationResult Validate(Block block)
        => BlockValidator.Validate(block, PrevHash, ChainDifficulty, MaxTxCount);

    private static void AssertRejected(BlockValidationResult result, BlockReason expected)
    {
        Assert.False(result.Valid);
        Assert.Equal(expected, result.ReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
    }
}