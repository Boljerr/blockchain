using BlockChain.Models;
using static System.FormattableString;

namespace BlockChain.Simulation;

public static class RunReportFormatter
{
    public static void Write(RunReport report, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);

        foreach (BlockReport block in report.Blocks)
            WriteBlock(block, writer);

        writer.WriteLine(Invariant($"Rejected at submission ({report.Rejections.Count})"));
        foreach (RejectedSubmission rejection in report.Rejections)
            writer.WriteLine(Invariant(
                $"  #{rejection.SubmissionNumber} {rejection.Transaction.TransactionId}: {rejection.ReasonCode} - {rejection.Explanation}"));
        writer.WriteLine();

        writer.WriteLine(Invariant($"Remaining in pool ({report.Remaining.Count})"));
        foreach (RemainingTransaction remaining in report.Remaining)
            writer.WriteLine(Invariant(
                $"  {remaining.Transaction.TransactionId}: {remaining.ReasonCode} - {remaining.Explanation}"));
        writer.WriteLine();

        writer.WriteLine(Invariant($"Post-run submissions ({report.PostRunOutcomes.Count})"));
        foreach (SubmissionOutcome outcome in report.PostRunOutcomes)
            writer.WriteLine(Invariant(
                $"  {outcome.Transaction.TransactionId}: {(outcome.Result.Accepted ? "accepted" : "rejected")}, {outcome.Result.ReasonCode} - {outcome.Result.Explanation}"));
        writer.WriteLine();

        WriteSummary(report, writer);
    }

    private static void WriteBlock(BlockReport report, TextWriter writer)
    {
        BlockHeader header = report.Block.Header;

        writer.WriteLine(Invariant($"Block {report.Height}"));
        writer.WriteLine(Invariant($"  transactions: {report.Block.Transactions.Count}"));
        writer.WriteLine(Invariant($"  prev_hash:    {header.PrevHash}"));
        writer.WriteLine(Invariant($"  tx_root:      {header.TxRoot}"));
        writer.WriteLine(Invariant($"  hash:         {report.Block.DeclaredHash}"));
        writer.WriteLine(Invariant($"  difficulty:   {header.Difficulty}"));
        writer.WriteLine(Invariant($"  nonce:        {header.Nonce}"));
        writer.WriteLine(Invariant($"  attempts:     {report.Attempts}"));
        writer.WriteLine(Invariant($"  mining time:  {report.MiningTime.TotalMilliseconds:F1} ms"));
        writer.WriteLine(Invariant($"  deferred:     {report.Deferred.Count}"));
        writer.WriteLine();
    }

    private static void WriteSummary(RunReport report, TextWriter writer)
    {
        SimulationConfig config = report.Configuration;

        writer.WriteLine("Summary");
        writer.WriteLine(Invariant($"  users:                  {report.UserCount}"));
        writer.WriteLine(Invariant($"  generated transactions: {report.GeneratedCount}"));
        writer.WriteLine(Invariant($"  submissions:            {report.SubmissionCount}"));
        writer.WriteLine(Invariant($"  included:               {report.IncludedCount}"));
        writer.WriteLine(Invariant($"  rejected:               {report.Rejections.Count}"));
        writer.WriteLine(Invariant($"  remaining:              {report.Remaining.Count}"));
        writer.WriteLine(Invariant($"  accepted blocks:        {report.Blocks.Count}"));
        writer.WriteLine(Invariant($"  initial funds:          {report.InitialTotal}"));
        writer.WriteLine(Invariant($"  final funds:            {report.FinalTotal}"));
        writer.WriteLine(Invariant($"  conserved:              {(report.FundsConserved ? "yes" : "no")}"));
        writer.WriteLine(Invariant($"  generation seed:        {config.GenerationSeed}"));
        writer.WriteLine(Invariant($"  selection seed:         {config.SelectionSeed}"));
        writer.WriteLine(Invariant($"  difficulty:             {config.Difficulty}"));
        writer.WriteLine(Invariant($"  mining time:            {report.MiningTime.TotalMilliseconds:F0} ms"));
        writer.WriteLine(Invariant($"  total time:             {report.TotalTime.TotalMilliseconds:F0} ms"));
    }
}