namespace SoundFXStudio.Models;

/// <summary>
/// Identifies a game application for audio capture.
/// Persisted by name/executable, NOT by process ID (PIDs change every launch).
/// </summary>
public sealed class GameProcessInfo
{
    public string ProcessName { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;

    public override string ToString() => string.IsNullOrEmpty(DisplayName) ? ProcessName : DisplayName;
}
