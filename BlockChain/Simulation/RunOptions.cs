using System.Globalization;
using BlockChain.Models;

namespace BlockChain.Simulation;

public enum RunMode
{
    Demo,
    Full,
    Replay
}

public sealed record RunOptions(RunMode Mode, int Seed, string? ReplayFolder)
{
    public const string Usage = """
        Usage:
          dotnet run --project BlockChain -- --demo
          dotnet run --project BlockChain -- --full [--seed N]
          dotnet run --project BlockChain -- --replay <folder>
        """;

    private static readonly int DefaultSeed = new SimulationConfig().GenerationSeed;

    public static RunOptions? Parse(string[] args) => args switch
    {
        ["--demo"] => new RunOptions(RunMode.Demo, 0, null),
        ["--full"] => new RunOptions(RunMode.Full, DefaultSeed, null),
        ["--full", "--seed", var text] when TryParseSeed(text, out int seed) => new RunOptions(RunMode.Full, seed, null),
        ["--replay", var folder] when !folder.StartsWith('-') => new RunOptions(RunMode.Replay, 0, folder),
        _ => null
    };

    private static bool TryParseSeed(string text, out int seed)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seed) && seed < int.MaxValue;
}