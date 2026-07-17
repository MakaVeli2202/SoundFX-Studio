using SoundFXStudio.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundFXStudio.Services;

public class ConfigService
{
    private readonly string _configPath;
    private readonly string _backupPath;
    private readonly ILogService? _logService;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigService(ILogService? logService = null)
    {
        _logService = logService;
        var appFolder = GetAppFolder();
        _configPath = Path.Combine(appFolder, "config.json");
        _backupPath = Path.Combine(appFolder, "config.backup.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            var defaultConfig = CreateDefaultConfig();
            ApplyProjectCalibrationIfAvailable(defaultConfig);
            _logService?.Info("Config Loaded");
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions);
            var normalized = Normalize(config ?? CreateDefaultConfig(), out var migrated);
            if (migrated)
            {
                _logService?.Info("Config Migration Executed");
                Save(normalized);
            }

            _logService?.Info("Config Loaded");
            return normalized;
        }
        catch (Exception ex)
        {
            _logService?.Error("Config Load Failed", ex);

            if (File.Exists(_backupPath))
            {
                try
                {
                    var backupJson = File.ReadAllText(_backupPath);
                    var backup = JsonSerializer.Deserialize<AppConfig>(backupJson, SerializerOptions);
                    var normalized = Normalize(backup ?? CreateDefaultConfig(), out var migrated);
                    if (migrated)
                    {
                        _logService?.Info("Config Migration Executed");
                        Save(normalized);
                    }

                    _logService?.Warning("Config Restored From Backup");
                    _logService?.Info("Config Loaded");
                    return normalized;
                }
                catch (Exception backupEx)
                {
                    _logService?.Error("Config Load Failed", backupEx);
                }
            }

            var defaultConfig = CreateDefaultConfig();
            ApplyProjectCalibrationIfAvailable(defaultConfig);
            _logService?.Warning("Config Loaded");
            return defaultConfig;
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(GetAppFolder());

            var json = JsonSerializer.Serialize(config, SerializerOptions);

            if (File.Exists(_configPath))
            {
                File.Copy(_configPath, _backupPath, true);
                _logService?.Info("Config Backup Created");
            }

            File.WriteAllText(_configPath, json);
            _logService?.Info("Config Saved");
        }
        catch (Exception ex)
        {
            _logService?.Error("Config Save Failed", ex);
            throw;
        }
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
        config.Settings.KeyboardLayout = KeyboardLayoutMode.Automatic;
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
        migrated |= MigrateLegacyKeyboardCalibration(config.Settings.KeyboardCalibration, config.Settings.KeyboardLayout);
        if (config.Settings.KeyboardCalibration.KeyboardWindowScale <= 0)
        {
            config.Settings.KeyboardCalibration.KeyboardWindowScale = 0.85;
            migrated = true;
        }

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

