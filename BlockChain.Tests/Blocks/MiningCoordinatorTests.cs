using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Blocks;

public class MiningCoordinatorTests
{
    private static readonly string AliceKey = new('a', 64);
    private static readonly string BobKey = new('b', 64);
    private static readonly string CarolKey = new('c', 64);

    private static readonly Transaction Payment =
        TestTransactions.Create(AliceKey, BobKey, 10, 1);

    [Fact]
    public void Mine_FirstCandidateSucceeds_OtherCandidatesAreNotSearched()
    {
        MiningCandidate[] candidates = CreateCandidates(
            CreateHeader(difficulty: 0));

        // Difficulty 0 makes the first tested nonce succeed.
        MiningCompetitionResult result = MiningCoordinator.Mine(
            candidates,
            initialAttempts: 1,
            maxAttemptsPerCandidate: 10);

        Assert.True(result.Success);
        Assert.Same(candidates[0], result.Winner);
        Assert.Equal(1UL, result.TotalAttempts);

        Block block = Assert.IsType<Block>(result.Block);

        Assert.Equal(0UL, block.Header.Nonce);
        Assert.Equal(
            BlockHasher.ComputeHash(block.Header),
            block.DeclaredHash);

        CandidateMiningReport winner = result.Candidates[0];

        Assert.Equal(1UL, winner.Attempts);
        Assert.Equal((ulong?)1, winner.NextNonce);
        Assert.Equal(MiningSearchStatus.Found, winner.LastStatus);
        Assert.False(winner.AttemptLimitReached);

        Assert.All(result.Candidates.Skip(1), candidate =>
        {
            Assert.Equal(0UL, candidate.Attempts);
            Assert.Equal((ulong?)0, candidate.NextNonce);
            Assert.Null(candidate.LastStatus);
            Assert.False(candidate.AttemptLimitReached);
        });
    }

    [Fact]
    public void Mine_IncreasesBudget_AndResumesFromUntestedNonce()
    {
        BlockHeader header = FindHeaderWithThirdRoundSolution();

        MiningCompetitionResult result = MiningCoordinator.Mine(
            CreateCandidates(header),
            initialAttempts: 1,
            maxAttemptsPerCandidate: 7);

        Assert.True(result.Success);
        Assert.Equal(1, result.Winner!.Number);

        Block block = Assert.IsType<Block>(result.Block);

        Assert.InRange(block.Header.Nonce, 3UL, 6UL);
        Assert.StartsWith("0", block.DeclaredHash);
        Assert.Equal(
            BlockHasher.ComputeHash(block.Header),
            block.DeclaredHash);

        CandidateMiningReport winner = result.Candidates[0];

        Assert.Equal(block.Header.Nonce + 1, winner.Attempts);
        Assert.Equal(
            (ulong?)(block.Header.Nonce + 1),
            winner.NextNonce);

        // Round 1 searches nonce 0.
        // Round 2 searches nonces 1 and 2.
        // Candidate 1 wins in round 3, so the others stop at nonce 3.
        Assert.All(result.Candidates.Skip(1), candidate =>
        {
            Assert.Equal(3UL, candidate.Attempts);
            Assert.Equal((ulong?)3, candidate.NextNonce);

            Assert.Equal(
                MiningSearchStatus.BudgetExhausted,
                candidate.LastStatus);

            Assert.False(candidate.AttemptLimitReached);
        });

        Assert.Equal(
            winner.Attempts + 12UL,
            result.TotalAttempts);
    }

