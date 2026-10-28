using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Simulation;

internal sealed class FakeTransactionService : ITransactionService
{
    public bool FailPrepareApply { get; init; }

    public int PrepareApplyCalls { get; private set; }

    public SimulationInput Generate(SimulationConfig config) => throw new NotSupportedException();

    public SimulationInput CreateDemo() => throw new NotSupportedException();

    public void SaveInputs(SimulationInput input, string directory) => throw new NotSupportedException();

    public SimulationInput LoadInputs(string directory) => throw new NotSupportedException();

    public TransactionSession CreateSession(IReadOnlyList<User> users)
    {
        var balances = users.ToDictionary(u => u.PublicKey, u => u.InitialBalance, StringComparer.Ordinal);
        var ledger = new LedgerSnapshot(balances, new HashSet<string>(StringComparer.Ordinal));
        return new TransactionSession(ledger, users.Sum(u => u.InitialBalance));
    }

    public AdmissionResult AddToPool(TransactionSession session, Transaction transaction)
    {
        session.SubmissionCount++;

        TransactionReason reason =
            session.Ledger.ConfirmedTransactionIds.Contains(transaction.TransactionId) ? TransactionReason.AlreadyConfirmed
            : session.Pending.ContainsKey(transaction.TransactionId) ? TransactionReason.AlreadyPending
            : !session.Ledger.Balances.ContainsKey(transaction.Receiver) ? TransactionReason.UnknownReceiver
            : TransactionReason.None;

        if (reason != TransactionReason.None)
        {
            session.Rejections.Add(new RejectedSubmission(session.SubmissionCount, transaction, reason, reason.ToString()));
            return new AdmissionResult(false, reason, reason.ToString());
        }

        session.Pending.Add(transaction.TransactionId, transaction);
        return new AdmissionResult(true, TransactionReason.None, "Added to pool.");
    }

    public CandidateSelection SelectCandidate(TransactionSession session, Random selectionRandom, int maxCount)
    {
        var balances = new Dictionary<string, long>(session.Ledger.Balances, StringComparer.Ordinal);
        var selected = new List<Transaction>();
        var deferred = new List<RemainingTransaction>();

        foreach (Transaction transaction in session.Pending.Values.OrderBy(_ => selectionRandom.Next()))
        {
            if (selected.Count == maxCount)
                break;

            if (balances[transaction.Sender] < transaction.Amount)
            {
                deferred.Add(new RemainingTransaction(transaction, TransactionReason.InsufficientBalance, "Insufficient balance."));
                continue;
            }

            balances[transaction.Sender] -= transaction.Amount;
            balances[transaction.Receiver] += transaction.Amount;
            selected.Add(transaction);
        }

        return new CandidateSelection(selected, deferred);
    }

    public ApplyResult PrepareApply(IReadOnlyList<Transaction> transactions, LedgerSnapshot state)
    {
        PrepareApplyCalls++;

        if (FailPrepareApply)
            return new ApplyResult(false, TransactionReason.InsufficientBalance, "Forced failure.", state, null, []);

        var balances = new Dictionary<string, long>(state.Balances, StringComparer.Ordinal);
        foreach (Transaction transaction in transactions)
        {
            balances[transaction.Sender] -= transaction.Amount;
            balances[transaction.Receiver] += transaction.Amount;
        }

        var confirmed = new HashSet<string>(state.ConfirmedTransactionIds, StringComparer.Ordinal);
        string[] includedIds = transactions.Select(t => t.TransactionId).ToArray();
        confirmed.UnionWith(includedIds);

        return new ApplyResult(true, TransactionReason.None, "Applied.", state, new LedgerSnapshot(balances, confirmed), includedIds);
    }

    public void CommitAccepted(TransactionSession session, ApplyResult prepared)
    {
        if (!ReferenceEquals(session.Ledger, prepared.OriginalLedger))
            throw new InvalidOperationException("Ledger changed since PrepareApply.");

        session.Ledger = prepared.ProposedLedger!;
        foreach (string id in prepared.IncludedIds)
            session.Pending.Remove(id);
    }

    public IReadOnlyList<RemainingTransaction> ExplainRemaining(TransactionSession session)
        => session.Pending.Values
            .Select(t => new RemainingTransaction(t, TransactionReason.InsufficientBalance, "Insufficient balance."))
            .ToArray();
}