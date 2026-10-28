namespace BlockChain.Models;

public sealed record Block
{
    public BlockHeader Header { get; }

    public IReadOnlyList<Transaction> Transactions { get; }

    public string DeclaredHash { get; }

    public Block(
        BlockHeader header,
        IReadOnlyList<Transaction> transactions,
        string declaredHash)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(declaredHash);

        Header = header;
        Transactions = Array.AsReadOnly(transactions.ToArray());
        DeclaredHash = declaredHash;
    }
}