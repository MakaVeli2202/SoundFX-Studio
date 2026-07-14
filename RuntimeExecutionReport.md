# Runtime Execution Report

## Summary

The runtime architecture exists and the sound path is working. The remaining action types are only partially implemented. `ActionId` wins for execution, while `SoundId` remains the compatibility/display fallback.

## Combo Runtime: Partial

### Current Code

```csharp
using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ComboActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public ComboActionHandler(AppConfig config, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        foreach (var actionId in ParseActionIds(action.Payload))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_config.Actions.Any(item => item.Id == actionId) || _config.Profiles.SelectMany(profile => profile.Actions).Any(item => item.Id == actionId))
            {
                await _executeActionAsync(actionId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<Guid> ParseActionIds(string payload)
    {
        foreach (var token in payload.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var actionId))
            {
                yield return actionId;
            }
        }
    }
}
```

### Current Behavior

* Executes a list of action ids from `action.Payload`.
* Can chain actions in order.
* Supports nested dispatch through `ActionExecutor`.

### What Executes

* Only action ids that are already valid in config.
* No explicit step model for sound, wait, sound, wait, sound.

### What Is Still Missing

* No real combo schema for step-by-step authored combos.
* No typed steps for `PlaySound`, `Wait`, `StopSound`, or per-step metadata.
* No combo editor UI.

### Required Implementation

* Add a real combo step model later if the product needs authored timing.
* Until then, this is a dispatcher shell, not a full combo engine.

### Combo Runtime: NOT IMPLEMENTED for authored timing

## Macro Runtime: Partial

### Current Code

```csharp
using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class MacroActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public MacroActionHandler(AppConfig config, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        foreach (var (delayMs, actionId) in ParseSteps(action.Payload))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            if (actionId.HasValue && (_config.Actions.Any(item => item.Id == actionId.Value) || _config.Profiles.SelectMany(profile => profile.Actions).Any(item => item.Id == actionId.Value)))
            {
                await _executeActionAsync(actionId.Value, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<(int DelayMs, Guid? ActionId)> ParseSteps(string payload)
    {
        foreach (var line in payload.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("wait:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line[5..], out var delayMs))
            {
                yield return (delayMs, null);
            }
            else if (Guid.TryParse(line, out var actionId))
            {
                yield return (0, actionId);
            }
        }
    }
}
```

### Current Behavior

* Supports `wait:` lines.
* Supports executing action ids line by line.

### What Executes

* Waits.
* Dispatches another action by id.

### What Is Still Missing

* No direct runtime verbs for `PlaySound`, `StopSound`, `SwitchProfile`, `ChangeVolume`, `SetRouting`, or `StartPlaylist`.
* No real macro command language.
* No macro editor UI.

### Macro Runtime: NOT IMPLEMENTED for the required verb set

## Playlist Runtime: Partial

### Current Code

```csharp
using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class PlaylistActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly Func<Guid, CancellationToken, Task> _executeActionAsync;

    public PlaylistActionHandler(AppConfig config, Func<Guid, CancellationToken, Task> executeActionAsync)
    {
        _config = config;
        _executeActionAsync = executeActionAsync;
    }

    public async Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        var actionIds = ParseActionIds(action.Payload).ToList();
        if (actionIds.Count == 0)
        {
            return;
        }

        if (string.Equals(action.PlaylistMode, "Single", StringComparison.OrdinalIgnoreCase))
        {
            await _executeActionAsync(actionIds[0], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(action.PlaylistMode, "Shuffle", StringComparison.OrdinalIgnoreCase))
        {
            actionIds = actionIds.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        foreach (var actionId in actionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_config.Actions.Any(item => item.Id == actionId) || _config.Profiles.SelectMany(profile => profile.Actions).Any(item => item.Id == actionId))
            {
                await _executeActionAsync(actionId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<Guid> ParseActionIds(string payload)
    {
        foreach (var token in payload.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var actionId))
            {
                yield return actionId;
            }
        }
    }
}
```

### Current Behavior

* Parses a list of action ids.
* Supports `Single` and `Shuffle` string modes.
* Sequential execution is default.

### What Executes

* Dispatch of action ids.
* Shuffle ordering.

### What Is Still Missing

* No true playlist runtime with queued track state or current index.
* No repeat loop behavior.
* No playlist advancement model beyond action-id dispatch.
* No editor UI.

### Playlist Runtime: NOT IMPLEMENTED for full playlist playback

## Profile Runtime: Partial

### Current Code

```csharp
using SoundFXStudio.Models;

namespace SoundFXStudio.Services;

public sealed class ProfileActionHandler : IActionHandler
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;

    public ProfileActionHandler(AppConfig config, ConfigService configService)
    {
        _config = config;
        _configService = configService;
    }

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Payload))
        {
            return Task.CompletedTask;
        }

        var profile = _config.Profiles.FirstOrDefault(item => string.Equals(item.Id, action.Payload, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return Task.CompletedTask;
        }

        _config.ActiveProfileId = profile.Id;
        _configService.Save(_config);
        return Task.CompletedTask;
    }
}
```

### Current Behavior

* Switches active profile id.
* Persists config.

### What Executes

* Profile selection change.
* Save to disk.

### What Is Still Missing

* No explicit refresh of bindings after switch inside the handler.
* No active-profile UI confirmation.
* No profile-scoped routing refresh.

### Profile Runtime: PARTIAL

## Playback Modes: Missing

### Current Code

`AudioPlayer` currently does this:

