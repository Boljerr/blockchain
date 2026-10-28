using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Simulation;

namespace BlockChain.Tests.Simulation;

public class RunnerTests
{
    private static readonly string AliceKey = new('a', 64);
    private static readonly string BobKey = new('b', 64);
    private static readonly string CarolKey = new('c', 64);
    private static readonly string UnknownKey = new('d', 64);

    private static readonly SimulationConfig Config = new() { Difficulty = 2, CandidateLimit = 100 };

    [Fact]
    public void Run_ProducesLinkedBlocksWithHeightTimestamps()
    {
        RunReport report = new Runner(new FakeTransactionService()).Run(CreateInput());

        Assert.Equal([1, 2, 3], report.Blocks.Select(b => b.Height));
        Assert.Equal([100, 100, 50], report.Blocks.Select(b => b.Block.Transactions.Count));

        string prevHash = Genesis.Create(Config.Difficulty).DeclaredHash;
        foreach (BlockReport blockReport in report.Blocks)
        {
            BlockHeader header = blockReport.Block.Header;
            Assert.Equal(prevHash, header.PrevHash);
            Assert.Equal(Config.TimestampBase + blockReport.Height, header.Timestamp);
            Assert.Equal(header.Nonce + 1, blockReport.Attempts);
            Assert.True(BlockValidator.Validate(blockReport.Block, prevHash, Config.Difficulty, Config.CandidateLimit).Valid);
            prevHash = blockReport.Block.DeclaredHash;
        }
    }

    [Fact]
    public void Run_AccountsForEveryTransaction()
    {
        SimulationInput input = CreateInput();

        RunReport report = new Runner(new FakeTransactionService()).Run(input);

        Assert.Equal(250, report.IncludedCount);
        Assert.Equal(TransactionReason.UnknownReceiver, Assert.Single(report.Rejections).ReasonCode);
        Assert.Equal(CarolKey, Assert.Single(report.Remaining).Transaction.Sender);
        Assert.Equal(input.Transactions.Count, report.IncludedCount + report.Rejections.Count + report.Remaining.Count);
        Assert.Equal(report.InitialTotal, report.FinalTotal);

        SubmissionOutcome replay = Assert.Single(report.PostRunOutcomes);
        Assert.False(replay.Result.Accepted);
        Assert.Equal(TransactionReason.AlreadyConfirmed, replay.Result.ReasonCode);
    }

    [Fact]
    public void Run_SameInput_ProducesSameBlocks()
    {
        RunReport first = new Runner(new FakeTransactionService()).Run(CreateInput());
        RunReport second = new Runner(new FakeTransactionService()).Run(CreateInput());

        Assert.Equal(
            first.Blocks.Select(b => b.Block.DeclaredHash),
            second.Blocks.Select(b => b.Block.DeclaredHash));
    }

    [Fact]
    public void Run_OwnBlockRejected_ThrowsWithoutRetrying()
    {
        var service = new FakeTransactionService { FailPrepareApply = true };

        Assert.Throws<InvalidOperationException>(() => new Runner(service).Run(CreateInput()));
        Assert.Equal(1, service.PrepareApplyCalls);
    }

    private static SimulationInput CreateInput()
    {
        User[] users =
        [
            new("Alice", AliceKey, 1_000_000),
            new("Bob", BobKey, 1_000_000),
            new("Carol", CarolKey, 50)
        ];

        List<Transaction> transactions = Enumerable.Range(1, 250)
            .Select(sequence => sequence % 2 == 0
                ? TestTransactions.Create(AliceKey, BobKey, 10, sequence)
                : TestTransactions.Create(BobKey, AliceKey, 10, sequence))
            .ToList();
        transactions.Add(TestTransactions.Create(CarolKey, AliceKey, 1_000, 251));
        transactions.Add(TestTransactions.Create(AliceKey, UnknownKey, 5, 252));

        return new SimulationInput(Config, users, transactions, [transactions[0]]);
    }
}