using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Structured result from a SOFA file load operation.
/// Successful loads contain a valid HrtfProfile.
/// Failed loads contain error information without throwing.
/// </summary>
public sealed class SofaHrtfLoadResult
{
    public bool Success { get; init; }
    public HrtfProfile? Profile { get; init; }
    public string? ErrorMessage { get; init; }
    public int DirectionsLoaded { get; init; }
    public int IrLength { get; init; }
    public int SampleRate { get; init; }

    public static SofaHrtfLoadResult Ok(HrtfProfile profile) => new()
    {
        Success = true,
        Profile = profile,
        DirectionsLoaded = profile.Entries.Length,
        IrLength = profile.IrLength,
        SampleRate = profile.SampleRate
    };

    public static SofaHrtfLoadResult Fail(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}
