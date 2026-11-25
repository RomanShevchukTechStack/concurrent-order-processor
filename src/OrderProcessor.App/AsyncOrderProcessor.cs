namespace OrderProcessor.App;

/// <summary>
/// Stage 2, first step. Same sequential order as the baseline, but file and HTTP calls are
/// asynchronous. It is not faster yet: each order still waits for the previous one.
/// </summary>
public sealed class AsyncOrderProcessor(ProductApiClient client)
{
    public async Task<ProcessingReport> ProcessAsync(string inputPath)
    {
        var report = new ProcessingReport();
        var lineNumber = 0;

        await foreach (var line in File.ReadLinesAsync(inputPath))
        {
            lineNumber++;
            if (lineNumber == 1 || string.IsNullOrWhiteSpace(line))
            {
                continue; // header or blank line
            }

            if (!OrderParser.TryParse(line, lineNumber, out var order, out var failure))
            {
                report.AddFailed(failure!);
                continue;
            }

            report.AddResult(order!, await client.GetAsync(order!.ProductId));
        }

        return report;
    }
}
