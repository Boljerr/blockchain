using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Transactions;

public class BlockApplyTests
{
    private static readonly string A = new('a', 64);
    private static readonly string B = new('b', 64);
    private static readonly string C = new('c', 64);

    private static readonly Transaction Funding = TestTransactions.Create(C, A, 50, 1);
    private static readonly Transaction Payment = TestTransactions.Create(A, B, 120, 2);

    private readonly TransactionService _service = new();

    [Fact]
    public void PrepareApply_FundingBeforePayment_ProposesNewLedgerAndLeavesOriginalUnchanged()
    {
        LedgerSnapshot ledger = CreateLedger();

        ApplyResult result = _service.PrepareApply([Funding, Payment], ledger);

        Assert.True(result.Success);
        LedgerSnapshot proposed = Assert.IsType<LedgerSnapshot>(result.ProposedLedger);
        Assert.Equal(30, proposed.Balances[A]);
        Assert.Equal(220, proposed.Balances[B]);
        Assert.Equal(50, proposed.Balances[C]);
        Assert.Equal(300, proposed.Balances.Values.Sum());
        Assert.Equal([Funding.TransactionId, Payment.TransactionId], result.IncludedIds);
        Assert.Equal(100, ledger.Balances[A]);
        Assert.Empty(ledger.ConfirmedTransactionIds);
    }

    [Fact]
    public void PrepareApply_PaymentBeforeFunding_IsRejected()
    {
        AssertRejected(_service.PrepareApply([Payment, Funding], CreateLedger()), TransactionReason.InsufficientBalance);
    }

    [Fact]
    public void PrepareApply_TwoPaymentsTogetherAboveBalance_IsRejected()
    {
        Transaction[] block = [TestTransactions.Create(A, B, 70, 3), TestTransactions.Create(A, C, 60, 4)];

        AssertRejected(_service.PrepareApply(block, CreateLedger()), TransactionReason.InsufficientBalance);
    }

    [Fact]
    public void PrepareApply_SameTransactionTwice_IsRejected()
    {
        AssertRejected(_service.PrepareApply([Funding, Funding], CreateLedger()), TransactionReason.DuplicateInBlock);
    }

    [Fact]
    public void PrepareApply_AlreadyConfirmedTransaction_IsRejected()
    {
        LedgerSnapshot afterFunding = _service.PrepareApply([Funding], CreateLedger()).ProposedLedger!;

        AssertRejected(_service.PrepareApply([Funding], afterFunding), TransactionReason.AlreadyConfirmed);
    }

    [Fact]
    public void PrepareApply_AmountChangedAfterId_IsRejected()
    {
        AssertRejected(
            _service.PrepareApply([Funding with { Amount = 5 }], CreateLedger()),
            TransactionReason.TransactionIdMismatch);
    }

    [Fact]
    public void PrepareApply_BalancesBeyondLongRange_IsRejectedAsOverflow()
    {
        var ledger = new LedgerSnapshot(
            new Dictionary<string, long> { [A] = 100, [B] = long.MaxValue - 10 },
            new HashSet<string>());

        AssertRejected(
            _service.PrepareApply([TestTransactions.Create(A, B, 50, 1)], ledger),
            TransactionReason.ArithmeticOverflow);
    }

    private LedgerSnapshot CreateLedger()
        => _service.CreateSession([new User("A", A, 100), new User("B", B, 100), new User("C", C, 100)]).Ledger;

    private static void AssertRejected(ApplyResult result, TransactionReason expected)
    {
        Assert.False(result.Success);
        Assert.Equal(expected, result.ReasonCode);
        Assert.Null(result.ProposedLedger);
        Assert.Empty(result.IncludedIds);
    }
}