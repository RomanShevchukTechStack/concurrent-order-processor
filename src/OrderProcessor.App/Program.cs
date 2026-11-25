using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OrderProcessor.App;

// Usage:
//   generate [--rows 5000] [--seed 42] [--output data/orders.csv]
//   process  [--mode sync|async] [--input data/orders.csv] [--api http://localhost:5080]
//            [--timeout-ms 5000] [--report data/report.txt]
//            [--measure true] [--benchmarks-dir benchmarks]
var command = args.FirstOrDefault();
var options = ParseOptions(args.Skip(1));

switch (command)
{
    case "generate":
        var rows = int.Parse(options.GetValueOrDefault("rows", "5000"));
        var seed = int.Parse(options.GetValueOrDefault("seed", "42"));
        var output = options.GetValueOrDefault("output", Path.Combine("data", "orders.csv"));
        OrderGenerator.Generate(output, rows, seed);
        Console.WriteLine($"Wrote {rows} orders to {Path.GetFullPath(output)}");
        break;

    case "process":
        return await RunProcess(options);

    default:
        Console.Error.WriteLine("Commands: generate, process");
        return 1;
}

return 0;

static async Task<int> RunProcess(Dictionary<string, string> options)
{
    var mode = options.GetValueOrDefault("mode", "sync");
    if (mode is not ("sync" or "async"))
    {
        Console.Error.WriteLine($"Mode '{mode}' is not supported yet. Available: sync, async");
        return 1;
    }

    var input = options.GetValueOrDefault("input", Path.Combine("data", "orders.csv"));
    var api = options.GetValueOrDefault("api", "http://localhost:5080");
    var timeoutMs = int.Parse(options.GetValueOrDefault("timeout-ms", "5000"));
    var reportPath = options.GetValueOrDefault("report", Path.Combine("data", "report.txt"));
    var measure = options.GetValueOrDefault("measure", "false") == "true";

    using var http = new HttpClient
    {
        BaseAddress = new Uri(api.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromMilliseconds(timeoutMs),
    };

    var client = new ProductApiClient(http);
    var scope = measure ? MeasurementScope.Start() : null;

    var report = mode == "sync"
        ? new SequentialOrderProcessor(client).Process(input)
        : await new AsyncOrderProcessor(client).ProcessAsync(input);

    var measurement = scope?.Stop();

    var text = report.ToText();
    var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
    if (reportDirectory is not null)
    {
        Directory.CreateDirectory(reportDirectory);
    }

    File.WriteAllText(reportPath, text);
    Console.WriteLine(text);
    Console.WriteLine($"Report written to {Path.GetFullPath(reportPath)}");

    if (measurement is not null)
    {
        await SaveBenchmark(options, http, mode, report, measurement);
    }

    return 0;
}

static async Task SaveBenchmark(
    Dictionary<string, string> options,
    HttpClient http,
    string mode,
    ProcessingReport report,
    Measurement measurement)
{
    // Read the API's own settings so they are recorded exactly, not typed in by hand.
    SimulationInfo? simulation = null;
    try
    {
        simulation = await http.GetFromJsonAsync<SimulationInfo>(
            "simulation", new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        Console.Error.WriteLine($"Could not read API settings: {ex.Message}");
    }

    var directory = options.GetValueOrDefault("benchmarks-dir", "benchmarks");
    var row = new BenchmarkRow(
        DateTime.UtcNow,
        BenchmarkStore.DescribeCommit(),
        mode,
        report.Processed.Count + report.Failed.Count,
        simulation?.DelayMs.ToString(CultureInfo.InvariantCulture) ?? "unknown",
        simulation?.ErrorRate.ToString(CultureInfo.InvariantCulture) ?? "unknown",
        simulation?.Seed.ToString(CultureInfo.InvariantCulture) ?? "unknown",
        Concurrency: 1,
        measurement,
        report.Processed.Count,
        report.Failed.Count,
        report.GrandTotal);

    BenchmarkStore.Append(directory, row);
    Console.WriteLine(
        $"Measured: {measurement.ElapsedMs} ms, peak {measurement.PeakWorkingSetMb:F1} MB, " +
        $"allocated {measurement.AllocatedMb:F1} MB, " +
        $"GC {measurement.Gen0Collections}/{measurement.Gen1Collections}/{measurement.Gen2Collections}");
    Console.WriteLine($"Saved to {Path.GetFullPath(directory)}");
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var list = args.ToList();
    for (var i = 0; i < list.Count - 1; i += 2)
    {
        result[list[i].TrimStart('-')] = list[i + 1];
    }

    return result;
}
