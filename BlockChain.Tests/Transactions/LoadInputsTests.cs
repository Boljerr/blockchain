using System.Text.Json.Nodes;
using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Tests.Transactions;

public sealed class LoadInputsTests : IDisposable
{
    private readonly TransactionService _service = new();
    private readonly string _folder = Directory.CreateTempSubdirectory("blockchain-load-").FullName;

    public static TheoryData<string, string> DamagedFiles => new()
    {
        { "truncated", "is not valid simulation input JSON" },
        { "empty", "is not valid simulation input JSON" },
        { "null document", "contains null" },
        { "text instead of number", "is not valid simulation input JSON" },
        { "users missing", "is not valid simulation input JSON" },
        { "negative balance", "Users[0].InitialBalance is -1" },
        { "balance overflow", "makes the total of initial balances overflow" },
        { "duplicate key", "Users[1].PublicKey repeats" },
        { "malformed key", "Users[2].PublicKey is not 64 lowercase" },
        { "null user", "Users[1] is null" },
        { "null transaction", "Transactions[0] is null" },
        { "null post-run submission", "PostRunSubmissions[0] is null" },
        { "difficulty 0", "Configuration.Difficulty is 0" },
        { "difficulty 65", "Configuration.Difficulty is 65" },
        { "candidate limit 0", "Configuration.CandidateLimit is 0" },
        { "candidate limit 101", "Configuration.CandidateLimit is 101" },
        { "no initial attempts", "Configuration.InitialMiningAttempts must be positive" },
        { "no attempt limit", "Configuration.MaxMiningAttemptsPerCandidate is 0" },
        { "negative timestamp", "Configuration.TimestampBase is -1" },
        { "user count mismatch", "Configuration.UserCount is 4, but the file lists 3 users" },
        { "transaction count mismatch", "Configuration.TransactionCount is 6, but the file lists 5 transactions" }
    };

    [Fact]
    public void LoadInputs_SavedDemo_RoundTripsIncludingTransactionsThatPoolAdmissionRejects()
    {
        SimulationInput demo = _service.CreateDemo();
        _service.SaveInputs(demo, _folder);

        SimulationInput loaded = _service.LoadInputs(_folder);

        Assert.Equal(demo.Configuration, loaded.Configuration);
        Assert.Equal(demo.Users, loaded.Users);
        Assert.Equal(demo.Transactions, loaded.Transactions);
        Assert.Equal(demo.PostRunSubmissions, loaded.PostRunSubmissions);
    }

    [Theory]
    [MemberData(nameof(DamagedFiles))]
    public void LoadInputs_DamagedFile_ThrowsInvalidDataWithReason(string damage, string expectedMessage)
    {
        WriteDamagedDemo(damage);

        var exception = Assert.Throws<InvalidDataException>(() => _service.LoadInputs(_folder));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadInputs_MissingFile_ThrowsIOException()
    {
        Assert.ThrowsAny<IOException>(() => _service.LoadInputs(Path.Combine(_folder, "missing")));
    }

    [Fact]
    public void LoadInputs_ManyProblems_ListsFirstTenAndCountsTheRest()
    {
        SimulationInput input = _service.Generate(new SimulationConfig { UserCount = 25, TransactionCount = 10 });
        _service.SaveInputs(input, _folder);
        JsonNode root = ReadSaved();
        foreach (JsonNode? user in root["Users"]!.AsArray())
            user!["InitialBalance"] = -5;
        WriteSaved(root.ToJsonString());

        var exception = Assert.Throws<InvalidDataException>(() => _service.LoadInputs(_folder));

        Assert.Contains("has 25 problem(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("... and 15 more", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Users[9]", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Users[10]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_GeneratedAndDemoInputs_HaveNone()
    {
        Assert.Empty(SimulationInputValidator.FindProblems(_service.CreateDemo()));
        Assert.Empty(SimulationInputValidator.FindProblems(_service.Generate(new SimulationConfig())));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private void WriteDamagedDemo(string damage)
    {
        _service.SaveInputs(_service.CreateDemo(), _folder);
        string json = File.ReadAllText(SavedPath);
        JsonNode root = ReadSaved();
        JsonNode config = root["Configuration"]!;
        JsonArray users = root["Users"]!.AsArray();

        switch (damage)
        {
            case "truncated": WriteSaved(json[..(json.Length / 2)]); return;
            case "empty": WriteSaved(""); return;
            case "null document": WriteSaved("null"); return;
            case "text instead of number": config["Difficulty"] = "five"; break;
            case "users missing": root.AsObject().Remove("Users"); break;
            case "negative balance": users[0]!["InitialBalance"] = -1; break;
            case "balance overflow": users[0]!["InitialBalance"] = long.MaxValue; break;
            case "duplicate key": users[1]!["PublicKey"] = users[0]!["PublicKey"]!.GetValue<string>(); break;
            case "malformed key": users[2]!["PublicKey"] = "ABC"; break;
            case "null user": users[1] = null; break;
            case "null transaction": root["Transactions"]![0] = null; break;
            case "null post-run submission": root["PostRunSubmissions"]![0] = null; break;
            case "difficulty 0": config["Difficulty"] = 0; break;
            case "difficulty 65": config["Difficulty"] = 65; break;
            case "candidate limit 0": config["CandidateLimit"] = 0; break;
            case "candidate limit 101": config["CandidateLimit"] = 101; break;
            case "no initial attempts": config["InitialMiningAttempts"] = 0; break;
            case "no attempt limit": config["MaxMiningAttemptsPerCandidate"] = 0; break;
            case "negative timestamp": config["TimestampBase"] = -1; break;
            case "user count mismatch": config["UserCount"] = 4; break;
            case "transaction count mismatch": config["TransactionCount"] = 6; break;
            default: throw new ArgumentOutOfRangeException(nameof(damage), damage, null);
        }

        WriteSaved(root.ToJsonString());
    }

    private string SavedPath => Path.Combine(_folder, "inputs.json");

    private JsonNode ReadSaved() => JsonNode.Parse(File.ReadAllText(SavedPath))!;

    private void WriteSaved(string json) => File.WriteAllText(SavedPath, json);
}
