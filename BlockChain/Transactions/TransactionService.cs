using System.Text;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Transactions;

public sealed class TransactionService : ITransactionService
{
    private readonly HashFunc _hash = new();
    private readonly TransactionCodec _codec = new();

    public SimulationInput Generate(SimulationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateGenerationConfig(config);
     
        var random = new Random(config.GenerationSeed);
        
        var users = new List<User>(config.UserCount);
        var publicKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < config.UserCount; i++)
        {
            string name = FormattableString.Invariant($"User{i + 1:D4}");

            string publicKey = GeneratePublicKey(config.GenerationSeed, i + 1, publicKeys);
            
            long balance = random.NextInt64(config.MinInitialBalance, config.MaxInitialBalance + 1);
            
            users.Add(new User(name, publicKey, balance));
        }
        
        var transactions = new List<Transaction>(config.TransactionCount);
        var transactionIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < config.TransactionCount; i++)
        {
            int senderIndex = random.Next(users.Count);
            
            int receiverIndex = random.Next(users.Count - 1);

            if (receiverIndex >= senderIndex)
            {
                receiverIndex++;
            }

            string sender = users[senderIndex].PublicKey;
            string receiver = users[receiverIndex].PublicKey;
            
            long amount = random.NextInt64(config.MinAmount - 1, config.MaxAmount) + 1;

            long sequence = (long)i + 1;
            
            string id = _codec.ComputeId(sender, receiver, amount, sequence);

            if (!transactionIds.Add(id))
            {
                throw new InvalidOperationException($"Transaction ID collision at sequence {sequence}.");
            }
            transactions.Add(new Transaction(sender, receiver, amount, sequence, id));
        }

        return new SimulationInput(
            config,
            users.AsReadOnly(),
            transactions.AsReadOnly(),
            Array.Empty<Transaction>());
    }

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

    private string GeneratePublicKey(
        int seed,
        int index,
        HashSet<string> existingKeys)
    {
        for (int retry = 0; retry < 1_000; retry++)
        {
            string text = retry == 0
                ? FormattableString.Invariant($"user|{seed}|{index}")
                : FormattableString.Invariant($"user|{seed}|{index}|{retry}");

            string key = _hash.ComputeHash(Encoding.UTF8.GetBytes(text));

            if (existingKeys.Add(key))
            {
                return key;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a unique public key for user {index}.");
    }
    private static void ValidateGenerationConfig(SimulationConfig config)
    {
        if (config.UserCount < 2)
        {
            throw new ArgumentException(
                "Generation requires at least two users.", nameof(config));
        }

        if (config.TransactionCount < 0)
        {
            throw new ArgumentException(
                "Transaction count cannot be negative.", nameof(config));
        }

        if (config.MinInitialBalance < 100 ||
            config.MaxInitialBalance > 1_000_000 ||
            config.MinInitialBalance > config.MaxInitialBalance)
        {
            throw new ArgumentException(
                "Initial balances must be within 100–1,000,000, with min <= max.",
                nameof(config));
        }

        if (config.MinAmount <= 0 ||
            config.MinAmount > config.MaxAmount)
        {
            throw new ArgumentException(
                "Amounts must be positive, with min <= max.", nameof(config));
        }
    }
}