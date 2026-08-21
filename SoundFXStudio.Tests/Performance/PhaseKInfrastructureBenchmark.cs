using System.Diagnostics;
using SoundFXStudio.Models;
using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests.Performance;

/// <summary>
/// Benchmark tests for Phase K infrastructure: timestamp monitor, health monitor,
/// latency snapshot creation, and measurement provider overhead.
/// </summary>
public class PhaseKInfrastructureBenchmark
{
    [Fact]
    public void TimestampMonitor_FullLifecycle_HasLowOverhead()
    {
        var monitor = new AudioBlockTimestampMonitor(4096);
        const int iterations = 100_000;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
            monitor.RecordOutputSubmit();
        }
        sw.Stop();

        var usPerBlock = sw.Elapsed.TotalMicroseconds / iterations;
        // Full lifecycle should be well under 5µs
        Assert.True(usPerBlock < 5.0, $"Timestamp monitor overhead: {usPerBlock:F3}µs per block");
    }

    [Fact]
    public void TimestampMonitor_Snapshot_IsFast()
    {
        var monitor = new AudioBlockTimestampMonitor(1024);
        for (int i = 0; i < 1024; i++)
        {
            monitor.RecordCaptureTimestamp();
            monitor.RecordDspEntry();
            monitor.RecordDspExit();
            monitor.RecordOutputSubmit();
        }

        var sw = Stopwatch.StartNew();
        const int iterations = 10_000;
        for (int i = 0; i < iterations; i++)
            monitor.GetSnapshot();
        sw.Stop();

        var usPerCall = sw.Elapsed.TotalMicroseconds / iterations;
        Assert.True(usPerCall < 50, $"Timestamp snapshot overhead: {usPerCall:F1}µs per call");
    }

    [Fact]
    public void HealthMonitor_UpdateState_HasLowOverhead()
    {
        var monitor = new AudioProductionHealthMonitor();
        const int iterations = 100_000;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 30,
                OverBudgetBlockCount = 0
            });
        }
        sw.Stop();

        var usPerCall = sw.Elapsed.TotalMicroseconds / iterations;
        Assert.True(usPerCall < 1.0, $"Health monitor overhead: {usPerCall:F3}µs per call");
    }

    [Fact]
    public void AudioLatencySnapshot_Creation_HasLowOverhead()
    {
        const int iterations = 100_000;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = new AudioLatencySnapshot
            {
                ConfiguredCaptureLatencyMs = 20,
                ConfiguredOutputLatencyMs = 100,
                OutputBufferCount = 2,
                MeasuredDspP95Us = 400,
                MeasuredDspP99Us = 500,
                MeasuredDspMaxUs = 600,
                MeasuredDspAverageUs = 350,
                EstimatedApplicationLatencyMs = 120,
                SampleRate = 48000,
                FramesPerBlock = 480,
                BlockDurationMs = 10,
                DspBudgetPercent = 5,
                HealthStatus = AudioHealthStatus.Healthy
            };
        }
        sw.Stop();

        var nsPerCreate = sw.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
        Assert.True(nsPerCreate < 1000, $"Snapshot creation: {nsPerCreate:F1}ns per creation");
    }

    [Fact]
    public void LatencyConfiguration_SafetyWarnings_IsFast()
    {
        const int iterations = 100_000;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.LowLatency);
            _ = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.Safe);
            _ = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.Balanced);
        }
        sw.Stop();

        var usPerCall = sw.Elapsed.TotalMicroseconds / (iterations * 3);
        Assert.True(usPerCall < 1.0, $"Safety warnings overhead: {usPerCall:F3}µs per call");
    }

    [Fact]
    public void CombinedMonitorPipeline_HasAcceptableOverhead()
    {
        var tsMonitor = new AudioBlockTimestampMonitor(4096);
        var healthMonitor = new AudioProductionHealthMonitor();
        var dspMonitor = new AudioProcessingMonitor(4096);
        dspMonitor.SetBlockDurationUs(10000); // 10ms block

        const int iterations = 50_000;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            // Simulate full pipeline monitoring
            tsMonitor.RecordCaptureTimestamp();
            tsMonitor.RecordDspEntry();
            dspMonitor.StartTiming();
            // Simulate DSP work (minimal)
            Thread.SpinWait(10);
            dspMonitor.StopTiming();
            tsMonitor.RecordDspExit();
            tsMonitor.RecordOutputSubmit();

            // Periodic health check (every 100 blocks = ~4Hz at 48kHz/480)
            if (i % 100 == 0)
            {
                var dspSnap = dspMonitor.GetSnapshot();
                healthMonitor.UpdateState(new AudioLatencySnapshot
                {
                    DspBudgetPercent = (dspSnap.P99Us / 10000) * 100,
                    OverBudgetBlockCount = dspSnap.OverBudgetBlockCount,
                    HealthStatus = dspSnap.OverBudgetBlockCount > 0
                        ? AudioHealthStatus.Critical
                        : AudioHealthStatus.Healthy
                });
            }
        }
        sw.Stop();

        var usPerBlock = sw.Elapsed.TotalMicroseconds / iterations;
        // Combined overhead should be under 10µs per block
        Assert.True(usPerBlock < 10.0, $"Combined pipeline overhead: {usPerBlock:F3}µs per block");
    }
}
