namespace BlockChain.Models;

public sealed record User(
    string Name,
    string PublicKey,
    long InitialBalance);