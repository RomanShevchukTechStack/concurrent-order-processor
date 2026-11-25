using System.Net;
using System.Text.Json;

namespace OrderProcessor.App;

/// <summary>Reads product details from the test API. Every failure is returned, never thrown.</summary>
public sealed class ProductApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Blocking call, used by the sequential baseline.</summary>
    public ProductLookup Get(string productId)
    {
        try
        {
            using var request = CreateRequest(productId);
            using var response = http.Send(request);

            var byStatus = FromStatus(response);
            if (byStatus is not null)
            {
                return byStatus;
            }

            using var stream = response.Content.ReadAsStream();
            return FromBody(JsonSerializer.Deserialize<ProductDto>(stream, JsonOptions));
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or JsonException)
        {
            return FromException(ex);
        }
    }

    /// <summary>Non-blocking call, same results as <see cref="Get"/>.</summary>
    public async Task<ProductLookup> GetAsync(string productId)
    {
        try
        {
            using var request = CreateRequest(productId);
            using var response = await http.SendAsync(request);

            var byStatus = FromStatus(response);
            if (byStatus is not null)
            {
                return byStatus;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            return FromBody(await JsonSerializer.DeserializeAsync<ProductDto>(stream, JsonOptions));
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or JsonException)
        {
            return FromException(ex);
        }
    }

    private static HttpRequestMessage CreateRequest(string productId) =>
        new(HttpMethod.Get, $"products/{Uri.EscapeDataString(productId)}");

    /// <summary>Returns a result for 404 and error statuses, or null when the body must be read.</summary>
    private static ProductLookup? FromStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ProductLookup.NotFound();
        }

        return response.IsSuccessStatusCode
            ? null
            : ProductLookup.Error($"HTTP {(int)response.StatusCode}");
    }

    private static ProductLookup FromBody(ProductDto? product) =>
        product is null
            ? ProductLookup.Error("empty response body")
            : ProductLookup.Found(product.Price);

    private static ProductLookup FromException(Exception ex) => ex switch
    {
        // HttpClient.Timeout surfaces as a canceled task.
        TaskCanceledException => ProductLookup.Error("timeout"),
        HttpRequestException => ProductLookup.Error($"request failed: {ex.Message}"),
        _ => ProductLookup.Error($"bad response body: {ex.Message}"),
    };
}
