# SoundFX Studio Audit

## Purpose
Keyboard-first WPF soundboard. Main UI centers on a full keyboard surface with hotkey-triggered sound playback.

## Version 1 Scope
- Finish keyboard geometry.
- Finish Library page.
- Finish Settings page.
- Finish Audio page.
- Stop after those four areas are complete.
- Do not add Statistics, Marketplace, Cloud Sync, Voice Changer, AI Features, Sound Packs, DSP effects, Pitch Shifting, Reverb, or Echo.

## Version 1 Definition
Version 1 is complete when the app supports:
- Import MP3, WAV, FLAC, and OGG.
- Import sounds from direct http/https URLs.
- Assign image, custom name, keyboard key, favorite, category, and loop.
- Search library, preview sound, and play via keyboard.
- Global hotkeys and persistent assignments.
- Keyboard visualization with empty, assigned, hover, pressed, playing, and selected states.
- Audio device selection with persistence and validation.
- Theme selection and language selection.

After Version 1, begin a separate Version 1.1 milestone for VB-Cable, Voicemeeter, Discord routing, OBS routing, and the audio setup wizard.

## Current Status
- Latest build passed with `dotnet build d:\Projects\SoundFXStudio\SoundFXStudio.slnx`.
- Keyboard MVP work is in place and stable.
- Remaining work is polish, geometry tuning, and adjacent non-keyboard phases.

## Key Reference
- Behringer-style layout reference path used for UI direction:
  - `C:\dev\tools\FulcrumWeb\ConsoleEmulatorClient\ConsoleEmulator.UI\Behringer\BehringerWindow.xaml`

## Main Files
- `SoundFXStudio/Views/MainWindow.xaml`
  - Main shell window.
  - Main shell window with keyboard, library, settings, profiles, and statistics tabs.
  - The header selector is now labeled `Language` instead of `Layout`.
- `SoundFXStudio/Views/MainWindow.xaml.cs`
  - Thin host.
  - Creates `MainViewModel`, attaches window hooks, handles drag/drop.
- `SoundFXStudio/ViewModels/MainViewModel.cs`
  - Core app state and behavior.
  - Loads config, profiles, sounds, devices.
  - Handles keyboard input, playback, hotkeys, assignments, save/load.
  - Also owns the new right-click key actions and layout switching.
- `SoundFXStudio/Services/KeyboardLayoutService.cs`
  - Builds the full keyboard layout.
  - Supports `KeyboardLayoutMode.English` and `KeyboardLayoutMode.German`.
- `SoundFXStudio/Services/KeyboardHookService.cs`
  - Low-level global keyboard hook for background key visualization.
- `SoundFXStudio/Services/HotkeyService.cs`
  - Registers global hotkeys with `WM_HOTKEY`.
- `SoundFXStudio/Controls/KeyboardControl.xaml`
  - Renders the keyboard keys as buttons.
  - Uses `DisplayLabel` for the visible text and now drives empty, assigned, hovered, pressed, playing, and selected states.
- `SoundFXStudio/Controls/KeyboardLayoutPanel.cs`
  - Custom panel for arranging the full keyboard grid.
- `SoundFXStudio/Models/KeyboardKey.cs`
  - Key model with `KeyName`, `DisplayLabel`, size, position, assignment state.
- `SoundFXStudio/Models/AppSettings.cs`
  - Stores devices, theme, global hotkeys, layout mode, startup flags.
- `SoundFXStudio/Models/KeyboardLayoutMode.cs`
  - Enum for English/German keyboard layout selection.
- `SoundFXStudio/App.xaml`
  - Global button style / pressed-state styling.

## Input / Playback Flow
- Foreground keys: `MainWindow.xaml.cs` forwards `PreviewKeyDown` to `MainViewModel.HandlePreviewKeyDown`.
- Background keys: `KeyboardHookService` listens globally and calls the same viewmodel path when the app is not active.
- Key token matching is physical-token based in `MainViewModel.ToKeyToken(...)`.
- Visible keyboard labels come from `KeyboardKey.DisplayLabel`, not `KeyName`.
- Sound playback is handled by `AudioPlayer`.

