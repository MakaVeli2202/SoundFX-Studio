namespace SoundFXStudio.Services.ArtTune;

/// <summary>
/// Outcome of a Tune Library sync against the ArtTuneDB GitHub repository.
/// </summary>
public sealed class ArtTuneUpdateResult
{
    /// <summary>
    /// True when new profiles were actually downloaded and applied.
    /// </summary>
    public bool Updated { get; init; }

    /// <summary>
    /// True when the cached library already matches the remote version (nothing to do).
    /// </summary>
    public bool IsCurrent { get; init; }

    /// <summary>
    /// Remote library version string (e.g. "2026.07.1-Overhaul").
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Number of headphone EQ profiles produced.
    /// </summary>
    public int HeadphoneProfiles { get; init; }

    /// <summary>
    /// Number of game tune profiles produced.
    /// </summary>
    public int GamingProfiles { get; init; }

    /// <summary>
    /// Number of HRTF profiles produced (0 or 1).
    /// </summary>
    public int HrtfProfiles { get; init; }

    /// <summary>
    /// Human readable summary message for status bars.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Set when the whole sync failed; otherwise null.
    /// </summary>
    public string? Error { get; init; }
}