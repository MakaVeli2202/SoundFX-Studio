using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Resolves an AudioLatencyMode to concrete output configuration values.
/// Centralizes all latency magic numbers in one place.
/// </summary>
public sealed class AudioLatencyConfiguration
{
    /// <summary>WASAPI capture buffer duration in ms. Matches ProcessLoopbackCapture.DefaultBufferMilliseconds.</summary>
    public const double CaptureBufferMs = 20.0;

    /// <summary>Default latency mode.</summary>
    public const AudioLatencyMode DefaultMode = AudioLatencyMode.Balanced;

    /// <summary>
    /// Resolves the given mode to a complete configuration.
    /// Validates all inputs and throws on invalid combinations.
    /// </summary>
    public static AudioLatencyConfigurationData Resolve(AudioLatencyMode mode)
    {
        return mode switch
        {
            AudioLatencyMode.Safe => new AudioLatencyConfigurationData
            {
                Mode = AudioLatencyMode.Safe,
                DesiredLatencyMs = 200,
                NumberOfBuffers = 3
            },
            AudioLatencyMode.Balanced => new AudioLatencyConfigurationData
            {
                Mode = AudioLatencyMode.Balanced,
                DesiredLatencyMs = 100,
                NumberOfBuffers = 2
            },
            AudioLatencyMode.LowLatency => new AudioLatencyConfigurationData
            {
                Mode = AudioLatencyMode.LowLatency,
                DesiredLatencyMs = 50,
                NumberOfBuffers = 2
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown latency mode.")
        };
    }

    /// <summary>
    /// Validates a configuration. Returns true if valid, false otherwise.
    /// </summary>
    public static bool Validate(AudioLatencyConfigurationData config)
    {
        if (config.DesiredLatencyMs <= 0) return false;
        if (config.NumberOfBuffers < 2) return false;
        return true;
    }

    /// <summary>
    /// Checks for potentially problematic latency mode configurations.
    /// Returns warnings about modes that may cause audible issues.
    /// Empty array means no warnings.
    /// </summary>
    public static string[] GetSafetyWarnings(AudioLatencyMode mode)
    {
        var warnings = new List<string>();

        if (mode == AudioLatencyMode.LowLatency)
        {
            warnings.Add("LowLatency mode (50ms output buffer) may cause audio glitches on slower systems.");
            warnings.Add("Requires capture restart to apply. Audio will briefly interrupt during mode change.");
        }
        else if (mode == AudioLatencyMode.Safe)
        {
            warnings.Add("Safe mode (200ms output buffer) adds perceptible latency to real-time audio.");
        }

        return warnings.ToArray();
    }

    /// <summary>
    /// Gets estimated application pipeline latency in ms for the given mode.
    /// This is the sum of capture buffer + output buffer — it does NOT include
    /// external components (Voicemeeter, OS mixer, device driver).
    /// </summary>
    public static double GetEstimatedPipelineLatencyMs(AudioLatencyMode mode)
    {
        var config = Resolve(mode);
        return CaptureBufferMs + config.DesiredLatencyMs;
    }
}

/// <summary>
/// Concrete latency configuration values resolved from a mode.
/// </summary>
public sealed class AudioLatencyConfigurationData
{
    public AudioLatencyMode Mode { get; init; }
    public int DesiredLatencyMs { get; init; }
    public int NumberOfBuffers { get; init; }

    /// <summary>
    /// Whether changing to this mode requires restarting audio capture.
    /// All mode changes require restart since WaveOutEvent cannot be
    /// reconfigured while playing.
    /// </summary>
    public bool RequiresRestart => true;
}
