using System.Text;
using BlockChain.Blocks;
using BlockChain.Hash;
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
            TxRoot.Compute([CreateTransaction("a12f", "b34e", 100, 1), CreateTransaction("b34e", "c56d", 50, 2)])
        };

        for (ulong nonce = 0; nonce < 1_000; nonce++)
            hashes.Add(BlockHasher.ComputeHash(genesis.Header with { Nonce = nonce }));

        Assert.All(hashes, hash => Assert.Matches("^[0-9a-f]{64}$", hash));

        Assert.Contains(hashes, hash => hash.StartsWith('0'));
    }

    private static Transaction CreateTransaction(string sender, string receiver, long amount, long sequence)
    {
        string id = new HashFunc().ComputeHash(Encoding.UTF8.GetBytes($"{sender}|{receiver}|{amount}|{sequence}"));
        return new Transaction(sender, receiver, amount, sequence, id);
    }
}