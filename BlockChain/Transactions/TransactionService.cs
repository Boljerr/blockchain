using BlockChain.Models;

namespace BlockChain.Transactions;

public sealed class TransactionService : ITransactionService
{
    public SimulationInput Generate(SimulationConfig config)
        => throw new NotImplementedException();

    public SimulationInput CreateDemo()
        => throw new NotImplementedException();

    public void SaveInputs(SimulationInput input, string directory)
        => throw new NotImplementedException();

    public SimulationInput LoadInputs(string directory)
        => throw new NotImplementedException();

    public TransactionSession CreateSession(IReadOnlyList<User> users)
        => throw new NotImplementedException();

    public AdmissionResult AddToPool(
        TransactionSession session,
        Transaction transaction)
        => throw new NotImplementedException();

    public CandidateSelection SelectCandidate(
        TransactionSession session,
        Random selectionRandom,
        int maxCount)
        => throw new NotImplementedException();

    public ApplyResult PrepareApply(
        IReadOnlyList<Transaction> transactions,
        LedgerSnapshot state)
        => throw new NotImplementedException();

    public void CommitAccepted(
        TransactionSession session,
        ApplyResult prepared)
        => throw new NotImplementedException();

    public IReadOnlyList<RemainingTransaction> ExplainRemaining(
        TransactionSession session)
        => throw new NotImplementedException();
}