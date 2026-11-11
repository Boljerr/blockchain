using System.Text;
using BlockChain.Blocks;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class NonceHasherTests
{
    private static readonly HashFunc HashFunc = new();

    private static readonly BlockHeader Template = new(
        new string('0', 64),
        1_793_206_327,
        BlockValidator.SupportedVersion,
        new string('7', 64),
        0,
        3);

    [Theory]
    [InlineData("", "677035964b6f2deb97f3b7e5be8b78960d951b21e8a1eb5cf5179ff3b4619a22")]
    [InlineData("genesis-5", "0fa026b689742bc9ab1945ff65467c74fab08c604034618ebde29d57b2e9b9e3")]
    [InlineData("genesis-2", "253ca5d2e4ac01f7145e4e0c68a1ec7a90b4164307713fcaa32d40f6cc308b39")]
    public void ComputeHash_KnownInputs_KeepV02Values(string input, string expected)
    {
        string actual = input switch
        {
            "" => HashFunc.ComputeHash([]),
            "genesis-5" => Genesis.Create(5).DeclaredHash,
            "genesis-2" => Genesis.Create(2).DeclaredHash,
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null)
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Absorb_SplitAtAnyPoint_EqualsHashOfWholeInput()
    {
        byte[] data = Encoding.UTF8.GetBytes("prev|1793206327|2|root|ąčę|18446744073709551615|5");
        string whole = HashFunc.ComputeHash(data);

        for (int split = 0; split <= data.Length; split++)
        {
            HashState prefix = HashFunc.Absorb(HashFunc.InitialState, data.AsSpan(0, split), 0);
            HashState all = HashFunc.Absorb(prefix, data.AsSpan(split), split);

            Assert.Equal(whole, HashFunc.Format(HashFunc.Finish(all)));
        }
    }

    [Theory]
    [InlineData(0UL, 1)]
    [InlineData(9UL, 3)]
    [InlineData(10UL, 3)]
    [InlineData(99UL, 9)]
    [InlineData(100UL, 10)]
    [InlineData(123_456_789UL, 64)]
    [InlineData(ulong.MaxValue, 5)]
    public void Digest_MatchesFullHeaderHash(ulong nonce, int difficulty)
    {
        BlockHeader template = Template with { Difficulty = difficulty };
        var hasher = new NonceHasher(template);

        Assert.Equal(
            BlockHasher.ComputeHash(template with { Nonce = nonce }),
            HashFunc.Format(hasher.Digest(nonce)));
    }

    [Fact]
    public void Digest_RandomHeadersAndNonces_MatchFullHeaderHash()
    {
        var random = new Random(20_261_111);

        for (int i = 0; i < 300; i++)
        {
            BlockHeader template = new(
                RandomText(random),
                random.NextInt64(0, long.MaxValue),
                random.Next(),
                RandomText(random),
                0,
                random.Next(0, 65));
            var hasher = new NonceHasher(template);

            for (int j = 0; j < 5; j++)
            {
                ulong nonce = (ulong)random.NextInt64() >> random.Next(0, 64);

                Assert.Equal(
                    BlockHasher.ComputeHash(template with { Nonce = nonce }),
                    HashFunc.Format(hasher.Digest(nonce)));
            }
        }
    }

    [Fact]
    public void SerializeHeader_EqualsPartsAroundNonce()
    {
        BlockHeader header = Template with { Nonce = 4_242 };

        Assert.Equal(
            BlockHasher.SerializeBeforeNonce(header) + "4242" + BlockHasher.SerializeAfterNonce(header),
            BlockHasher.SerializeHeader(header));
    }

    [Fact]
    public void LeadingZeroBits_AgreesWithLeadingZeroHexCharacters()
    {
        var random = new Random(7);
        var digests = new List<HashState>
        {
            new(0, 0, 0, 0),
            new(0, 0, 0, 1),
            new(0, 1, 0, 0),
            new(0x0000_0fff_ffff_ffff, 0, 0, 0),
            new(ulong.MaxValue, 0, 0, 0)
        };

        for (int i = 0; i < 1_000; i++)
            digests.Add(HashFunc.Finish(HashFunc.Absorb(HashFunc.InitialState, BitConverter.GetBytes(i), 0)));

        for (int i = 0; i < 200; i++)
        {
            ulong word = (ulong)random.NextInt64() >> random.Next(0, 64);
            digests.Add(new HashState(0, word, (ulong)random.NextInt64(), 0));
        }

        foreach (HashState digest in digests)
        {
            string hex = HashFunc.Format(digest);
            int zeroCharacters = hex.Length - hex.TrimStart('0').Length;

            Assert.Equal(zeroCharacters, digest.LeadingZeroBits / 4);
        }
    }

    [Theory]
    [InlineData(1, 0UL)]
    [InlineData(2, 0UL)]
    [InlineData(3, 0UL)]
    [InlineData(3, 5_000UL)]
    public void SearchRange_FindsSameNonceAsFullHashScan(int difficulty, ulong startNonce)
    {
        BlockHeader template = Template with { Difficulty = difficulty };
        string prefix = new('0', difficulty);

        ulong expectedNonce = startNonce;
        while (!BlockHasher.ComputeHash(template with { Nonce = expectedNonce }).StartsWith(prefix, StringComparison.Ordinal))
            expectedNonce++;

        MiningSearchResult result = Miner.SearchRange(template, [], startNonce, 1_000_000);

        Block block = Assert.IsType<Block>(result.Block);
        Assert.Equal(expectedNonce, block.Header.Nonce);
        Assert.Equal(expectedNonce - startNonce + 1, result.Attempts);
        Assert.Equal(BlockHasher.ComputeHash(block.Header), block.DeclaredHash);
    }

    private static string RandomText(Random random)
    {
        const string alphabet = "0123456789abcdefą|";
        char[] text = new char[random.Next(0, 70)];

        for (int i = 0; i < text.Length; i++)
            text[i] = alphabet[random.Next(alphabet.Length)];

        return new string(text);
    }
}
