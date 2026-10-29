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

    private static readonly Transaction Payment = TestTransactions.Create(AliceKey, BobKey, 100, 1);

    [Fact]
    public void TryAccept_WrongPrevHash_ChangesNothing()
    {
        var chain = new Chain(new TransactionService(), Difficulty, MaxTxCount);
        TransactionSession session = CreateSessionWithPool(Payment);
        Block block = Mine(new string('f', 64), [Payment]);

        State before = State.Capture(chain, session);
        AcceptResult result = chain.TryAccept(block, session);

        Assert.False(result.Accepted);
        Assert.Equal(BlockReason.PrevHashMismatch, result.BlockReason);
        AssertUnchanged(before, chain, session);
    }

    [Fact]
    public void TryAccept_InvalidProofOfWork_ChangesNothing()
    {
        var chain = new Chain(new TransactionService(), Difficulty, MaxTxCount);
        TransactionSession session = CreateSessionWithPool(Payment);
        string prefix = new string('0', Difficulty);
        BlockHeader template = CreateTemplate(chain.TipHash, [Payment]);
        BlockHeader unsolved = Enumerable.Range(0, 100)
            .Select(nonce => template with { Nonce = (ulong)nonce })
            .First(header => !BlockHasher.ComputeHash(header).StartsWith(prefix, StringComparison.Ordinal));
        var block = new Block(unsolved, [Payment], BlockHasher.ComputeHash(unsolved));

        State before = State.Capture(chain, session);
        AcceptResult result = chain.TryAccept(block, session);

        Assert.False(result.Accepted);
        Assert.Equal(BlockReason.InsufficientProofOfWork, result.BlockReason);
        AssertUnchanged(before, chain, session);
    }

    [Fact]
    public void TryAccept_AlteredAmountWithOldId_ChangesNothing()
    {
        var service = new TransactionService();
        var chain = new Chain(service, Difficulty, MaxTxCount);
        TransactionSession session = service.CreateSession(
        [
            new User("Alice", AliceKey, 1_000),
            new User("Bob", BobKey, 1_000)
        ]);
        Assert.True(service.AddToPool(session, Payment).Accepted);

        Transaction altered = Payment with { Amount = 200 };
        Block block = Mine(chain.TipHash, [altered]);
        Assert.True(BlockValidator.Validate(block, chain.TipHash, Difficulty, MaxTxCount).Valid);

        State before = State.Capture(chain, session);
        AcceptResult result = chain.TryAccept(block, session);

        Assert.False(result.Accepted);
        Assert.Equal(TransactionReason.TransactionIdMismatch, result.TransactionReason);
        AssertUnchanged(before, chain, session);
    }

    private static TransactionSession CreateSessionWithPool(params Transaction[] pending)
    {
        var ledger = new LedgerSnapshot(
            new Dictionary<string, long> { [AliceKey] = 1_000, [BobKey] = 1_000 },
            new HashSet<string>());
        var session = new TransactionSession(ledger, 2_000);

        foreach (Transaction transaction in pending)
            session.Pending.Add(transaction.TransactionId, transaction);

        return session;
    }

    private static BlockHeader CreateTemplate(string prevHash, IReadOnlyList<Transaction> transactions)
        => new(prevHash, 1_793_206_327, BlockValidator.SupportedVersion, TxRoot.Compute(transactions), 0, Difficulty);

    private static Block Mine(string prevHash, IReadOnlyList<Transaction> transactions)
        => Miner.Mine(CreateTemplate(prevHash, transactions), transactions).Block;

    private static void AssertUnchanged(State before, Chain chain, TransactionSession session)
    {
        State after = State.Capture(chain, session);

        Assert.Same(before.Ledger, after.Ledger);
        Assert.Equal(before.PoolCount, after.PoolCount);
        Assert.Equal(before.ConfirmedCount, after.ConfirmedCount);
        Assert.Equal(before.Height, after.Height);
    }

    private sealed record State(LedgerSnapshot Ledger, int PoolCount, int ConfirmedCount, int Height)
    {
        public static State Capture(Chain chain, TransactionSession session) => new(
            session.Ledger,
            session.PendingTransactions.Count,
            session.Ledger.ConfirmedTransactionIds.Count,
            chain.Height);
    }
}