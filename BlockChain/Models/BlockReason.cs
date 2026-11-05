namespace BlockChain.Models;

public enum BlockReason
{
    None,
    PrevHashMismatch,
    UnsupportedVersion,
    InvalidTransactionCount,
    MalformedTransactionId,
    TxRootMismatch,
    DifficultyMismatch,
    HashMismatch,
    InsufficientProofOfWork
}