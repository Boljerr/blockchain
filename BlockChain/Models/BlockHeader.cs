namespace BlockChain.Models;

public sealed record BlockHeader(
    string PrevHash,
    long Timestamp,
    int Version,
    string TxRoot,
    ulong Nonce,
    int Difficulty);