## Layout Notes
- German layout should swap visible Y/Z labels and German punctuation/labels while keeping physical token matching stable.
- User wanted the layout to resemble the Behringer reference more closely.
- Print Screen, Scroll Lock, and Pause were moved to the top-right cluster above Insert/Home/PageUp.
- Key buttons now support background image, assignment name, sound name overlay, category accent strip, hover, pressed, playing, and selected states.
- Right-click key actions now exist in MVVM for assign/remove image/rename binding and are surfaced in the key menu.
- Library now supports direct URL-based sound import through the existing add flow.

## Version 1 Workflow
- Keyboard first: right-click a key, edit assignment, save.
- Library first: add sound, open assignment wizard, save.
- The same assignment flow should handle import, drop, and edit paths.

## Assignment Wizard
- New shared assignment window: `Views/Dialogs/SoundAssignmentWindow`.
- Fields: Sound File, Custom Name, Keyboard Key, Image, Category, Volume, Favorite, Loop.
- Save should create or update `SoundEntry`, update the assigned `KeyboardKey`, refresh the keyboard immediately, and save through `ConfigService`.
- Drag-and-drop should open the wizard instead of silently importing.
- Keyboard context menu should be simplified to `Edit Assignment` and `Clear Assignment`.

## Current UI Notes
- Global `Button` style lives in `App.xaml`.
- Keyboard key button style lives in `KeyboardControl.xaml`.
- `MainWindow.xaml` now defines dark styles for `TextBox`, `ComboBox`, `ComboBoxItem`, `ListView`, `ListViewItem`, and `CheckBox` so dropdowns and the library stay readable on the dark background.
- The header language dropdown and the settings language dropdown both bind to `KeyboardLayoutOptions`.
- Extra button margin was removed to stop clipping on the right/bottom edges.
- `UseLayoutRounding` and `SnapsToDevicePixels` are enabled on the keyboard control.
- `KeyboardControl.xaml` is the main surface to keep tuning for Behringer-style spacing, pressed animation, playback animation, overlays, and selection outline behavior.

## Useful Search Targets
- `CreateKeyboard(` in `KeyboardLayoutService.cs`
- `HandlePreviewKeyDown` in `MainViewModel.cs`
- `HandlePhysicalKey` in `MainViewModel.cs`
- `KeyboardHookService` in `Services/KeyboardHookService.cs`
- `DisplayLabel` in `KeyboardControl.xaml` and `KeyboardKey.cs`
- `AssignSelectedSoundToSelectedKey`, `RemoveSoundFromKey`, `ChooseKeyImage`, `RenameBinding` in `MainViewModel.cs`

## Next Likely Work
- Finish the last Behringer-style keyboard geometry tuning for ANSI and ISO German spacing.
- Tighten the Library page layout and item styling.
- Finish the Settings page.
- Finish the Audio page.
- Add the shared assignment wizard and simplify keyboard/library assignment flow.
- Keep Version 1 scope locked; defer routing setup, VB-Cable, and Voicemeeter to Version 1.1.

## Architect View
- Foundation: 95%
- Keyboard Engine: 92%
- Audio Engine: 75%
- UI Shell: 80%
- Library: 58%
- Settings: 45%
- Profiles: 30%
- Polish: 62%

## Phase Order
1. Finish the keyboard first. It is the product identity.
2. Finish the Library page.
3. Finish the Settings page.
4. Finish the Audio page.
5. Stop. Version 1 complete.

## Keyboard Target
- Full ANSI keyboard layout.
- Full ISO German keyboard layout.
- Correct spacing, sizes, navigation block, arrow cluster, and numpad.
- Hover, press, playback, assigned, empty, and selected visual states.
- Image backgrounds, sound name overlay, category color, hotkey label, and assignment indicators.
- Remaining keyboard work is fit and finish only.

## Audio Target
- Finish audio device detection, selection, validation, and persistence.
- Keep VB-Cable and routing setup out of Version 1.

## Design Rule
- Do not turn the keyboard into a plain sound grid.
- The keyboard is the flagship feature.
