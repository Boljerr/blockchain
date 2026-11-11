using BlockChain.Blocks;
using BlockChain.Models;
using static System.FormattableString;

namespace BlockChain.Transactions;

public static class SimulationInputValidator
{
    public const int MaxCandidateLimit = 100;
    public const int MaxDifficulty = 64;
    public const long MaxTimestampBase = 253_402_300_799;

    public static IReadOnlyList<string> FindProblems(SimulationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var problems = new List<string>();
        SimulationConfig config = input.Configuration;

        if (config.Difficulty is < 1 or > MaxDifficulty)
            problems.Add(Invariant($"Configuration.Difficulty is {config.Difficulty}; expected 1 to {MaxDifficulty}."));

        if (config.CandidateLimit is < 1 or > MaxCandidateLimit)
            problems.Add(Invariant($"Configuration.CandidateLimit is {config.CandidateLimit}; expected 1 to {MaxCandidateLimit}."));

        if (config.InitialMiningAttempts == 0)
            problems.Add("Configuration.InitialMiningAttempts must be positive.");

        if (config.MaxMiningAttemptsPerCandidate == 0 ||
            config.MaxMiningAttemptsPerCandidate > ulong.MaxValue / MiningCoordinator.CandidateCount)
        {
            problems.Add(Invariant(
                $"Configuration.MaxMiningAttemptsPerCandidate is {config.MaxMiningAttemptsPerCandidate}; expected 1 to {ulong.MaxValue / MiningCoordinator.CandidateCount}."));
        }

        if (config.TimestampBase is < 0 or > MaxTimestampBase)
            problems.Add(Invariant($"Configuration.TimestampBase is {config.TimestampBase}; expected 0 to {MaxTimestampBase}."));

        if (config.UserCount != input.Users.Count)
            problems.Add(Invariant($"Configuration.UserCount is {config.UserCount}, but the file lists {input.Users.Count} users."));

        if (config.TransactionCount != input.Transactions.Count)
            problems.Add(Invariant($"Configuration.TransactionCount is {config.TransactionCount}, but the file lists {input.Transactions.Count} transactions."));

        var keys = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;

        for (int i = 0; i < input.Users.Count; i++)
        {
            User? user = input.Users[i];

            if (user is null)
            {
                problems.Add(Invariant($"Users[{i}] is null."));
                continue;
            }

            if (!IsLowercaseHex64(user.PublicKey))
                problems.Add(Invariant($"Users[{i}].PublicKey is not 64 lowercase hexadecimal characters."));
            else if (!keys.Add(user.PublicKey))
                problems.Add(Invariant($"Users[{i}].PublicKey repeats the key of an earlier user."));

            if (user.InitialBalance < 0)
                problems.Add(Invariant($"Users[{i}].InitialBalance is {user.InitialBalance}; it cannot be negative."));
            else if (total > long.MaxValue - user.InitialBalance)
                problems.Add(Invariant($"Users[{i}].InitialBalance makes the total of initial balances overflow."));
            else
                total += user.InitialBalance;
        }

        AddNullEntries(input.Transactions, nameof(SimulationInput.Transactions), problems);
        AddNullEntries(input.PostRunSubmissions, nameof(SimulationInput.PostRunSubmissions), problems);

        return problems;
    }

    private static void AddNullEntries(IReadOnlyList<Transaction?> transactions, string listName, List<string> problems)
    {
        for (int i = 0; i < transactions.Count; i++)
        {
            if (transactions[i] is null)
                problems.Add(Invariant($"{listName}[{i}] is null."));
        }
    }

    private static bool IsLowercaseHex64(string? value)
        => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
