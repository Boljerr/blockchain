using BlockChain.Blocks;
using BlockChain.Models;

namespace BlockChain.Tests.Blocks;

public class MiningSearchTests
{
    private static BlockHeader CreateHeader(int difficulty)
        => new(
            PrevHash: new string('0', 64),
            Timestamp: 1_793_206_326,
            Version: BlockValidator.SupportedVersion,
            TxRoot: TxRoot.Compute([]),
            Nonce: 0,
            Difficulty: difficulty);

    [Fact]
    public void SearchRange_StartsAtRequestedNonce()
    {
        // Difficulty 0 makes the first tested nonce succeed.
        BlockHeader template = CreateHeader(difficulty: 0);

        MiningSearchResult result = Miner.SearchRange(
            template,
            [],
            startNonce: 42,
            maxAttempts: 10);

        Assert.Equal(MiningSearchStatus.Found, result.Status);
        Assert.Equal(1UL, result.Attempts);
        Assert.Equal((ulong?)43, result.NextNonce);

        Block block = Assert.IsType<Block>(result.Block);

        Assert.Equal(42UL, block.Header.Nonce);
        Assert.Equal(template with { Nonce = 42 }, block.Header);
        Assert.Equal(
            BlockHasher.ComputeHash(block.Header),
            block.DeclaredHash);
    }

    [Fact]
    public void SearchRange_ExhaustedBudget_CanResumeWithoutRepeatingNonce()
    {
        BlockHeader template = CreateHeader(difficulty: 3);
        string prefix = new('0', template.Difficulty);

        // Find a nonce known to fail, without assuming a particular hash.
        ulong failingNonce = (ulong)Enumerable.Range(0, 10_000)
            .First(nonce =>
                !BlockHasher.ComputeHash(
                    template with { Nonce = (ulong)nonce })
                .StartsWith(prefix, StringComparison.Ordinal));

        MiningSearchResult first = Miner.SearchRange(
            template,
            [],
            startNonce: failingNonce,
            maxAttempts: 1);

        Assert.Equal(
            MiningSearchStatus.BudgetExhausted,
            first.Status);

        Assert.Null(first.Block);
        Assert.Equal(1UL, first.Attempts);
        Assert.Equal((ulong?)(failingNonce + 1), first.NextNonce);

        ulong nextNonce = first.NextNonce!.Value;

        MiningSearchResult second = Miner.SearchRange(
            template,
            [],
            startNonce: nextNonce,
            maxAttempts: 1);

        Assert.Equal(1UL, second.Attempts);
        Assert.Equal((ulong?)(nextNonce + 1), second.NextNonce);

        if (second.Block is { } block)
            Assert.Equal(nextNonce, block.Header.Nonce);
    }

    [Fact]
    public void SearchRange_AlreadyCancelled_DoesNoWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        MiningSearchResult result = Miner.SearchRange(
            CreateHeader(difficulty: 3),
            [],
            startNonce: 100,
            maxAttempts: 1_000,
            cancellationToken: cancellation.Token);

        Assert.Equal(MiningSearchStatus.Cancelled, result.Status);
        Assert.Null(result.Block);
        Assert.Equal(0UL, result.Attempts);
        Assert.Equal((ulong?)100, result.NextNonce);
    }

    [Fact]
    public void SearchRange_ZeroBudget_DoesNotAdvance()
    {
        MiningSearchResult result = Miner.SearchRange(
            CreateHeader(difficulty: 3),
            [],
            startNonce: 100,
            maxAttempts: 0);

        Assert.Equal(
            MiningSearchStatus.BudgetExhausted,
            result.Status);

        Assert.Equal(0UL, result.Attempts);
        Assert.Equal((ulong?)100, result.NextNonce);
    }

    [Fact]
    public void SearchRange_LastPossibleNonce_DoesNotOverflow()
    {
        MiningSearchResult result = Miner.SearchRange(
            CreateHeader(difficulty: 0),
            [],
            startNonce: ulong.MaxValue,
            maxAttempts: 1);

        Assert.Equal(MiningSearchStatus.Found, result.Status);
        Assert.Equal(1UL, result.Attempts);
        Assert.Null(result.NextNonce);

        Block block = Assert.IsType<Block>(result.Block);
        Assert.Equal(ulong.MaxValue, block.Header.Nonce);
    }
}