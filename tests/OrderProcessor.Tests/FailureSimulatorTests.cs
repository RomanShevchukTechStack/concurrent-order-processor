using Microsoft.Extensions.Options;
using OrderProcessor.Api;

namespace OrderProcessor.Tests;

public class FailureSimulatorTests
{
    [Fact]
    public void Zero_error_rate_never_fails()
    {
        var simulator = Create(errorRate: 0, seed: 1);

        Assert.All(Enumerable.Range(1, 500), i => Assert.Equal(FailureKind.None, simulator.NextFailure($"P{i:D3}")));
    }

    [Fact]
    public void Full_error_rate_always_fails()
    {
        var simulator = Create(errorRate: 1, seed: 1);

        Assert.All(Enumerable.Range(1, 500), i => Assert.NotEqual(FailureKind.None, simulator.NextFailure($"P{i:D3}")));
    }

    [Fact]
    public void Same_seed_gives_same_failures()
    {
        var first = Pattern(Create(0.3, 42));
        var second = Pattern(Create(0.3, 42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seed_gives_different_failures()
    {
        Assert.NotEqual(Pattern(Create(0.3, 1)), Pattern(Create(0.3, 2)));
    }

    [Fact]
    public void Failure_share_is_close_to_error_rate()
    {
        var failures = Pattern(Create(0.3, 42)).Count(kind => kind != FailureKind.None);

        // 200 products x 5 attempts = 1000 requests.
        Assert.InRange(failures, 250, 350);
    }

    [Fact]
    public void Result_does_not_depend_on_request_order()
    {
        var forward = Create(0.3, 42);
        var backward = Create(0.3, 42);

        var ids = Enumerable.Range(1, 200).Select(i => ProductCatalog.Id(i)).ToList();
        var forwardResult = ids.ToDictionary(id => id, id => forward.NextFailure(id));
        var backwardResult = Enumerable.Reverse(ids).ToDictionary(id => id, id => backward.NextFailure(id));

        Assert.Equal(forwardResult, backwardResult);
    }

    private static FailureSimulator Create(double errorRate, int seed) =>
        new(Options.Create(new SimulationOptions { ErrorRate = errorRate, Seed = seed }));

    private static List<FailureKind> Pattern(FailureSimulator simulator) =>
        Enumerable.Range(1, 200)
            .SelectMany(i => Enumerable.Range(0, 5).Select(_ => simulator.NextFailure(ProductCatalog.Id(i))))
            .ToList();
}
