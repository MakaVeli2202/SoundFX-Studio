# Action System Architecture

## Current Flow

Keyboard Key
→ Sound Assignment
→ AudioPlayer

## Future Flow

Keyboard Key
→ Action Assignment
→ ActionExecutor
→ Action Handler
→ Runtime

## Runtime Design

```csharp
public interface IActionHandler
{
    Task ExecuteAsync(
        ActionDefinition action,
        CancellationToken cancellationToken);
}
```

`ActionExecutor` looks up `ActionDefinition`, resolves `ActionType`, and dispatches to a handler.

Planned handlers:

* `SoundActionHandler`
* `ComboActionHandler`
* `MacroActionHandler`
* `PlaylistActionHandler`
* `ProfileActionHandler`

## Working Path

Keyboard Key
→ Action
→ ActionExecutor
→ SoundActionHandler
→ AudioPlayer

## Backward Compatibility

Existing configs keep working because `SoundId` remains on `KeyAssignment`.

Migration rule:

* Old: `Key` → `SoundId`
* New: `Key` → `ActionId` where `Action.Type == Sound`

If `ActionId` exists, the new executor path runs. If only `SoundId` exists, the legacy direct sound path still runs.

## Config Upgrade Path

1. Load existing config.
2. Normalize missing collections.
3. Create or reuse a `Sound` action for each legacy `SoundId` assignment.
4. Copy that action id onto `KeyAssignment.ActionId`.
5. Keep `SoundId` as fallback until the new runtime is fully dominant.