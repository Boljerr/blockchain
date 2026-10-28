using BlockChain.Models;
using static System.FormattableString;

namespace BlockChain.Simulation;

public static class BlocksCsv
{
    public static void Write(RunReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("height,hash,nonce,transactions");
        foreach (BlockReport block in report.Blocks)
            writer.WriteLine(Invariant(
                $"{block.Height},{block.Block.DeclaredHash},{block.Block.Header.Nonce},{block.Block.Transactions.Count}"));
    }
}