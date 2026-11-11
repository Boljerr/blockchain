using System.Diagnostics;
using System.Globalization;
using BlockChain.Blocks;
using BlockChain.Models;
using static System.FormattableString;

const ulong AttemptsPerHeader = 2_000_000;
const int Difficulty = 9;

int runs = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 5;

BlockHeader[] headers = Enumerable.Range(1, 5)
    .Select(i => new BlockHeader(
        new string((char)('0' + i), 64),
        1_793_206_326 + i,
        2,
        new string((char)('a' + i), 64),
        0,
        Difficulty))
    .ToArray();

Measure(headers, 100_000);

var rates = new List<double>();
var lines = new List<string>();

for (int run = 1; run <= runs; run++)
{
    (ulong attempts, TimeSpan elapsed) = Measure(headers, AttemptsPerHeader);
    double rate = attempts / elapsed.TotalSeconds;

    rates.Add(rate);
    lines.Add(Invariant($"{run},{attempts},{elapsed.TotalMilliseconds:F1},{rate:F0},{elapsed.TotalNanoseconds / attempts:F1}"));
}

double mean = rates.Average();
double deviation = Math.Sqrt(rates.Sum(rate => (rate - mean) * (rate - mean)) / Math.Max(1, rates.Count - 1));

Console.WriteLine("run,attempts,elapsed_ms,attempts_per_second,ns_per_attempt");
lines.ForEach(Console.WriteLine);
Console.WriteLine(Invariant($"mean attempts/s: {mean:F0} ± {deviation:F0} (sample std. dev., {runs} runs)"));

static (ulong Attempts, TimeSpan Elapsed) Measure(BlockHeader[] headers, ulong attemptsPerHeader)
{
    ulong attempts = 0;
    var stopwatch = Stopwatch.StartNew();

    foreach (BlockHeader header in headers)
    {
        MiningSearchResult result = Miner.SearchRange(header, [], 0, attemptsPerHeader);

        if (result.Status != MiningSearchStatus.BudgetExhausted)
            throw new InvalidOperationException("A fixed header found a solution, so runs would not do equal work.");

        attempts += result.Attempts;
    }

    stopwatch.Stop();

    return (attempts, stopwatch.Elapsed);
}
