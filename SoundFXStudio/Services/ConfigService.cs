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
            return Normalize(config ?? CreateDefaultConfig());
        }
        catch
        {
            if (File.Exists(_backupPath))
            {
                try
                {
                    var backupJson = File.ReadAllText(_backupPath);
                    var backup = JsonSerializer.Deserialize<AppConfig>(backupJson, SerializerOptions);
                    return Normalize(backup ?? CreateDefaultConfig());
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

    private static AppConfig Normalize(AppConfig config)
    {
        config.Sounds ??= new();
        config.Profiles ??= new();
        config.Categories ??= new();
        config.Settings ??= new AppSettings();

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
}