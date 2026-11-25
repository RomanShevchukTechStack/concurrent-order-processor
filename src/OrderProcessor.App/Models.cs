namespace OrderProcessor.App;

public sealed record Order(string OrderId, string CustomerId, string ProductId, int Quantity);

public sealed record ProcessedOrder(Order Order, decimal Total);

public enum FailureCategory
{
    InvalidRow,
    UnknownProduct,
    ApiError,
}

public sealed record FailedOrder(string OrderId, FailureCategory Category, string Detail);

public sealed record ProductDto(string Id, string Name, decimal Price);

public enum ProductLookupStatus
{
    Found,
    NotFound,
    Error,
}

public sealed record ProductLookup(ProductLookupStatus Status, decimal Price = 0, string Detail = "")
{
    public static ProductLookup Found(decimal price) => new(ProductLookupStatus.Found, price);

    public static ProductLookup NotFound() => new(ProductLookupStatus.NotFound);

    public static ProductLookup Error(string detail) => new(ProductLookupStatus.Error, Detail: detail);
}

public sealed record SimulationInfo(int DelayMs, double ErrorRate, int Seed, int TimeoutMs);