```csharp
public void Play(string soundId, string filePath, float volume = 1f, bool loop = false, int outputDeviceNumber = -1)
{
    if (!File.Exists(filePath))
    {
        return;
    }

    Stop(soundId);
    ...
}
```

### Current Behavior

* Restart is effectively implemented.
* Overlap is not implemented because `Play` always calls `Stop(soundId)` first.
* Ignore is not implemented.
* Toggle is not implemented.

### Playback Mode Plan

* `Restart`: keep current behavior.
* `Overlap`: use distinct playback session ids per trigger instead of one session per `soundId`.
* `Ignore`: return immediately if the sound is already playing.
* `Toggle`: if playing, stop; otherwise start.

### Playback Modes: Missing

## Hold To Play: Missing

### Current Code

* [MainViewModel.HandlePreviewKeyDown](SoundFXStudio/ViewModels/MainViewModel.cs#L417) calls `HandlePhysicalKey(e.Key, isKeyDown: true)`.
* [MainViewModel.HandlePreviewKeyUp](SoundFXStudio/ViewModels/MainViewModel.cs#L422) calls `HandlePhysicalKey(e.Key, isKeyDown: false)`.
* [MainViewModel.HandlePhysicalKey](SoundFXStudio/ViewModels/MainViewModel.cs#L599) plays on key down and only resets visuals on key up.
* [KeyboardHookService](SoundFXStudio/Services/KeyboardHookService.cs) only emits key down.
* [HotkeyService](SoundFXStudio/Services/HotkeyService.cs) emits hotkey press only.

### Current Behavior

* KeyDown plays.
* KeyUp only clears visuals.

### What Is Still Missing

* No stop-on-release for hold-to-play.
* No per-assignment release behavior.
* No global key-up hook.

### Hold To Play Plan

* Add an execution path that keeps a map from physical key to active action or sound session.
* On key down, start playback and record the active session.
* On key up, stop the recorded session if the binding is configured as hold-to-play or release-to-stop.
* For hotkeys, this would require a key-up capable global hook, not only `WM_HOTKEY`.

### Hold To Play: NOT IMPLEMENTED

## Per-Key Routing: Missing

### Current Code

* `MainViewModel.PlaySound` reads `Settings.OutputDeviceId` globally.
* `SoundActionHandler` also reads `Settings.OutputDeviceId` globally.
* `AudioPlayer.Play` accepts a single `outputDeviceNumber`.

### Current Behavior

* Routing is global.
* No per-key routing.

### What Is Still Missing

* No routing field on `KeyAssignment` or `ActionDefinition`.
* No route resolution in executor/handler based on binding.
* No per-key device selection in `AudioPlayer`.

### Suggested Architecture

* Add routing metadata to the assignment or action.
* Resolve device at execution time inside the handler.
* Keep `AudioPlayer` device selection per call.

### Per-Key Routing: NOT IMPLEMENTED

## Latency Measurement: Missing

### Current Code

* No `Stopwatch` around key-down to audio-start.
* No metrics collection in `MainViewModel`.
* No startup latency measurement.

### Current Behavior

* Latency is not measured.

### What Is Still Missing

* Average latency.
* Worst latency.
* Current-session metrics.
* Startup timing.

### Suggested Architecture

* Start a `Stopwatch` on key down.
* Stop it when `SoundActionHandler` calls `AudioPlayer.Play` or when playback actually starts.
* Track per-session and aggregate metrics in a small telemetry service.

### Latency Measurement: NOT IMPLEMENTED

## Action Display System: Partial

### Current Code

* [MainViewModel.RefreshAssignments](SoundFXStudio/ViewModels/MainViewModel.cs#L574)
  * `AssignedSoundId = assignment?.SoundId`
  * `AssignedSoundName = sound?.Name`
  * `AssignmentName = assignment?.BindingName`
* [KeyboardKey.AssignedSoundName](SoundFXStudio/Models/KeyboardKey.cs) is what `KeyboardControl.xaml` binds to.
* [KeyboardControl.xaml](SoundFXStudio/Controls/KeyboardControl.xaml) binds `AssignedSoundName` and `AssignmentName` in the keyboard UI.

### Current Behavior

* Sound actions display correctly because `SoundId` is still filled in.
* Non-sound actions will not display a meaningful `AssignedSoundName` because the UI only resolves sound names.

### What Is Still Missing

* `AssignedActionName` model property.
* UI binding to action display name.
* Fallback display logic for action types other than sound.

### Suggested Migration

* Keep `AssignedSoundName` for legacy display.
* Add `AssignedActionName` later.
* Let display resolve `ActionId` first, then fall back to `SoundId` only for legacy sound actions.

### Action Display System: PARTIAL

## Legacy Compatibility

### Current Rule in Code

* `ActionId` wins for execution in [MainViewModel.PlayKey](SoundFXStudio/ViewModels/MainViewModel.cs#L667).
* `SoundId` remains for display and fallback resolution.
* [ConfigService.MigrateLegacySoundAssignments](SoundFXStudio/Services/ConfigService.cs#L155) creates or reuses sound actions and backfills `ActionId`.

### Result

* Existing soundboard data continues to load.
* Existing sound playback still works.
* The action runtime path is now the primary execution path for sound assignments.

## Workflow Verdicts

* Assignment Workflow: Pass
* Playback Workflow: Pass for sound actions
* Display Workflow: Pass for sound actions, partial for future non-sound actions
* Config Save: Pass
* Config Load: Pass
* Action Migration: Pass
* Legacy Compatibility: Pass
