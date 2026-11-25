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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"products/{Uri.EscapeDataString(productId)}");
            using var response = http.Send(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ProductLookup.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return ProductLookup.Error($"HTTP {(int)response.StatusCode}");
            }

            using var stream = response.Content.ReadAsStream();
            var product = JsonSerializer.Deserialize<ProductDto>(stream, JsonOptions);
            return product is null
                ? ProductLookup.Error("empty response body")
                : ProductLookup.Found(product.Price);
        }
        catch (TaskCanceledException)
        {
            // HttpClient.Timeout surfaces as a canceled task.
            return ProductLookup.Error("timeout");
        }
        catch (HttpRequestException ex)
        {
            return ProductLookup.Error($"request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ProductLookup.Error($"bad response body: {ex.Message}");
        }
    }
}
