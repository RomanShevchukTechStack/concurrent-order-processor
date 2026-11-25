using OrderProcessor.App;

// Usage:
//   generate [--rows 5000] [--seed 42] [--output data/orders.csv]
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

    default:
        Console.Error.WriteLine("Usage: generate [--rows 5000] [--seed 42] [--output data/orders.csv]");
        return 1;
}

return 0;

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
