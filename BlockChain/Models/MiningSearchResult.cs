namespace BlockChain.Models;

public enum MiningSearchStatus
{
    Found,
    BudgetExhausted,
    Cancelled,
    NonceExhausted
}

public sealed record MiningSearchResult(
    MiningSearchStatus Status,
    Block? Block,
    ulong Attempts,
    ulong? NextNonce,
    TimeSpan Elapsed);