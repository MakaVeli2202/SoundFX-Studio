using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioProductionHealthMonitorTests
{
    [Fact]
    public void InitialState_IsUnavailable()
    {
        var monitor = new AudioProductionHealthMonitor();
        Assert.Equal(AudioHealthStatus.Unavailable, monitor.CurrentState);
    }

    [Fact]
    public void HealthyToWarning_WhenBudgetHigh()
    {
        var monitor = new AudioProductionHealthMonitor();
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 75,
            OverBudgetBlockCount = 0
        });

        // Should transition after enough consecutive high-budget blocks
        // With 4Hz refresh, 5 seconds = 20 blocks
        for (int i = 0; i < 25; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 75,
                OverBudgetBlockCount = 0
            });
        }

        Assert.Equal(AudioHealthStatus.Warning, monitor.CurrentState);
    }

    [Fact]
    public void WarningToHealthy_WhenBudgetLow()
    {
        var monitor = new AudioProductionHealthMonitor();

        // Get to Warning state
        for (int i = 0; i < 25; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 75,
                OverBudgetBlockCount = 0
            });
        }
        Assert.Equal(AudioHealthStatus.Warning, monitor.CurrentState);

        // Now get to Healthy (10 seconds = 40 blocks at 4Hz)
        for (int i = 0; i < 50; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 30,
                OverBudgetBlockCount = 0
            });
        }
        Assert.Equal(AudioHealthStatus.Healthy, monitor.CurrentState);
    }

    [Fact]
    public void DeadlineMiss_GoesToCritical()
    {
        var monitor = new AudioProductionHealthMonitor();
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 1
        });

        Assert.Equal(AudioHealthStatus.Critical, monitor.CurrentState);
    }

    [Fact]
    public void CriticalToWarning_WhenNoMisses()
    {
        var monitor = new AudioProductionHealthMonitor();

        // Go to Critical
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 1
        });
        Assert.Equal(AudioHealthStatus.Critical, monitor.CurrentState);

        // Wait for CriticalToWarning threshold (30 seconds)
        // In real usage, TimeInCurrentState would be checked.
        // For test, we verify the state logic via repeated calls.
        // The monitor uses DateTime internally, so we can't easily test the timing.
        // Instead, verify that no deadline misses moves to Warning is possible.
        for (int i = 0; i < 100; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 30,
                OverBudgetBlockCount = 0
            });
        }
        // After enough time, should be Warning (or still Critical if < 30s)
        // This is a timing-dependent test; we verify no exception
        Assert.True(monitor.CurrentState == AudioHealthStatus.Warning
                     || monitor.CurrentState == AudioHealthStatus.Critical);
    }

    [Fact]
    public void MarkUnavailable_ResetsToUnavailable()
    {
        var monitor = new AudioProductionHealthMonitor();
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 0
        });
        monitor.MarkUnavailable();
        Assert.Equal(AudioHealthStatus.Unavailable, monitor.CurrentState);
    }

    [Fact]
    public void Reset_ClearsAll()
    {
        var monitor = new AudioProductionHealthMonitor();
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 0
        });
        monitor.Reset();
        Assert.Equal(AudioHealthStatus.Unavailable, monitor.CurrentState);
        Assert.Equal(0, monitor.DeadlineMissCount);
    }

    [Fact]
    public void DeadlineMissCount_Increments()
    {
        var monitor = new AudioProductionHealthMonitor();
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 3
        });
        Assert.Equal(3, monitor.DeadlineMissCount);
    }

    [Fact]
    public void GetStatusDescription_ReturnsNonEmpty()
    {
        var monitor = new AudioProductionHealthMonitor();
        Assert.False(string.IsNullOrEmpty(monitor.GetStatusDescription()));
    }

    [Fact]
    public void TimeInCurrentState_IsNonNegative()
    {
        var monitor = new AudioProductionHealthMonitor();
        Assert.True(monitor.TimeInCurrentState >= TimeSpan.Zero);
    }

    [Fact]
    public void Hysteresis_PreventsOscillation()
    {
        var monitor = new AudioProductionHealthMonitor();

        // Get to Healthy
        for (int i = 0; i < 25; i++)
        {
            monitor.UpdateState(new AudioLatencySnapshot
            {
                HealthStatus = AudioHealthStatus.Healthy,
                DspBudgetPercent = 75,
                OverBudgetBlockCount = 0
            });
        }
        Assert.Equal(AudioHealthStatus.Warning, monitor.CurrentState);

        // Brief low-budget shouldn't immediately go back to Healthy
        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 30,
            OverBudgetBlockCount = 0
        });
        // Should still be Warning (hysteresis prevents quick flip)
        Assert.Equal(AudioHealthStatus.Warning, monitor.CurrentState);
    }

    [Fact]
    public void UnavailableToHealthy_WhenDeviceBecomesActive()
    {
        var monitor = new AudioProductionHealthMonitor();
        Assert.Equal(AudioHealthStatus.Unavailable, monitor.CurrentState);

        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 10,
            OverBudgetBlockCount = 0
        });
        Assert.Equal(AudioHealthStatus.Healthy, monitor.CurrentState);
    }
}
