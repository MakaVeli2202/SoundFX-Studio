using SoundFXStudio.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundFXStudio.Services;

public class ConfigService
{
    private readonly string _configPath;
    private readonly string _backupPath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigService()
    {
        var appFolder = GetAppFolder();
        _configPath = Path.Combine(appFolder, "config.json");
        _backupPath = Path.Combine(appFolder, "config.backup.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return CreateDefaultConfig();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions);
            var normalized = Normalize(config ?? CreateDefaultConfig(), out var migrated);
            if (migrated)
            {
                Save(normalized);
            }

            return normalized;
        }
        catch
        {
            if (File.Exists(_backupPath))
            {
                try
                {
                    var backupJson = File.ReadAllText(_backupPath);
                    var backup = JsonSerializer.Deserialize<AppConfig>(backupJson, SerializerOptions);
                    var normalized = Normalize(backup ?? CreateDefaultConfig(), out var migrated);
                    if (migrated)
                    {
                        Save(normalized);
                    }

                    return normalized;
                }
                catch
                {
                    // fall through to fresh config
                }
            }

            return CreateDefaultConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(GetAppFolder());

        var json = JsonSerializer.Serialize(config, SerializerOptions);

        if (File.Exists(_configPath))
        {
            File.Copy(_configPath, _backupPath, true);
        }

        File.WriteAllText(_configPath, json);
    }

    public string GetAppFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundFXStudio");

        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "Sounds"));
        Directory.CreateDirectory(Path.Combine(folder, "Images"));

        return folder;
    }

    public string GetSoundsFolder() => Path.Combine(GetAppFolder(), "Sounds");

    public string GetImagesFolder() => Path.Combine(GetAppFolder(), "Images");

    public bool HasSavedConfig() => File.Exists(_configPath);

    private static AppConfig CreateDefaultConfig()
    {
        var config = new AppConfig();
        config.Categories.Add(new Category { Name = "Meme", IsBuiltIn = true });
        config.Categories.Add(new Category { Name = "Gaming", IsBuiltIn = true });
        config.Categories.Add(new Category { Name = "Movies", IsBuiltIn = true });
        config.Categories.Add(new Category { Name = "Music", IsBuiltIn = true });
        config.Categories.Add(new Category { Name = "Anime", IsBuiltIn = true });
        config.Categories.Add(new Category { Name = "Custom", IsBuiltIn = true });

        config.Profiles.Add(new Profile { Name = "Gaming", Description = "Fast reactions and hype moments", AccentColor = "#00D4FF", IsDefault = true });
        config.Profiles.Add(new Profile { Name = "Discord", Description = "General voice chat", AccentColor = "#8A6CFF" });
        config.Profiles.Add(new Profile { Name = "Streaming", Description = "On-air soundboard", AccentColor = "#FF4D8D" });
        config.Profiles.Add(new Profile { Name = "Meetings", Description = "Clean and quiet", AccentColor = "#22C55E" });
        config.Settings.KeyboardLayout = KeyboardLayoutMode.English;
        config.ActiveProfileId = config.Profiles.First().Id;

        return config;
    }

    private static AppConfig Normalize(AppConfig config, out bool migrated)
    {
        migrated = false;
        config.Sounds ??= new();
        config.Actions ??= new();
        config.Combos ??= new();
        config.KeyChords ??= new();
        config.Playlists ??= new();
        config.Macros ??= new();
        config.RoutingPresets ??= new();
        config.Profiles ??= new();
        config.Categories ??= new();
        config.Settings ??= new AppSettings();
        config.Settings.KeyboardCalibration ??= new KeyboardCalibrationSettings();
        config.Settings.KeyboardCalibration.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>();

        migrated |= MigrateLegacySoundAssignments(config);

        if (config.Profiles.Count == 0)
        {
            var defaults = CreateDefaultConfig();
            config.Profiles = defaults.Profiles;
            config.Categories = defaults.Categories;
            config.ActiveProfileId = defaults.ActiveProfileId;
        }

        if (string.IsNullOrWhiteSpace(config.ActiveProfileId) && config.Profiles.Count > 0)
        {
            config.ActiveProfileId = config.Profiles.First().Id;
        }

        return config;
    }

    private static bool MigrateLegacySoundAssignments(AppConfig config)
    {
        var changed = false;
        var soundActions = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in config.Actions.Where(action => action.Type == ActionType.Sound && !string.IsNullOrWhiteSpace(action.Payload)))
        {
            soundActions[action.Payload] = action;
        }

        foreach (var profile in config.Profiles)
        {
            profile.KeyChords ??= new();
            foreach (var assignment in profile.Assignments)
            {
                if (string.IsNullOrWhiteSpace(assignment.SoundId))
                {
                    continue;
                }

                if (!soundActions.TryGetValue(assignment.SoundId, out var action))
                {
                    var sound = config.Sounds.FirstOrDefault(item => string.Equals(item.Id, assignment.SoundId, StringComparison.OrdinalIgnoreCase));
                    action = new ActionDefinition
                    {
                        Name = sound?.Name ?? assignment.SoundId,
                        Description = sound is null ? "Legacy sound action" : $"Play {sound.Name}",
                        Type = ActionType.Sound,
                        IconPath = sound?.ImagePath ?? string.Empty,
                        Category = sound?.Category ?? string.Empty,
                        Payload = assignment.SoundId
                    };

                    config.Actions.Add(action);
                    soundActions[assignment.SoundId] = action;
                    changed = true;
                }

                if (assignment.ActionId != action.Id)
                {
                    assignment.ActionId = action.Id;
                    changed = true;
                }
            }
        }

        foreach (var assignment in config.Profiles.SelectMany(profile => profile.Assignments))
        {
            if (assignment.ActionId is null || !string.IsNullOrWhiteSpace(assignment.SoundId))
            {
                continue;
            }

            var action = config.Actions.FirstOrDefault(item => item.Id == assignment.ActionId.Value);
            if (action is not null && action.Type == ActionType.Sound && !string.IsNullOrWhiteSpace(action.Payload))
            {
                assignment.SoundId = action.Payload;
                changed = true;
            }
        }

        return changed;
    }
}