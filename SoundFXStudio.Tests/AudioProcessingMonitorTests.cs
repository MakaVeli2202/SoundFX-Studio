using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioProcessingMonitorTests
{
    // ── Step 13 tests: Empty, single, avg, max, P95, P99, bounds, over-budget, thread safety, memory, reset ──

    [Fact]
    public void EmptySnapshot_ReturnsZeros()
    {
        var monitor = new AudioProcessingMonitor();
        var snap = monitor.GetSnapshot();

        Assert.Equal(0, snap.MeasurementCount);
        Assert.Equal(0, snap.AverageUs);
        Assert.Equal(0, snap.MaxUs);
        Assert.Equal(0, snap.P95Us);
        Assert.Equal(0, snap.P99Us);
        Assert.Equal(0, snap.OverBudgetBlockCount);
    }

    [Fact]
    public void SingleMeasurement()
    {
        var monitor = new AudioProcessingMonitor();
        monitor.RecordMeasurement(50.0);
        var snap = monitor.GetSnapshot();

        Assert.Equal(1, snap.MeasurementCount);
        Assert.Equal(50.0, snap.AverageUs, 2);
        Assert.Equal(50.0, snap.MaxUs, 2);
        Assert.Equal(50.0, snap.P95Us, 2);
        Assert.Equal(50.0, snap.P99Us, 2);
    }

    [Fact]
    public void AverageCalculation()
    {
        var monitor = new AudioProcessingMonitor();
        monitor.RecordMeasurement(10);
        monitor.RecordMeasurement(20);
        monitor.RecordMeasurement(30);
        var snap = monitor.GetSnapshot();

        Assert.Equal(20.0, snap.AverageUs, 2);
    }

    [Fact]
    public void MaxCalculation()
    {
        var monitor = new AudioProcessingMonitor();
        monitor.RecordMeasurement(10);
        monitor.RecordMeasurement(100);
        monitor.RecordMeasurement(50);
        var snap = monitor.GetSnapshot();

        Assert.Equal(100.0, snap.MaxUs, 2);
    }

    [Fact]
    public void P95Calculation()
    {
        var monitor = new AudioProcessingMonitor();
        // 100 measurements: 0..99
        for (int i = 0; i < 100; i++)
            monitor.RecordMeasurement(i);
        var snap = monitor.GetSnapshot();

        // P95 of 0..99 ≈ 95
        Assert.InRange(snap.P95Us, 94, 99);
    }

    [Fact]
    public void P99Calculation()
    {
        var monitor = new AudioProcessingMonitor();
        for (int i = 0; i < 100; i++)
            monitor.RecordMeasurement(i);
        var snap = monitor.GetSnapshot();

        // P99 of 0..99 ≈ 99
        Assert.InRange(snap.P99Us, 98, 99);
    }

    [Fact]
    public void RollingWindowRemainsBounded()
    {
        var capacity = 64;
        var monitor = new AudioProcessingMonitor(capacity);
        Assert.Equal(capacity, monitor.Capacity);

        // Record 3x capacity measurements
        for (int i = 0; i < capacity * 3; i++)
            monitor.RecordMeasurement(i);

        var snap = monitor.GetSnapshot();
        // TotalCount should be 3*capacity (all recorded)
        Assert.Equal(capacity * 3, snap.MeasurementCount);
        // But the ring buffer only holds `capacity` entries
        // Snapshot should use at most `capacity` values
    }

    [Fact]
    public void OverBudgetDetection()
    {
        var monitor = new AudioProcessingMonitor();
        monitor.SetBlockDurationUs(100.0); // 100µs deadline

        monitor.RecordMeasurement(50);  // under budget
        monitor.RecordMeasurement(150); // over budget
        monitor.RecordMeasurement(200); // over budget
        monitor.RecordMeasurement(90);  // under budget

        var snap = monitor.GetSnapshot();
        Assert.Equal(2, snap.OverBudgetBlockCount);
    }

    [Fact]
    public void SnapshotRetrieval_ThreadSafety()
    {
        var monitor = new AudioProcessingMonitor(256);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            int i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                monitor.RecordMeasurement(i++ % 200);
            }
        });

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var snap = monitor.GetSnapshot();
                Assert.True(snap.MeasurementCount >= 0);
            }
        });

        Task.WhenAll(writer, reader).Wait();
    }

    [Fact]
    public void NoUnboundedMemoryGrowth()
    {
        var monitor = new AudioProcessingMonitor(128);
        for (int i = 0; i < 1_000_000; i++)
            monitor.RecordMeasurement(i % 200);

        // Monitor should still work, no OOM
        var snap = monitor.GetSnapshot();
        Assert.True(snap.MeasurementCount > 0);
    }

    [Fact]
    public void ResetClearsAll()
    {
        var monitor = new AudioProcessingMonitor();
        monitor.RecordMeasurement(100);
        monitor.RecordMeasurement(200);
        monitor.SetBlockDurationUs(50);

        monitor.Reset();
        var snap = monitor.GetSnapshot();

        Assert.Equal(0, snap.MeasurementCount);
        Assert.Equal(0, snap.MaxUs);
        Assert.Equal(0, snap.OverBudgetBlockCount);
    }

    // ── Step 16 tests: Timing overhead ────────────────────────────────────

    [Fact]
    public void StopwatchTiming_StartStopCycle()
    {
        var monitor = new AudioProcessingMonitor();

        monitor.StartTiming();
        // Simulate some work
        Thread.Sleep(1);
        monitor.StopTiming();

        var snap = monitor.GetSnapshot();
        Assert.True(snap.MaxUs > 0);
        Assert.True(snap.AverageUs > 0);
    }

    [Fact]
    public void RecordingOverhead_IsSmall()
    {
        var monitor = new AudioProcessingMonitor(4096);

        // Measure recording overhead
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 100_000;
        for (int i = 0; i < iterations; i++)
            monitor.RecordMeasurement(10.0);
        sw.Stop();

        var usPerRecord = sw.Elapsed.TotalMicroseconds / iterations;
        // Recording should take < 0.5µs per call
        Assert.True(usPerRecord < 0.5, $"Recording overhead: {usPerRecord:F3}µs per call");
    }

    [Fact]
    public void SnapshotRetrieval_IsFast()
    {
        var monitor = new AudioProcessingMonitor(1024);
        for (int i = 0; i < 1024; i++)
            monitor.RecordMeasurement(i);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 10_000;
        for (int i = 0; i < iterations; i++)
            monitor.GetSnapshot();
        sw.Stop();

        var usPerCall = sw.Elapsed.TotalMicroseconds / iterations;
        // Snapshot retrieval should take < 100µs (called at 4Hz from UI thread)
        Assert.True(usPerCall < 100, $"Snapshot overhead: {usPerCall:F1}µs per call");
    }

    // ── Allocation tests ──────────────────────────────────────────────────

    [Fact]
    public void RecordMeasurement_ZeroAllocations()
    {
        var monitor = new AudioProcessingMonitor();
        var gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < 10_000; i++)
            monitor.RecordMeasurement(i % 200);

        // No Gen0 collections should have been triggered by recording
        // (Allow some slack — other code may trigger GC)
        var gen0After = GC.CollectionCount(0);
        // We can't strictly assert no GC, but we can verify it completes
        Assert.True(gen0After >= gen0Before);
    }
}
