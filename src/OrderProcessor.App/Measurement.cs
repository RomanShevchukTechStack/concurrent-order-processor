using System.Diagnostics;

namespace OrderProcessor.App;

public sealed record Measurement(
    long ElapsedMs,
    double PeakWorkingSetMb,
    double AllocatedMb,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);

/// <summary>
/// Measures one piece of work: wall time, allocations and GC counts for that work only, plus
/// the peak working set of the whole process (which also includes runtime startup).
/// </summary>
public sealed class MeasurementScope
{
    private readonly Stopwatch _stopwatch;
    private readonly long _allocatedBefore;
    private readonly int _gen0Before;
    private readonly int _gen1Before;
    private readonly int _gen2Before;

    private MeasurementScope()
    {
        // Start from a clean heap so earlier garbage does not land in the counts.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        _gen0Before = GC.CollectionCount(0);
        _gen1Before = GC.CollectionCount(1);
        _gen2Before = GC.CollectionCount(2);
        _stopwatch = Stopwatch.StartNew();
    }

    public static MeasurementScope Start() => new();

    public Measurement Stop()
    {
        _stopwatch.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - _allocatedBefore;
        var peak = Process.GetCurrentProcess().PeakWorkingSet64;

        return new Measurement(
            _stopwatch.ElapsedMilliseconds,
            peak / 1024.0 / 1024.0,
            allocated / 1024.0 / 1024.0,
            GC.CollectionCount(0) - _gen0Before,
            GC.CollectionCount(1) - _gen1Before,
            GC.CollectionCount(2) - _gen2Before);
    }
}
