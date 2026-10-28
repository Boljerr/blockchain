using BlockChain.Models;

namespace BlockChain.Transactions;

public interface ITransactionService
{
    SimulationInput Generate(SimulationConfig config);

    SimulationInput CreateDemo();

    void SaveInputs(SimulationInput input, string directory);

    SimulationInput LoadInputs(string directory);

    TransactionSession CreateSession(IReadOnlyList<User> users);

    AdmissionResult AddToPool(
        TransactionSession session,
        Transaction transaction);

    CandidateSelection SelectCandidate(
        TransactionSession session,
        Random selectionRandom,
        int maxCount);

    ApplyResult PrepareApply(
        IReadOnlyList<Transaction> transactions,
        LedgerSnapshot state);

    void CommitAccepted(
        TransactionSession session,
        ApplyResult prepared);

    IReadOnlyList<RemainingTransaction> ExplainRemaining(
        TransactionSession session);
}