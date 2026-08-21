namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Represents an immutable snapshot of the audio output configuration.
/// Describes NAudio WaveOutEvent buffer settings. These are CONFIGURED values,
/// not measured hardware latencies.
/// </summary>
public sealed class AudioOutputLatencyInfo
{
    /// <summary>
    /// WaveOutEvent DesiredLatency as configured. This is the total requested
    /// output buffer latency, NOT the per-buffer size.
    /// </summary>
    public int DesiredLatencyMs { get; init; }

    /// <summary>
    /// Number of output buffers (WaveOutEvent NumberOfBuffers).
    /// </summary>
    public int NumberOfBuffers { get; init; }

    /// <summary>
    /// Estimated per-buffer duration: DesiredLatencyMs / NumberOfBuffers.
    /// </summary>
    public double EstimatedPerBufferMs { get; init; }

    /// <summary>
    /// Estimated output buffer latency. Equal to DesiredLatencyMs.
    /// This is a configured value, not a measured hardware value.
    /// </summary>
    public double EstimatedOutputBufferLatencyMs { get; init; }

    /// <summary>
    /// The sample rate used for buffer calculations.
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Number of channels.
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Creates an info snapshot from WaveOutEvent configuration parameters.
    /// </summary>
    public static AudioOutputLatencyInfo FromConfiguration(
        int desiredLatencyMs, int numberOfBuffers, int sampleRate, int channels)
    {
        return new AudioOutputLatencyInfo
        {
            DesiredLatencyMs = desiredLatencyMs,
            NumberOfBuffers = numberOfBuffers,
            EstimatedPerBufferMs = numberOfBuffers > 0 ? (double)desiredLatencyMs / numberOfBuffers : 0,
            EstimatedOutputBufferLatencyMs = desiredLatencyMs,
            SampleRate = sampleRate,
            Channels = channels
        };
    }
}
