using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OrderProcessor.App;

/// <summary>One benchmark run: what was run, under which API settings, and what was measured.</summary>
public sealed record BenchmarkRow(
    DateTime DateUtc,
    string Commit,
    string Mode,
    int Orders,
    string ApiDelayMs,
    string ApiErrorRate,
    string ApiSeed,
    int Concurrency,
    Measurement Measurement,
    int Processed,
    int Failed,
    decimal GrandTotal)
{
    public static readonly string[] Columns =
    [
        "date_utc", "commit", "mode", "orders", "api_delay_ms", "api_error_rate", "api_seed",
        "concurrency", "elapsed_ms", "peak_working_set_mb", "allocated_mb", "gc0", "gc1", "gc2",
        "processed", "failed", "grand_total",
    ];

    public string[] ToFields() =>
    [
        DateUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        Commit,
        Mode,
        Orders.ToString(CultureInfo.InvariantCulture),
        ApiDelayMs,
        ApiErrorRate,
        ApiSeed,
        Concurrency.ToString(CultureInfo.InvariantCulture),
        Measurement.ElapsedMs.ToString(CultureInfo.InvariantCulture),
        Measurement.PeakWorkingSetMb.ToString("F1", CultureInfo.InvariantCulture),
        Measurement.AllocatedMb.ToString("F1", CultureInfo.InvariantCulture),
        Measurement.Gen0Collections.ToString(CultureInfo.InvariantCulture),
        Measurement.Gen1Collections.ToString(CultureInfo.InvariantCulture),
        Measurement.Gen2Collections.ToString(CultureInfo.InvariantCulture),
        Processed.ToString(CultureInfo.InvariantCulture),
        Failed.ToString(CultureInfo.InvariantCulture),
        GrandTotal.ToString("F2", CultureInfo.InvariantCulture),
    ];
}

/// <summary>
/// Appends runs to <c>results.csv</c> and rebuilds <c>README.md</c> (a markdown table of all runs)
/// in the benchmarks folder. Both files are meant to be committed.
/// </summary>
public static class BenchmarkStore
{
    public static void Append(string directory, BenchmarkRow row)
    {
        Directory.CreateDirectory(directory);
        var csvPath = Path.Combine(directory, "results.csv");

        if (!File.Exists(csvPath))
        {
            File.WriteAllText(csvPath, string.Join(',', BenchmarkRow.Columns) + Environment.NewLine);
        }

        File.AppendAllText(csvPath, string.Join(',', row.ToFields()) + Environment.NewLine);
        WriteMarkdown(directory, File.ReadAllLines(csvPath));
    }

    private static void WriteMarkdown(string directory, string[] csvLines)
    {
        var text = new StringBuilder();
        text.AppendLine("# Benchmarks");
        text.AppendLine();
        text.AppendLine("Generated from `results.csv` by `process --measure true`. Do not edit by hand.");
        text.AppendLine("Written findings go in `notes.md`.");
        text.AppendLine();
        text.AppendLine("How to read:");
        text.AppendLine("- `elapsed_ms`, `allocated_mb` and `gc*` cover the processing only (file, HTTP, report).");
        text.AppendLine("- `peak_working_set_mb` is the peak of the whole process and includes runtime startup.");
        text.AppendLine("- Compare runs only when orders, API settings, concurrency and machine are the same.");
        text.AppendLine("- Each run is a fresh process and a fresh API, so the first-call JIT cost is included.");
        text.AppendLine("- A `+dirty` commit means uncommitted changes outside `benchmarks/` existed during the run.");
        text.AppendLine();

        text.AppendLine("| " + string.Join(" | ", BenchmarkRow.Columns) + " |");
        text.AppendLine("|" + string.Concat(BenchmarkRow.Columns.Select(_ => " --- |")));
        foreach (var line in csvLines.Skip(1).Where(l => l.Length > 0))
        {
            text.AppendLine("| " + string.Join(" | ", line.Split(',')) + " |");
        }

        File.WriteAllText(Path.Combine(directory, "README.md"), text.ToString());
    }

    /// <summary>Short commit hash of the working directory's repo, with +dirty for local changes.</summary>
    public static string DescribeCommit()
    {
        var hash = Git("rev-parse --short HEAD");
        if (hash is null)
        {
            return "unknown";
        }

        var changes = Git("status --porcelain -- . \":(exclude)benchmarks\"");
        return string.IsNullOrWhiteSpace(changes) ? hash : hash + "+dirty";
    }

    private static string? Git(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            var output = process!.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
