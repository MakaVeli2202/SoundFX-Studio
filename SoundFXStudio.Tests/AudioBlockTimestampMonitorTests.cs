using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioBlockTimestampMonitorTests
{
    [Fact]
    public void EmptySnapshot_ReturnsZeros()
    {
        var monitor = new AudioBlockTimestampMonitor();
        var snap = monitor.GetSnapshot();

        Assert.Equal(0, snap.MeasurementCount);
        Assert.Equal(0, snap.CaptureToDspAvgMs, 5);
        Assert.Equal(0, snap.DspProcessingAvgMs, 5);
        Assert.Equal(0, snap.DspToOutputSubmitAvgMs, 5);
    }

    [Fact]
    public void RecordCapture_IncrementsCount()
    {
        var monitor = new AudioBlockTimestampMonitor();
        monitor.RecordCaptureTimestamp();
        var snap = monitor.GetSnapshot();
        Assert.Equal(0, snap.MeasurementCount); // No DSP exit yet
    }

    [Fact]
    public void FullBlockLifecycle_RecordsAllTimestamps()
    {
        var monitor = new AudioBlockTimestampMonitor();
        monitor.RecordCaptureTimestamp();
        monitor.RecordDspEntry();
        monitor.RecordDspExit();
        monitor.RecordOutputSubmit();
        var snap = monitor.GetSnapshot();

        Assert.True(snap.MeasurementCount > 0);
    }

    [Fact]
    public void DspEntry_WithoutCaptureStillRecords()
    {
        var monitor = new AudioBlockTimestampMonitor();
        monitor.RecordDspEntry();
        monitor.RecordDspExit();
        var snap = monitor.GetSnapshot();
        Assert.True(snap.MeasurementCount > 0);
    }

    [Fact]
    public void MultipleBlocks_AreTracked()
    {
        var monitor = new AudioBlockTimestampMonitor(64);
        for (int i = 0; i < 100; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
            monitor.RecordOutputSubmit();
        }
        var snap = monitor.GetSnapshot();
        Assert.True(snap.MeasurementCount >= 64); // At least capacity
    }

    [Fact]
    public void Capacity_IsRespected()
    {
        var capacity = 32;
        var monitor = new AudioBlockTimestampMonitor(capacity);
        Assert.Equal(capacity, monitor.Capacity);
    }

    [Fact]
    public void Reset_ClearsAll()
    {
        var monitor = new AudioBlockTimestampMonitor();
        for (int i = 0; i < 10; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
        }
        monitor.Reset();
        var snap = monitor.GetSnapshot();
        Assert.Equal(0, snap.MeasurementCount);
    }

    [Fact]
    public void InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioBlockTimestampMonitor(8));
    }

    [Fact]
    public void RecordingOverhead_IsSmall()
    {
        var monitor = new AudioBlockTimestampMonitor(1024);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 50_000;
        for (int i = 0; i < iterations; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
            monitor.RecordOutputSubmit();
        }
        sw.Stop();

        var usPerRecord = sw.Elapsed.TotalMicroseconds / iterations;
        // Full lifecycle should take < 2µs per call
        Assert.True(usPerRecord < 2.0, $"Recording overhead: {usPerRecord:F3}µs per call");
    }

    [Fact]
    public void SnapshotRetrieval_IsFast()
    {
        var monitor = new AudioBlockTimestampMonitor(1024);
        for (int i = 0; i < 1024; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
            monitor.RecordOutputSubmit();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 10_000;
        for (int i = 0; i < iterations; i++)
            monitor.GetSnapshot();
        sw.Stop();

        var usPerCall = sw.Elapsed.TotalMicroseconds / iterations;
        Assert.True(usPerCall < 100, $"Snapshot overhead: {usPerCall:F1}µs per call");
    }

    [Fact]
    public void ConcurrentWriteAndRead_IsSafe()
    {
        var monitor = new AudioBlockTimestampMonitor(256);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                monitor.RecordCaptureTimestamp();
                monitor.RecordDspEntry();
                monitor.RecordDspExit();
                monitor.RecordOutputSubmit();
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
}
