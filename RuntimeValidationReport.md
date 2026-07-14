# Runtime Validation Report

## 1. Assignment Workflow: Pass

Workflow traced:

* User selects a sound in the library.
* User right-clicks a keyboard key and chooses Assign Sound.
* [MainViewModel.AssignSelectedSoundToSelectedKey](SoundFXStudio/ViewModels/MainViewModel.cs#L1290) creates or updates a `KeyAssignment`.
* The assignment writes both `SoundId` and `ActionId`.
* [MainViewModel.RefreshAssignments](SoundFXStudio/ViewModels/MainViewModel.cs#L574) refreshes the keyboard model.
* [MainViewModel.Save](SoundFXStudio/ViewModels/MainViewModel.cs) persists the config.

Data written:

* `SoundId = SelectedSound.Id`
* `ActionId = EnsureSoundAction(SelectedSound).Id`

Result:

* Current assignment workflow is compatible with both legacy display and runtime execution.

## 2. Playback Workflow: Pass

Workflow traced:

* Physical key press enters [MainViewModel.HandlePhysicalKey](SoundFXStudio/ViewModels/MainViewModel.cs#L599).
* [MainViewModel.PlayKey](SoundFXStudio/ViewModels/MainViewModel.cs#L667) resolves the assignment.
* If `ActionId` exists, `ActionExecutor.ExecuteAsync(actionId)` is called.
* [ActionExecutor.ExecuteAsync](SoundFXStudio/Services/ActionExecutor.cs#L1) resolves the action and dispatches by `ActionType`.
* [SoundActionHandler.ExecuteAsync](SoundFXStudio/Services/SoundActionHandler.cs#L1) resolves the sound payload and calls [AudioPlayer.Play](SoundFXStudio/Services/AudioPlayer.cs).

Result:

* Action-driven playback works.

## 3. Display Workflow: Pass

Workflow traced:

* [MainViewModel.RefreshAssignments](SoundFXStudio/ViewModels/MainViewModel.cs#L574) maps the assignment to keyboard UI state.
* `AssignedSoundName` comes from the matched `SoundEntry.Name`.
* `AssignmentName` comes from `KeyAssignment.BindingName`.
* `AssignedSoundId` comes from `KeyAssignment.SoundId`.

Result:

* Action assignments display correctly because the current workflow stores `SoundId` alongside `ActionId`.

## 4. Config Save: Pass

* Save writes the full `AppConfig` graph.
* `KeyAssignment.SoundId` and `KeyAssignment.ActionId` are serialized.
* The current workflow keeps legacy and new identifiers together.

## 5. Config Load: Pass

* [ConfigService.Load](SoundFXStudio/Services/ConfigService.cs#L18) deserializes the config.
* [ConfigService.Normalize](SoundFXStudio/Services/ConfigService.cs#L109) ensures collections exist.
* Legacy assignments are migrated in [ConfigService.MigrateLegacySoundAssignments](SoundFXStudio/Services/ConfigService.cs#L155).

## 6. Action Migration: Pass

Migration behavior:

* Existing `SoundId` values are converted into `ActionDefinition` entries of type `Sound`.
* The generated action id is stored back onto `KeyAssignment.ActionId`.
* Duplicate creation is prevented by indexing actions by legacy `Payload` and reusing an existing action when found.

## 7. Legacy Compatibility: Pass

Rule:

* `ActionId` wins.
* `SoundId` remains fallback/migration data.

If both exist:

* runtime executes `ActionId`
* display still resolves `SoundId`
* migration keeps the two aligned for sound actions

## Key Conclusion

The current application is not split into two incompatible systems right now. The write path stores both ids for sound assignments, the runtime uses `ActionId`, and the keyboard UI still renders from `SoundId` for compatibility.