using BlockChain.Models;
using BlockChain.Transactions;

namespace BlockChain.Blocks;

public sealed class Chain
{
    private static readonly AcceptResult Accepted =
        new(true, BlockReason.None, TransactionReason.None, "Block accepted.");

    private readonly ITransactionService _service;
    private readonly List<Block> _blocks;

    public int Difficulty { get; }

    public int MaxTxCount { get; }

    public IReadOnlyList<Block> Blocks { get; }

    public int Height => _blocks.Count - 1;

    public string TipHash => _blocks[^1].DeclaredHash;

    public Chain(ITransactionService service, int difficulty, int maxTxCount)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
        Difficulty = difficulty;
        MaxTxCount = maxTxCount;

        _blocks = [Genesis.Create(difficulty)];
        Blocks = _blocks.AsReadOnly();
    }

    public AcceptResult TryAccept(Block block, TransactionSession session)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(session);

        BlockValidationResult check = BlockValidator.Validate(block, TipHash, Difficulty, MaxTxCount);
        if (!check.Valid)
            return new AcceptResult(false, check.ReasonCode, TransactionReason.None, check.Explanation);

        ApplyResult prepared = _service.PrepareApply(block.Transactions, session.Ledger);
        if (!prepared.Success)
            return new AcceptResult(false, BlockReason.None, prepared.ReasonCode, prepared.Explanation);

        _service.CommitAccepted(session, prepared);
        _blocks.Add(block);
        return Accepted;
    }
}