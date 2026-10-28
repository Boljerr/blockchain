using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class MinerTests
{
    private static readonly Transaction[] Transactions = [TestTransactions.Create("a12f", "b34e", 100, 1)];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mine_FindsHashWithPrefixForDifficulty(int difficulty)
    {
        BlockHeader template = CreateTemplate(difficulty);

        MineResult result = Miner.Mine(template, Transactions);

        Block block = result.Block;
        Assert.StartsWith(new string('0', difficulty), block.DeclaredHash);
        Assert.Equal(BlockHasher.ComputeHash(block.Header), block.DeclaredHash);
        Assert.Equal(template with { Nonce = block.Header.Nonce }, block.Header);
        Assert.Equal(block.Header.Nonce + 1, result.Attempts);
    }

    [Fact]
    public void Mine_NoSolutionWithinMaxAttempts_Throws()
    {
        BlockHeader template = CreateTemplate(8);

        Assert.Throws<InvalidOperationException>(() => Miner.Mine(template, Transactions, maxAttempts: 1_000));
    }

    private static BlockHeader CreateTemplate(int difficulty)
        => new(new string('0', 64), 1_793_206_327, 1, TxRoot.Compute(Transactions), 0, difficulty);
}