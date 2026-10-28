namespace BlockChain.Models;

public sealed record SimulationInput
{
    public SimulationConfig Configuration { get; }

    public IReadOnlyList<User> Users { get; }

    public IReadOnlyList<Transaction> Transactions { get; }

    public IReadOnlyList<Transaction> PostRunSubmissions { get; }

    public SimulationInput(
        SimulationConfig configuration,
        IReadOnlyList<User> users,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Transaction> postRunSubmissions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(postRunSubmissions);

        Configuration = configuration;
        Users = Array.AsReadOnly(users.ToArray());
        Transactions = Array.AsReadOnly(transactions.ToArray());
        PostRunSubmissions = Array.AsReadOnly(postRunSubmissions.ToArray());
    }
}