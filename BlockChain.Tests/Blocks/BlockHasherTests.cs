using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class BlockHasherTests
{
    private static readonly BlockHeader KnownHeader = new(
        PrevHash: "00ab",
        Timestamp: 1_793_206_327,
        Version: 1,
        TxRoot: "cd34",
        Nonce: ulong.MaxValue,
        Difficulty: 3);

    [Fact]
    public void SerializeHeader_KnownHeader_ProducesExactString()
    {
        Assert.Equal(
            "00ab|1793206327|1|cd34|18446744073709551615|3",
            BlockHasher.SerializeHeader(KnownHeader));
    }

    [Fact]
    public void ComputeHash_SameHeader_HashesIdenticallyTwice()
    {
        BlockHeader copy = KnownHeader with { };

        Assert.Equal(BlockHasher.ComputeHash(KnownHeader), BlockHasher.ComputeHash(copy));
    }

    [Theory]
    [InlineData(nameof(BlockHeader.PrevHash))]
    [InlineData(nameof(BlockHeader.Timestamp))]
    [InlineData(nameof(BlockHeader.Version))]
    [InlineData(nameof(BlockHeader.TxRoot))]
    [InlineData(nameof(BlockHeader.Nonce))]
    [InlineData(nameof(BlockHeader.Difficulty))]
    public void ComputeHash_ChangingOneField_ChangesHash(string field)
    {
        BlockHeader changed = field switch
        {
            nameof(BlockHeader.PrevHash) => KnownHeader with { PrevHash = "00ac" },
            nameof(BlockHeader.Timestamp) => KnownHeader with { Timestamp = KnownHeader.Timestamp + 1 },
            nameof(BlockHeader.Version) => KnownHeader with { Version = 2 },
            nameof(BlockHeader.TxRoot) => KnownHeader with { TxRoot = "cd35" },
            nameof(BlockHeader.Nonce) => KnownHeader with { Nonce = KnownHeader.Nonce - 1 },
            nameof(BlockHeader.Difficulty) => KnownHeader with { Difficulty = 4 },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        Assert.NotEqual(BlockHasher.ComputeHash(KnownHeader), BlockHasher.ComputeHash(changed));
    }
}