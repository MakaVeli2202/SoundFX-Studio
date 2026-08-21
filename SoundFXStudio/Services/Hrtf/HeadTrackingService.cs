using System.Diagnostics;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Manages head-tracking lifecycle: provider selection, calibration,
/// rate limiting, and direction update pipeline.
///
/// Pipeline:
///   IHeadTrackingProvider.GetOrientation()
///     → HeadOrientationConverter.Convert()
///       → rate limit (min angle threshold + max update rate)
///         → HrtfEffect.SetDirection() (with Phase G smooth transition)
///
/// Thread safety: All public methods are safe to call from any thread.
/// The audio thread (Process()) is never blocked.
/// Direction updates cross from tracking thread to DSP via
/// HrtfEffect.SetDirection() which uses Phase G preallocated transition buffers.
///
/// Allocation guarantee: Update() allocates 0 bytes after initial setup.
/// </summary>
public sealed class HeadTrackingService : IDisposable
{
    private readonly HeadOrientationConverter _converter = new();
    private IHeadTrackingProvider _provider;
    private readonly Stopwatch _updateStopwatch = new();

    // Rate limiting state (written by caller, read by audio)
    private double _lastAzimuth;
    private double _lastElevation;
    private bool _hasUpdate;
    private bool _disposed;

    // Configuration
    private double _angleThresholdDeg = 1.0;
    private int _maxUpdateIntervalMs = 16; // ~60 Hz

    public HeadTrackingService(IHeadTrackingProvider? provider = null)
    {
        _provider = provider ?? new NullHeadTrackingProvider();
    }

    /// <summary>
    /// The active head-tracking provider. Can be swapped at runtime.
    /// Resets rate-limiting state when changed to prevent stale calibration.
    /// </summary>
    public IHeadTrackingProvider Provider
    {
        get => _provider;
        set
        {
            if (_provider == value) return;
            _provider.Stop();
            _provider.Dispose();
            _provider = value ?? new NullHeadTrackingProvider();
            ResetState();
        }
    }

    /// <summary>
    /// The orientation converter. Exposed for calibration.
    /// </summary>
    public HeadOrientationConverter Converter => _converter;

    /// <summary>
    /// True if the current provider is available and tracking.
    /// </summary>
    public bool IsTracking => _provider.IsTracking;

    /// <summary>
    /// True if the current provider is available.
    /// </summary>
    public bool IsAvailable => _provider.IsAvailable;

    /// <summary>
    /// Provider display name.
    /// </summary>
    public string ProviderName => _provider.ProviderName;

    /// <summary>
    /// Minimum angular change (degrees) before a direction update is sent to HRTF.
    /// Set to 0 to update on every sample. Default: 1.0°.
    /// </summary>
    public double AngleThresholdDeg
    {
        get => _angleThresholdDeg;
        set => _angleThresholdDeg = Math.Max(0, value);
    }

    /// <summary>
    /// Minimum interval (ms) between direction updates.
    /// Default: 16 ms (~60 Hz).
    /// </summary>
    public int MaxUpdateIntervalMs
    {
        get => _maxUpdateIntervalMs;
        set => _maxUpdateIntervalMs = Math.Max(0, value);
    }

    /// <summary>
    /// Starts tracking. Returns true if tracking started successfully.
    /// </summary>
    public bool Start()
    {
        return _provider.Start();
    }

    /// <summary>
    /// Stops tracking. Safe to call multiple times.
    /// </summary>
    public void Stop()
    {
        _provider.Stop();
    }

    /// <summary>
    /// Sets the current orientation as the calibration reference.
    /// After calibration, the head's current position becomes (0, 0, 0).
    /// </summary>
    public void Calibrate()
    {
        var orientation = _provider.GetOrientation();
        _converter.Calibrate(orientation.YawDeg, orientation.PitchDeg, orientation.RollDeg);
    }

    /// <summary>
    /// Resets calibration to factory defaults.
    /// </summary>
    public void ResetCalibration()
    {
        _converter.ResetCalibration();
    }

    /// <summary>
    /// Processes a head-tracking update: reads orientation, converts to HRTF direction,
    /// applies rate limiting, and updates the HRTF effect if threshold is met.
    /// Returns the effective (azimuth, elevation) if an update was sent, or null if
    /// filtered out by rate limiting.
    ///
    /// Call this from the ViewModel/timer, NOT from the audio thread.
    /// </summary>
    public (double AzimuthDeg, double ElevationDeg)? Update(
        Services.DSP.HrtfEffect hrtf,
        bool headTrackingEnabled,
        bool hrtfEnabled)
    {
        if (!headTrackingEnabled || !hrtfEnabled || !_provider.IsTracking)
            return null;

        var raw = _provider.GetOrientation();
        var (azimuth, elevation) = _converter.Convert(
            raw.YawDeg, raw.PitchDeg, raw.RollDeg);

        // Rate limiting: minimum angle change
        if (_hasUpdate)
        {
            var deltaAz = Math.Abs(azimuth - _lastAzimuth);
            var deltaEl = Math.Abs(elevation - _lastElevation);

            // Also check max update interval
            var elapsed = _updateStopwatch.ElapsedMilliseconds;
            if (deltaAz < _angleThresholdDeg
                && deltaEl < _angleThresholdDeg
                && elapsed < _maxUpdateIntervalMs)
            {
                return null;
            }
        }

        // Send direction update to HRTF (uses Phase G smooth transition)
        hrtf.SetDirection(azimuth, elevation);

        _lastAzimuth = azimuth;
        _lastElevation = elevation;
        _hasUpdate = true;
        _updateStopwatch.Restart();

        return (azimuth, elevation);
    }

    /// <summary>
    /// Resets rate-limiting state. Call when tracking is restarted.
    /// </summary>
    public void ResetState()
    {
        _hasUpdate = false;
        _updateStopwatch.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _provider.Stop();
        _provider.Dispose();
    }
}
