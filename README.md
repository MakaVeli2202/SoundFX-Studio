# SoundFX Studio

SoundFX Studio is a Windows desktop application for soundboard playback, keyboard-triggered actions, voice-changing workflows, and audio routing. The current release prioritizes stability and a polished desktop experience while preserving existing user workflows.

## Install

**Nothing to install first.** The setup carries the app and the Voicemeeter installer, so there is no .NET runtime, no SDK and no driver to fetch beforehand. All you need is Windows x64 and the one-liner:

```powershell
irm https://raw.githubusercontent.com/MakaVeli2202/SoundFX-Studio/main/install.ps1 | iex
```

That downloads the latest release, installs it with the Voicemeeter audio driver, and handles the administrator prompt through UAC. From the menu you can **Install**, **Upgrade**, **Uninstall**, or **Exit**. Upgrading installs over the current copy, so your sounds, settings and key bindings are kept.

Prefer a file? Download [install.ps1](install.ps1) and run `powershell -ExecutionPolicy Bypass -File install.ps1`.

Useful switches:
- `-Install` / `-Upgrade` / `-Uninstall` - run a job without the menu
- `-Status` - show what is installed right now
- `-CheckUpdate` - print the latest release tag
- `-Silent` - no prompts (scripts and CI)
- `-RemoveData` / `-RemoveVoicemeeter` - with `-Uninstall`, also drop your sounds/settings or the Voicemeeter driver

Restart once after the first install so the Voicemeeter driver finishes loading.

## Key capabilities
- Play and organize soundboard entries from keyboard-triggered profiles
- Use hotkeys and keybindings for rapid sound playback
- Configure voice changer input/output routing
- Integrate with Voicemeeter for advanced mixer control
- Persist profiles, categories, and settings locally

## Architecture at a glance
- MainWindow hosts the shell and navigation experience.
- MainViewModel coordinates the application state and commands.
- Services handle audio playback, hotkeys, routing, logging, and config persistence.
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
