namespace BlockChain.Models;

public sealed record Transaction(
    string Sender,
    string Receiver,
    long Amount,
    long Sequence,
    string TransactionId);