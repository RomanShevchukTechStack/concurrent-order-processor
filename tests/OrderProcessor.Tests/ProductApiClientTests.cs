using System.Net;
using System.Text;
using OrderProcessor.App;

namespace OrderProcessor.Tests;

public class ProductApiClientTests
{
    [Fact]
    public async Task Ok_response_gives_price()
    {
        var client = ClientFor(_ => Json(HttpStatusCode.OK, """{"id":"P001","name":"Product 001","price":5.37}"""));

        Assert.Equal(ProductLookup.Found(5.37m), client.Get("P001"));
        Assert.Equal(ProductLookup.Found(5.37m), await client.GetAsync("P001"));
    }

    [Fact]
    public async Task Not_found_gives_not_found()
    {
        var client = ClientFor(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(ProductLookup.NotFound(), client.Get("X1"));
        Assert.Equal(ProductLookup.NotFound(), await client.GetAsync("X1"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    [InlineData(HttpStatusCode.TooManyRequests, "HTTP 429")]
    [InlineData(HttpStatusCode.GatewayTimeout, "HTTP 504")]
    public async Task Error_status_gives_error_with_code(HttpStatusCode status, string detail)
    {
        var client = ClientFor(_ => new HttpResponseMessage(status));

        Assert.Equal(ProductLookup.Error(detail), client.Get("P001"));
        Assert.Equal(ProductLookup.Error(detail), await client.GetAsync("P001"));
    }

    [Fact]
    public async Task Client_timeout_gives_timeout_error()
    {
        var client = ClientFor(_ => throw new TaskCanceledException("timed out"));

        Assert.Equal(ProductLookup.Error("timeout"), client.Get("P001"));
        Assert.Equal(ProductLookup.Error("timeout"), await client.GetAsync("P001"));
    }

    [Fact]
    public async Task Network_failure_gives_request_failed_error()
    {
        var client = ClientFor(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(ProductLookup.Error("request failed: connection refused"), client.Get("P001"));
        Assert.Equal(ProductLookup.Error("request failed: connection refused"), await client.GetAsync("P001"));
    }

    [Fact]
    public async Task Malformed_body_gives_error()
    {
        var client = ClientFor(_ => Json(HttpStatusCode.OK, "not json"));

        Assert.Equal(ProductLookupStatus.Error, client.Get("P001").Status);
        Assert.Equal(ProductLookupStatus.Error, (await client.GetAsync("P001")).Status);
    }

    private static ProductApiClient ClientFor(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("http://test/") });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Answers both the blocking and the async HttpClient calls.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
