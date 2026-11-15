using System.Diagnostics;
using System.Globalization;
using BlockChain.Blocks;
using BlockChain.Models;
using BlockChain.Simulation;
using BlockChain.Transactions;
using static System.FormattableString;

const ulong AttemptsPerHeader = 2_000_000;
const int Difficulty = 9;
bool includeParallelMeasurements = true;
bool includeSimulationMeasurements = true; // Optional full-simulation benchmark.
const int SimulationDifficulty = 3; // Applies only to full-simulation measurements.

int runs = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 5;
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runs);

BlockHeader[] headers = Enumerable.Range(1, 5)
    .Select(i => new BlockHeader(
        new string((char)('0' + i), 64),
        1_793_206_326 + i,
        2,
        new string((char)('a' + i), 64),
        0,
        Difficulty))
    .ToArray();

Console.WriteLine(Invariant($"Fixed-work benchmark: {runs} runs per mode, {(ulong)headers.Length * AttemptsPerHeader} attempts per run, difficulty {Difficulty}."));
Console.WriteLine("Warmup (excluded from results).");
Measure(headers, 100_000);
if (includeParallelMeasurements)
    await MeasureParallel(headers, 100_000);

var sequentialRates = new List<double>();
var parallelRates = new List<double>();
var sequentialTimes = new List<double>();
var parallelTimes = new List<double>();

Console.WriteLine("run,mode,attempts,elapsed_ms,attempts_per_second,ns_per_attempt");
for (int run = 1; run <= runs; run++)
{
    bool[] order = !includeParallelMeasurements ? [false]
        : run % 2 == 1 ? [false, true] : [true, false];
    foreach (bool parallel in order)
    {
        (ulong attempts, TimeSpan elapsed) = parallel
            ? await MeasureParallel(headers, AttemptsPerHeader)
            : Measure(headers, AttemptsPerHeader);
        double rate = attempts / elapsed.TotalSeconds;
        (parallel ? parallelRates : sequentialRates).Add(rate);
        (parallel ? parallelTimes : sequentialTimes).Add(elapsed.TotalMilliseconds);
        string mode = parallel ? "parallel" : "sequential";
        Console.WriteLine(Invariant($"{run},{mode},{attempts},{elapsed.TotalMilliseconds:F1},{rate:F0},{elapsed.TotalNanoseconds / attempts:F1}"));
    }
}

WriteRate("sequential", sequentialRates);
WriteTiming("fixed-work sequential", sequentialTimes);
if (includeParallelMeasurements)
{
    WriteRate("parallel", parallelRates);
    WriteTiming("fixed-work parallel", parallelTimes);
    Console.WriteLine(Invariant($"parallel speedup (mean elapsed time): {sequentialTimes.Average() / parallelTimes.Average():F2}x"));
    Console.WriteLine("Parallel throughput uses total attempts / wall-clock time, including task scheduling and waiting for all workers.");
}

if (includeSimulationMeasurements)
    await MeasureSimulationModes(runs, SimulationDifficulty);

static (ulong Attempts, TimeSpan Elapsed) Measure(BlockHeader[] headers, ulong attemptsPerHeader)
{
    ulong attempts = 0;
    var stopwatch = Stopwatch.StartNew();

    foreach (BlockHeader header in headers)
        attempts += SearchFixedHeader(header, attemptsPerHeader);

    stopwatch.Stop();

    return (attempts, stopwatch.Elapsed);
}

static async Task<(ulong Attempts, TimeSpan Elapsed)> MeasureParallel(BlockHeader[] headers, ulong attemptsPerHeader)
{
    var stopwatch = Stopwatch.StartNew();
    Task<ulong>[] workers = headers
        .Select(header => Task.Run(() => SearchFixedHeader(header, attemptsPerHeader)))
        .ToArray();
    ulong[] results = await Task.WhenAll(workers);
    stopwatch.Stop();

    ulong attempts = 0;
    foreach (ulong result in results)
        attempts += result;
    return (attempts, stopwatch.Elapsed);
}

