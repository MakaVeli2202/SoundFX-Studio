using SoundFXStudio.Models;
using SoundFXStudio.Services.Diagnostics;
using Xunit;

namespace SoundFXStudio.Tests;

public class AudioLatencyConfigurationTests
{
    // ── Step 14 tests ────────────────────────────────────────────────────

    [Fact]
    public void SafeMode_ResolvesValidConfiguration()
    {
        var config = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Safe);
        Assert.Equal(AudioLatencyMode.Safe, config.Mode);
        Assert.True(AudioLatencyConfiguration.Validate(config));
    }

    [Fact]
    public void BalancedMode_ResolvesValidConfiguration()
    {
        var config = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Balanced);
        Assert.Equal(AudioLatencyMode.Balanced, config.Mode);
        Assert.True(AudioLatencyConfiguration.Validate(config));
    }

    [Fact]
    public void LowLatency_ResolvesValidConfiguration()
    {
        var config = AudioLatencyConfiguration.Resolve(AudioLatencyMode.LowLatency);
        Assert.Equal(AudioLatencyMode.LowLatency, config.Mode);
        Assert.True(AudioLatencyConfiguration.Validate(config));
    }

    [Fact]
    public void DesiredLatency_IsPositive()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var config = AudioLatencyConfiguration.Resolve(mode);
            Assert.True(config.DesiredLatencyMs > 0,
                $"{mode}: DesiredLatencyMs={config.DesiredLatencyMs}");
        }
    }

    [Fact]
    public void BufferCount_IsAtLeastTwo()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var config = AudioLatencyConfiguration.Resolve(mode);
            Assert.True(config.NumberOfBuffers >= 2,
                $"{mode}: NumberOfBuffers={config.NumberOfBuffers}");
        }
    }

    [Fact]
    public void SafeMode_HasLargerLatencyThanBalanced()
    {
        var safe = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Safe);
        var balanced = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Balanced);
        Assert.True(safe.DesiredLatencyMs > balanced.DesiredLatencyMs);
    }

    [Fact]
    public void BalancedMode_HasLargerLatencyThanLowLatency()
    {
        var balanced = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Balanced);
        var low = AudioLatencyConfiguration.Resolve(AudioLatencyMode.LowLatency);
        Assert.True(balanced.DesiredLatencyMs > low.DesiredLatencyMs);
    }

    [Fact]
    public void AllModes_RequireRestart()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var config = AudioLatencyConfiguration.Resolve(mode);
            Assert.True(config.RequiresRestart, $"{mode}: RequiresRestart should be true");
        }
    }

    [Fact]
    public void DefaultMode_IsBalanced()
    {
        Assert.Equal(AudioLatencyMode.Balanced, AudioLatencyConfiguration.DefaultMode);
    }

    [Fact]
    public void ConfigurationData_IsEffectivelyImmutable()
    {
        var config = AudioLatencyConfiguration.Resolve(AudioLatencyMode.Balanced);
        // Properties are init-only — verify they can be read
        Assert.Equal(AudioLatencyMode.Balanced, config.Mode);
        Assert.Equal(100, config.DesiredLatencyMs);
        Assert.Equal(2, config.NumberOfBuffers);
    }

    [Fact]
    public void CaptureBufferMs_Is20()
    {
        Assert.Equal(20.0, AudioLatencyConfiguration.CaptureBufferMs);
    }

    // ── Output latency info tests ────────────────────────────────────────

    [Fact]
    public void OutputInfo_FromConfiguration()
    {
        var info = AudioOutputLatencyInfo.FromConfiguration(100, 2, 48000, 2);
        Assert.Equal(100, info.DesiredLatencyMs);
        Assert.Equal(2, info.NumberOfBuffers);
        Assert.Equal(50.0, info.EstimatedPerBufferMs);
        Assert.Equal(100.0, info.EstimatedOutputBufferLatencyMs);
        Assert.Equal(48000, info.SampleRate);
        Assert.Equal(2, info.Channels);
    }

    // ── Null measurement provider ────────────────────────────────────────

    [Fact]
    public void NullProvider_ReturnsNull()
    {
        var provider = new NullAudioLatencyMeasurementProvider();
        Assert.False(provider.IsAvailable);
        Assert.Null(provider.GetMeasuredLatencyMs());
        Assert.False(provider.Start());
        provider.Stop(); // Should not throw
    }

    // ── Safety warnings tests ────────────────────────────────────────────

    [Fact]
    public void LowLatency_HasSafetyWarnings()
    {
        var warnings = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.LowLatency);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Contains("50ms"));
    }

    [Fact]
    public void SafeMode_HasSafetyWarnings()
    {
        var warnings = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.Safe);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Contains("200ms"));
    }

    [Fact]
    public void BalancedMode_NoWarnings()
    {
        var warnings = AudioLatencyConfiguration.GetSafetyWarnings(AudioLatencyMode.Balanced);
        Assert.Empty(warnings);
    }

    [Fact]
    public void AllModes_HaveEstimatedPipelineLatency()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var latency = AudioLatencyConfiguration.GetEstimatedPipelineLatencyMs(mode);
            Assert.True(latency > 0, $"{mode}: pipeline latency should be > 0");
        }
    }

    [Fact]
    public void EstimatedPipeline_SafeMode_IsLargest()
    {
        var safe = AudioLatencyConfiguration.GetEstimatedPipelineLatencyMs(AudioLatencyMode.Safe);
        var balanced = AudioLatencyConfiguration.GetEstimatedPipelineLatencyMs(AudioLatencyMode.Balanced);
        var low = AudioLatencyConfiguration.GetEstimatedPipelineLatencyMs(AudioLatencyMode.LowLatency);

        Assert.True(safe > balanced);
        Assert.True(balanced > low);
    }

    [Fact]
    public void EstimatedPipeline_IncludesCaptureBuffer()
    {
        foreach (var mode in Enum.GetValues<AudioLatencyMode>())
        {
            var config = AudioLatencyConfiguration.Resolve(mode);
            var pipeline = AudioLatencyConfiguration.GetEstimatedPipelineLatencyMs(mode);
            Assert.Equal(AudioLatencyConfiguration.CaptureBufferMs + config.DesiredLatencyMs, pipeline);
        }
    }
}
