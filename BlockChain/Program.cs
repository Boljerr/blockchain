using System.Globalization;
using BlockChain.Models;
using BlockChain.Simulation;
using BlockChain.Transactions;
using static System.FormattableString;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

bool? parallelOverride = null;

if (args.Length > 0)
{
    string lastArgument = args[args.Length - 1];

    if (lastArgument is "--parallel" or "--sequential")
    {
        parallelOverride = lastArgument == "--parallel";

        Array.Resize(ref args, args.Length - 1);
    }
}

RunOptions? options = RunOptions.Parse(args);

if (options is null)
{
    Console.Error.WriteLine(RunOptions.Usage);

    Console.Error.WriteLine(
        "Append --parallel or --sequential to choose the mining mode.");

    return 2;
}

var service = new TransactionService();

try
{
    SimulationInput input = options.Mode switch
    {
        RunMode.Demo => service.CreateDemo(),

        RunMode.Full => service.Generate(new SimulationConfig
        {
            GenerationSeed = options.Seed,
            SelectionSeed = options.Seed + 1
        }),

        RunMode.Replay => service.LoadInputs(options.ReplayFolder!),

        _ => throw new InvalidOperationException(
            "Unsupported run mode.")
    };

    if (parallelOverride is { } parallel)
    {
        input = new SimulationInput(
            input.Configuration with
            {
                ParallelMining = parallel
            },
            input.Users,
            input.Transactions,
            input.PostRunSubmissions);
    }

    string miningMode = input.Configuration.ParallelMining
        ? "parallel"
        : "sequential";

    string suffix = input.Configuration.ParallelMining
        ? "-parallel"
        : "";

    string folder = options.Mode switch
    {
        RunMode.Demo => Path.Combine(
            "runs",
            $"demo{suffix}"),

        RunMode.Full => Path.Combine(
            "runs",
            Invariant($"full-seed-{options.Seed}{suffix}")),

        RunMode.Replay => options.ReplayFolder!,

        _ => throw new InvalidOperationException(
            "Unsupported run mode.")
    };

    if (options.Mode != RunMode.Replay)
        service.SaveInputs(input, folder);

    RunReport report = await new Runner(service).RunAsync(input);

    Console.WriteLine($"Mining mode: {miningMode}");
    RunReportFormatter.Write(report, Console.Out);

    string csvName = options.Mode == RunMode.Replay
        ? $"blocks.replay.{miningMode}.csv"
        : "blocks.csv";

    using (var csv = new StreamWriter(
               Path.Combine(folder, csvName)))
    {
        BlocksCsv.Write(report, csv);
    }

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