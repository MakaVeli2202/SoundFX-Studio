using SoundFXStudio.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Key = System.Windows.Input.Key;

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
    private bool _disposed;

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
            _getConfig(),
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
        _hotkeyService.Attach(window);
        _hotkeyPressedHandler = (_, args) =>
        {
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
            if (_window?.IsActive == true)
            {
                return;
            }

            handlePhysicalKey(args.Key, true);
        };
        _keyboardHookService.KeyDown += _keyboardDownHandler;

        _keyboardUpHandler = (_, args) =>
        {
            if (_window?.IsActive == true)
            {
                return;
            }

            handlePhysicalKey(args.Key, false);
        };
        _keyboardHookService.KeyUp += _keyboardUpHandler;

        _keyboardHookService.Attach();
        RegisterGlobalHotkeys();
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

            if (_hotkeyService.Register(assignment.Id, assignment.HotkeyText))
            {
                _logService?.Info($"Hotkey Registered: {assignment.HotkeyText}");
            }
            else
            {
                _logService?.Warning($"Hotkey Registration Failed: {assignment.HotkeyText}");
            }
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
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, token, StringComparison.OrdinalIgnoreCase));
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
