namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Null head-tracking provider. Used when no head-tracking hardware is available.
/// All operations are safe and return sensible defaults.
/// </summary>
public sealed class NullHeadTrackingProvider : IHeadTrackingProvider
{
    public bool IsAvailable => false;
    public bool IsTracking => false;
    public string ProviderName => "None";

    public bool Start() => false;

    public void Stop() { }

    public HeadOrientation GetOrientation() => new(0, 0, 0);

    public void Dispose() { }
}
