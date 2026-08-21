using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Persists imported HRTF profiles as JSON under the application data folder.
/// Preset profiles must never be stored through this interface.
/// </summary>
public interface IHrtfProfileStore
{
    /// <summary>
    /// Loads all previously imported profiles.
    /// Returns an empty list if no profiles exist or storage is corrupt.
    /// </summary>
    IReadOnlyList<HrtfProfile> LoadAll();

    /// <summary>
    /// Saves an imported profile. Deep-clones before writing.
    /// The profile must have a valid Id and must not be a preset.
    /// </summary>
    void Save(HrtfProfile profile);

    /// <summary>
    /// Deletes a profile by its Id. No-op if not found.
    /// </summary>
    void Delete(string profileId);
}
