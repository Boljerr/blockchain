namespace BlockChain.Models;

public enum TransactionReason
{
    None,
    InvalidIdentifierFormat,
    UnknownSender,
    UnknownReceiver,
    SelfPayment,
    InvalidAmount,
    InvalidSequence,
    TransactionIdMismatch,
    AlreadyPending,
    AlreadyConfirmed,
    DuplicateInBlock,
    InsufficientBalance,
    ArithmeticOverflow,
    TotalFundsMismatch
}