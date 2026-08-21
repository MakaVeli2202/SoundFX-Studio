namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Interface for latency measurement providers.
///
/// Supports both synchronous (snapshot) and asynchronous (on-demand) measurement.
///
/// Possible implementations:
/// - Loopback hardware measurement (external audio interface with loopback cable)
/// - Acoustic microphone measurement (mic capturing speaker output)
/// - Software loopback measurement (test tone → capture → correlation)
///
/// All measurements must clearly state what they measure:
/// - "Application pipeline latency" = output scheduling + OS mixer up to loopback point
/// - "Physical latency" = includes device driver + speaker + acoustic propagation
/// </summary>
public interface IAudioLatencyMeasurementProvider
{
    /// <summary>Human-readable provider name.</summary>
    string ProviderName { get; }

    /// <summary>Whether the provider can currently perform measurements.</summary>
    bool IsAvailable { get; }

    /// <summary>Latest measured latency in ms, or null if not yet measured.</summary>
    double? GetMeasuredLatencyMs();

    /// <summary>Starts the measurement provider. Returns false if it cannot start.</summary>
    bool Start();

    /// <summary>Stops the measurement provider. Safe to call multiple times.</summary>
    void Stop();

    /// <summary>
    /// Performs an on-demand round-trip measurement.
    /// Returns the measured latency in ms, or null if measurement failed.
    /// This is a blocking call that may take several seconds.
    /// </summary>
    Task<double?> MeasureRoundTripLatencyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-readable description of what this provider measures.
    /// Must be honest about limitations.
    /// </summary>
    string MeasurementDescription { get; }
}

/// <summary>
/// Null/no-op measurement provider. Returns null for all measurements.
/// </summary>
public sealed class NullAudioLatencyMeasurementProvider : IAudioLatencyMeasurementProvider
{
    public string ProviderName => "None";
    public bool IsAvailable => false;
    public double? GetMeasuredLatencyMs() => null;
    public bool Start() => false;
    public void Stop() { }
    public Task<double?> MeasureRoundTripLatencyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<double?>(null);
    public string MeasurementDescription => "No measurement hardware available.";
}
