using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Persists imported HRTF profiles as JSON under the application data folder.
/// Profiles are serialized with all HRIR data so they survive independently
/// of the original SOFA files.
/// </summary>
public sealed class HrtfProfileStore : IHrtfProfileStore
{
    private readonly string _storagePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HrtfProfileStore(string? appFolder = null)
    {
        var folder = appFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundFXStudio");
        _storagePath = Path.Combine(folder, "imported_hrtf_profiles.json");
    }

    public IReadOnlyList<HrtfProfile> LoadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_storagePath))
                return Array.Empty<HrtfProfile>();

            try
            {
                var json = File.ReadAllText(_storagePath);
                var profiles = JsonSerializer.Deserialize<List<HrtfProfile>>(json, SerializerOptions);
                return profiles?.Where(p => p is not null).Select(p => p!).ToList()
                       ?? (IReadOnlyList<HrtfProfile>)Array.Empty<HrtfProfile>();
            }
            catch
            {
                return Array.Empty<HrtfProfile>();
            }
        }
    }

    public void Save(HrtfProfile profile)
    {
        if (profile is null) return;

        lock (_lock)
        {
            var existing = LoadAllInternal();
            var existingIndex = existing.FindIndex(p =>
                string.Equals(p.Id, profile.Id, StringComparison.Ordinal));

            var clone = profile.Clone();

            if (existingIndex >= 0)
                existing[existingIndex] = clone;
            else
                existing.Add(clone);

            WriteAll(existing);
        }
    }

    public void Delete(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return;

        lock (_lock)
        {
            var existing = LoadAllInternal();
            var removed = existing.RemoveAll(p =>
                string.Equals(p.Id, profileId, StringComparison.Ordinal));

            if (removed > 0)
                WriteAll(existing);
        }
    }

    private List<HrtfProfile> LoadAllInternal()
    {
        if (!File.Exists(_storagePath))
            return new List<HrtfProfile>();

        try
        {
            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<List<HrtfProfile>>(json, SerializerOptions)
                   ?? new List<HrtfProfile>();
        }
        catch
        {
            return new List<HrtfProfile>();
        }
    }

    private void WriteAll(List<HrtfProfile> profiles)
    {
        try
        {
            var folder = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            // Write to temp file first, then atomically replace
            var tempPath = _storagePath + ".tmp";
            var json = JsonSerializer.Serialize(profiles, SerializerOptions);
            File.WriteAllText(tempPath, json);

            // Atomic replace
            if (File.Exists(_storagePath))
                File.Delete(_storagePath);
            File.Move(tempPath, _storagePath);
        }
        catch
        {
            // Best effort — do not crash the app
            try { File.Delete(_storagePath + ".tmp"); } catch { }
        }
    }
}
