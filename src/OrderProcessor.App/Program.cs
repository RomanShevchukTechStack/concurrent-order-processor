using OrderProcessor.App;

// Usage:
//   generate [--rows 5000] [--seed 42] [--output data/orders.csv]
//   process  [--mode sync] [--input data/orders.csv] [--api http://localhost:5080]
//            [--timeout-ms 5000] [--report data/report.txt]
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
        return RunProcess(options);

    default:
        Console.Error.WriteLine("Commands: generate, process");
        return 1;
}

return 0;

static int RunProcess(Dictionary<string, string> options)
{
    var mode = options.GetValueOrDefault("mode", "sync");
    if (mode != "sync")
    {
        Console.Error.WriteLine($"Mode '{mode}' is not supported yet. Available: sync");
        return 1;
    }

    var input = options.GetValueOrDefault("input", Path.Combine("data", "orders.csv"));
    var api = options.GetValueOrDefault("api", "http://localhost:5080");
    var timeoutMs = int.Parse(options.GetValueOrDefault("timeout-ms", "5000"));
    var reportPath = options.GetValueOrDefault("report", Path.Combine("data", "report.txt"));

    using var http = new HttpClient
    {
        BaseAddress = new Uri(api.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromMilliseconds(timeoutMs),
    };

    var report = new SequentialOrderProcessor(new ProductApiClient(http)).Process(input);
    var text = report.ToText();

    var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
    if (reportDirectory is not null)
    {
        Directory.CreateDirectory(reportDirectory);
    }

    File.WriteAllText(reportPath, text);
    Console.WriteLine(text);
    Console.WriteLine($"Report written to {Path.GetFullPath(reportPath)}");
    return 0;
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
