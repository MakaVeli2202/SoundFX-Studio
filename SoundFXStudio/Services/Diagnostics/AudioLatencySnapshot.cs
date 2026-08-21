namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Clear latency model separating configured, measured, estimated, and unknown values.
///
/// Latency categories:
/// 1. Configured — directly from API configuration (capture buffer, output buffer)
/// 2. Measured — from real Stopwatch timing (DSP processing)
/// 3. Estimated — sum of configured values (application pipeline estimate)
/// 4. Round-trip — measured via software loopback (application pipeline only)
/// 5. External — unknown (Voicemeeter, OS mixer, device, physical)
/// 6. Physical — unknown unless measured by hardware/mic
///
/// EstimatedApplicationLatency calculation:
///   = ConfiguredCaptureLatencyMs + MeasuredDspContributionMs + ConfiguredOutputLatencyMs
///
/// MeasuredDspContributionMs = DspP99Us / 1000 (converted to ms)
/// This represents the DSP processing contribution within one audio block.
///
/// IMPORTANT: EstimatedApplicationLatency does NOT include Voicemeeter buffering,
/// Windows OS mixer latency, audio device driver buffering, or physical output latency.
/// These are external and unknown.
/// </summary>
public sealed class AudioLatencySnapshot
{
    // ── Configured values ────────────────────────────────────────────────

    /// <summary>WASAPI capture buffer configured duration in ms.</summary>
    public double ConfiguredCaptureLatencyMs { get; init; }

    /// <summary>WaveOutEvent output buffer configured duration in ms.</summary>
    public double ConfiguredOutputLatencyMs { get; init; }

    /// <summary>Number of output buffers.</summary>
    public int OutputBufferCount { get; init; }

    // ── Measured DSP values ──────────────────────────────────────────────

    /// <summary>Measured DSP processing P95 in microseconds.</summary>
    public double MeasuredDspP95Us { get; init; }

    /// <summary>Measured DSP processing P99 in microseconds.</summary>
    public double MeasuredDspP99Us { get; init; }

    /// <summary>Measured DSP processing max in microseconds.</summary>
    public double MeasuredDspMaxUs { get; init; }

    /// <summary>Measured DSP processing average in microseconds.</summary>
    public double MeasuredDspAverageUs { get; init; }

    /// <summary>
    /// DSP processing contribution to latency in ms (P99).
    /// This is the maximum time DSP takes within one block — not an additional delay
    /// on top of the block duration, but rather time consumed within the block.
    /// </summary>
    public double MeasuredDspContributionMs => MeasuredDspP99Us / 1000.0;

    // ── Estimated application pipeline ───────────────────────────────────

    /// <summary>
    /// Estimated application-level pipeline latency in ms.
    /// = ConfiguredCaptureLatencyMs + MeasuredDspContributionMs + ConfiguredOutputLatencyMs
    ///
    /// This is an ESTIMATE based on configured values and measured DSP time.
    /// It does NOT include Voicemeeter, OS mixer, driver, or physical device latency.
    /// </summary>
    public double EstimatedApplicationLatencyMs { get; init; }

    // ── Round-trip measurement (nullable) ────────────────────────────────

    /// <summary>
    /// Measured round-trip latency via software loopback in ms.
    /// Measures: application output → Windows audio engine → loopback capture → correlation.
    /// Does NOT measure: Voicemeeter routing or physical output.
    /// Null if no loopback measurement has been performed.
    /// </summary>
    public double? MeasuredRoundTripLatencyMs { get; init; }

    // ── Physical latency (nullable) ──────────────────────────────────────

    /// <summary>
    /// Measured end-to-end physical latency in ms.
    /// MUST remain null unless measured via loopback hardware or acoustic mic.
    /// Do NOT fabricate this value.
    /// </summary>
    public double? MeasuredEndToEndLatencyMs { get; init; }

    // ── External latency documentation ───────────────────────────────────

    /// <summary>Whether any external latency component is known.</summary>
    public bool ExternalLatencyKnown => false;

    /// <summary>
    /// Description of known external latency components.
    /// Currently: "Voicemeeter buffering, Windows OS mixer, and device driver latency are unknown."
    /// </summary>
    public string ExternalLatencyDescription =>
        "Voicemeeter buffering, Windows OS mixer, and device driver latency are not measurable from the application.";

    // ── WaveFormat ───────────────────────────────────────────────────────

    /// <summary>Audio sample rate in Hz.</summary>
    public int SampleRate { get; init; }

    /// <summary>Frames per Process() call.</summary>
    public int FramesPerBlock { get; init; }

    /// <summary>Duration of one audio block in milliseconds.</summary>
    public double BlockDurationMs { get; init; }

    // ── Budget and health ────────────────────────────────────────────────

    /// <summary>DSP budget usage: (P99_us / blockDuration_us) × 100.</summary>
    public double DspBudgetPercent { get; init; }

    /// <summary>Number of blocks where DSP exceeded the audio deadline.</summary>
    public long OverBudgetBlockCount { get; init; }

    /// <summary>Diagnostic health status.</summary>
    public AudioHealthStatus HealthStatus { get; init; }

    // ── Underrun tracking ────────────────────────────────────────────────

    /// <summary>Capture starvation events (capture returned 0 frames when requested).</summary>
    public long CaptureStarvationCount { get; init; }

    /// <summary>Output underrun events (if detectable from NAudio).</summary>
    public long OutputUnderrunCount { get; init; }

    // ── Measurement metadata ─────────────────────────────────────────────

    /// <summary>Wall-clock time over which statistics were collected.</summary>
    public TimeSpan MeasurementDuration { get; init; }

    /// <summary>Total number of audio blocks processed.</summary>
    public long MeasurementsCollected { get; init; }

    /// <summary>Timestamp of this snapshot (UTC).</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Production audio health state with hysteresis.
/// </summary>
public enum AudioHealthStatus
{
    /// <summary>Pipeline healthy. DSP P99 well below deadline.</summary>
    Healthy,

    /// <summary>DSP budget high or approaching deadline. No actual deadline misses.</summary>
    Warning,

    /// <summary>Processing deadline misses observed, or device failure detected.</summary>
    Critical,

    /// <summary>Audio pipeline not active or device unavailable.</summary>
    Unavailable
}
