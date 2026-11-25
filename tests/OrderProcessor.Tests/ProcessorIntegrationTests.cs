using Microsoft.AspNetCore.Mvc.Testing;
using OrderProcessor.App;

namespace OrderProcessor.Tests;

/// <summary>Runs both processors against the real test API, hosted in memory.</summary>
public sealed class ProcessorIntegrationTests : IDisposable
{
    // Prices from ProductCatalog: P001 = 5.37, P002 = 5.74, P003 = 6.11.
    private const string SmallCsv = """
        OrderId,CustomerId,ProductId,Quantity
        O1,C1,P001,2
        O2,C1,P002,3
        O3,C2,X999,1
        O4,C2,P003,abc
        O5,,P001,1
        """;

    private readonly string _inputPath = Path.Combine(Path.GetTempPath(), $"orders-{Guid.NewGuid():N}.csv");

    public void Dispose()
    {
        if (File.Exists(_inputPath))
        {
            File.Delete(_inputPath);
        }
    }

    [Fact]
    public void Sequential_processor_gives_expected_report()
    {
        File.WriteAllText(_inputPath, SmallCsv);
        using var factory = ApiFactory(errorRate: 0);

        var report = new SequentialOrderProcessor(Client(factory)).Process(_inputPath);

        AssertSmallReport(report);
    }

    [Fact]
    public async Task Async_processor_gives_expected_report()
    {
        File.WriteAllText(_inputPath, SmallCsv);
        using var factory = ApiFactory(errorRate: 0);

        var report = await new AsyncOrderProcessor(Client(factory)).ProcessAsync(_inputPath);

        AssertSmallReport(report);
    }

    [Fact]
    public async Task Sync_and_async_give_identical_report_with_api_errors()
    {
        // Larger input with API failures. Each processor gets a fresh API, because the failure
        // pattern depends on how often each product was already requested.
        OrderGenerator.Generate(_inputPath, rows: 400, seed: 7);

        string syncText;
        using (var factory = ApiFactory(errorRate: 0.2))
        {
            syncText = new SequentialOrderProcessor(Client(factory)).Process(_inputPath).ToText();
        }

        string asyncText;
        using (var factory = ApiFactory(errorRate: 0.2))
        {
            asyncText = (await new AsyncOrderProcessor(Client(factory)).ProcessAsync(_inputPath)).ToText();
        }

        Assert.Contains("ApiError:", syncText);
        Assert.Equal(syncText, asyncText);
    }

    [Fact]
    public async Task Api_errors_are_reported_and_other_orders_keep_correct_totals()
    {
        File.WriteAllText(_inputPath, SmallCsv);
        using var factory = ApiFactory(errorRate: 1);

        var report = await new AsyncOrderProcessor(Client(factory)).ProcessAsync(_inputPath);

        // Every API call fails, but the two invalid rows still get their own parse reasons
        // and every valid order is listed as an API error, none lost.
        Assert.Empty(report.Processed);
        Assert.Equal(5, report.Failed.Count);
        Assert.Equal(3, report.Failed.Count(f => f.Category == FailureCategory.ApiError));
        Assert.Equal(2, report.Failed.Count(f => f.Category == FailureCategory.InvalidRow));
        Assert.Equal(["O1", "O2", "O3", "O4", "O5"], report.Failed.Select(f => f.OrderId).Order());
    }

    [Fact]
    public async Task Slow_api_beyond_client_timeout_is_reported_as_timeout_and_run_finishes()
    {
        File.WriteAllText(_inputPath, "OrderId,CustomerId,ProductId,Quantity\nO1,C1,P001,1\n");
        using var factory = ApiFactory(errorRate: 0, delayMs: 300);
        var http = factory.CreateClient();
        http.Timeout = TimeSpan.FromMilliseconds(50);

        var report = await new AsyncOrderProcessor(new ProductApiClient(http)).ProcessAsync(_inputPath);

        Assert.Equal(new FailedOrder("O1", FailureCategory.ApiError, "timeout"), Assert.Single(report.Failed));
    }

    private static void AssertSmallReport(ProcessingReport report)
    {
        Assert.Equal(["O1", "O2"], report.Processed.Select(p => p.Order.OrderId));
        Assert.Equal(27.96m, report.GrandTotal); // 2 x 5.37 + 3 x 5.74

        Assert.Equal(
            [
                new FailedOrder("O3", FailureCategory.UnknownProduct, "X999"),
                new FailedOrder("O4", FailureCategory.InvalidRow, "quantity 'abc' is not a number"),
                new FailedOrder("O5", FailureCategory.InvalidRow, "empty customer id"),
            ],
            report.Failed);
    }

    private static WebApplicationFactory<Program> ApiFactory(double errorRate, int delayMs = 0) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Simulation:ErrorRate", errorRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Simulation:DelayMs", delayMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Simulation:TimeoutMs", "20");
            builder.UseSetting("Simulation:Seed", "42");
        });

    private static ProductApiClient Client(WebApplicationFactory<Program> factory) =>
        new(new HttpClient(new SyncOverAsyncHandler(factory.Server.CreateHandler()))
        {
            BaseAddress = new Uri("http://localhost/"),
        });

    /// <summary>
    /// The in-memory test server only supports async calls. This lets the blocking
    /// <c>HttpClient.Send</c> used by the sequential processor work in tests.
    /// </summary>
    private sealed class SyncOverAsyncHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }
}
