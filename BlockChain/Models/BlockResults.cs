namespace BlockChain.Models;

public sealed record MineResult(
    Block Block,
    ulong Attempts,
    TimeSpan Elapsed);

public sealed record BlockValidationResult(
    bool Valid,
    BlockReason ReasonCode,
    string Explanation);

public sealed record AcceptResult(
    bool Accepted,
    BlockReason BlockReason,
    TransactionReason TransactionReason,
    string Explanation);