    private static void ApplyProjectCalibrationIfAvailable(AppConfig config)
    {
        var calibrationPath = GetProjectCalibrationFilePath();
        if (!File.Exists(calibrationPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(calibrationPath);
            var calibration = JsonSerializer.Deserialize<KeyboardCalibrationSettings>(json, SerializerOptions);
            if (calibration is not null)
            {
                calibration.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>();
                MigrateLegacyKeyboardCalibration(calibration, config.Settings.KeyboardLayout);
                if (calibration.KeyboardWindowScale <= 0)
                {
                    calibration.KeyboardWindowScale = 0.85;
                }
                config.Settings.KeyboardCalibration = calibration;
            }
        }
        catch
        {
            // Ignore project calibration load failures and keep the persisted config.
        }
    }

    private static string GetProjectCalibrationFilePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "SoundFXStudio.slnx");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(current.FullName, "SoundFXStudio", "keyboard-calibration.json");
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "keyboard-calibration.json");
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

    private static bool MigrateLegacyKeyboardCalibration(KeyboardCalibrationSettings calibration, KeyboardLayoutMode layoutMode)
    {
        calibration.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>(StringComparer.OrdinalIgnoreCase);

        var hasLegacyClusterOffsets = HasAnyNonZero(
            calibration.EscOffsetX, calibration.EscOffsetY,
            calibration.F1ToF4OffsetX, calibration.F1ToF4OffsetY,
            calibration.F5ToF8OffsetX, calibration.F5ToF8OffsetY,
            calibration.F9ToF12OffsetX, calibration.F9ToF12OffsetY,
            calibration.PrintScrollPauseOffsetX, calibration.PrintScrollPauseOffsetY,
            calibration.MainTypingOffsetX, calibration.MainTypingOffsetY,
            calibration.NavigationOffsetX, calibration.NavigationOffsetY,
            calibration.ArrowOffsetX, calibration.ArrowOffsetY,
            calibration.NumpadOffsetX, calibration.NumpadOffsetY);

        var hasLegacySpecialWidths = HasAnyNonZero(
            calibration.SpacebarWidthAdjustment,
            calibration.BackspaceWidthAdjustment,
            calibration.EnterWidthAdjustment,
            calibration.IsoEnterWidthAdjustment,
            calibration.LeftShiftWidthAdjustment,
            calibration.RightShiftWidthAdjustment,
            calibration.NumpadEnterWidthAdjustment,
            calibration.TabWidthAdjustment,
            calibration.CapsLockWidthAdjustment);

        if (!hasLegacyClusterOffsets && !hasLegacySpecialWidths)
        {
            return false;
        }

        var layoutService = new KeyboardLayoutService();
        var effectiveLayout = layoutMode == KeyboardLayoutMode.Automatic ? KeyboardLayoutMode.EnglishUS : layoutMode;
        var keys = layoutService.CreateKeyboard(effectiveLayout);

        foreach (var key in keys)
        {
            var keyOverride = GetOrCreateOverride(calibration.KeyOverrides, key.Id);

            if (hasLegacyClusterOffsets)
            {
                var (clusterOffsetX, clusterOffsetY) = GetLegacyClusterOffset(calibration, key);
                keyOverride.OffsetX += clusterOffsetX;
                keyOverride.OffsetY += clusterOffsetY;
            }

            if (hasLegacySpecialWidths)
            {
                keyOverride.WidthAdjustment += GetLegacySpecialWidthAdjustment(calibration, key);
            }
        }

        ZeroLegacyCalibration(calibration);
        return true;
    }

    private static KeyCalibrationOverrideSettings GetOrCreateOverride(
        Dictionary<string, KeyCalibrationOverrideSettings> keyOverrides,
        string keyId)
    {
        if (!keyOverrides.TryGetValue(keyId, out var existing))
        {
            existing = new KeyCalibrationOverrideSettings();
            keyOverrides[keyId] = existing;
        }

        return existing;
    }

    private static (double OffsetX, double OffsetY) GetLegacyClusterOffset(KeyboardCalibrationSettings calibration, KeyboardKey key)
    {
        if (string.Equals(key.KeyName, "ESC", StringComparison.OrdinalIgnoreCase))
        {
            return (calibration.EscOffsetX, calibration.EscOffsetY);
        }

        if (IsFunctionKey(key.KeyName, 1, 4))
        {
            return (calibration.F1ToF4OffsetX, calibration.F1ToF4OffsetY);
        }

        if (IsFunctionKey(key.KeyName, 5, 8))
        {
            return (calibration.F5ToF8OffsetX, calibration.F5ToF8OffsetY);
        }

        if (IsFunctionKey(key.KeyName, 9, 12))
        {
            return (calibration.F9ToF12OffsetX, calibration.F9ToF12OffsetY);
        }

        if (IsPrintScrollPauseKey(key.KeyName))
        {
            return (calibration.PrintScrollPauseOffsetX, calibration.PrintScrollPauseOffsetY);
        }

        if (IsNavigationKey(key.KeyName))
        {
            return (calibration.NavigationOffsetX, calibration.NavigationOffsetY);
        }

        if (IsArrowKey(key.KeyName))
        {
            return (calibration.ArrowOffsetX, calibration.ArrowOffsetY);
        }

        if (IsNumpadKey(key))
        {
            return (calibration.NumpadOffsetX, calibration.NumpadOffsetY);
        }

        return (calibration.MainTypingOffsetX, calibration.MainTypingOffsetY);
    }

    private static double GetLegacySpecialWidthAdjustment(KeyboardCalibrationSettings calibration, KeyboardKey key)
    {
        if (string.Equals(key.KeyName, "SPACE", StringComparison.OrdinalIgnoreCase))
        {
            return calibration.SpacebarWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "BACKSPACE", StringComparison.OrdinalIgnoreCase))
        {
            return calibration.BackspaceWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "TAB", StringComparison.OrdinalIgnoreCase))
        {
            return calibration.TabWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "CAPS LOCK", StringComparison.OrdinalIgnoreCase))
        {
            return calibration.CapsLockWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "OEM102", StringComparison.OrdinalIgnoreCase))
        {
            return calibration.IsoEnterWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "ENTER", StringComparison.OrdinalIgnoreCase))
        {
            return key.RowIndex == 4 ? calibration.NumpadEnterWidthAdjustment : calibration.EnterWidthAdjustment;
        }

        if (string.Equals(key.KeyName, "SHIFT", StringComparison.OrdinalIgnoreCase))
        {
            return key.ColumnIndex < 5 ? calibration.LeftShiftWidthAdjustment : calibration.RightShiftWidthAdjustment;
        }

        return 0;
    }

    private static bool IsFunctionKey(string keyName, int first, int last)
    {
        if (!keyName.StartsWith('F'))
        {
            return false;
        }

        return int.TryParse(keyName[1..], out var number) && number >= first && number <= last;
    }

    private static bool IsNavigationKey(string keyName)
        => keyName is "INSERT" or "HOME" or "PAGE UP" or "DELETE" or "END" or "PAGE DOWN";

    private static bool IsPrintScrollPauseKey(string keyName)
        => keyName is "PRINT SCREEN" or "SCROLL LOCK" or "PAUSE";

    private static bool IsArrowKey(string keyName)
        => keyName is "LEFT" or "DOWN" or "RIGHT" or "UP";

    private static bool IsNumpadKey(KeyboardKey key)
        => key.RowIndex >= 1 && key.ColumnIndex >= 16.25;

    private static bool HasAnyNonZero(params double[] values)
        => values.Any(value => Math.Abs(value) > double.Epsilon);

    private static void ZeroLegacyCalibration(KeyboardCalibrationSettings calibration)
    {
        calibration.EscOffsetX = 0;
        calibration.EscOffsetY = 0;
        calibration.F1ToF4OffsetX = 0;
        calibration.F1ToF4OffsetY = 0;
        calibration.F5ToF8OffsetX = 0;
        calibration.F5ToF8OffsetY = 0;
        calibration.F9ToF12OffsetX = 0;
        calibration.F9ToF12OffsetY = 0;
        calibration.PrintScrollPauseOffsetX = 0;
        calibration.PrintScrollPauseOffsetY = 0;
        calibration.MainTypingOffsetX = 0;
        calibration.MainTypingOffsetY = 0;
        calibration.NavigationOffsetX = 0;
        calibration.NavigationOffsetY = 0;
        calibration.ArrowOffsetX = 0;
        calibration.ArrowOffsetY = 0;
        calibration.NumpadOffsetX = 0;
        calibration.NumpadOffsetY = 0;

        calibration.SpacebarWidthAdjustment = 0;
        calibration.BackspaceWidthAdjustment = 0;
        calibration.EnterWidthAdjustment = 0;
        calibration.IsoEnterWidthAdjustment = 0;
        calibration.LeftShiftWidthAdjustment = 0;
        calibration.RightShiftWidthAdjustment = 0;
        calibration.NumpadEnterWidthAdjustment = 0;
        calibration.TabWidthAdjustment = 0;
        calibration.CapsLockWidthAdjustment = 0;
    }
}