namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Abstraction for head-tracking hardware/software providers.
/// Implementations expose real-time head orientation (yaw, pitch, roll)
/// for driving HRTF direction updates.
///
/// The application compiles and runs without any provider hardware.
/// Use NullHeadTrackingProvider when no hardware is available.
/// </summary>
public interface IHeadTrackingProvider : IDisposable
{
    /// <summary>
    /// True if head-tracking hardware/software is detected and usable.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// True if tracking is currently active (Start() has been called successfully).
    /// </summary>
    bool IsTracking { get; }

    /// <summary>
    /// Human-readable provider name (e.g., "OpenTrack", "Webcam Face Tracking").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Starts head tracking. Safe to call multiple times (idempotent).
    /// Returns false if tracking cannot start (no hardware, permission denied, etc.).
    /// </summary>
    bool Start();

    /// <summary>
    /// Stops head tracking. Safe to call multiple times (idempotent).
    /// </summary>
    void Stop();

    /// <summary>
    /// Returns the current head orientation in degrees.
    /// Values are raw (un-calibrated) and provider-specific.
    /// Callers must apply calibration offset.
    /// </summary>
    HeadOrientation GetOrientation();
}

/// <summary>
/// Head orientation in degrees. Zero = forward-facing.
/// Positive yaw = head turned left. Positive pitch = head tilted up.
/// Roll = head tilt to shoulder (available for future use, not used by HRTF).
/// </summary>
public readonly record struct HeadOrientation(double YawDeg, double PitchDeg, double RollDeg);
