using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Simulation;
using BlockChain.Transactions;

namespace BlockChain.Tests.Blocks;

public class ParallelMiningTests
{
    private static readonly string AliceKey = new('a', 64);
    private static readonly string BobKey = new('b', 64);
    private static readonly string CarolKey = new('c', 64);

    private static readonly Transaction Payment =
        TestTransactions.Create(AliceKey, BobKey, 10, 1);

    [Fact]
    public async Task MineParallelAsync_SelectsOneWinner()
    {
        MiningCandidate[] candidates = CreateCandidates(
            difficulty: 0);

        MiningCompetitionResult result =
            await MiningCoordinator.MineParallelAsync(
                candidates,
                initialAttempts: 1,
                maxAttemptsPerCandidate: 1);

        Assert.True(result.Success);

        MiningCandidate winner =
            Assert.IsType<MiningCandidate>(result.Winner);

        Block block = Assert.IsType<Block>(result.Block);

        Assert.Contains(winner, candidates);
        Assert.Equal(winner.Header, block.Header);

        Assert.Equal(
            BlockHasher.ComputeHash(block.Header),
            block.DeclaredHash);

        Assert.Equal(
            MiningCoordinator.CandidateCount,
            result.Candidates.Count);

        Assert.InRange(
            result.TotalAttempts,
            1UL,
            (ulong)MiningCoordinator.CandidateCount);

        CandidateMiningReport winnerReport = result.Candidates.Single(candidate =>
            candidate.CandidateNumber == winner.Number);

        Assert.Equal(
            MiningSearchStatus.Found,
            winnerReport.LastStatus);

        Assert.All(result.Candidates, candidate =>
        {
            Assert.InRange(candidate.Attempts, 0UL, 1UL);

            Assert.True(
                candidate.LastStatus == MiningSearchStatus.Found ||
                candidate.LastStatus == MiningSearchStatus.Cancelled);
        });
    }

    [Fact]
    public async Task MineParallelAsync_AttemptLimitsStopSearch()
    {
        MiningCompetitionResult result =
            await MiningCoordinator.MineParallelAsync(
                CreateCandidates(difficulty: 64),
                initialAttempts: 1,
                maxAttemptsPerCandidate: 3);

        Assert.False(result.Success);
        Assert.Null(result.Winner);
        Assert.Null(result.Block);

        Assert.Equal(
            3UL * (ulong)MiningCoordinator.CandidateCount,
            result.TotalAttempts);

        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(3UL, candidate.Attempts);
            Assert.Equal((ulong?)3, candidate.NextNonce);
            Assert.True(candidate.AttemptLimitReached);
        });
    }

    [Fact]
    public async Task MineParallelAsync_CancellationStopsWorkers()
    {
        using var cancellation = new CancellationTokenSource();

        Task<MiningCompetitionResult> mining =
            MiningCoordinator.MineParallelAsync(
                CreateCandidates(difficulty: 64),
                cancellationToken: cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mining.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task MineParallelAsync_WorkerFailureStopsOtherWorkers()
    {
        MiningCandidate[] candidates = CreateCandidates(
            difficulty: 64);

        MiningCandidate original = candidates[0];

        candidates[0] = new MiningCandidate(
            original.Number,
            original.Header with { Difficulty = 65 },
            original.Transactions,
            original.DeferredTransactions);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            MiningCoordinator.MineParallelAsync(candidates)
                .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_OnlyWinningPaymentChangesBalances(
        bool parallel)
    {
        var config = new SimulationConfig
        {
            Difficulty = 0,
            ParallelMining = parallel,
            UserCount = 3,
            TransactionCount = 2
        };

        User[] users =
        [
            new User("Alice", AliceKey, 100),
            new User("Bob", BobKey, 0),
            new User("Carol", CarolKey, 0)
        ];

        Transaction[] payments =
        [
            TestTransactions.Create(AliceKey, BobKey, 70, 1),
            TestTransactions.Create(AliceKey, CarolKey, 60, 2)
        ];

        var input = new SimulationInput(
            config,
            users,
            payments,
            []);

        RunReport report = await new Runner(
            new TransactionService()).RunAsync(input);

        Assert.True(report.AccountingHolds);
        Assert.True(report.FundsConserved);
        Assert.Equal(100L, report.FinalTotal);

        BlockReport acceptedBlock = Assert.Single(report.Blocks);

        Transaction winningPayment = Assert.Single(
            acceptedBlock.Block.Transactions);

        RemainingTransaction remaining = Assert.Single(
            report.Remaining);

        Assert.Equal(
            100L - winningPayment.Amount,
            report.FinalLedger.Balances[AliceKey]);

        Assert.Equal(
            winningPayment.Amount,
            report.FinalLedger.Balances[winningPayment.Receiver]);

        Assert.Equal(
            0L,
            report.FinalLedger.Balances[remaining.Transaction.Receiver]);

        Assert.Equal(
            winningPayment.TransactionId,
            Assert.Single(report.FinalLedger.ConfirmedTransactionIds));
    }

    private static MiningCandidate[] CreateCandidates(int difficulty)
    {
        var header = new BlockHeader(
            PrevHash: new string('0', 64),
            Timestamp: 1_793_206_327,
            Version: BlockValidator.SupportedVersion,
            TxRoot: TxRoot.Compute([Payment]),
            Nonce: 0,
            Difficulty: difficulty);

        return Enumerable
            .Range(1, MiningCoordinator.CandidateCount)
            .Select(number => new MiningCandidate(
                number,
                header,
                [Payment],
                []))
            .ToArray();
    }
}