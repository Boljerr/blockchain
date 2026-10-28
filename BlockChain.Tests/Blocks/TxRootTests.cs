using BlockChain.Blocks;
using BlockChain.Hash;

namespace BlockChain.Tests.Blocks;

public class TxRootTests
{
    [Fact]
    public void Compute_EmptyList_EqualsHashOfEmptyBytes()
    {
        Assert.Equal(new HashFunc().ComputeHash([]), TxRoot.Compute([]));
    }
}