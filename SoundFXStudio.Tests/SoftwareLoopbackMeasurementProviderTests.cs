using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class SoftwareLoopbackMeasurementProviderTests
{
    [Fact]
    public void ProviderName_IsSoftwareLoopback()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        Assert.Equal("Software Loopback", provider.ProviderName);
    }

    [Fact]
    public void Initially_NotAvailable()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void GetMeasuredLatency_ReturnsNull_BeforeMeasurement()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        Assert.Null(provider.GetMeasuredLatencyMs());
    }

    [Fact]
    public void Start_ReturnsTrue()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        Assert.True(provider.Start());
    }

    [Fact]
    public void Stop_DoesNotThrow()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        provider.Stop();
    }

    [Fact]
    public void MeasureRoundTrip_ReturnsNull_WhenNotRunning()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        var result = provider.MeasureRoundTripLatencyAsync().GetAwaiter().GetResult();
        Assert.Null(result);
    }

    [Fact]
    public void MeasurementDescription_ContainsHonestLimitations()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        var desc = provider.MeasurementDescription;
        Assert.Contains("Voicemeeter", desc);
        Assert.Contains("NOT", desc);
    }

    [Fact]
    public void SetCaptureProviderFactory_DoesNotThrow()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        provider.SetCaptureProviderFactory(() => new NullAudioLatencyMeasurementProvider());
    }

    [Fact]
    public void MeasureRoundTrip_ReturnsNull_WhenRunning_ButNoHardware()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        provider.Start();
        // Without real WASAPI loopback, this returns null
        var result = provider.MeasureRoundTripLatencyAsync().GetAwaiter().GetResult();
        Assert.Null(result);
    }

    [Fact]
    public void CancellationToken_CanCancelMeasurement()
    {
        var provider = new SoftwareLoopbackMeasurementProvider();
        provider.Start();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        // Should either return null or throw OperationCanceledException
        try
        {
            var result = provider.MeasureRoundTripLatencyAsync(cts.Token).GetAwaiter().GetResult();
            // If it returned, it should be null (no hardware)
            Assert.Null(result);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    }
}
