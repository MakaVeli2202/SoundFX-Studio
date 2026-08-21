namespace SoundFXStudio.Models;

/// <summary>
/// Audio latency profiles for the gaming output pipeline.
///
/// Values are derived from the audit of current NAudio WaveOutEvent defaults:
/// - Default DesiredLatency = 100ms
/// - Default NumberOfBuffers = 2
///
/// Safe:
/// - 200ms DesiredLatency, 3 buffers
/// - Rationale: Maximum stability. Allows ample time for Voicemeeter routing
///   and OS mixer processing. Best for slower machines or when audio glitches
///   are observed at lower settings.
///
/// Balanced:
/// - 100ms DesiredLatency, 2 buffers
/// - Rationale: NAudio default. Matches current behavior exactly.
///   Suitable for most users and hardware configurations.
///
/// LowLatency:
/// - 50ms DesiredLatency, 2 buffers
/// - Rationale: Reduced output buffering. Still conservative (minimum 2 buffers).
///   Provides perceptibly tighter response. May cause glitches on very slow
///   machines or with high DSP load.
///
/// Do NOT create an UltraLowLatency mode. 50ms is the safe minimum.
/// </summary>
public enum AudioLatencyMode
{
    /// <summary>
    /// 200ms output latency, 3 buffers. Maximum stability.
    /// </summary>
    Safe,

    /// <summary>
    /// 100ms output latency, 2 buffers. NAudio default, current behavior.
    /// </summary>
    Balanced,

    /// <summary>
    /// 50ms output latency, 2 buffers. Reduced latency, still conservative.
    /// </summary>
    LowLatency
}
