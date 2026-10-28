using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class HashFormatTests
{
    [Fact]
    public void AllHashes_AreSixtyFourLowercaseHexCharacters()
    {
        Block genesis = Genesis.Create(3);
        var hashes = new List<string>
        {
            genesis.DeclaredHash,
            TxRoot.Compute([]),
            TxRoot.Compute([TestTransactions.Create("a12f", "b34e", 100, 1), TestTransactions.Create("b34e", "c56d", 50, 2)])
        };

        for (ulong nonce = 0; nonce < 1_000; nonce++)
            hashes.Add(BlockHasher.ComputeHash(genesis.Header with { Nonce = nonce }));

        Assert.All(hashes, hash => Assert.Matches("^[0-9a-f]{64}$", hash));

        Assert.Contains(hashes, hash => hash.StartsWith('0'));
    }
}