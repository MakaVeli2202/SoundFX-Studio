# SoundFX Studio architecture overview

## Application purpose
SoundFX Studio is a WPF desktop soundboard and voice-processing application for Windows. It combines:

- soundboard playback for mapped audio files
- keyboard and hotkey-driven triggering
- microphone and output routing for voice changer workflows
- Voicemeeter integration for advanced audio mixing
- user-configurable profiles and settings persistence

## High-level flow

```text
User input
  -> MainWindow / keyboard / hotkeys
  -> MainViewModel
  -> Services (audio, routing, voice changer, config, hotkeys)
  -> Windows audio / Voicemeeter / playback engine
```

## Key components

- MainWindow: top-level shell and window orchestration.
- MainViewModel: central state for soundboard actions, routing, voice changer, and settings.
- ConfigService: persists app settings and migrates older config files.
- TriggerService / HotkeyService: register and process keyboard shortcuts.
- VoiceChangerService: real-time voice processing for microphone input.
- VoicemeeterRemote: thin wrapper around the native Voicemeeter API.
- FileLogService: background file logging for diagnostics.

## Runtime notes

- The app preserves existing user workflows and configuration files.
- Audio routing and voice changer behavior should only be changed with care because they are tightly coupled to the user’s current Windows audio setup.
- The UI should remain visually modern while keeping the existing hero section and keyboard button intact.
