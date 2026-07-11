# Copilot Instructions - SoundFX Studio

## Project Overview
WPF soundboard app (.NET 8). Users map audio files to keyboard keys, organize into profiles, and trigger sounds via physical keyboard or virtual keyboard clicks.

## Tech Stack
- **UI**: WPF (.NET 8), MVVM, custom chrome (WindowStyle=None)
- **Audio**: NAudio 2.3.0
- **Config**: JSON file at `%AppData%/SoundFXStudio/config.json`
- **Tests**: xUnit 2.9.2 (unit) + FlaUI.UIA3 5.0.0 (UI automation)
- **No DI container** - services instantiated directly

## Architecture
```
SoundFXStudio/
  Models/         - Data classes (SoundEntry, Profile, AppConfig, KeyboardKey, etc.)
  ViewModels/     - MainViewModel.cs (single VM, ~2400 lines)
  Views/          - MainWindow.xaml + Dialogs/
  Controls/       - KeyboardControl (custom WPF UserControl)
  Services/       - ConfigService, AudioPlayer, KeyboardLayoutService, etc.
  Converters/     - WPF value converters
  Infrastructure/ - RelayCommand, ObservableObject, AsyncRelayCommand
SoundFXStudio.Tests/       - Unit/integration tests (xUnit)
SoundFXStudio.UI.Tests/    - UI automation tests (FlaUI)
```

## Running Tests
```powershell
# All tests
.\run-tests.ps1

# Unit only
.\run-tests.ps1 -Unit

# UI only
.\run-tests.ps1 -UI

# Filter by name
.\run-tests.ps1 -Filter "MainWindow"

# Individual
dotnet test SoundFXStudio.Tests
dotnet test SoundFXStudio.UI.Tests
```

## UI Test Structure (FlaUI)
- `AppFixture.cs` - Launches app, finds window via UIA, dismisses setup wizard
- Tests use `[Collection("App")]` to share the app instance
- Pattern: navigate to tab -> find controls -> assert
- Key APIs: `FindFirstDescendant(cf => cf.ByControlType(ControlType.X))`, `.Click()`, `.Name`

## Bug Fix Workflow

### Step 1: Build & Run Tests
```powershell
dotnet build
dotnet test SoundFXStudio.Tests
dotnet test SoundFXStudio.UI.Tests
```

### Step 2: Identify Failures
Look for failed test names. Each test describes what should work:
- `LibraryTab_HasAddButton` = Add button missing
- `RoutingTab_HasOutputDeviceComboBox` = Device list empty
- `AddSound_CancelClosesDialog` = Dialog doesn't close
- etc.

### Step 3: Find the Code
```
Bug: "Add button missing" -> LibraryTabTests.cs:47
Look at: Views/MainWindow.xaml (Library tab XAML)
Look at: ViewModels/MainViewModel.cs (AddSoundCommand)
```

### Step 4: Fix & Verify
```powershell
dotnet build
dotnet test SoundFXStudio.UI.Tests --filter "FailedTestName"
```

## Key Commands in MainViewModel
| Command | Method | What it does |
|---|---|---|
| AddSoundCommand | AddSound() | Opens file dialog, imports audio |
| DeleteSoundCommand | DeleteSelectedSound() | Removes selected sound |
| PlaySelectedSoundCommand | PlaySelectedSound() | Plays selected sound |
| EditSoundCommand | EditSound() | Opens SoundAssignmentWindow |
| CreateProfileCommand | CreateProfile() | InputBox -> new profile |
| DeleteProfileCommand | DeleteSelectedProfile() | Confirms -> deletes profile |
| AutoConfigureAudioCommand | AutoConfigureAudio() | Picks best devices |
| OpenSetupWizardCommand | OpenSetupWizard() | Shows SetupWizardWindow |
| SaveCommand | Save() | Writes config to disk |
| RefreshCommand | Refresh() | Reloads config |
| HandleDropFiles | HandleDropFiles() | Drag-drop import |

## Key UI Elements
- **MainWindow**: TabControl with 5 tabs (Keyboard, Routing, Library, Settings, Presets)
- **SoundAssignmentWindow**: Add/Edit dialog with Browse, Name, Category, Volume, Image, Save/Cancel
- **SetupWizardWindow**: First-run wizard with Output/Input device selection, Finish button
- **KeyboardControl**: Custom UserControl with 104+ key buttons positioned absolutely

## Common Patterns
```csharp
// Finding controls
var tab = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
var buttons = win.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
var namedBtn = buttons.FirstOrDefault(b => b.Name.Contains("Add"));

// Clicking
element.Click();
Thread.Sleep(500); // WPF animations need time

// Checking visibility
Assert.False(element.IsOffscreen);
```

## Copilot Prompts

### Write a new UI test
```
Write a FlaUI UI test in SoundFXStudio.UI.Tests that:
1. Navigates to the [TAB] tab
2. [DESCRIBE ACTION]
3. Asserts [EXPECTED RESULT]
Follow existing patterns in MainWindowTests.cs.
Use [Collection("App")] and AppFixture.
```

### Fix a failing test
```
The UI test [TEST_NAME] is failing with:
[ERROR MESSAGE]
Find the cause in Views/[VIEW].xaml or ViewModels/MainViewModel.cs
and fix it. The test expects [EXPECTED BEHAVIOR].
```

### Add unit tests
```
Write xUnit tests for [CLASS/METHOD] in SoundFXStudio.Tests.
Follow existing patterns in ChordRuntimeServiceTests.cs.
Test: [DESCRIBE SCENARIOS]
```

### Fix a bug
```
Bug: [DESCRIBE BUG]
Steps to reproduce: [STEPS]
Find the code in the SoundFXStudio project and fix it.
The relevant code is likely in [FILE/PATTERN].
```

### Refactor
```
Refactor [METHOD/CLASS] in [FILE].
Current code: [PASTE CODE]
Goal: [WHAT YOU WANT]
Keep existing tests passing.
```
