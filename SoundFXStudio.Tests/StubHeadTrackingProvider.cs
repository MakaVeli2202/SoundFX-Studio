namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Test head-tracking provider with configurable orientation.
/// Allows tests to simulate head movements without hardware.
/// </summary>
public sealed class StubHeadTrackingProvider : IHeadTrackingProvider
{
    private bool _isTracking;
    private HeadOrientation _orientation;

    public bool IsAvailable { get; set; } = true;
    public bool IsTracking => _isTracking;
    public string ProviderName => "Stub";

    public HeadOrientation Orientation
    {
        get => _orientation;
        set => _orientation = value;
    }

    public bool Start()
    {
        _isTracking = true;
        return true;
    }

    public void Stop()
    {
        _isTracking = false;
    }

    public HeadOrientation GetOrientation() => _orientation;

    public void Dispose() { }
}
