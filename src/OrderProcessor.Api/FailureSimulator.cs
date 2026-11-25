using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace OrderProcessor.Api;

public sealed class SimulationOptions
{
    /// <summary>Fixed response delay in milliseconds.</summary>
    public int DelayMs { get; set; }

    /// <summary>Share of requests that fail, from 0.0 to 1.0.</summary>
    public double ErrorRate { get; set; }

    /// <summary>Seed that makes the failure pattern repeatable.</summary>
    public int Seed { get; set; } = 42;

    /// <summary>How long a simulated timeout holds the request.</summary>
    public int TimeoutMs { get; set; } = 10_000;
}

public enum FailureKind
{
    None,
    ServerError,
    TooManyRequests,
    Timeout,
}

/// <summary>
/// Decides whether a request fails. The decision depends only on the seed, the product id and
/// how many times that product was requested before. It does not depend on request order or
/// timing, so the same run produces the same failures even when requests arrive concurrently.
/// </summary>
public sealed class FailureSimulator(IOptions<SimulationOptions> options)
{
    private readonly SimulationOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, int> _requestCounts = new();

    public int DelayMs => _options.DelayMs;

    public int TimeoutMs => _options.TimeoutMs;

    public FailureKind NextFailure(string productId)
    {
        if (_options.ErrorRate <= 0)
        {
            return FailureKind.None;
        }

        var attempt = _requestCounts.AddOrUpdate(productId, 1, (_, count) => count + 1);
        var hash = Hash(_options.Seed, productId, attempt);

        // Top 32 bits give the failure roll, low bits pick the failure type.
        var roll = (hash >> 32) / (double)uint.MaxValue;
        if (roll >= _options.ErrorRate)
        {
            return FailureKind.None;
        }

        return (hash % 3) switch
        {
            0 => FailureKind.ServerError,
            1 => FailureKind.TooManyRequests,
            _ => FailureKind.Timeout,
        };
    }

    // FNV-1a. string.GetHashCode is randomized per process, so it cannot be used here.
    private static ulong Hash(int seed, string productId, int attempt)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in $"{seed}:{productId}:{attempt}")
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        // Final avalanche so the top and low bits are both well mixed.
        hash ^= hash >> 33;
        hash *= 0xff51afd7ed558ccdUL;
        hash ^= hash >> 33;
        return hash;
    }
}
