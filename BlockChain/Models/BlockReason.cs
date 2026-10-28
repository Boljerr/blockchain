namespace BlockChain.Models;

public enum BlockReason
{
    None,
    PrevHashMismatch,
    UnsupportedVersion,
    InvalidTransactionCount,
    TxRootMismatch,
    DifficultyMismatch,
    HashMismatch,
    InsufficientProofOfWork
}