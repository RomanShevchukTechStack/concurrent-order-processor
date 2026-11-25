namespace OrderProcessor.App;

/// <summary>Stage 1 baseline. One order at a time, blocking file and HTTP calls.</summary>
public sealed class SequentialOrderProcessor(ProductApiClient client)
{
    public ProcessingReport Process(string inputPath)
    {
        var report = new ProcessingReport();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(inputPath))
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

            var lookup = client.Get(order!.ProductId);
            switch (lookup.Status)
            {
                case ProductLookupStatus.Found:
                    report.AddProcessed(new ProcessedOrder(order, order.Quantity * lookup.Price));
                    break;
                case ProductLookupStatus.NotFound:
                    report.AddFailed(new FailedOrder(order.OrderId, FailureCategory.UnknownProduct, order.ProductId));
                    break;
                default:
                    report.AddFailed(new FailedOrder(order.OrderId, FailureCategory.ApiError, lookup.Detail));
                    break;
            }
        }

        return report;
    }
}
