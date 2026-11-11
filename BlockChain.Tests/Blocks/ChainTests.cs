using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Blocks;

public class ChainTests
{
    private const int Difficulty = 3;
    private const int MaxTxCount = 100;

    private static readonly string AliceKey = new('a', 64);
    private static readonly string BobKey = new('b', 64);
    private static readonly string CarolKey = new('c', 64);

    private static readonly Transaction Payment = TestTransactions.Create(AliceKey, BobKey, 100, 1);

    private readonly TransactionService _service = new();

    [Fact]
    public void TryAccept_ValidBlock_UpdatesChainLedgerAndPoolTogether()
    {
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(Payment);
        Block block = Mine(chain.TipHash, [Payment]);

        AcceptResult result = chain.TryAccept(block, session);

        Assert.True(result.Accepted);
        Assert.Equal(1, chain.Height);
        Assert.Equal(block.DeclaredHash, chain.TipHash);
        Assert.Equal(900, session.Ledger.Balances[AliceKey]);
        Assert.Equal(1_100, session.Ledger.Balances[BobKey]);
        Assert.Contains(Payment.TransactionId, session.Ledger.ConfirmedTransactionIds);
        Assert.Empty(session.PendingTransactions);
    }

    [Fact]
    public void TryAccept_WrongPrevHash_ChangesNothing()
    {
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(Payment);
        Block block = Mine(new string('f', 64), [Payment]);

        AssertRejectedWithoutChanges(chain, session, block, BlockReason.PrevHashMismatch);
    }

    [Fact]
    public void TryAccept_InvalidProofOfWork_ChangesNothing()
    {
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(Payment);
        string prefix = new string('0', Difficulty);
        BlockHeader template = CreateTemplate(chain.TipHash, [Payment]);
        BlockHeader unsolved = Enumerable.Range(0, 100)
            .Select(nonce => template with { Nonce = (ulong)nonce })
            .First(header => !BlockHasher.ComputeHash(header).StartsWith(prefix, StringComparison.Ordinal));
        var block = new Block(unsolved, [Payment], BlockHasher.ComputeHash(unsolved));

        AssertRejectedWithoutChanges(chain, session, block, BlockReason.InsufficientProofOfWork);
    }

    [Fact]
    public void TryAccept_AlteredAmountWithOldId_ChangesNothing()
    {
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(Payment);
        Block block = Mine(chain.TipHash, [Payment with { Amount = 200 }]);
        Assert.True(BlockValidator.Validate(block, chain.TipHash, Difficulty, MaxTxCount).Valid);

        AssertRejectedWithoutChanges(chain, session, block, TransactionReason.TransactionIdMismatch);
    }

    [Fact]
    public void TryAccept_TwoPaymentsTogetherAboveBalance_ChangesNothing()
    {
        Transaction first = TestTransactions.Create(CarolKey, AliceKey, 70, 2);
        Transaction second = TestTransactions.Create(CarolKey, BobKey, 60, 3);
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(first, second);
        Block block = Mine(chain.TipHash, [first, second]);

        AssertRejectedWithoutChanges(chain, session, block, TransactionReason.InsufficientBalance);
    }

    [Fact]
    public void TryAccept_LastTransactionRepeatedWithSameMerkleRoot_ChangesNothing()
    {
        Transaction[] three =
        [
            Payment,
            TestTransactions.Create(BobKey, CarolKey, 10, 2),
            TestTransactions.Create(CarolKey, AliceKey, 5, 3)
        ];
        Transaction[] repeated = [.. three, three[^1]];
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(three);
        Block block = Miner.Mine(CreateTemplate(chain.TipHash, three), repeated).Block;
        Assert.True(BlockValidator.Validate(block, chain.TipHash, Difficulty, MaxTxCount).Valid);

        AssertRejectedWithoutChanges(chain, session, block, TransactionReason.DuplicateInBlock);
    }

    [Fact]
    public void TryAccept_ReplayOfConfirmedPaymentInNextBlock_ChangesNothing()
    {
        var chain = new Chain(_service, Difficulty, MaxTxCount);
        TransactionSession session = CreateSession(Payment);
        Assert.True(chain.TryAccept(Mine(chain.TipHash, [Payment]), session).Accepted);
        Block replay = Mine(chain.TipHash, [Payment]);

        AssertRejectedWithoutChanges(chain, session, replay, TransactionReason.AlreadyConfirmed);
    }

    private TransactionSession CreateSession(params Transaction[] pending)
    {
        TransactionSession session = _service.CreateSession(
        [
            new User("Alice", AliceKey, 1_000),
            new User("Bob", BobKey, 1_000),
            new User("Carol", CarolKey, 100)
        ]);

        foreach (Transaction transaction in pending)
            Assert.True(_service.AddToPool(session, transaction).Accepted);

        return session;
    }

    private static BlockHeader CreateTemplate(string prevHash, IReadOnlyList<Transaction> transactions)
        => new(prevHash, 1_793_206_327, BlockValidator.SupportedVersion, TxRoot.Compute(transactions), 0, Difficulty);

    private static Block Mine(string prevHash, IReadOnlyList<Transaction> transactions)
        => Miner.Mine(CreateTemplate(prevHash, transactions), transactions).Block;

    private static void AssertRejectedWithoutChanges(
        Chain chain, TransactionSession session, Block block, BlockReason expected)
    {
        AcceptResult result = AcceptWithoutChanges(chain, session, block);

        Assert.Equal(expected, result.BlockReason);
    }

    private static void AssertRejectedWithoutChanges(
        Chain chain, TransactionSession session, Block block, TransactionReason expected)
    {
        AcceptResult result = AcceptWithoutChanges(chain, session, block);

        Assert.Equal(expected, result.TransactionReason);
    }

    private static AcceptResult AcceptWithoutChanges(Chain chain, TransactionSession session, Block block)
    {
        State before = State.Capture(chain, session);

        AcceptResult result = chain.TryAccept(block, session);

        State after = State.Capture(chain, session);
        Assert.False(result.Accepted);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
        Assert.Same(before.Ledger, after.Ledger);
        Assert.Equal(before.PoolIds, after.PoolIds);
        Assert.Equal(before.ConfirmedCount, after.ConfirmedCount);
        Assert.Equal(before.Height, after.Height);
        Assert.Equal(before.TipHash, after.TipHash);
        return result;
    }

    private sealed record State(LedgerSnapshot Ledger, string[] PoolIds, int ConfirmedCount, int Height, string TipHash)
    {
        public static State Capture(Chain chain, TransactionSession session) => new(
            session.Ledger,
            session.PendingTransactions.Keys.Order(StringComparer.Ordinal).ToArray(),
            session.Ledger.ConfirmedTransactionIds.Count,
            chain.Height,
            chain.TipHash);
    }
}