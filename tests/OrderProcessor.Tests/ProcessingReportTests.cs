using OrderProcessor.App;

namespace OrderProcessor.Tests;

public class ProcessingReportTests
{
    [Fact]
    public void Found_product_adds_processed_order_with_total()
    {
        var report = new ProcessingReport();
        var order = new Order("O1", "C1", "P001", 3);

        report.AddResult(order, ProductLookup.Found(5.37m));

        Assert.Single(report.Processed);
        Assert.Equal(16.11m, report.GrandTotal);
        Assert.Empty(report.Failed);
    }

    [Fact]
    public void Missing_product_is_reported_as_unknown_product()
    {
        var report = new ProcessingReport();

        report.AddResult(new Order("O1", "C1", "X999", 1), ProductLookup.NotFound());

        Assert.Equal(new FailedOrder("O1", FailureCategory.UnknownProduct, "X999"), Assert.Single(report.Failed));
        Assert.Equal(0m, report.GrandTotal);
    }

    [Fact]
    public void Lookup_error_is_reported_as_api_error_with_detail()
    {
        var report = new ProcessingReport();

        report.AddResult(new Order("O1", "C1", "P001", 1), ProductLookup.Error("HTTP 500"));

        Assert.Equal(new FailedOrder("O1", FailureCategory.ApiError, "HTTP 500"), Assert.Single(report.Failed));
    }

    [Fact]
    public void Text_has_counts_per_category_total_and_failed_list()
    {
        var report = new ProcessingReport();
        report.AddResult(new Order("O1", "C1", "P001", 2), ProductLookup.Found(5.37m));
        report.AddResult(new Order("O2", "C1", "X999", 1), ProductLookup.NotFound());
        report.AddFailed(new FailedOrder("O3", FailureCategory.InvalidRow, "empty customer id"));

        var text = report.ToText();

        Assert.Contains("Processed orders: 1", text);
        Assert.Contains("Failed orders: 2", text);
        Assert.Contains("  InvalidRow: 1", text);
        Assert.Contains("  UnknownProduct: 1", text);
        Assert.Contains("Grand total: 10.74", text);
        Assert.Contains("O2,UnknownProduct,X999", text);
        Assert.Contains("O3,InvalidRow,empty customer id", text);
    }
}
