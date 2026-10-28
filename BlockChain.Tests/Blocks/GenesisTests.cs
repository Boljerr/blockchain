using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class GenesisTests
{
    [Fact]
    public void Create_CalledTwice_ProducesIdenticalBlocks()
    {
        Block first = Genesis.Create(3);
        Block second = Genesis.Create(3);

        Assert.Equal(first.Header, second.Header);
        Assert.Equal(first.Transactions, second.Transactions);
        Assert.Equal(first.DeclaredHash, second.DeclaredHash);
    }
}