using System.Globalization;
using BlockChain.Models;
using BlockChain.Simulation;
using BlockChain.Transactions;
using static System.FormattableString;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

RunOptions? options = RunOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(RunOptions.Usage);
    return 2;
}

var service = new TransactionService();

try
{
    (SimulationInput input, string blocksCsvPath) = Prepare(options, service);

    RunReport report = new Runner(service).Run(input);

    RunReportFormatter.Write(report, Console.Out);
    using (var csv = new StreamWriter(blocksCsvPath))
        BlocksCsv.Write(report, csv);

    if (!report.AccountingHolds)
    {
        Console.Error.WriteLine(Invariant(
            $"WARNING: accounting invariant failed: {report.GeneratedCount} submitted != {report.IncludedCount} included + {report.Rejections.Count} rejected + {report.Remaining.Count} remaining"));
        return 1;
    }

    return 0;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Run stopped: {ex.Message}");
    return 1;
}
catch (IOException ex)
{
    Console.Error.WriteLine($"Run folder error: {ex.Message}");
    return 1;
}

static (SimulationInput Input, string BlocksCsvPath) Prepare(RunOptions options, ITransactionService service)
{
    switch (options.Mode)
    {
        case RunMode.Demo:
        {
            SimulationInput demo = service.CreateDemo();
            return (demo, SaveInputs(service, demo, Path.Combine("runs", "demo")));
        }
        case RunMode.Full:
        {
            var config = new SimulationConfig { GenerationSeed = options.Seed, SelectionSeed = options.Seed + 1 };
            SimulationInput full = service.Generate(config);
            return (full, SaveInputs(service, full, Path.Combine("runs", Invariant($"full-seed-{options.Seed}"))));
        }
        default:
        {
            string folder = options.ReplayFolder!;
            return (service.LoadInputs(folder), Path.Combine(folder, "blocks.replay.csv"));
        }
    }
}

static string SaveInputs(ITransactionService service, SimulationInput input, string folder)
{
    Directory.CreateDirectory(folder);
    service.SaveInputs(input, folder);
    return Path.Combine(folder, "blocks.csv");
}