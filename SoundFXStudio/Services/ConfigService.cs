using SoundFXStudio.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundFXStudio.Services;

/// <summary>
/// Loads and saves the application's persistent configuration while preserving
/// backward compatibility for older versions of the settings file.
/// </summary>
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

    public ConfigService(ILogService? logService = null, string? appFolder = null)
    {
        _logService = logService;
        var folder = appFolder ?? GetAppFolder();
        _configPath = Path.Combine(folder, "config.json");
        _backupPath = Path.Combine(folder, "config.backup.json");
    }

    /// <summary>
    /// Loads the persisted configuration, falling back to defaults when the file is missing or invalid.
    /// </summary>
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

            ApplyProjectCalibrationIfAvailable(normalized);
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

    /// <summary>
    /// Persists the supplied configuration and writes a backup copy before overwriting the active file.
    /// </summary>
    public void Save(AppConfig config)
    {
        try
        {
            var folder = Path.GetDirectoryName(_configPath) ?? GetAppFolder();
            Directory.CreateDirectory(folder);

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

    /// <summary>
    /// Returns the writable application data folder used for configuration, sounds, and images.
    /// </summary>
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
        config.Profiles ??= new();
        config.Categories ??= new();
        config.Settings ??= new AppSettings();
        config.Settings.KeyboardCalibration ??= new KeyboardCalibrationSettings();
        config.Settings.KeyboardCalibration.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>();

        if (string.IsNullOrWhiteSpace(config.Settings.VoiceChangerPresetId))
        {
            config.Settings.VoiceChangerPresetId = "normal";
            migrated = true;
        }

        if (Math.Abs(config.Settings.FormantShift) < 0.01f)
        {
            config.Settings.FormantShift = 1f;
            migrated = true;
        }

        if (string.IsNullOrWhiteSpace(config.Settings.VoiceChangerToggleKey))
        {
            config.Settings.VoiceChangerToggleKey = "C+V";
            migrated = true;
        }

        migrated |= MigrateLegacyKeyboardCalibration(config.Settings.KeyboardCalibration, config.Settings.KeyboardLayout);
        if (config.Settings.KeyboardCalibration.KeyboardWindowScale < 0.5)
        {
            config.Settings.KeyboardCalibration.KeyboardWindowScale = 0.8;
            migrated = true;
        }

        var calibration = config.Settings.KeyboardCalibration;
        if (calibration.OpenKeyboardButtonWidth < 100 || calibration.OpenKeyboardButtonWidth > 600)
        {
            calibration.OpenKeyboardButtonWidth = 220;
            migrated = true;
        }
        if (calibration.OpenKeyboardButtonHeight < 20 || calibration.OpenKeyboardButtonHeight > 200)
        {
            calibration.OpenKeyboardButtonHeight = 48;
            migrated = true;
        }
        if (calibration.OpenKeyboardButtonX < 0 || calibration.OpenKeyboardButtonX > 1500)
        {
            calibration.OpenKeyboardButtonX = 36;
            migrated = true;
        }
        if (calibration.OpenKeyboardButtonY < 0 || calibration.OpenKeyboardButtonY > 1200)
        {
            calibration.OpenKeyboardButtonY = 488;
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
            var projectCal = JsonSerializer.Deserialize<KeyboardCalibrationSettings>(json, SerializerOptions);
            if (projectCal is null)
            {
                return;
            }

            projectCal.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>();
            MigrateLegacyKeyboardCalibration(projectCal, config.Settings.KeyboardLayout);
            if (projectCal.KeyboardWindowScale < 0.5)
            {
                projectCal.KeyboardWindowScale = 0.8;
            }

            var existing = config.Settings.KeyboardCalibration ?? new KeyboardCalibrationSettings();
            existing.KeyOverrides ??= new Dictionary<string, KeyCalibrationOverrideSettings>();
            MergeCalibration(existing, projectCal);
            config.Settings.KeyboardCalibration = existing;
        }
        catch
        {
            // Ignore project calibration load failures and keep the persisted config.
        }
    }

    private static void MergeCalibration(KeyboardCalibrationSettings target, KeyboardCalibrationSettings source)
    {
        var defaults = new KeyboardCalibrationSettings();

        MergeIfDefault(target, source, defaults, cal => cal.KeyUnit, (cal, v) => cal.KeyUnit = v);
        MergeIfDefault(target, source, defaults, cal => cal.GapX, (cal, v) => cal.GapX = v);
        MergeIfDefault(target, source, defaults, cal => cal.GapY, (cal, v) => cal.GapY = v);
        MergeIfDefault(target, source, defaults, cal => cal.Gap, (cal, v) => cal.Gap = v);
        MergeIfDefault(target, source, defaults, cal => cal.OffsetX, (cal, v) => cal.OffsetX = v);
        MergeIfDefault(target, source, defaults, cal => cal.OffsetY, (cal, v) => cal.OffsetY = v);
        MergeIfDefault(target, source, defaults, cal => cal.ButtonScale, (cal, v) => cal.ButtonScale = v);
        MergeIfDefault(target, source, defaults, cal => cal.InnerSectionInsetXPercent, (cal, v) => cal.InnerSectionInsetXPercent = v);
        MergeIfDefault(target, source, defaults, cal => cal.InnerSectionInsetYPercent, (cal, v) => cal.InnerSectionInsetYPercent = v);
        MergeIfDefault(target, source, defaults, cal => cal.InnerSectionOffsetXPercent, (cal, v) => cal.InnerSectionOffsetXPercent = v);
        MergeIfDefault(target, source, defaults, cal => cal.InnerSectionOffsetYPercent, (cal, v) => cal.InnerSectionOffsetYPercent = v);
        MergeIfDefault(target, source, defaults, cal => cal.KeyboardWindowScale, (cal, v) => cal.KeyboardWindowScale = v);
        if (source.OpenKeyboardButtonX > 0)
            MergeIfDefault(target, source, defaults, cal => cal.OpenKeyboardButtonX, (cal, v) => cal.OpenKeyboardButtonX = v);
        if (source.OpenKeyboardButtonY > 0)
            MergeIfDefault(target, source, defaults, cal => cal.OpenKeyboardButtonY, (cal, v) => cal.OpenKeyboardButtonY = v);
        if (source.OpenKeyboardButtonWidth > 0)
            MergeIfDefault(target, source, defaults, cal => cal.OpenKeyboardButtonWidth, (cal, v) => cal.OpenKeyboardButtonWidth = v);
        if (source.OpenKeyboardButtonHeight > 0)
            MergeIfDefault(target, source, defaults, cal => cal.OpenKeyboardButtonHeight, (cal, v) => cal.OpenKeyboardButtonHeight = v);

        MergeIfDefault(target, source, defaults, cal => cal.CapsLockIndicatorOffsetX, (cal, v) => cal.CapsLockIndicatorOffsetX = v);
        MergeIfDefault(target, source, defaults, cal => cal.CapsLockIndicatorOffsetY, (cal, v) => cal.CapsLockIndicatorOffsetY = v);
        MergeIfDefault(target, source, defaults, cal => cal.NumLockIndicatorOffsetX, (cal, v) => cal.NumLockIndicatorOffsetX = v);
        MergeIfDefault(target, source, defaults, cal => cal.NumLockIndicatorOffsetY, (cal, v) => cal.NumLockIndicatorOffsetY = v);
        MergeIfDefault(target, source, defaults, cal => cal.ScrollLockIndicatorOffsetX, (cal, v) => cal.ScrollLockIndicatorOffsetX = v);
        MergeIfDefault(target, source, defaults, cal => cal.ScrollLockIndicatorOffsetY, (cal, v) => cal.ScrollLockIndicatorOffsetY = v);

        MergeIfDefault(target, source, defaults, cal => cal.InnerSectionInsetPercent, (cal, v) => cal.InnerSectionInsetPercent = v);

        foreach (var pair in source.KeyOverrides)
        {
            if (!target.KeyOverrides.ContainsKey(pair.Key))
            {
                target.KeyOverrides[pair.Key] = pair.Value;
            }
        }
    }

    private static void MergeIfDefault(
        KeyboardCalibrationSettings target,
        KeyboardCalibrationSettings source,
        KeyboardCalibrationSettings defaults,
        Func<KeyboardCalibrationSettings, double> getter,
        Action<KeyboardCalibrationSettings, double> setter)
    {
        if (Math.Abs(getter(target) - getter(defaults)) < double.Epsilon)
        {
            setter(target, getter(source));
        }
    }

    private static string GetProjectCalibrationFilePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "SoundFXStudio.sln");
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