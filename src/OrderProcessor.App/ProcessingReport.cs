using System.Globalization;
using System.Text;

namespace OrderProcessor.App;

public sealed class ProcessingReport
{
    private readonly List<ProcessedOrder> _processed = [];
    private readonly List<FailedOrder> _failed = [];

    public IReadOnlyList<ProcessedOrder> Processed => _processed;

    public IReadOnlyList<FailedOrder> Failed => _failed;

    public decimal GrandTotal => _processed.Sum(p => p.Total);

    public void AddProcessed(ProcessedOrder order) => _processed.Add(order);

    public void AddFailed(FailedOrder order) => _failed.Add(order);

    /// <summary>Text summary. Same input and same API behavior give the same text.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        text.AppendLine($"Processed orders: {_processed.Count}");
        text.AppendLine($"Failed orders: {_failed.Count}");
        foreach (var group in _failed.GroupBy(f => f.Category).OrderBy(g => g.Key))
        {
            text.AppendLine($"  {group.Key}: {group.Count()}");
        }

        text.AppendLine($"Grand total: {GrandTotal.ToString("F2", CultureInfo.InvariantCulture)}");

        if (_failed.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Failed orders (OrderId, Category, Detail):");
            foreach (var failed in _failed)
            {
                text.AppendLine($"{failed.OrderId},{failed.Category},{failed.Detail}");
            }
        }

        return text.ToString();
    }
}
