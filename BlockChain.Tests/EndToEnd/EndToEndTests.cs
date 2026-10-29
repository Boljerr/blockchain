using BlockChain.Models;
using BlockChain.Simulation;
using BlockChain.Transactions;

namespace BlockChain.Tests.EndToEnd;

public class EndToEndTests
{
    private const string NeedsService =
        "Needs the real TransactionService (CreateDemo, Generate, SaveInputs, LoadInputs). Finish at integration.";

    private static readonly SimulationConfig SmallConfig = new()
    {
        UserCount = 50,
        TransactionCount = 500,
        Difficulty = 2
    };

    [Fact]
    public void Demo_RunsAsDesigned()
    {
        var service = new TransactionService();
        SimulationInput input = service.CreateDemo();

        RunReport report = new Runner(service).Run(input);

        BlockReport block1 = report.Blocks[0];
        BlockReport block2 = report.Blocks[1];
        Assert.Contains(block1.Block.Transactions, t => t.Sequence == 1);
        Assert.Contains(block1.Deferred, d => d.Transaction.Sequence == 2);
        Assert.Contains(block2.Block.Transactions, t => t.Sequence == 2);

        Assert.Equal(30, FinalBalance(report, input, "A"));
        Assert.Equal(220, FinalBalance(report, input, "B"));
        Assert.Equal(50, FinalBalance(report, input, "C"));
        Assert.Equal(300, report.FinalTotal);

        Assert.All(report.Rejections, r => Assert.NotEqual(TransactionReason.None, r.ReasonCode));
        Assert.Contains(report.Rejections, r => r.ReasonCode == TransactionReason.UnknownReceiver);
        Assert.Contains(report.Rejections, r => r.ReasonCode == TransactionReason.InvalidAmount);
        Assert.Contains(report.Rejections, r => r.ReasonCode == TransactionReason.TransactionIdMismatch);

        SubmissionOutcome resubmission = Assert.Single(report.PostRunOutcomes);
        Assert.False(resubmission.Result.Accepted);
        Assert.Equal(TransactionReason.AlreadyConfirmed, resubmission.Result.ReasonCode);

        Assert.True(report.AccountingHolds);
    }

    [Fact]
    public void Replay_FromSavedInputs_IsIdentical()
    {
        var service = new TransactionService();
        SimulationInput input = service.Generate(SmallConfig);
        string folder = Directory.CreateTempSubdirectory("blockchain-replay-").FullName;

        try
        {
            service.SaveInputs(input, folder);
            RunReport original = new Runner(service).Run(input);

            SimulationInput loaded = service.LoadInputs(folder);
            RunReport replayed = new Runner(new TransactionService()).Run(loaded);

            Assert.Equal(input.Transactions.Select(t => t.TransactionId), loaded.Transactions.Select(t => t.TransactionId));
            Assert.Equal(original.Blocks.Select(b => b.Block.DeclaredHash), replayed.Blocks.Select(b => b.Block.DeclaredHash));
            Assert.Equal(SortedBalances(original), SortedBalances(replayed));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SmallRun_LastBlockWithinLimitAndFundsConserved()
    {
        var service = new TransactionService();

        RunReport report = new Runner(service).Run(service.Generate(SmallConfig));

        Assert.InRange(report.Blocks[^1].Block.Transactions.Count, 1, SmallConfig.CandidateLimit);
        Assert.Equal(report.InitialTotal, report.FinalTotal);
    }

    private static long FinalBalance(RunReport report, SimulationInput input, string name)
        => report.FinalLedger.Balances[input.Users.Single(u => u.Name == name).PublicKey];

    private static IEnumerable<KeyValuePair<string, long>> SortedBalances(RunReport report)
        => report.FinalLedger.Balances.OrderBy(pair => pair.Key, StringComparer.Ordinal);
}