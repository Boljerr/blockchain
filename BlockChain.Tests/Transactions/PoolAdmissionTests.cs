using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Transactions;

public class PoolAdmissionTests
{
    private static readonly string A = new('a', 64);
    private static readonly string B = new('b', 64);
    private static readonly string Unknown = new('d', 64);

    private readonly TransactionService _service = new();

    public static TheoryData<string, TransactionReason> InvalidSubmissions => new()
    {
        { "unknown sender", TransactionReason.UnknownSender },
        { "unknown receiver", TransactionReason.UnknownReceiver },
        { "self-payment", TransactionReason.SelfPayment },
        { "zero amount", TransactionReason.InvalidAmount },
        { "negative amount", TransactionReason.InvalidAmount },
        { "zero sequence", TransactionReason.InvalidSequence },
        { "amount changed after ID", TransactionReason.TransactionIdMismatch },
        { "uppercase ID", TransactionReason.InvalidIdentifierFormat }
    };

    [Fact]
    public void AddToPool_ValidPayment_IsPending()
    {
        TransactionSession session = CreateSession();
        Transaction payment = TestTransactions.Create(A, B, 70, 1);

        AdmissionResult result = _service.AddToPool(session, payment);

        Assert.True(result.Accepted);
        Assert.Equal(payment, session.PendingTransactions[payment.TransactionId]);
        Assert.Empty(session.RejectedSubmissions);
    }

    [Theory]
    [MemberData(nameof(InvalidSubmissions))]
    public void AddToPool_InvalidSubmission_IsRejectedWithVisibleReason(string submission, TransactionReason expected)
    {
        TransactionSession session = CreateSession();
        LedgerSnapshot ledger = session.Ledger;

        AdmissionResult result = _service.AddToPool(session, Build(submission));

        Assert.False(result.Accepted);
        Assert.Equal(expected, result.ReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
        Assert.Equal(expected, Assert.Single(session.RejectedSubmissions).ReasonCode);
        Assert.Empty(session.PendingTransactions);
        Assert.Same(ledger, session.Ledger);
    }

    [Fact]
    public void AddToPool_SameTransactionTwice_SecondIsAlreadyPending()
    {
        TransactionSession session = CreateSession();
        Transaction payment = TestTransactions.Create(A, B, 70, 1);
        _service.AddToPool(session, payment);

        AdmissionResult result = _service.AddToPool(session, payment);

        Assert.Equal(TransactionReason.AlreadyPending, result.ReasonCode);
        Assert.Single(session.PendingTransactions);
    }

    [Fact]
    public void AddToPool_ConfirmedTransactionAgain_IsAlreadyConfirmed()
    {
        TransactionSession session = CreateSession();
        Transaction payment = TestTransactions.Create(A, B, 70, 1);
        _service.AddToPool(session, payment);
        _service.CommitAccepted(session, _service.PrepareApply([payment], session.Ledger));

        AdmissionResult result = _service.AddToPool(session, payment);

        Assert.Equal(TransactionReason.AlreadyConfirmed, result.ReasonCode);
        Assert.Empty(session.PendingTransactions);
        Assert.Equal(30, session.Ledger.Balances[A]);
    }

    [Fact]
    public void AddToPool_PaymentsTogetherAboveBalance_AreBothAdmittedWithoutReservingFunds()
    {
        TransactionSession session = CreateSession();

        Assert.True(_service.AddToPool(session, TestTransactions.Create(A, B, 70, 1)).Accepted);
        Assert.True(_service.AddToPool(session, TestTransactions.Create(A, B, 60, 2)).Accepted);

        Assert.Equal(2, session.PendingTransactions.Count);
        Assert.Equal(100, session.Ledger.Balances[A]);
    }

    private TransactionSession CreateSession()
        => _service.CreateSession([new User("A", A, 100), new User("B", B, 100)]);

    private static Transaction Build(string submission) => submission switch
    {
        "unknown sender" => TestTransactions.Create(Unknown, B, 10, 1),
        "unknown receiver" => TestTransactions.Create(A, Unknown, 10, 1),
        "self-payment" => TestTransactions.Create(A, A, 10, 1),
        "zero amount" => TestTransactions.Create(A, B, 0, 1),
        "negative amount" => TestTransactions.Create(A, B, -5, 1),
        "zero sequence" => TestTransactions.Create(A, B, 10, 0),
        "amount changed after ID" => TestTransactions.Create(A, B, 10, 1) with { Amount = 20 },
        "uppercase ID" => WithUppercaseId(TestTransactions.Create(A, B, 10, 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(submission), submission, null)
    };

    private static Transaction WithUppercaseId(Transaction transaction)
        => transaction with { TransactionId = transaction.TransactionId.ToUpperInvariant() };
}