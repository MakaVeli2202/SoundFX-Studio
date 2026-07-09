# Building an installer for SoundFX Studio

Prerequisites
- .NET SDK (for `dotnet publish`)
- Inno Setup (for compiling `installer.iss`, optional — see below)

Quick steps

1. From the repo root run (PowerShell):

```powershell
.\build-installer.ps1
```

2. If Inno Setup is on PATH, the script will invoke `iscc.exe` to compile `installer.iss` and place the output in `installer-output`.

3. If you don't have Inno Setup installed, either install it from https://jrsoftware.org/ and ensure `ISCC.exe` is on PATH, or open `installer.iss` in the Inno Setup IDE and compile manually.

Notes
- The script publishes the `SoundFXStudio` project into the repository `publish` folder; `installer.iss` packages everything under `publish` and an optional `installer-assets` folder if present.
- If you want to include extra assets (Voicemeeter installer, default sounds), create an `installer-assets` folder next to `installer.iss`.
