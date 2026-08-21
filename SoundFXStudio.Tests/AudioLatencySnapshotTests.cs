using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioLatencySnapshotTests
{
    [Fact]
    public void DefaultSnapshot_HasExpectedValues()
    {
        var snapshot = new AudioLatencySnapshot();

        Assert.Equal(0, snapshot.ConfiguredCaptureLatencyMs);
        Assert.Equal(0, snapshot.ConfiguredOutputLatencyMs);
        Assert.Equal(0, snapshot.OutputBufferCount);
        Assert.Equal(0, snapshot.MeasuredDspP95Us);
        Assert.Equal(0, snapshot.MeasuredDspP99Us);
        Assert.Equal(0, snapshot.MeasuredDspMaxUs);
        Assert.Equal(0, snapshot.MeasuredDspAverageUs);
        Assert.Equal(0, snapshot.EstimatedApplicationLatencyMs);
        Assert.Null(snapshot.MeasuredRoundTripLatencyMs);
        Assert.Null(snapshot.MeasuredEndToEndLatencyMs);
        Assert.False(snapshot.ExternalLatencyKnown);
        Assert.Equal(0, snapshot.SampleRate);
        Assert.Equal(0, snapshot.FramesPerBlock);
        Assert.Equal(0, snapshot.BlockDurationMs);
        Assert.Equal(0, snapshot.DspBudgetPercent);
        Assert.Equal(0, snapshot.OverBudgetBlockCount);
        Assert.Equal(0, snapshot.CaptureStarvationCount);
        Assert.Equal(0, snapshot.OutputUnderrunCount);
    }

    [Fact]
    public void DspContributionMs_IsP99DividedBy1000()
    {
        var snapshot = new AudioLatencySnapshot
        {
            MeasuredDspP99Us = 500
        };
        Assert.Equal(0.5, snapshot.MeasuredDspContributionMs, 5);
    }

    [Fact]
    public void ExternalLatencyKnown_IsAlwaysFalse()
    {
        var snapshot = new AudioLatencySnapshot();
        Assert.False(snapshot.ExternalLatencyKnown);
    }

    [Fact]
    public void ExternalLatencyDescription_IsHonest()
    {
        var snapshot = new AudioLatencySnapshot();
        Assert.Contains("not measurable", snapshot.ExternalLatencyDescription.ToLowerInvariant());
    }

    [Fact]
    public void HealthStatus_DefaultsToHealthy()
    {
        var snapshot = new AudioLatencySnapshot();
        Assert.Equal(AudioHealthStatus.Healthy, snapshot.HealthStatus);
    }

    [Fact]
    public void Timestamp_IsSetOnCreation()
    {
        var before = DateTime.UtcNow;
        var snapshot = new AudioLatencySnapshot();
        var after = DateTime.UtcNow;

        Assert.True(snapshot.Timestamp >= before);
        Assert.True(snapshot.Timestamp <= after);
    }

    [Fact]
    public void MeasuredRoundTrip_WhenNull_IndicatesNotMeasured()
    {
        var snapshot = new AudioLatencySnapshot();
        Assert.Null(snapshot.MeasuredRoundTripLatencyMs);
    }

    [Fact]
    public void MeasuredEndToEnd_WhenNull_IndicatesNotMeasured()
    {
        var snapshot = new AudioLatencySnapshot();
        Assert.Null(snapshot.MeasuredEndToEndLatencyMs);
    }

    [Fact]
    public void InitProperties_CanBeSet()
    {
        var snapshot = new AudioLatencySnapshot
        {
            ConfiguredCaptureLatencyMs = 20,
            ConfiguredOutputLatencyMs = 100,
            OutputBufferCount = 2,
            MeasuredDspP95Us = 400,
            MeasuredDspP99Us = 500,
            MeasuredDspMaxUs = 600,
            MeasuredDspAverageUs = 350,
            EstimatedApplicationLatencyMs = 120,
            MeasuredRoundTripLatencyMs = 130,
            SampleRate = 48000,
            FramesPerBlock = 480,
            BlockDurationMs = 10,
            DspBudgetPercent = 5,
            OverBudgetBlockCount = 0,
            CaptureStarvationCount = 0,
            OutputUnderrunCount = 0,
            MeasurementDuration = TimeSpan.FromSeconds(10),
            MeasurementsCollected = 4800,
            HealthStatus = AudioHealthStatus.Healthy
        };

        Assert.Equal(20, snapshot.ConfiguredCaptureLatencyMs);
        Assert.Equal(100, snapshot.ConfiguredOutputLatencyMs);
        Assert.Equal(2, snapshot.OutputBufferCount);
        Assert.Equal(400, snapshot.MeasuredDspP95Us);
        Assert.Equal(500, snapshot.MeasuredDspP99Us);
        Assert.Equal(130, snapshot.MeasuredRoundTripLatencyMs);
        Assert.Equal(48000, snapshot.SampleRate);
        Assert.Equal(4800, snapshot.MeasurementsCollected);
    }
}
