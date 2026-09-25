# SoundFX Studio

SoundFX Studio is a Windows desktop application for soundboard playback, keyboard-triggered actions, voice changing, and game audio enhancement via the **Art Tune** stack. The current release prioritizes stability and a polished desktop experience while preserving existing user workflows.

## What's inside
- **Soundboard** — play and organize sound entries from keyboard-triggered profiles
- **Voice Changer** — real-time voice changing over Voicemeeter routing
- **Art Tune (game audio)** — one-click install and apply of the Art of War audio stack: VB-CABLE + Voicemeeter, ReaPlugs, Equalizer APO, HeSuVi, LEQ Control Panel, and the ArtTuneDB library (headphone EQ, game tunes, HRTF presets, JSFX, VST). Rewires and renames your endpoints to *Art Tune / Art Tune + / Art Tune Unified Output* and writes the `config.txt` Includes exactly as the ArtTuneDB guide directs.

## Install (one line)

Paste this into PowerShell — it downloads the latest stable release and installs it silently:

```powershell
irm https://raw.githubusercontent.com/MakaVeli2202/SoundFX-Studio/main/install.ps1 | iex
```

Re-running it at any time upgrades an existing installation in place (sounds and settings are preserved). SoundFX Studio is also available for one-click install inside the [Forge](https://github.com/MakaVeli2202/Forg) app manager.

## Releasing

Push a version tag to trigger the release pipeline (`GitHub Actions` builds the Inno Setup installer and attaches it to a GitHub Release):

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Key capabilities
- Play and organize soundboard entries from keyboard-triggered profiles
- Use hotkeys and keybindings for rapid sound playback
- Change your voice live with configurable pitch/formant presets over Voicemeeter
- **Art Tune:** install the full Art of War game audio stack with one click, then apply any game/version tune (8-ch or 16-ch), headphone EQ and LEQ release time
- Persist profiles, categories, and settings locally

## Architecture at a glance
- MainWindow hosts the shell and navigation experience.
- MainViewModel coordinates the application state and commands.
- Services handle audio playback, hotkeys, routing, logging, and config persistence.
- `Services/ArtTune/ArtTuneStackService.cs` drives `Assets/ArtTune/ArtTune-OneClick.ps1` (elevated) for stack install, apply-tune, endpoint rename, and LEQ release-time config.
- The UI remains centered on the existing hero section and keyboard-launching experience.

## Development setup
1. Install .NET 8 SDK.
2. Restore packages: dotnet restore SoundFXStudio/SoundFXStudio.csproj
3. Build: dotnet build SoundFXStudio/SoundFXStudio.csproj -c Debug
4. Run: dotnet run --project SoundFXStudio/SoundFXStudio.csproj

## Documentation
- See docs/architecture-overview.md for the current architecture map.

## Notes
- The app uses Windows-specific WPF and native audio integrations; keep behavior changes conservative when modifying voice changer or routing logic.
- Art Tune shell integration (Equalizer APO, Voicemeeter, endpoint rename, LEQ registry edits) requires elevation and is performed inside the bundled PowerShell script, not directly in the SDK.