# SoundFX Studio Project Map

This note is the shortest useful map of the repo. Use it when you need to answer questions like "where does this value come from?" or "which file should I change?".

## High-Level Shape

SoundFX Studio is a WPF app on .NET 8. The core app lives in `SoundFXStudio/`, UI tests live in `SoundFXStudio.UI.Tests/`, and unit tests live in `SoundFXStudio.Tests/`.

Main UI flow:

Keyboard key click or hotkey
→ `MainViewModel`
→ sound / action lookup
→ `AudioPlayer` or `ActionExecutor`
→ runtime behavior

## Folder Map

`SoundFXStudio/Models/`
Data objects and enums. Good first stop for state that is saved, loaded, or passed between view models.

`SoundFXStudio/ViewModels/`
App behavior and state transitions. `MainViewModel` is the central orchestrator and wires most services together directly, without a DI container.

`SoundFXStudio/Views/`
Main window and dialogs. If a control is visible but not working, or a window looks wrong, start here.

`SoundFXStudio/Controls/`
Reusable WPF controls. The keyboard surface lives here.

`SoundFXStudio/Services/`
Config, audio, keyboard layout, routing, and action execution logic.

`SoundFXStudio.UI.Tests/`
FlaUI tests for the app shell and major flows.

`SoundFXStudio.Tests/`
Unit tests for runtime services and logic.

## Core Runtime Flow

### Current sound path

Keyboard key
→ `KeyAssignment`
→ `SoundEntry`
→ `AudioPlayer`

### Current action path

Keyboard key
→ `KeyAssignment.ActionId`
→ `ActionExecutor`
→ action handler
→ runtime behavior

### Backward compatibility

Legacy configs still work because `SoundId` remains on `KeyAssignment`.

Migration rule:

1. Old configs use `SoundId`.
2. New configs can use `ActionId`.
3. If `ActionId` exists, the action runtime runs.
4. If only `SoundId` exists, the legacy sound path still works.

## Runtime Design

`ActionExecutor` resolves an `ActionDefinition`, checks `ActionType`, and dispatches to the right handler.

```csharp
public interface IActionHandler
{
     Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken);
}
```

Likely handlers:

* `SoundActionHandler`
* `ComboActionHandler`
* `MacroActionHandler`
* `PlaylistActionHandler`
* `ProfileActionHandler`

## Main View Entry Points

`SoundFXStudio/App.xaml`
Starts the app and wires global WPF resources.

`SoundFXStudio/App.xaml.cs`
Loads config, shows setup wizard if needed, then creates `MainWindow`.

`SoundFXStudio/Views/MainWindow.xaml`
Main shell UI. Tabs are Keyboard, Routing, Library, Settings, Presets, and Statistics.

`SoundFXStudio/Views/MainWindow.xaml.cs`
Window event handlers for keyboard layout changes, calibration window launch, keyboard window launch, drag-drop, and system actions.

`SoundFXStudio/ViewModels/MainViewModel.cs`
The main state hub. It owns the config, sound library, routing, keyboard view model, trigger service, and runtime command wiring.

## Keyboard Surface

`SoundFXStudio/Controls/KeyboardControl.xaml`
Builds the key buttons. Each button binds to a `KeyboardKey` and exposes `AutomationId` plus accessible `Name`.

Important data flow:

* `KeyboardKey.ImagePath` controls the key image.
* `KeyboardKey.HasAssignment` drives opacity and visibility.
* `KeyboardKey.IsSelected` drives selection glow.
* `KeyboardKey.AutomationName` is what UI tests often see.

`SoundFXStudio/Models/KeyboardKey.cs`
Defines the visible label, automation name, image path, assignment fields, and selection state.

`SoundFXStudio/ViewModels/KeyboardViewModel.cs`
Builds the keyboard, refreshes assignments, handles key clicks, and rebuilds the layout when the keyboard mode changes.

## Calibration Flow

`SoundFXStudio/Views/Dialogs/KeyboardCalibrationWindow.xaml`
Contains the global geometry controls, cluster offsets, special-key widths, per-key overrides, and the live preview.

`SoundFXStudio/Views/Dialogs/KeyboardCalibrationWindow.xaml.cs`
Owns the preview values, per-key item list, JSON editor, and save/revert logic.

Current simplified calibration flow:

1. Open calibration window.
2. Click a key in the live preview.
3. Edit offset and size values in the per-key panel.
4. Save permanently or close to keep live changes.

If a user asks about the calibration UI, the key places are the preview click hook in `KeyboardCalibrationWindow.xaml.cs`, the per-key item list, and `KeyboardLayoutPanel` / `KeyboardClusterLayout` for geometry application.

## Where Images Usually Fail

If an image is not showing on a keyboard button or sound tile, check these files in this order:

1. `SoundFXStudio/Controls/KeyboardControl.xaml`
    * This is the actual keyboard button template.
    * The image element binds to `ImagePath` and is inside the button visuals.
    * The button opacity changes based on assignment state.

2. `SoundFXStudio/Models/KeyboardKey.cs`
    * Confirms the `ImagePath`, `HasAssignment`, `IsSelected`, and label fields exist and raise change notifications.

3. `SoundFXStudio/ViewModels/KeyboardViewModel.cs`
    * Refreshes assignment state onto each key.

4. `SoundFXStudio/ViewModels/MainViewModel.cs`
    * Updates key assignment, image paths, and save/load behavior.

5. `SoundFXStudio/Views/Dialogs/SoundAssignmentWindow.xaml`
    * If the issue is in the Add Sound dialog, this is where the browse button and image preview live.

6. `SoundFXStudio/Views/Dialogs/SoundAssignmentWindow.xaml.cs`
    * Wires Browse and Choose actions and sets the selected image path.

Common causes:

* The binding path is wrong or the data item is not raising property changed.
* The key has no assignment, so the template opacity makes the image appear very dim.
* The image path exists in config but the file is missing on disk.
* The preview is showing the wrong `DataContext` after a selection change.

For the specific "white button" symptom, start with `KeyboardControl.xaml`, then check `KeyboardKey.ImagePath` and `HasAssignment`, then trace the assignment source in `KeyboardViewModel.RefreshAssignments()` and `MainViewModel.AssignSoundToKeyFromUi()`.

## Tests

`SoundFXStudio.UI.Tests/`
Use these for click-through behavior, selectors, and window flow. The shared `AppFixture` starts the exe and locates the main window.

`SoundFXStudio.Tests/`
Use these for runtime logic and config migration.

## Build And Run

Typical commands from the repo root:

```powershell
dotnet build .\SoundFXStudio\SoundFXStudio.csproj -c Debug
dotnet test .\SoundFXStudio.Tests\SoundFXStudio.Tests.csproj -c Debug
dotnet test .\SoundFXStudio.UI.Tests\SoundFXStudio.UI.Tests.csproj -c Debug --no-build
```

## Notes For Another AI

* This repo does not use a DI container. Services are instantiated directly.
* The main app state is centralized in `MainViewModel`.
* WPF window issues usually live in the view `.xaml` or its code-behind, not in the tests.
* UI automation IDs were added for reliability, so prefer `AutomationId` before `Name` in tests.
* If a feature seems broken but the build still passes, check binding and runtime data flow first.