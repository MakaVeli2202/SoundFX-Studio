# SoundFX Studio

SoundFX Studio is a Windows desktop application for soundboard playback, keyboard-triggered actions, voice-changing workflows, and audio routing. The current release prioritizes stability and a polished desktop experience while preserving existing user workflows.

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
