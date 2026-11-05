using BlockChain.Blocks;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class TxRootTests
{
    private static readonly HashFunc Hash = new();

    private static readonly Transaction[] Transactions = Enumerable.Range(1, 5)
        .Select(sequence => TestTransactions.Create(new string('a', 64), new string('b', 64), 10 * sequence, sequence))
        .ToArray();

    [Fact]
    public void Compute_EmptyList_EqualsHashOfEmptyBytes()
    {
        Assert.Equal(Hash.ComputeHash([]), TxRoot.Compute([]));
    }

    [Fact]
    public void Compute_OneTransaction_IsItsId()
    {
        Assert.Equal(Leaf(0), TxRoot.Compute(Transactions[..1]));
    }

    [Fact]
    public void Compute_TwoTransactions_HashesBothIdsAsBytes()
    {
        string expected = Node(Leaf(0), Leaf(1));

        Assert.Equal(expected, TxRoot.Compute(Transactions[..2]));
    }

    [Fact]
    public void Compute_ThreeTransactions_PairsOddLeafWithItself()
    {
        string expected = Node(Node(Leaf(0), Leaf(1)), Node(Leaf(2), Leaf(2)));

        Assert.Equal(expected, TxRoot.Compute(Transactions[..3]));
    }

    [Fact]
    public void Compute_FiveTransactions_PairsOddNodeWithItselfOnEveryLevel()
    {
        string level1Left = Node(Leaf(0), Leaf(1));
        string level1Middle = Node(Leaf(2), Leaf(3));
        string level1Right = Node(Leaf(4), Leaf(4));
        string level2Left = Node(level1Left, level1Middle);
        string level2Right = Node(level1Right, level1Right);
        string expected = Node(level2Left, level2Right);

        Assert.Equal(expected, TxRoot.Compute(Transactions));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Compute_ChangedContentWithRecomputedId_ChangesRoot(int count)
    {
        Transaction[] original = Transactions[..count];
        Transaction last = original[^1];
        Transaction[] changed = [.. original[..^1], TestTransactions.Create(last.Sender, last.Receiver, last.Amount + 1, last.Sequence)];

        Assert.NotEqual(TxRoot.Compute(original), TxRoot.Compute(changed));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Compute_SwappedTransactions_ChangesRoot(int count)
    {
        Transaction[] original = Transactions[..count];
        Transaction[] swapped = [original[1], original[0], .. original[2..]];

        Assert.NotEqual(TxRoot.Compute(original), TxRoot.Compute(swapped));
    }

    [Fact]
    public void Compute_LastTransactionRepeated_GivesSameRoot_KnownCollision()
    {
        Transaction[] three = Transactions[..3];
        Transaction[] threeWithLastRepeated = [.. three, three[^1]];

        Assert.Equal(TxRoot.Compute(three), TxRoot.Compute(threeWithLastRepeated));
    }

    [Fact]
    public void Compute_MalformedId_Throws()
    {
        Transaction malformed = Transactions[0] with { TransactionId = "xyz" };

        Assert.Throws<ArgumentException>(() => TxRoot.Compute([malformed]));
    }

    private static string Leaf(int index) => Transactions[index].TransactionId;

    private static string Node(string leftHex, string rightHex)
        => Hash.ComputeHash([.. Convert.FromHexString(leftHex), .. Convert.FromHexString(rightHex)]);
}