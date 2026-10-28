namespace BlockChain.Models;

public sealed record SimulationInput(
    SimulationConfig Configuration,
    IReadOnlyList<User> Users,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<Transaction> PostRunSubmissions);