    [Fact]
    public void Mine_AllCandidatesReachLimit_ReturnsFailure()
    {
        BlockHeader header = CreateHeader(difficulty: 64);
        string requiredPrefix = new('0', header.Difficulty);

        // Confirm that the exact search range has no solution.
        for (ulong nonce = 0; nonce < 3; nonce++)
        {
            string hash = BlockHasher.ComputeHash(
                header with { Nonce = nonce });

            Assert.False(
                hash.StartsWith(
                    requiredPrefix,
                    StringComparison.Ordinal));
        }

        MiningCompetitionResult result = MiningCoordinator.Mine(
            CreateCandidates(header),
            initialAttempts: 1,
            maxAttemptsPerCandidate: 3);

        Assert.False(result.Success);
        Assert.Null(result.Winner);
        Assert.Null(result.Block);
        Assert.Equal(15UL, result.TotalAttempts);

        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(3UL, candidate.Attempts);
            Assert.Equal((ulong?)3, candidate.NextNonce);
            Assert.True(candidate.AttemptLimitReached);

            Assert.Equal(
                MiningSearchStatus.BudgetExhausted,
                candidate.LastStatus);
        });
    }

    [Fact]
    public void Mine_OnlyAcceptedWinnerChangesBalancesAndPool()
    {
        var service = new TransactionService();

        TransactionSession session = service.CreateSession(
        [
            new User("Alice", AliceKey, 100),
            new User("Bob", BobKey, 0),
            new User("Carol", CarolKey, 0)
        ]);

        Transaction[] payments =
        [
            TestTransactions.Create(AliceKey, BobKey, 70, 1),
            TestTransactions.Create(AliceKey, CarolKey, 60, 2)
        ];

        foreach (Transaction payment in payments)
        {
            Assert.True(
                service.AddToPool(session, payment).Accepted);
        }

        var chain = new Chain(
            service,
            difficulty: 0,
            maxTxCount: 100);

        LedgerSnapshot originalLedger = session.Ledger;

        var originalPending = session.PendingTransactions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();

        var random = new Random(123);

        MiningCandidate[] candidates = Enumerable
            .Range(1, MiningCoordinator.CandidateCount)
            .Select(number =>
            {
                CandidateSelection selection = service.SelectCandidate(
                    session,
                    random,
                    maxCount: 1);

                Assert.Single(selection.SelectedTransactions);

                BlockHeader header = CreateHeader(difficulty: 0) with
                {
                    PrevHash = chain.TipHash,
                    TxRoot = TxRoot.Compute(
                        selection.SelectedTransactions)
                };

                return new MiningCandidate(
                    number,
                    header,
                    selection.SelectedTransactions,
                    selection.DeferredTransactions);
            })
            .ToArray();

        MiningCompetitionResult result = MiningCoordinator.Mine(
            candidates,
            initialAttempts: 1,
            maxAttemptsPerCandidate: 10);

        Assert.True(result.Success);

        // Selection and mining must leave confirmed state intact.
        Assert.Same(originalLedger, session.Ledger);
        Assert.Equal(0, chain.Height);
        Assert.Empty(session.Ledger.ConfirmedTransactionIds);

        Assert.Equal(
            originalPending,
            session.PendingTransactions
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray());

        Block block = Assert.IsType<Block>(result.Block);
        Transaction winningPayment = Assert.Single(block.Transactions);

        AcceptResult accepted = chain.TryAccept(block, session);

        Assert.True(accepted.Accepted);
        Assert.Equal(1, chain.Height);

        Assert.Equal(
            100L - winningPayment.Amount,
            session.Ledger.Balances[AliceKey]);

        Assert.Equal(
            winningPayment.Amount,
            session.Ledger.Balances[winningPayment.Receiver]);

        Transaction remainingPayment = payments.Single(
            payment =>
                payment.TransactionId != winningPayment.TransactionId);

        Assert.Equal(
            0L,
            session.Ledger.Balances[remainingPayment.Receiver]);

        Assert.Equal(
            remainingPayment,
            Assert.Single(session.PendingTransactions).Value);

        Assert.Equal(
            winningPayment.TransactionId,
            Assert.Single(session.Ledger.ConfirmedTransactionIds));

        Assert.Equal(
            100L,
            session.Ledger.Balances.Values.Sum());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void Mine_WrongCandidateCount_Throws(int count)
    {
        MiningCandidate[] candidates = CreateCandidates(
            CreateHeader(difficulty: 0),
            count);

        Assert.Throws<ArgumentException>(
            () => MiningCoordinator.Mine(candidates));
    }

    [Theory]
    [InlineData(0UL, 10UL)]
    [InlineData(1UL, 0UL)]
    public void Mine_ZeroBudget_Throws(
        ulong initialAttempts,
        ulong maxAttemptsPerCandidate)
    {
        MiningCandidate[] candidates = CreateCandidates(
            CreateHeader(difficulty: 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MiningCoordinator.Mine(
                candidates,
                initialAttempts,
                maxAttemptsPerCandidate));
    }

    private static BlockHeader CreateHeader(int difficulty)
        => new(
            PrevHash: new string('0', 64),
            Timestamp: 1_793_206_327,
            Version: BlockValidator.SupportedVersion,
            TxRoot: TxRoot.Compute([Payment]),
            Nonce: 0,
            Difficulty: difficulty);

    private static MiningCandidate[] CreateCandidates(
        BlockHeader header,
        int count = MiningCoordinator.CandidateCount)
    {
        // Candidate contents may overlap or be identical.
        return Enumerable.Range(1, count)
            .Select(number => new MiningCandidate(
                number,
                header,
                [Payment],
                []))
            .ToArray();
    }

    private static BlockHeader FindHeaderWithThirdRoundSolution()
    {
        // Find a fixed input where nonces 0, 1 and 2 fail,
        // and the first successful nonce is between 3 and 6.
        for (int offset = 0; offset < 10_000; offset++)
        {
            BlockHeader header = CreateHeader(difficulty: 1) with
            {
                Timestamp = 1_793_206_327L + offset
            };

            MiningSearchResult probe = Miner.SearchRange(
                header,
                [Payment],
                startNonce: 0,
                maxAttempts: 7);

            if (probe.Block is { } block &&
                block.Header.Nonce >= 3)
            {
                return header;
            }
        }

        throw new InvalidOperationException(
            "Could not find a header for the third-round test.");
    }
}