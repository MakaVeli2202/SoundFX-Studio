using SoundFXStudio.Models;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Timers;
using Key = System.Windows.Input.Key;
using Timer = System.Timers.Timer;

namespace SoundFXStudio.Services;

public sealed class TriggerService : IDisposable
{
    private readonly HotkeyService _hotkeyService;
    private readonly KeyboardHookService _keyboardHookService;
    private readonly ActionExecutor _actionExecutor;
    private readonly AudioPlayer _audioPlayer;
    private readonly Func<AppConfig> _getConfig;
    private readonly Func<KeyboardKey, KeyAssignment?> _getAssignmentForKey;
    private readonly Func<string, KeyAssignment?> _getAssignmentForKeyToken;
    private readonly Func<string, SoundEntry?> _resolveSound;
    private readonly Func<SoundEntry, ActionDefinition> _ensureSoundAction;
    private readonly Func<KeyboardKey?> _getSelectedKey;
    private readonly Action<KeyboardKey> _updateKeyVisualState;
    private readonly Action<string> _setStatusText;
    private readonly Action _updateTitle;
    private readonly Action _raiseSoundCollectionStats;
    private readonly Action<SoundEntry, KeyAssignment?> _playSound;
    private readonly Action<Action> _runOnUiThread;
    private readonly ILogService? _logService;
    private readonly HashSet<string> _pressedTriggerTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Guid? ActionId, string? SoundId, KeyPlaybackMode KeyPlaybackMode, CancellationTokenSource? CancellationTokenSource)> _activeTriggers = new(StringComparer.OrdinalIgnoreCase);
    private EventHandler<HotkeyEventArgs>? _hotkeyPressedHandler;
    private EventHandler<KeyboardHookKeyEventArgs>? _keyboardDownHandler;
    private EventHandler<KeyboardHookKeyEventArgs>? _keyboardUpHandler;
    private Window? _window;
    private Action<Key, bool>? _handlePhysicalKey;
    private bool _disposed;
    private readonly Dictionary<Key, string> _bareMuteKeys = new();
    private readonly HashSet<Key> _heldMuteKeys = new();
    private Timer? _chordTimer;
    private KeyAssignment? _chordPendingAssignment;
    private Key _chordPendingKey;
    private string _chordPendingToken = string.Empty;
    private readonly object _chordLock = new();

    public TriggerService(
        HotkeyService hotkeyService,
        KeyboardHookService keyboardHookService,
        ActionExecutor actionExecutor,
        AudioPlayer audioPlayer,
        Func<AppConfig> getConfig,
        Func<KeyboardKey, KeyAssignment?> getAssignmentForKey,
        Func<string, KeyAssignment?> getAssignmentForKeyToken,
        Func<string, SoundEntry?> resolveSound,
        Func<SoundEntry, ActionDefinition> ensureSoundAction,
        Func<KeyboardKey?> getSelectedKey,
        Action<KeyboardKey> updateKeyVisualState,
        Action<string> setStatusText,
        Action updateTitle,
        Action raiseSoundCollectionStats,
        Action<SoundEntry, KeyAssignment?> playSound,
        Action<Action> runOnUiThread,
        ILogService? logService = null)
    {
        _hotkeyService = hotkeyService;
        _keyboardHookService = keyboardHookService;
        _actionExecutor = actionExecutor;
        _audioPlayer = audioPlayer;
        _getConfig = getConfig;
        _getAssignmentForKey = getAssignmentForKey;
        _getAssignmentForKeyToken = getAssignmentForKeyToken;
        _resolveSound = resolveSound;
        _ensureSoundAction = ensureSoundAction;
        _getSelectedKey = getSelectedKey;
        _updateKeyVisualState = updateKeyVisualState;
        _setStatusText = setStatusText;
        _updateTitle = updateTitle;
        _raiseSoundCollectionStats = raiseSoundCollectionStats;
        _playSound = playSound;
        _runOnUiThread = runOnUiThread;
        _logService = logService;

        ChordRuntimeService = new ChordRuntimeService(
            _getConfig,
            _getAssignmentForKeyToken,
            assignment =>
            {
                ExecuteAssignmentOnce(assignment);
                return Task.CompletedTask;
            },
            actionId => _actionExecutor.ExecuteAsync(actionId));
    }

    public ChordRuntimeService ChordRuntimeService { get; }

    public void AttachWindow(Window window, Action<Key, bool> handlePhysicalKey)
    {
        ThrowIfDisposed();

        _window = window;
        _handlePhysicalKey = handlePhysicalKey;
        _hotkeyService.Attach(window);
        _hotkeyPressedHandler = (_, args) =>
        {
            switch (args.OwnerId)
            {
                case HkMuteAll:
                    _logService?.Info("Mute All triggered");
                    System.Windows.Application.Current.Dispatcher.Invoke(() => App.ToggleMuteAll());
                    return;
                case HkMuteHear:
                    _logService?.Info("Mute Hear triggered");
                    System.Windows.Application.Current.Dispatcher.Invoke(() => App.ToggleMuteHear());
                    return;
                case HkMuteTeam:
                    _logService?.Info("Mute Team triggered");
                    System.Windows.Application.Current.Dispatcher.Invoke(() => App.ToggleMuteTeam());
                    return;
                case HkStopAll:
                    _logService?.Info("Stop All triggered");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        (System.Windows.Application.Current.MainWindow?.DataContext as ViewModels.MainViewModel)?.StopAllSounds());
                    return;
            }

            var assignment = ActiveProfile?.Assignments.FirstOrDefault(item => string.Equals(item.Id, args.OwnerId, StringComparison.OrdinalIgnoreCase));
            if (assignment is null)
            {
                return;
            }

            _logService?.Info($"Hotkey Triggered: {args.HotkeyText}");
            ExecuteAssignmentOnce(assignment);
        };
        _hotkeyService.HotkeyPressed += _hotkeyPressedHandler;

        _keyboardDownHandler = (_, args) =>
        {
            if (IsOwnWindowForeground())
            {
                return;
            }

            if (_bareMuteKeys.TryGetValue(args.Key, out var muteAction))
            {
                if (_heldMuteKeys.Add(args.Key))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        switch (muteAction)
                        {
                            case HkMuteAll: App.ToggleMuteAll(); break;
                            case HkMuteHear: App.ToggleMuteHear(); break;
                            case HkMuteTeam: App.ToggleMuteTeam(); break;
                            case HkStopAll:
                                (System.Windows.Application.Current.MainWindow?.DataContext as ViewModels.MainViewModel)?.StopAllSounds();
                                break;
                        }
                    });
                }
                return;
            }

            handlePhysicalKey(args.Key, true);
        };
        _keyboardHookService.KeyDown += _keyboardDownHandler;
        _keyboardUpHandler = (_, args) =>
        {
            if (IsOwnWindowForeground())
            {
                return;
            }

            _heldMuteKeys.Remove(args.Key);

            handlePhysicalKey(args.Key, false);
        };
        _keyboardHookService.KeyUp += _keyboardUpHandler;

        _keyboardHookService.Attach();
        RegisterGlobalHotkeys();
    }

    public bool IsBareMuteKey(Key key) => _bareMuteKeys.ContainsKey(key);

    public bool TryHandleBareMuteKey(Key key)
    {
        if (!_bareMuteKeys.TryGetValue(key, out var muteAction))
        {
            return false;
        }

        switch (muteAction)
        {
            case HkMuteAll: App.ToggleMuteAll(); break;
            case HkMuteHear: App.ToggleMuteHear(); break;
            case HkMuteTeam: App.ToggleMuteTeam(); break;
            case HkStopAll:
                (System.Windows.Application.Current.MainWindow?.DataContext as ViewModels.MainViewModel)?.StopAllSounds();
                break;
        }
        return true;
    }

    public void RegisterGlobalHotkeys()
    {
        ThrowIfDisposed();

        var profile = ActiveProfile;
        if (profile is not null)
        {
            foreach (var assignment in profile.Assignments.Where(item => !string.IsNullOrWhiteSpace(item.HotkeyText)))
            {
                _logService?.Info($"Hotkey Unregistered: {assignment.HotkeyText}");
            }
        }

        _hotkeyService.Clear();

        if (profile is null)
        {
            return;
        }

        foreach (var assignment in profile.Assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.HotkeyText))
            {
                continue;
            }

            if (IsBareKey(assignment.HotkeyText))
            {
                _logService?.Info($"Bare key handled by hook (not registered): {assignment.HotkeyText}");
                continue;
            }

            if (_hotkeyService.Register(assignment.Id, assignment.HotkeyText))
            {
                _logService?.Info($"Hotkey Registered: {assignment.HotkeyText}");
            }
            else
            {
                _logService?.Warning($"Hotkey Registration Failed: {assignment.HotkeyText}");
            }
        }

        var settings = _getConfig().Settings;
        if (settings is not null)
        {
            RegisterMuteHotkeys(settings.MuteAllKey, settings.MuteHearKey, settings.MuteTeamKey, settings.StopAllKey);
        }
    }

    private static bool IsBareKey(string hotkeyText)
    {
        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return true;
        }

        foreach (var part in hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                case "ALT":
                case "SHIFT":
                case "WIN":
                case "WINDOWS":
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseBareKey(string? hotkeyText, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(hotkeyText)) return false;
        if (hotkeyText.Contains('+')) return false;
        return HotkeyService.TryParseKey(hotkeyText, out key);
    }

    private void RegisterMuteBareKey(string actionId, string? hotkeyText, Key parsedKey)
    {
        RemoveMuteBareKey(actionId);
        if (parsedKey != Key.None)
            _bareMuteKeys[parsedKey] = actionId;
    }

    private void RemoveMuteBareKey(string actionId)
    {
        foreach (var kv in _bareMuteKeys.Where(kv => kv.Value == actionId).ToList())
            _bareMuteKeys.Remove(kv.Key);
    }

    public void RegisterMuteHotkeys(string muteAllKey, string muteHearKey, string muteTeamKey, string stopAllKey)
    {
        ThrowIfDisposed();

        _hotkeyService.Unregister(HkMuteAll);
        _hotkeyService.Unregister(HkMuteHear);
        _hotkeyService.Unregister(HkMuteTeam);
        _hotkeyService.Unregister(HkStopAll);

        if (TryParseBareKey(muteAllKey, out var allKey))
            RegisterMuteBareKey(HkMuteAll, muteAllKey, allKey);
        else if (!string.IsNullOrWhiteSpace(muteAllKey))
        {
            RemoveMuteBareKey(HkMuteAll);
            _hotkeyService.Register(HkMuteAll, muteAllKey);
        }

        if (TryParseBareKey(muteHearKey, out var hearKey))
            RegisterMuteBareKey(HkMuteHear, muteHearKey, hearKey);
        else if (!string.IsNullOrWhiteSpace(muteHearKey))
        {
            RemoveMuteBareKey(HkMuteHear);
            _hotkeyService.Register(HkMuteHear, muteHearKey);
        }

        if (TryParseBareKey(muteTeamKey, out var teamKey))
            RegisterMuteBareKey(HkMuteTeam, muteTeamKey, teamKey);
        else if (!string.IsNullOrWhiteSpace(muteTeamKey))
        {
            RemoveMuteBareKey(HkMuteTeam);
            _hotkeyService.Register(HkMuteTeam, muteTeamKey);
        }

        if (TryParseBareKey(stopAllKey, out var stopKey))
            RegisterMuteBareKey(HkStopAll, stopAllKey, stopKey);
        else if (!string.IsNullOrWhiteSpace(stopAllKey))
        {
            RemoveMuteBareKey(HkStopAll);
            _hotkeyService.Register(HkStopAll, stopAllKey);
        }
    }

    private const string HkMuteAll = "_mute_all";
    private const string HkMuteHear = "_mute_hear";
    private const string HkMuteTeam = "_mute_team";
    private const string HkStopAll = "_stop_all";

    private static bool IsOwnWindowForeground()
    {
        if (System.Windows.Application.Current is not { } app)
        {
            return false;
        }

        foreach (var window in app.Windows)
        {
            if (window is System.Windows.Window w && w.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    public bool HandleChordKeyDown(Key key)
    {
        var token = KeyToToken(key);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (_chordLock)
        {
            if (_chordPendingAssignment is not null)
            {
                if (string.Equals(token, _chordPendingAssignment.ChordKey, StringComparison.OrdinalIgnoreCase))
                {
                    CancelChordTimer();
                    _logService?.Info($"Chord triggered: {_chordPendingToken} + {token}");
                    ExecuteAssignmentOnce(_chordPendingAssignment);
                    _chordPendingAssignment = null;
                    _chordPendingToken = string.Empty;
                    return true;
                }

                CancelChordTimer();
                var saved = _chordPendingAssignment;
                _chordPendingAssignment = null;
                _chordPendingToken = string.Empty;
                System.Windows.Application.Current.Dispatcher.Invoke(() => ExecuteAssignmentOnce(saved));
            }
        }

        var assignment = FindAssignmentByChordKey(key, token);
        if (assignment is not null)
        {
            lock (_chordLock)
            {
                _chordPendingAssignment = assignment;
                _chordPendingKey = key;
                _chordPendingToken = token;
                var timeout = GetChordTimeoutMs();
                _chordTimer = new Timer(timeout);
                _chordTimer.Elapsed += (_, _) => OnChordTimeout(token, assignment);
                _chordTimer.AutoReset = false;
                _chordTimer.Start();
                _logService?.Info($"Chord pending for {token}, waiting for chord key...");
                return true;
            }
        }

        return false;
    }

    public bool HandleChordKeyUp(Key key)
    {
        var token = KeyToToken(key);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (_chordLock)
        {
            if (_chordPendingAssignment is not null
                && string.Equals(token, _chordPendingToken, StringComparison.OrdinalIgnoreCase))
            {
                // Primary key released, but chord timer is still running
                // Don't fire yet - wait for chord key or timeout
                return true;
            }
        }

        return false;
    }

    private void OnChordTimeout(string token, KeyAssignment assignment)
    {
        Key savedKey;
        lock (_chordLock)
        {
            if (_chordPendingAssignment != assignment)
            {
                return;
            }

            savedKey = _chordPendingKey;
            _chordPendingAssignment = null;
            _chordPendingToken = string.Empty;
            _chordTimer = null;
        }

        _logService?.Info($"Chord timeout for {token}, firing single key");
        System.Windows.Application.Current.Dispatcher.Invoke(() => _handlePhysicalKey?.Invoke(savedKey, true));
    }

    private void CancelChordTimer()
    {
        _chordTimer?.Stop();
        _chordTimer?.Dispose();
        _chordTimer = null;
    }

    private KeyAssignment? FindAssignmentByChordKey(Key key, string token)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        foreach (var assignment in profile.Assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.ChordKey))
            {
                continue;
            }

            if (string.Equals(assignment.HotkeyText, token, StringComparison.OrdinalIgnoreCase))
            {
                return assignment;
            }

            if (string.Equals(assignment.KeyId, token, StringComparison.OrdinalIgnoreCase))
            {
                return assignment;
            }
        }

        return null;
    }

    private static string KeyToToken(Key key)
    {
        if (key == Key.None)
        {
            return string.Empty;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return string.Empty;
        }

        return key switch
        {
            Key.Escape => "ESC",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.CapsLock => "CAPS LOCK",
            Key.Apps => "MENU",
            Key.PrintScreen => "PRINT SCREEN",
            Key.Scroll => "SCROLL LOCK",
            Key.Pause => "PAUSE",
            Key.NumLock => "NUM LOCK",
            Key.PageUp => "PAGE UP",
            Key.PageDown => "PAGE DOWN",
            Key.Space => "SPACE",
            Key.Return => "ENTER",
            Key.Oem102 => "OEM102",
            _ => key.ToString().ToUpperInvariant()
        };
    }

    private int GetChordTimeoutMs()
    {
        try
        {
            return _getConfig().Settings?.ChordTimeoutMs ?? 1000;
        }
        catch
        {
            return 1000;
        }
    }

    public void HandleKeyTrigger(KeyboardKey key, string triggerToken, bool isKeyDown)
    {
        ThrowIfDisposed();

        var assignment = _getAssignmentForKey(key);
        if (assignment is null)
        {
            return;
        }

        HandleAssignmentTrigger(key, assignment, triggerToken, isKeyDown);
    }

    public void HandleAssignmentTrigger(KeyAssignment assignment, string triggerToken, bool isKeyDown)
        => HandleAssignmentTrigger(null, assignment, triggerToken, isKeyDown);

    public void ExecuteAssignmentOnce(KeyAssignment assignment)
    {
        ThrowIfDisposed();

        var action = ResolveActionForAssignment(assignment);
        if (action is not null)
        {
            _ = _actionExecutor.ExecuteAsync(action.Id);
            return;
        }

        var sound = _resolveSound(assignment.SoundId);
        if (sound is not null)
        {
            _playSound(sound, assignment);
        }
    }

    public void StopAssignmentPlayback(KeyAssignment assignment)
    {
        ThrowIfDisposed();

        var action = ResolveActionForAssignment(assignment);
        if (action is not null && action.Type == ActionType.Sound && !string.IsNullOrWhiteSpace(action.Payload))
        {
            _audioPlayer.Stop(action.Payload);
            return;
        }

        if (!string.IsNullOrWhiteSpace(assignment.SoundId))
        {
            _audioPlayer.Stop(assignment.SoundId);
        }
    }

    internal KeyAssignment? GetAssignmentForKeyToken(string token)
    {
        ThrowIfDisposed();

        var profile = ActiveProfile;
        if (profile is null || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = NormalizeHotkeyToken(token);
        return profile.Assignments.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.ChordKey)
            && (string.Equals(item.KeyId, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeHotkeyToken(item.HotkeyText), normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeHotkeyToken(string? hotkeyText)
    {
        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return string.Empty;
        }

        var parts = hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[^1].Trim().ToUpperInvariant();
    }

    private void HandleAssignmentTrigger(KeyboardKey? keyboardKey, KeyAssignment assignment, string triggerToken, bool isKeyDown)
    {
        var action = ResolveActionForAssignment(assignment);
        var sound = action is null ? _resolveSound(assignment.SoundId) : null;

        if (isKeyDown)
        {
            if (action is null && sound is null)
            {
                return;
            }

            if (_pressedTriggerTokens.Contains(triggerToken))
            {
                return;
            }

            _pressedTriggerTokens.Add(triggerToken);
            _logService?.Info($"Trigger Activated: {triggerToken}");

            if (action is not null && action.KeyPlaybackMode == KeyPlaybackMode.Toggle && TryStopActiveTrigger(triggerToken, keyboardKey))
            {
                _logService?.Info($"Toggle Stopped: {triggerToken}");
                return;
            }

            if (action is not null)
            {
                StartActionTrigger(triggerToken, action, assignment);
                return;
            }

            if (sound is not null)
            {
                _playSound(sound, assignment);
            }

            return;
        }

        _pressedTriggerTokens.Remove(triggerToken);
        _logService?.Info($"Trigger Released: {triggerToken}");

        if (action is not null && action.KeyPlaybackMode == KeyPlaybackMode.Toggle)
        {
            return;
        }

        if (TryStopActiveTrigger(triggerToken, keyboardKey))
        {
            return;
        }
    }

    private ActionDefinition? ResolveActionForAssignment(KeyAssignment assignment)
    {
        if (assignment.ActionId is Guid actionId)
        {
            return _getConfig().Actions.FirstOrDefault(item => item.Id == actionId)
                   ?? _getConfig().Profiles.SelectMany(profile => profile.Actions).FirstOrDefault(item => item.Id == actionId);
        }

        var sound = _resolveSound(assignment.SoundId);
        return sound is null ? null : _ensureSoundAction(sound);
    }

    private void StartActionTrigger(string triggerToken, ActionDefinition action, KeyAssignment assignment)
    {
        var shouldTrack = action.KeyPlaybackMode is KeyPlaybackMode.HoldToPlay or KeyPlaybackMode.ReleaseToStop or KeyPlaybackMode.Toggle
                          || (action.Type == ActionType.Playlist && string.Equals(action.PlaylistMode, "Repeat", StringComparison.OrdinalIgnoreCase));

        CancellationTokenSource? cancellationTokenSource = null;
        if (shouldTrack)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _activeTriggers[triggerToken] = (action.Id, ResolveSoundIdForAction(action), action.KeyPlaybackMode, cancellationTokenSource);
        }

        if (action.KeyPlaybackMode == KeyPlaybackMode.Toggle)
        {
            _logService?.Info($"Toggle Activated: {triggerToken}");
        }
        else if (action.KeyPlaybackMode is KeyPlaybackMode.HoldToPlay or KeyPlaybackMode.ReleaseToStop)
        {
            _logService?.Info($"HoldToPlay Started: {triggerToken}");
        }

        _ = _actionExecutor.ExecuteAsync(action.Id, cancellationTokenSource?.Token ?? CancellationToken.None);

        if (action.Type == ActionType.Sound)
        {
            _runOnUiThread(() =>
            {
                var sound = ResolveSoundIdForAction(action) is { } soundId ? _resolveSound(soundId) : null;
                if (sound is not null)
                {
                    sound.PlayCount++;
                    sound.LastPlayedUtc = DateTime.UtcNow;
                    _raiseSoundCollectionStats();
                    _setStatusText($"Playing {sound.Name}");
                    _updateTitle();
                    if (_getSelectedKey() is not null && string.Equals(_getSelectedKey()!.Id, assignment.KeyId, StringComparison.OrdinalIgnoreCase))
                    {
                        _getSelectedKey()!.State = KeyState.Playing;
                    }
                }
            });
        }
    }

    private bool TryStopActiveTrigger(string triggerToken, KeyboardKey? keyboardKey)
    {
        if (!_activeTriggers.TryGetValue(triggerToken, out var active))
        {
            return false;
        }

        active.CancellationTokenSource?.Cancel();
        _activeTriggers.Remove(triggerToken);

        if (active.KeyPlaybackMode == KeyPlaybackMode.Toggle)
        {
            _logService?.Info($"Toggle Stopped: {triggerToken}");
        }
        else if (active.KeyPlaybackMode is KeyPlaybackMode.HoldToPlay or KeyPlaybackMode.ReleaseToStop)
        {
            _logService?.Info($"HoldToPlay Ended: {triggerToken}");
        }

        if (active.ActionId is Guid actionId)
        {
            var action = _getConfig().Actions.FirstOrDefault(item => item.Id == actionId)
                         ?? _getConfig().Profiles.SelectMany(profile => profile.Actions).FirstOrDefault(item => item.Id == actionId);

            if (action is not null && action.Type == ActionType.Sound)
            {
                var soundId = ResolveSoundIdForAction(action);
                if (!string.IsNullOrWhiteSpace(soundId))
                {
                    _audioPlayer.Stop(soundId);
                }
            }
        }

        if (keyboardKey is not null)
        {
            _updateKeyVisualState(keyboardKey);
        }

        return true;
    }

    private static string? ResolveSoundIdForAction(ActionDefinition action)
        => action.Type == ActionType.Sound ? action.Payload : null;

    private Profile? ActiveProfile => _getConfig().Profiles.FirstOrDefault(item => string.Equals(item.Id, _getConfig().ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        ?? _getConfig().Profiles.FirstOrDefault(item => item.IsDefault)
        ?? _getConfig().Profiles.FirstOrDefault();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logService?.Info("Disposing TriggerService");

        foreach (var trigger in _activeTriggers.Values)
        {
            trigger.CancellationTokenSource?.Cancel();
            trigger.CancellationTokenSource?.Dispose();
        }

        _activeTriggers.Clear();
        _pressedTriggerTokens.Clear();

        if (_hotkeyPressedHandler is not null)
        {
            _hotkeyService.HotkeyPressed -= _hotkeyPressedHandler;
            _hotkeyPressedHandler = null;
        }

        if (_keyboardDownHandler is not null)
        {
            _keyboardHookService.KeyDown -= _keyboardDownHandler;
            _keyboardDownHandler = null;
        }

        if (_keyboardUpHandler is not null)
        {
            _keyboardHookService.KeyUp -= _keyboardUpHandler;
            _keyboardUpHandler = null;
        }

        _logService?.Info("Hotkey Unregistered");
        _hotkeyService.Dispose();
        _keyboardHookService.Dispose();

        _window = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TriggerService));
        }
    }
}
