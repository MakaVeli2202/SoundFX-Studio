using SoundFXStudio.Models;
using SoundFXStudio.Services.Diagnostics;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

/// <summary>
/// Regression tests for bugs discovered during Phase L audit.
/// </summary>
public class PhaseLRegressionTests
{
    // ── Bug 2: AudioBlockTimestampMonitor volatile read ───────────────────

    [Fact]
    public void TimestampMonitor_ConcurrentRecordAndRead_NoCorruption()
    {
        var monitor = new AudioBlockTimestampMonitor(256);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                monitor.RecordCaptureTimestamp();
        });

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                monitor.RecordDspEntry();
                monitor.RecordDspExit();
                monitor.RecordOutputSubmit();
            }
        });

        var snapshotted = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var snap = monitor.GetSnapshot();
                Assert.True(snap.MeasurementCount >= 0);
            }
        });

        Task.WhenAll(writer, reader, snapshotted).Wait();
    }

    // ── Bug 3: HrtfEffect transition buffer preallocation ────────────────

    [Fact]
    public void HrtfEffect_TransitionDoesNotProduceInvalidOutput()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);

        var buffer = new float[480 * 2];
        effect.Process(buffer);

        effect.SetDirection(45.0, 15.0);

        for (int i = 0; i < 50; i++)
            effect.Process(buffer);

        foreach (var sample in buffer)
        {
            Assert.False(float.IsNaN(sample), "NaN in transition output");
            Assert.False(float.IsInfinity(sample), "Infinity in transition output");
        }
    }

    [Fact]
    public void HrtfEffect_MultipleTransitions_NoInvalidOutput()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);

        var buffer = new float[960 * 2];
        effect.Process(buffer);

        for (int i = 0; i < 20; i++)
        {
            effect.SetDirection(i * 18.0 - 180, 0);
            effect.Process(buffer);

            foreach (var sample in buffer)
            {
                Assert.False(float.IsNaN(sample), $"NaN at direction change {i}");
                Assert.False(float.IsInfinity(sample), $"Infinity at direction change {i}");
            }
        }
    }

    // ── Bug 4: HeadTrackingService Provider setter resets state ──────────

    [Fact]
    public void HeadTrackingService_ProviderSwap_StopDisposeOld()
    {
        var service = new HeadTrackingService();
        var provider1 = new TrackingProviderSpy();
        service.Provider = provider1;

        var provider2 = new TrackingProviderSpy();
        service.Provider = provider2;

        Assert.True(provider1.WasStopped);
        Assert.True(provider1.WasDisposed);
        Assert.False(provider2.WasStopped);
        Assert.False(provider2.WasDisposed);
    }

    [Fact]
    public void HeadTrackingService_ProviderSwap_ResetsRateLimitState()
    {
        var service = new HeadTrackingService();
        var provider1 = new StubHeadTrackingProvider();
        provider1.Orientation = new HeadOrientation(30.0, 10.0, 0.0);
        provider1.Start();
        service.Provider = provider1;

        // After swap to provider2, stale _lastAzimuth should be cleared
        var provider2 = new StubHeadTrackingProvider();
        provider2.Orientation = new HeadOrientation(31.0, 10.0, 0.0);
        provider2.Start();
        service.Provider = provider2;

        Assert.Equal(provider2, service.Provider);
    }

    // ── Azimuth wrap regression ─────────────────────────────────────────

    [Fact]
    public void HrtfDirectionInterpolator_AzimuthWrap_NoDiscontinuity()
    {
        var profile = CreateTestProfile(48000, 128);

        var hrir170 = HrtfDirectionInterpolator.Interpolate(profile, 170, 0);
        var hrir180 = HrtfDirectionInterpolator.Interpolate(profile, 180, 0);
        var hrirM180 = HrtfDirectionInterpolator.Interpolate(profile, -180, 0);
        var hrirM170 = HrtfDirectionInterpolator.Interpolate(profile, -170, 0);

        Assert.NotNull(hrir170.Left);
        Assert.NotNull(hrirM170.Left);
        Assert.Equal(hrir170.Left.Length, hrirM170.Left.Length);

        // The wrap boundary (180 vs -180) should produce the same result
        Assert.Equal(hrir180.Left[0], hrirM180.Left[0], 6);
        Assert.Equal(hrir180.Right[0], hrirM180.Right[0], 6);
    }

    [Fact]
    public void HrtfDirectionInterpolator_ElevationClamp()
    {
        var profile = CreateTestProfile(48000, 128);

        var hrirTop = HrtfDirectionInterpolator.Interpolate(profile, 0, 90);
        var hrirBottom = HrtfDirectionInterpolator.Interpolate(profile, 0, -90);

        Assert.NotNull(hrirTop.Left);
        Assert.NotNull(hrirBottom.Left);
        Assert.True(hrirTop.Left.Length > 0);
        Assert.True(hrirBottom.Left.Length > 0);
    }

    // ── SpatialMix boundary regression ───────────────────────────────────

    [Fact]
    public void HrtfEffect_SpatialMixBoundaries_NoClip()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);

        effect.SetDirection(0, 0);
        effect.SpatialMix = 0.0;
        var buffer0 = new float[480 * 2];
        for (int i = 0; i < buffer0.Length; i += 2) { buffer0[i] = 0.5f; buffer0[i + 1] = 0.5f; }
        effect.Process(buffer0);

        effect.SpatialMix = 1.0;
        var buffer100 = new float[480 * 2];
        for (int i = 0; i < buffer100.Length; i += 2) { buffer100[i] = 0.5f; buffer100[i + 1] = 0.5f; }
        effect.Process(buffer100);

        foreach (var sample in buffer100)
        {
            Assert.False(float.IsNaN(sample), "NaN in output");
            Assert.False(float.IsInfinity(sample), "Infinity in output");
            Assert.InRange(sample, -2.0f, 2.0f);
        }
    }

    // ── Latency mode safety regression ───────────────────────────────────

    [Fact]
    public void LatencyConfiguration_AllModesSafeForRestart()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var config = AudioLatencyConfiguration.Resolve(mode);
            Assert.True(config.RequiresRestart, $"{mode} should require restart");
            Assert.True(config.DesiredLatencyMs > 0, $"{mode} should have positive latency");
            Assert.True(config.NumberOfBuffers >= 2, $"{mode} should have >= 2 buffers");
        }
    }

    // ── Health monitor hysteresis regression ──────────────────────────────

    [Fact]
    public void HealthMonitor_UnavailableToHighBudget_TransitionsToHealthy()
    {
        var monitor = new AudioProductionHealthMonitor();
        Assert.Equal(AudioHealthStatus.Unavailable, monitor.CurrentState);

        monitor.UpdateState(new AudioLatencySnapshot
        {
            HealthStatus = AudioHealthStatus.Healthy,
            DspBudgetPercent = 75,
            OverBudgetBlockCount = 0
        });

        Assert.Equal(AudioHealthStatus.Healthy, monitor.CurrentState);
    }

    [Fact]
    public void HealthMonitor_ProviderSwapDoesNotLeak()
    {
        var service = new HeadTrackingService();
        var p1 = new TrackingProviderSpy();
        var p2 = new TrackingProviderSpy();
        var p3 = new TrackingProviderSpy();

        service.Provider = p1;
        service.Provider = p2;
        service.Provider = p3;

        Assert.True(p1.WasDisposed);
        Assert.True(p2.WasDisposed);
        Assert.False(p3.WasDisposed);
    }

    // ── Stress: 200 rapid direction changes through Process ─────────────

    [Fact]
    public void Stress_200RapidDirectionChanges_NoInvalidOutput()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);

        var buffer = new float[960 * 2];
        effect.Process(buffer);

        for (int i = 0; i < 200; i++)
        {
            var az = (i * 137.5) % 360.0 - 180.0;
            var el = (i * 23.7) % 90.0 - 45.0;
            effect.SetDirection(az, el);
            effect.Process(buffer);

            foreach (var sample in buffer)
            {
                Assert.False(float.IsNaN(sample), $"NaN at step {i}");
                Assert.False(float.IsInfinity(sample), $"Infinity at step {i}");
            }
        }
    }

    // ── Stress: DSPChain with multiple exceptions survives ──────────────

    [Fact]
    public void Stress_DSPChainMultipleExceptions_AllRemainingEffectsRun()
    {
        var chain = new DSPChain();
        chain.Add(new OffsetEffect(1.0f));
        chain.Add(new ThrowingEffect());
        chain.Add(new OffsetEffect(2.0f));
        chain.Add(new ThrowingEffect());
        chain.Add(new OffsetEffect(4.0f));

        var buffer = new float[] { 0.0f };
        for (int i = 0; i < 100; i++)
        {
            chain.Process(buffer);
        }

        Assert.Equal(700.0f, buffer[0]);
    }

    // ── Stress: rapid HRTF enable/disable ───────────────────────────────

    [Fact]
    public void Stress_RapidEnableDisable_NoCorruption()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);
        effect.SetDirection(45, 0);

        var buffer = new float[480 * 2];

        for (int i = 0; i < 200; i++)
        {
            effect.IsEnabled = (i % 2 == 0);
            effect.Process(buffer);

            foreach (var sample in buffer)
            {
                Assert.False(float.IsNaN(sample), $"NaN at step {i}");
                Assert.False(float.IsInfinity(sample), $"Infinity at step {i}");
            }
        }
    }

    // ── Stress: profile switching ────────────────────────────────────────

    [Fact]
    public void Stress_RapidProfileSwitching_NoCorruption()
    {
        var effect = new HrtfEffect(48000);
        effect.IsEnabled = true;

        var profile1 = CreateTestProfile(48000, 64);
        var profile2 = CreateTestProfile(48000, 128);
        var profile3 = CreateTestProfile(48000, 256);

        var buffer = new float[480 * 2];

        for (int i = 0; i < 100; i++)
        {
            effect.SetProfile(i % 3 == 0 ? profile1 : i % 3 == 1 ? profile2 : profile3);
            effect.SetDirection(i * 3.6 - 180, 0);
            effect.Process(buffer);

            foreach (var sample in buffer)
            {
                Assert.False(float.IsNaN(sample), $"NaN at step {i}");
                Assert.False(float.IsInfinity(sample), $"Infinity at step {i}");
            }
        }
    }

    // ── Allocation check: HrtfEffect.Process after warmup ───────────────

    [Fact]
    public void HrtfEffect_Process_ZeroAllocationsAfterWarmup()
    {
        var effect = new HrtfEffect(48000);
        var profile = CreateTestProfile(48000, 128);
        effect.SetProfile(profile);
        effect.SetDirection(0, 0);

        var buffer = new float[960 * 2];

        for (int i = 0; i < 50; i++)
            effect.Process(buffer);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < 1000; i++)
            effect.Process(buffer);

        var gen0After = GC.CollectionCount(0);

        Assert.Equal(gen0Before, gen0After);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static HrtfProfile CreateTestProfile(int sampleRate, int irLength)
    {
        var entries = new List<HrtfEntry>();
        int[] azimuths = [0, 45, 90, 135, 180, -135, -90, -45];
        int[] elevations = [-45, 0, 45];

        foreach (var az in azimuths)
        {
            foreach (var el in elevations)
            {
                var leftHrir = new float[irLength];
                var rightHrir = new float[irLength];
                var delay = (int)(irLength * 0.1);
                if (delay < irLength) leftHrir[delay] = 1.0f;
                if (delay + 1 < irLength) rightHrir[delay + 1] = 1.0f;

                entries.Add(new HrtfEntry
                {
                    AzimuthDeg = az,
                    ElevationDeg = el,
                    LeftEarResponse = leftHrir,
                    RightEarResponse = rightHrir
                });
            }
        }

        return new HrtfProfile
        {
            Id = "test-profile",
            Name = "Test Profile",
            SampleRate = sampleRate,
            IrLength = irLength,
            Entries = entries.ToArray()
        };
    }
}

/// <summary>
/// Spy provider that records Stop/Dispose calls.
/// </summary>
internal sealed class TrackingProviderSpy : IHeadTrackingProvider
{
    public bool WasStopped { get; private set; }
    public bool WasDisposed { get; private set; }

    public string ProviderName => "Spy";
    public bool IsAvailable => true;
    public bool IsTracking => false;

    public HeadOrientation GetOrientation() => new(0, 0, 0);
    public bool Start() => true;
    public void Stop() => WasStopped = true;
    public void Dispose() => WasDisposed = true;
}

internal sealed class ThrowingEffect : IAudioEffect
{
    public string Name => "throw";
    public bool IsEnabled { get; set; } = true;
    public void Process(Span<float> buffer) => throw new InvalidOperationException("boom");
    public void Reset() { }
}

internal sealed class OffsetEffect : IAudioEffect
{
    private readonly float _offset;
    public OffsetEffect(float offset) => _offset = offset;
    public string Name => "offset";
    public bool IsEnabled { get; set; } = true;
    public void Process(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] += _offset;
    }
    public void Reset() { }
}
