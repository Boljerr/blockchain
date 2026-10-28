namespace BlockChain.Models;

public sealed record SimulationConfig
{
    public int UserCount { get; init; } = 1_000;
    public int TransactionCount { get; init; } = 10_000;
    
    public long MinInitialBalance { get; init; } = 100;
    public long MaxInitialBalance { get; init; } = 1_000_000;
    
    public long MinAmount { get; init; } = 1;
    public long MaxAmount { get; init; } = 1_000;

    public int GenerationSeed { get; init; } = 676767;
    public int SelectionSeed { get; init; } = 676768;

    public int Difficulty { get; init; } = 3;
    public int CandidateLimit { get; init; } = 100;
    
    // Unix timestamp to be able to reproduce
    public long TimestampBase { get; init; } = 1_793_206_326; // 18:52
    
}