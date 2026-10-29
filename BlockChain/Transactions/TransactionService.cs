using System.Text;
using System.Text.Json;
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
    {
        string a = new('a', 64);
        string b = new('b', 64);
        string c = new('c', 64);
        string unknown = new('d', 64);
        
        Transaction Create(
            string sender,
            string receiver,
            long amount,
            long sequence)
            => new(
                sender,
               receiver,
                amount,
                sequence,
                _codec.ComputeId(sender, receiver, amount, sequence));

        User[] users =
        [
            new("A", a, 100),
            new("B", b, 100),
            new("C", c, 100)
        ];

        Transaction funding = Create(c, a, 50, 1);
        Transaction payment = Create(a, b, 120, 2);
        
        Transaction tampered = Create(a, b, 1, 5) with { Amount = 2 };
        
        Transaction[] transactions =
        [
            funding,
            payment,
            Create(a, unknown, 10, 3),
            Create(a, b, 0, 4),
            tampered
        ];
        var config = new SimulationConfig
        {
            UserCount = users.Length,
            TransactionCount = transactions.Length,
            GenerationSeed = 0,
            SelectionSeed = 0,
            Difficulty = 2,
            CandidateLimit = 100
        };

        return new SimulationInput(
            config,
            users,
            transactions,
            [funding]);
    }

    public void SaveInputs(SimulationInput input, string directory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        string json = JsonSerializer.Serialize(input, options);
        
        Directory.CreateDirectory(directory);
        
        string path = Path.Combine(directory, "inputs.json");
        File.WriteAllText(path, json, new UTF8Encoding(false));
        
    }

    public SimulationInput LoadInputs(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        
        string path = Path.Combine(directory, "inputs.json");
        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true
        };

        try
        {
            return JsonSerializer.Deserialize<SimulationInput>(json, options)
                   ?? throw new InvalidDataException(
                       "Saved simulation input cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Invalid simulation input in {path}", ex);
        }
    }
    public TransactionSession CreateSession(IReadOnlyList<User> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        var balances = new Dictionary<string, long>(StringComparer.Ordinal);
        long initialTotal = 0;

        foreach (User user in users)
        {
            ArgumentNullException.ThrowIfNull(user);

            string key = user.PublicKey;

            if (string.IsNullOrEmpty(key) || key.Length != 64 ||
                key.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            {
                throw new ArgumentException(
                    "Each user must have a 64-character lowercase hexadecimal public key.",
                    nameof(users));
            }

            if (user.InitialBalance < 0)
            {
                throw new ArgumentException(
                    "Initial balance cannot be negative", nameof(users));
            }

            if (!balances.TryAdd(key, user.InitialBalance))
            {
                throw new ArgumentException(
                    "Users cannot share the same public key.", nameof(users));
            }

            initialTotal = checked(initialTotal + user.InitialBalance);
        }

        var confirmedIds = new HashSet<string>(StringComparer.Ordinal);
        var ledger = new LedgerSnapshot(balances, confirmedIds);

        return new TransactionSession(ledger, initialTotal);
    }

    public AdmissionResult AddToPool(
        TransactionSession session,
        Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(transaction);

        session.SubmissionCount = checked(session.SubmissionCount + 1);

        AdmissionResult Reject(TransactionReason reason, string explanation)
        {
            session.Rejections.Add(new RejectedSubmission(
                session.SubmissionCount,
                transaction,
                reason,
                explanation));

            return new AdmissionResult(false, reason, explanation);
        }

        if (!IsValidIdentifier(transaction.Sender) ||
            !IsValidIdentifier(transaction.Receiver) ||
            !IsValidIdentifier(transaction.TransactionId))
        {
            return Reject(
                TransactionReason.InvalidIdentifierFormat,
                "Identifiers must contain 64 lowercase hexadecimal characters.");
        }

        if (!session.Ledger.Balances.ContainsKey(transaction.Sender))
            return Reject(TransactionReason.UnknownSender, "Sender doesn't exist.");
        if (!session.Ledger.Balances.ContainsKey(transaction.Receiver))
            return Reject(TransactionReason.UnknownReceiver, "Receiver doesn't exist.");

        if (transaction.Sender == transaction.Receiver)
            return Reject(TransactionReason.SelfPayment, "Self-payments are not allowed.");

        if (transaction.Amount <= 0)
            return Reject(TransactionReason.InvalidAmount, "Amount must be positive.");

        if (transaction.Sequence <= 0)
            return Reject(TransactionReason.InvalidSequence, "Sequence must be positive.");

        string expectedId = _codec.ComputeId(
            transaction.Sender,
            transaction.Receiver,
            transaction.Amount,
            transaction.Sequence);

        if (!string.Equals(
                transaction.TransactionId, expectedId, StringComparison.Ordinal))
        {
            return Reject(
                TransactionReason.TransactionIdMismatch,
                "Transaction ID does not match its contents.");
        }

        if (session.Ledger.ConfirmedTransactionIds.Contains(transaction.TransactionId))
        {
            return Reject(
                TransactionReason.AlreadyConfirmed,
                "Transaction is already confirmed.");
        }

        if (session.Pending.ContainsKey(transaction.TransactionId))
        {
            return Reject(
                TransactionReason.AlreadyPending,
                "Transaction is already pending.");
        }

        session.Pending.Add(transaction.TransactionId, transaction);

        return new AdmissionResult(true, TransactionReason.None, "Added to pool.");
    }

    public CandidateSelection SelectCandidate(
        TransactionSession session,
        Random selectionRandom,
        int maxCount)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selectionRandom);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        Transaction[] candidates =
            session.Pending.Values.OrderBy(t => t.TransactionId, StringComparer.Ordinal).ToArray();
        for (int i = candidates.Length - 1; i > 0; i--)
        {
            int j = selectionRandom.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var balances = new Dictionary<string, long>(
            session.Ledger.Balances, StringComparer.Ordinal);

        var selected = new List<Transaction>();
        var deferred = new List<RemainingTransaction>();

        foreach (Transaction transaction in candidates)
        {
            if (selected.Count == maxCount)
                break;
            if (balances[transaction.Sender] < transaction.Amount)
            {
                deferred.Add(new RemainingTransaction(
                    transaction,
                    TransactionReason.InsufficientBalance,
                    "Sender has insufficient funds at this point in selection"));
                continue;
            }

            try
            {
                long senderAfter = checked(balances[transaction.Sender] - transaction.Amount);
                long receiverAfter = checked(balances[transaction.Receiver] + transaction.Amount);

                balances[transaction.Sender] = senderAfter;
                balances[transaction.Receiver] = receiverAfter;
            }
            catch (OverflowException)
            {
                deferred.Add(new RemainingTransaction(
                    transaction,
                    TransactionReason.ArithmeticOverflow,
                    "Balance arithmetic exceeded the supported range"));
                continue;
            }

            selected.Add(transaction);
        }

        return new CandidateSelection(
            selected.AsReadOnly(),
            deferred.AsReadOnly());
    }

    public ApplyResult PrepareApply(
        IReadOnlyList<Transaction> transactions,
        LedgerSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(state);

        ApplyResult Reject(TransactionReason reason, string explanation)
            => new(false, reason, explanation, state, null, []);

        var balances = new Dictionary<string, long>(
            state.Balances, StringComparer.Ordinal);

        var confirmed = new HashSet<string>(
            state.ConfirmedTransactionIds, StringComparer.Ordinal);

        var includedIds = new List<string>(transactions.Count);

        try
        {
            long initialTotal = balances.Values.Sum();

            foreach (Transaction transaction in transactions)
            {
                ArgumentNullException.ThrowIfNull(transaction);

                if (!IsValidIdentifier(transaction.Sender) ||
                    !IsValidIdentifier(transaction.Receiver) ||
                    !IsValidIdentifier(transaction.TransactionId))
                {
                    return Reject(
                        TransactionReason.InvalidIdentifierFormat,
                        "Identifiers must contain 64 lowercase hexadecimal characters.");
                }

                if (!balances.ContainsKey(transaction.Sender))
                    return Reject(
                        TransactionReason.UnknownSender, "Sender does not exist.");

                if (!balances.ContainsKey(transaction.Receiver))
                    return Reject(
                        TransactionReason.UnknownReceiver, "Receiver does not exist.");

                if (transaction.Sender == transaction.Receiver)
                    return Reject(
                        TransactionReason.SelfPayment, "Self-payments are not allowed.");

                if (transaction.Amount <= 0)
                    return Reject(
                        TransactionReason.InvalidAmount, "Amount must be positive.");

                if (transaction.Sequence <= 0)
                    return Reject(
                        TransactionReason.InvalidSequence, "Sequence must be positive.");

                string expectedId = _codec.ComputeId(
                    transaction.Sender,
                    transaction.Receiver,
                    transaction.Amount,
                    transaction.Sequence);

                if (!string.Equals(
                        transaction.TransactionId,
                        expectedId,
                        StringComparison.Ordinal))
                {
                    return Reject(
                        TransactionReason.TransactionIdMismatch,
                        "Transaction ID does not match its contents.");
                }

                if (state.ConfirmedTransactionIds.Contains(transaction.TransactionId))
                {
                    return Reject(
                        TransactionReason.AlreadyConfirmed,
                        "Transaction is already confirmed.");
                }

                if (!confirmed.Add(transaction.TransactionId))
                {
                    return Reject(
                        TransactionReason.DuplicateInBlock,
                        "Transaction appears more than once in this block.");
                }

                if (balances[transaction.Sender] < transaction.Amount)
                {
                    return Reject(
                        TransactionReason.InsufficientBalance,
                        "Sender has insufficient funds at this point in the block.");
                }

                balances[transaction.Sender] = checked(
                    balances[transaction.Sender] - transaction.Amount);

                balances[transaction.Receiver] = checked(
                    balances[transaction.Receiver] + transaction.Amount);

                includedIds.Add(transaction.TransactionId);
            }

            if (balances.Values.Sum() != initialTotal)
            {
                return Reject(
                    TransactionReason.TotalFundsMismatch,
                    "Applying the transactions changed the total funds.");
            }

            var proposed = new LedgerSnapshot(balances, confirmed);

            return new ApplyResult(
                true,
                TransactionReason.None,
                "Transactions validated successfully.",
                state,
                proposed,
                includedIds);
        }
        catch (OverflowException)
        {
            return Reject(
                TransactionReason.ArithmeticOverflow,
                "Balance arithmetic exceeded the supported range.");
        }
    }

    public void CommitAccepted(
        TransactionSession session,
        ApplyResult prepared)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(prepared);

        LedgerSnapshot? proposed = prepared.ProposedLedger;

        if (!prepared.Success || proposed == null)
        {
            throw new InvalidOperationException(
                "Cannot commit an unsuccessful transaction.");
        }
        
        if (!ReferenceEquals(session.Ledger, prepared.OriginalLedger))
        {
            throw new InvalidOperationException(
                "Ledger chanced since preparation.");
        }

        session.Ledger = proposed;

        foreach (var id in prepared.IncludedIds)
        {
            session.Pending.Remove(id);
        }
    }
    public IReadOnlyList<RemainingTransaction> ExplainRemaining(
        TransactionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var remaining = new List<RemainingTransaction>();

        foreach (Transaction transaction in
                 session.Pending.Values.OrderBy(t => t.TransactionId, StringComparer.Ordinal))
        {
            ApplyResult check = PrepareApply([transaction], session.Ledger);

            remaining.Add(new RemainingTransaction(
                transaction,
                check.ReasonCode,
                check.Success
                    ? "Transactiton can execute against the current ledger but is still pending"
                    : check.Explanation));
        }
        return remaining.AsReadOnly();
    }

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

    private static bool IsValidIdentifier(string? value)
    {
        return value is { Length: 64 } &&
               value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}