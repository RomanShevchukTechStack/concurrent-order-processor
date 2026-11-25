namespace OrderProcessor.Api;

public sealed record Product(string Id, string Name, decimal Price);

public static class ProductCatalog
{
    public const int Count = 200;

    private static readonly Dictionary<string, Product> Products = Enumerable
        .Range(1, Count)
        .Select(i => new Product(Id(i), $"Product {i:D3}", 5m + (i * 37 % 9500) / 100m))
        .ToDictionary(p => p.Id);

    public static string Id(int number) => $"P{number:D3}";

    public static bool TryGet(string id, out Product product) =>
        Products.TryGetValue(id, out product!);
}