static ulong SearchFixedHeader(BlockHeader header, ulong attempts)
{
    MiningSearchResult result = Miner.SearchRange(header, [], 0, attempts);
    if (result.Status != MiningSearchStatus.BudgetExhausted || result.Attempts != attempts || result.NextNonce != attempts)
        throw new InvalidOperationException("Fixed-work search did not complete its exact attempt budget; results cannot be compared.");
    return result.Attempts;
}

static void WriteRate(string mode, List<double> samples)
{
    double mean = samples.Average();
    double deviation = Math.Sqrt(samples.Sum(value => (value - mean) * (value - mean)) / Math.Max(1, samples.Count - 1));
    Console.WriteLine(Invariant($"mean {mode} attempts/s: {mean:F0} ± {deviation:F0} (sample std. dev., {samples.Count} runs)"));
}

static async Task MeasureSimulationModes(int runs, int difficulty)
{
    var service = new TransactionService();
    SimulationInput input = service.Generate(new SimulationConfig { Difficulty = difficulty });
    var sequentialMining = new List<double>();
    var sequentialTotal = new List<double>();
    var parallelMining = new List<double>();
    var parallelTotal = new List<double>();

    Console.WriteLine();
    Console.WriteLine(Invariant($"Full simulation: {input.Users.Count} users, {input.Transactions.Count} transactions, difficulty {difficulty}, seeds {input.Configuration.GenerationSeed}/{input.Configuration.SelectionSeed}"));
    Console.WriteLine("Warmup: sequential and parallel (excluded from results).");
    await RunSimulation(service, input, parallel: false);
    await RunSimulation(service, input, parallel: true);

    Console.WriteLine("run,mode,mining_ms,total_ms,attempts,included,remaining");
    for (int run = 1; run <= runs; run++)
    {
        // Alternate the order; every run starts with fresh balances and pool.
        bool[] order = run % 2 == 1 ? [false, true] : [true, false];
        foreach (bool parallel in order)
        {
            RunReport report = await RunSimulation(service, input, parallel);
            (parallel ? parallelMining : sequentialMining).Add(report.MiningTime.TotalMilliseconds);
            (parallel ? parallelTotal : sequentialTotal).Add(report.TotalTime.TotalMilliseconds);
            string mode = parallel ? "parallel" : "sequential";
            decimal attempts = report.Blocks.Sum(block => (decimal)(block.Competition?.TotalAttempts ?? block.Attempts));
            Console.WriteLine(Invariant($"{run},{mode},{report.MiningTime.TotalMilliseconds:F1},{report.TotalTime.TotalMilliseconds:F1},{attempts:F0},{report.IncludedCount},{report.Remaining.Count}"));
        }
    }

    WriteTiming("sequential mining", sequentialMining);
    WriteTiming("sequential total", sequentialTotal);
    WriteTiming("parallel mining", parallelMining);
    WriteTiming("parallel total", parallelTotal);
    Console.WriteLine("Times exclude generation and console output. Parallel winners and attempt counts may differ.");
}

static async Task<RunReport> RunSimulation(TransactionService service, SimulationInput input, bool parallel)
{
    var configured = new SimulationInput(
        input.Configuration with { ParallelMining = parallel },
        input.Users, input.Transactions, input.PostRunSubmissions);
    RunReport report = await new Runner(service).RunAsync(configured);
    if (!report.FundsConserved || !report.AccountingHolds || report.FinalLedger.Balances.Values.Any(balance => balance < 0))
        throw new InvalidOperationException("Simulation benchmark failed its balance or accounting checks.");
    return report;
}

static void WriteTiming(string name, List<double> samples)
{
    double mean = samples.Average();
    double deviation = Math.Sqrt(samples.Sum(value => (value - mean) * (value - mean)) / Math.Max(1, samples.Count - 1));
    Console.WriteLine(Invariant($"{name}: {mean:F1} ± {deviation:F1} ms (sample std. dev., {samples.Count} runs)"));
}
