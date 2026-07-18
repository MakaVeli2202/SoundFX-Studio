using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Key = System.Windows.Input.Key;

namespace SoundFXStudio.ViewModels;

public sealed class KeyboardViewModel
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);

    private const int VK_CAPITAL = 0x14;
    private const int VK_NUMLOCK = 0x90;
    private const int VK_SCROLL = 0x91;

    private readonly Func<AppConfig> _getConfig;
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<KeyboardLayoutMode> _getKeyboardLayoutMode;
    private readonly KeyboardLayoutService _keyboardLayoutService;
    private readonly Func<Guid, Task> _executeActionAsync;
    private readonly AudioPlayer _audioPlayer;
    private readonly ObservableCollection<KeyboardKey> _keyboardKeys;
    private readonly ObservableCollection<SoundEntry> _sounds;
    private readonly ObservableCollection<Category> _categories;
    private readonly ObservableCollection<Profile> _profiles;
    private readonly ObservableCollection<AudioDeviceInfo> _outputDevices;
    private readonly Func<KeyboardKey?> _getSelectedKey;
    private readonly Action<KeyboardKey?> _setSelectedKey;
    private readonly Action<string> _setStatusText;
    private readonly Action<Action> _runOnUiThread;
    private readonly Action _updateTitle;
    private readonly Action _raiseSoundCollectionStats;
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<string> _pressedTriggerTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _unhighlightTimers = new();
    private readonly Dictionary<string, (Guid? ActionId, string? SoundId, KeyPlaybackMode KeyPlaybackMode, CancellationTokenSource? CancellationTokenSource)> _activeTriggers = new(StringComparer.OrdinalIgnoreCase);
    private ChordRuntimeService? _chordRuntimeService;

    public KeyboardViewModel(
        Func<AppConfig> getConfig,
        Func<AppSettings> getSettings,
        Func<KeyboardLayoutMode> getKeyboardLayoutMode,
        KeyboardLayoutService keyboardLayoutService,
        Func<Guid, Task> executeActionAsync,
        AudioPlayer audioPlayer,
        ObservableCollection<KeyboardKey> keyboardKeys,
        ObservableCollection<SoundEntry> sounds,
        ObservableCollection<Category> categories,
        ObservableCollection<Profile> profiles,
        ObservableCollection<AudioDeviceInfo> outputDevices,
        Func<KeyboardKey?> getSelectedKey,
        Action<KeyboardKey?> setSelectedKey,
        Action<string> setStatusText,
        Action<Action> runOnUiThread,
        Action updateTitle,
        Action raiseSoundCollectionStats)
    {
        _getConfig = getConfig;
        _getSettings = getSettings;
        _getKeyboardLayoutMode = getKeyboardLayoutMode;
        _keyboardLayoutService = keyboardLayoutService;
        _executeActionAsync = executeActionAsync;
        _audioPlayer = audioPlayer;
        _keyboardKeys = keyboardKeys;
        _sounds = sounds;
        _categories = categories;
        _profiles = profiles;
        _outputDevices = outputDevices;
        _getSelectedKey = getSelectedKey;
        _setSelectedKey = setSelectedKey;
        _setStatusText = setStatusText;
        _runOnUiThread = runOnUiThread;
        _updateTitle = updateTitle;
        _raiseSoundCollectionStats = raiseSoundCollectionStats;
    }

    public void AttachChordRuntimeService(ChordRuntimeService chordRuntimeService)
    {
        _chordRuntimeService = chordRuntimeService;
    }

    public void RebuildKeyboard()
    {
        var selectedKeyId = _getSelectedKey()?.Id;
        var keys = _keyboardLayoutService.CreateKeyboard(_getKeyboardLayoutMode());
        _keyboardKeys.Clear();
        foreach (var key in keys)
        {
            _keyboardKeys.Add(key);
        }

        _setSelectedKey(selectedKeyId is null ? null : _keyboardKeys.FirstOrDefault(item => string.Equals(item.Id, selectedKeyId, StringComparison.OrdinalIgnoreCase)));
        RefreshAssignments();
    }

    public void RefreshAssignments()
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var selectedKeyId = _getSelectedKey()?.Id;
        var calibration = _getSettings().KeyboardCalibration;
        var keyOverrides = calibration?.KeyOverrides;

        foreach (var key in _keyboardKeys)
        {
            var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
            var sound = assignment is null ? null : _sounds.FirstOrDefault(item => string.Equals(item.Id, assignment.SoundId, StringComparison.OrdinalIgnoreCase));
            var category = sound is null ? null : _categories.FirstOrDefault(item => string.Equals(item.Name, sound.Category, StringComparison.OrdinalIgnoreCase));
            var keyOverride = keyOverrides is not null && keyOverrides.TryGetValue(key.Id, out var value)
                ? value
                : null;

            key.ImagePath = assignment?.ImagePath ?? sound?.ImagePath;
            key.AssignedSoundId = assignment?.SoundId;
            key.AssignedSoundName = sound?.Name;
            key.AssignmentName = assignment?.BindingName;
            key.CategoryAccentColor = string.IsNullOrWhiteSpace(category?.AccentColor) ? "#00000000" : category.AccentColor;
            key.InnerInsetAdjustmentPercent = keyOverride?.InnerInsetAdjustmentPercent ?? 0;
            key.InnerInsetXAdjustmentPercent = keyOverride?.InnerInsetXAdjustmentPercent ?? 0;
            key.InnerInsetYAdjustmentPercent = keyOverride?.InnerInsetYAdjustmentPercent ?? 0;
            key.InnerOffsetXAdjustmentPercent = keyOverride?.InnerOffsetXAdjustmentPercent ?? 0;
            key.InnerOffsetYAdjustmentPercent = keyOverride?.InnerOffsetYAdjustmentPercent ?? 0;
            key.IsSelected = string.Equals(key.Id, selectedKeyId, StringComparison.OrdinalIgnoreCase);
            UpdateKeyVisualState(key);
            ApplyLockKeyVisualState(key);
            key.IsEnabled = true;
        }
    }

    public void HandlePhysicalKey(Key key, bool isKeyDown = true)
    {
        var token = NormalizeTokenForLayout(ToKeyToken(key, ModifierKeys.None));
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var keyboardKey = _keyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, token, StringComparison.OrdinalIgnoreCase));
        if (keyboardKey is null)
        {
            return;
        }

        _runOnUiThread(() =>
        {
            if (isKeyDown)
            {
                if (_unhighlightTimers.TryGetValue(token, out var cts))
                {
                    cts.Cancel();
                    _unhighlightTimers.Remove(token);
                }

                _pressedKeys.Add(key);
                keyboardKey.IsSelected = true;
                FlashKey(keyboardKey);
                _ = _chordRuntimeService?.HandleKeyDownAsync(token);

                var unhighlightCts = new CancellationTokenSource();
                _unhighlightTimers[token] = unhighlightCts;

                Task.Delay(300, unhighlightCts.Token).ContinueWith(_ =>
                {
                    if (!unhighlightCts.Token.IsCancellationRequested)
                    {
                        _runOnUiThread(() =>
                        {
                            keyboardKey.IsSelected = false;
                            _unhighlightTimers.Remove(token);
                        });
                    }
                });
            }
            else
            {
                if (_unhighlightTimers.TryGetValue(token, out var cts))
                {
                    cts.Cancel();
                    _unhighlightTimers.Remove(token);
                }

                _pressedKeys.Remove(key);

                if (key == Key.CapsLock)
                {
                    bool isOn = (GetKeyState(VK_CAPITAL) & 1) != 0;
                    keyboardKey.IsSelected = isOn;
                }
                else if (key == Key.NumLock)
                {
                    bool isOn = (GetKeyState(VK_NUMLOCK) & 1) != 0;
                    keyboardKey.IsSelected = isOn;
                }
                else if (key == Key.Scroll)
                {
                    bool isOn = (GetKeyState(VK_SCROLL) & 1) != 0;
                    keyboardKey.IsSelected = isOn;
                }
                else
                {
                    keyboardKey.IsSelected = false;
                }

                _ = _chordRuntimeService?.HandleKeyUpAsync(token);
            }
        });
    }

    public void HandleKeyClicked(object? parameter)
    {
        if (parameter is not KeyboardKey key)
        {
            return;
        }

        _setSelectedKey(key);

        if (key.AssignedSoundId is null)
        {
            _setStatusText($"Selected {key.DisplayLabel}");
            return;
        }

        PlayKey(key);
    }

    public void FlashKey(KeyboardKey key)
    {
        key.State = KeyState.Pressed;
        Task.Delay(120).ContinueWith(_ =>
        {
            _runOnUiThread(() => UpdateKeyVisualState(key));
        });
    }

    public void UpdateKeyVisualState(KeyboardKey key)
    {
        var isPlaying = !string.IsNullOrWhiteSpace(key.AssignedSoundId) && _audioPlayer.IsPlaying(key.AssignedSoundId);
        if (isPlaying)
        {
            key.State = KeyState.Playing;
            return;
        }

        key.State = key.HasAssignment ? KeyState.Assigned : KeyState.Empty;
    }

    private static void ApplyLockKeyVisualState(KeyboardKey key)
    {
        bool isOn = key.KeyName switch
        {
            "CAPS LOCK" => (GetKeyState(VK_CAPITAL) & 1) != 0,
            "NUM LOCK" => (GetKeyState(VK_NUMLOCK) & 1) != 0,
            "SCROLL LOCK" => (GetKeyState(VK_SCROLL) & 1) != 0,
            _ => false
        };

        if (isOn)
        {
            key.IsSelected = true;
        }
    }

    public async Task TrackPlaybackAsync(string soundId, string? keyId)
    {
        while (_audioPlayer.IsPlaying(soundId))
        {
            await Task.Delay(80).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        _runOnUiThread(() =>
        {
            var key = _keyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                UpdateKeyVisualState(key);
            }
        });
    }

    internal KeyAssignment? GetAssignmentForKeyToken(string token)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, token, StringComparison.OrdinalIgnoreCase));
    }

    internal void ExecuteAssignmentOnce(KeyAssignment assignment)
    {
        var action = ResolveActionForAssignment(assignment);
        if (action is not null)
        {
            _ = _executeActionAsync(action.Id);
            return;
        }

        var sound = ResolveSound(assignment.SoundId);
        if (sound is not null)
        {
            PlaySound(sound, assignment);
        }
    }

    private void PlayKey(KeyboardKey key)
    {
        var assignment = GetAssignmentForKeyToken(key.Id);
        if (assignment is null)
        {
            return;
        }

        ExecuteAssignmentOnce(assignment);
    }

    private void PlaySound(SoundEntry sound, KeyAssignment? assignment = null)
    {
        if (!File.Exists(sound.FilePath))
        {
            _setStatusText($"Missing file: {sound.Name}");
            return;
        }

        var deviceId = _getSettings().OutputDeviceId;
        var deviceIndex = _outputDevices.ToList().FindIndex(device => string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase));

        _audioPlayer.Play(
            sound.Id,
            sound.FilePath,
            assignment?.VolumeOverride ?? sound.Volume,
            assignment?.Loop ?? sound.Loop,
            PlaybackMode.Restart,
            deviceIndex);

        _runOnUiThread(() =>
        {
            sound.PlayCount++;
            sound.LastPlayedUtc = DateTime.UtcNow;
            _raiseSoundCollectionStats();
            _setStatusText($"Playing {sound.Name}");
            _updateTitle();
        });

        _ = TrackPlaybackAsync(sound.Id, assignment?.KeyId);
    }

    private ActionDefinition? ResolveActionForAssignment(KeyAssignment assignment)
    {
        if (assignment.ActionId is Guid actionId)
        {
            return _getConfig().Actions.FirstOrDefault(item => item.Id == actionId)
                   ?? _profiles.SelectMany(profile => profile.Actions).FirstOrDefault(item => item.Id == actionId);
        }

        var sound = ResolveSound(assignment.SoundId);
        return sound is null ? null : EnsureSoundAction(sound);
    }

    private SoundEntry? ResolveSound(string soundId)
        => _sounds.FirstOrDefault(item => string.Equals(item.Id, soundId, StringComparison.OrdinalIgnoreCase));

    private ActionDefinition EnsureSoundAction(SoundEntry sound)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            throw new InvalidOperationException("No profile available.");
        }

        var action = profile.Actions.FirstOrDefault(item => item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase));
        if (action is not null)
        {
            return action;
        }

        action = new ActionDefinition
        {
            Type = ActionType.Sound,
            Name = sound.Name,
            Description = $"Play {sound.Name}",
            Payload = sound.Id,
            Category = sound.Category,
            IconPath = sound.ImagePath ?? string.Empty,
            IsFavorite = sound.IsFavorite,
            PlaybackMode = PlaybackMode.Restart
        };

        profile.Actions.Add(action);
        return action;
    }

    private Profile? ActiveProfile => _profiles.FirstOrDefault(item => string.Equals(item.Id, _getConfig().ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        ?? _profiles.FirstOrDefault(item => item.IsDefault)
        ?? _profiles.FirstOrDefault();

    private static ModifierKeys GetModifierState()
        => (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? ModifierKeys.Control : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) ? ModifierKeys.Alt : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? ModifierKeys.Shift : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) ? ModifierKeys.Windows : ModifierKeys.None);

    private static string ToKeyToken(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("CTRL");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("SHIFT");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("ALT");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("WIN");
        }

        var token = key switch
        {
            Key.Escape => "ESC",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.CapsLock => "CAPS LOCK",
            Key.LeftShift or Key.RightShift => "SHIFT",
            Key.LeftCtrl or Key.RightCtrl => "CTRL",
            Key.LeftAlt or Key.RightAlt => "ALT",
            Key.LWin or Key.RWin => "WIN",
            Key.Apps => "MENU",
            Key.PrintScreen => "PRINT SCREEN",
            Key.Scroll => "SCROLL LOCK",
            Key.Pause => "PAUSE",
            Key.NumLock => "NUM LOCK",
            Key.PageUp => "PAGE UP",
            Key.PageDown => "PAGE DOWN",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "NUMPAD0",
            Key.NumPad1 => "NUMPAD1",
            Key.NumPad2 => "NUMPAD2",
            Key.NumPad3 => "NUMPAD3",
            Key.NumPad4 => "NUMPAD4",
            Key.NumPad5 => "NUMPAD5",
            Key.NumPad6 => "NUMPAD6",
            Key.NumPad7 => "NUMPAD7",
            Key.NumPad8 => "NUMPAD8",
            Key.NumPad9 => "NUMPAD9",
            Key.Add => "+",
            Key.Subtract => "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            Key.Decimal => ".",
            Key.Space => "SPACE",
            Key.Return => "ENTER",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.Oem6 => "]",
            Key.Oem5 => "\\",
            Key.Oem1 => ";",
            Key.Oem7 => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.Oem102 => "OEM102",
            Key.Left => "LEFT",
            Key.Right => "RIGHT",
            Key.Up => "UP",
            Key.Down => "DOWN",
            _ => key.ToString().ToUpperInvariant()
        };

        parts.Add(token);
        return string.Join("+", parts);
    }

    private string NormalizeTokenForLayout(string token)
    {
        if (_getKeyboardLayoutMode() != KeyboardLayoutMode.German)
        {
            return token;
        }

        var parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return token;
        }

        var lastIndex = parts.Length - 1;
        parts[lastIndex] = parts[lastIndex].ToUpperInvariant() switch
        {
            "Y" => "Z",
            "Z" => "Y",
            _ => parts[lastIndex]
        };

        return string.Join("+", parts);
    }
}
