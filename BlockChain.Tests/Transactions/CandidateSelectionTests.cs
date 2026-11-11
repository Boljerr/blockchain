using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Transactions;

public class CandidateSelectionTests
{
    private static readonly string A = new('a', 64);
    private static readonly string B = new('b', 64);
    private static readonly string C = new('c', 64);

    private readonly TransactionService _service = new();

    [Fact]
    public void SelectCandidate_TwoPaymentsTogetherAboveBalance_SelectsOneAndDefersTheOther()
    {
        TransactionSession session = CreateSession(
            TestTransactions.Create(A, B, 70, 1),
            TestTransactions.Create(A, C, 60, 2));
        LedgerSnapshot ledger = session.Ledger;

        CandidateSelection selection = _service.SelectCandidate(session, new Random(1), 100);

        Transaction selected = Assert.Single(selection.SelectedTransactions);
        RemainingTransaction deferred = Assert.Single(selection.DeferredTransactions);
        Assert.Equal(TransactionReason.InsufficientBalance, deferred.ReasonCode);
        Assert.Equal([1L, 2L], new[] { selected.Sequence, deferred.Transaction.Sequence }.Order());
        Assert.Same(ledger, session.Ledger);
        Assert.Equal(2, session.PendingTransactions.Count);
    }

    [Fact]
    public void SelectCandidate_PaymentIsSelectedOnlyAfterItsFundingInBlockOrder()
    {
        Transaction funding = TestTransactions.Create(C, A, 50, 1);
        Transaction payment = TestTransactions.Create(A, B, 120, 2);
        TransactionSession session = CreateSession(funding, payment);
        var outcomes = new HashSet<string>();

        for (int seed = 0; seed < 20; seed++)
        {
            CandidateSelection selection = _service.SelectCandidate(session, new Random(seed), 100);
            List<string> selectedIds = selection.SelectedTransactions.Select(t => t.TransactionId).ToList();

            if (selectedIds.Contains(payment.TransactionId))
            {
                Assert.True(selectedIds.IndexOf(funding.TransactionId) < selectedIds.IndexOf(payment.TransactionId));
                outcomes.Add("both selected");
            }
            else
            {
                Assert.Equal([funding.TransactionId], selectedIds);
                Assert.Equal(TransactionReason.InsufficientBalance, Assert.Single(selection.DeferredTransactions).ReasonCode);
                outcomes.Add("payment deferred");
            }
        }

        Assert.Equal(2, outcomes.Count);
    }

    [Fact]
    public void SelectCandidate_SelectedTransactions_NeverOverdrawInBlockOrder()
    {
        string[] keys = [A, B, C, new('e', 64), new('f', 64)];
        var random = new Random(42);
        Transaction[] payments = Enumerable.Range(1, 40)
            .Select(sequence =>
            {
                int sender = random.Next(keys.Length);
                int receiver = (sender + 1 + random.Next(keys.Length - 1)) % keys.Length;
                return TestTransactions.Create(keys[sender], keys[receiver], random.Next(1, 80), sequence);
            })
            .ToArray();
        TransactionSession session = CreateSession(keys, payments);
        LedgerSnapshot ledger = session.Ledger;

        for (int seed = 0; seed < 20; seed++)
        {
            CandidateSelection selection = _service.SelectCandidate(session, new Random(seed), 100);

            var balances = new Dictionary<string, long>(ledger.Balances);
            foreach (Transaction transaction in selection.SelectedTransactions)
            {
                balances[transaction.Sender] -= transaction.Amount;
                balances[transaction.Receiver] += transaction.Amount;
                Assert.True(balances[transaction.Sender] >= 0);
            }

            Assert.Equal(payments.Length, selection.SelectedTransactions.Count + selection.DeferredTransactions.Count);
            Assert.Same(ledger, session.Ledger);
        }
    }

    [Fact]
    public void SelectCandidate_RespectsMaxCount()
    {
        TransactionSession session = CreateSession(
            Enumerable.Range(1, 5).Select(sequence => TestTransactions.Create(A, B, 1, sequence)).ToArray());

        CandidateSelection selection = _service.SelectCandidate(session, new Random(1), 2);

        Assert.Equal(2, selection.SelectedTransactions.Count);
    }

    [Fact]
    public void SelectCandidate_SameSeed_SelectsSameTransactionsInSameOrder()
    {
        TransactionSession session = CreateSession(
            Enumerable.Range(1, 10).Select(sequence => TestTransactions.Create(A, B, 15, sequence)).ToArray());

        CandidateSelection first = _service.SelectCandidate(session, new Random(7), 100);
        CandidateSelection second = _service.SelectCandidate(session, new Random(7), 100);

        Assert.Equal(
            first.SelectedTransactions.Select(t => t.TransactionId),
            second.SelectedTransactions.Select(t => t.TransactionId));
    }

    private TransactionSession CreateSession(params Transaction[] pending)
        => CreateSession([A, B, C], pending);

    private TransactionSession CreateSession(string[] keys, Transaction[] pending)
    {
        TransactionSession session = _service.CreateSession(
            keys.Select((key, index) => new User($"U{index}", key, 100)).ToArray());

        foreach (Transaction transaction in pending)
            Assert.True(_service.AddToPool(session, transaction).Accepted);

        return session;
    }
}