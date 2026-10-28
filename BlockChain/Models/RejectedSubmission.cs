namespace BlockChain.Models;

public sealed record RejectedSubmission(
    long SubmissionNumber,
    Transaction Transaction,
    TransactionReason ReasonCode,
    string Explanation);