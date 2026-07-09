# Open-Source Research: Audio/Voicemeeter/Hotkey Integration Patterns

## Research Date
July 9, 2026

---

## 1. VOICEMEETER INTEGRATION APPS

### 1.1 VoiceMeeterWrapper (tocklime)
**URL**: https://github.com/tocklime/VoiceMeeterWrapper  
**Language**: C#  
**Stars**: 25  
**Last Update**: Mar 22, 2020  

**What it does**: Direct C# wrapper to Voicemeeter Remote API with example MIDI surface sync  

**Implementation Pattern**:
- P/Invoke wrapper around RemoteAPI.dll
- Example client for nanoKONTROL2 surface
- Isolated wrapper classes for COM/unmanaged code safety

**Key Takeaway**: Shows how to safely wrap Voicemeeter DLL calls. Recommended pattern for SoundFX.

---

### 1.2 Voicemeeter AudioCallback Example (A-tG)
**URL**: https://github.com/A-tG/Voicemeeter-AudioCallback-Simple-Example  
**Language**: C# (unsafe context)  
**Stars**: 0  
**Last Update**: Jun 10, 2021  

**What it does**: Demonstrates audio callback implementation for Voicemeeter API  

**Implementation Pattern**:
- Unsafe C# context for pointer operations
- Direct audio stream handling
- Shows audio callback registration

**Key Takeaway**: If you need real-time audio processing via Voicemeeter, this is the pattern.

---

### 1.3 VoiceSpritz (ZygoteCode)
**URL**: https://github.com/ZygoteCode/VoiceSpritz  
**Language**: C#  
**Status**: Work in Progress  
**Last Update**: Apr 20, 2025  

**What it does**: Real-time voice modification to Virtual Audio Cable (VAC)  

**Implementation Pattern**:
- Microphone input capture
- Real-time DSP/voice effects
- Route to VAC output

**Key Takeaway**: Example of advanced VAC integration; useful if extending to voice effects.

---

## 2. SOUND BOARD & HOTKEY TRIGGER APPS

### 2.1 SonicBoard (Kunal-CodeLab)
**URL**: https://github.com/Kunal-CodeLab/SonicBoard  
**Language**: C# / .NET 8  
**Framework**: WPF  
**Stars**: 0  
**Last Update**: Jun 17, 2026 (active development)  

**Features**:
- ✅ Global hotkeys  
- ✅ Multi-device audio routing  
- ✅ Smart microphone auto-muting  
- ✅ Customizable profiles  
- ✅ MP3, WAV, OGG, FLAC support  
- ✅ Emulator/Gaming focus (BGMI, Discord, GameLoop)  

**Directory Structure**:
```
Assets/          (Images, icons)
Helpers/         (Utility functions)
Models/          (Data models, profiles)
Resources/       (Strings, styles)
Services/        (Audio, hotkey, device management)
```

**Implementation Strengths**:
- Modern .NET 8 stack
- Comprehensive audio format support
- Smart profile switching
- Multi-device aware

**Recommendations for SoundFX**:
- Mirror `Services/` architecture for audio device management
- Study profile/configuration pattern
- Check how they handle device fallback

---

### 2.2 SoundSync (sugumar247)
**URL**: https://github.com/sugumar247/SoundSync  
**Language**: C#  
**Framework**: WPF + NAudio  
**Stars**: 6  
**Last Update**: Jul 5, 2026 (4 days ago - very active!)  

**Features**:
- ✅ Real-time multi-device audio routing  
- ✅ Broadcast system sound to multiple headsets/speakers  
- ✅ WPF UI + Console Application version  
- ✅ Tray icon integration (Hardcodet.NotifyIcon)  
- ✅ Theme support (recent refactor: "Added theme change button")  

**Key Refactoring (Recent)**:
- Tray icon refactored to WPF-native with Hardcodet.NotifyIcon
- Suggests moving away from legacy implementations

**Audio Stack**:
- NAudio (multi-device handling)
- WASAPI (Windows audio routing)

**Implementation Strengths**:
- Real-time audio streaming to multiple endpoints
- Modern WPF patterns (tray integration)
- Actively maintained

**Recommendations for SoundFX**:
- Study tray icon implementation (Hardcodet.NotifyIcon NuGet)
- Review NAudio usage for device enumeration/routing
- Check how they handle real-time audio without UI freezes

**Architecture Insight**: Two UI versions suggest good separation between core audio logic and UI.

---

## 3. GLOBAL HOTKEY IMPLEMENTATION

### 3.1 Hotkeys Library (mrousavy)
**URL**: https://github.com/mrousavy/Hotkeys  
**Language**: C#  
**Framework**: .NET  
**Stars**: 85  
**Last Update**: Jul 30, 2020  

**What it does**: Small, focused library for binding global hotkeys to any Windows app window  

**Features**:
- ✅ Works with UWP, WPF, Win Forms  
- ✅ Works with Windows Apps (Explorer, etc.)  
- ✅ Clean event-based API  

**Why It Matters**:
- Alternative to raw P/Invoke keyboard hooks
- Less boilerplate, more reliable
- Better maintained than custom implementations

**Recommendation for SoundFX**:
- Check if available as NuGet package
- If yes: Consider replacing `KeyboardHookService` with this library
- If no: Keep current keyboard hook implementation but cross-reference patterns

**Integration Pattern**:
```csharp
// Conceptual (from docs)
hotkey.Register(modifier, key, callback);
// vs. current: Raw hook service
```

---

## 4. AUDIO DEVICE MANAGEMENT PATTERNS

### 4.1 From SoundSync & SonicBoard

**Device Enumeration**:
- Use WASAPI API (Windows.Media.Devices namespace or NAudio)
- Enumerate at startup + on device change events
- Store device GUIDs (not friendly names)

**Multi-Device Routing**:
- NAudio's `WaveOutEvent` or `IWavePlayer` instances per device
- One audio source → N output devices
- Sync playback across devices

**Device Fallback Logic**:
```
1. Load stored device GUID from config
2. Enumerate current devices
3. If device exists → use it
4. If not → fall back to default playback device
5. Log the fallback for user feedback
```

**Virtual Cable Detection**:
- Check device name for "VB Audio", "VBAN", etc.
- Monitor for device state changes (connected/disconnected)
- Graceful degradation if cable removed

**Error Handling Pattern**:
```
Try:
  - Initialize audio device
Catch AudioException:
  - Log error with device info
  - Show user notification
  - Fall back to default device
  - Allow manual device selection in UI
```

---

## 5. SETUP WIZARD PATTERNS

### 5.1 What We Found
- **Good news**: SoundFX already has `SetupWizardWindow.xaml` pattern ✅
- **Challenge**: Open-source setup wizard examples are rare
- **Reality**: Most use InnoSetup (ISS) files, not embedded dialogs

### 5.2 Recommended Pattern (from research + best practices)

**First-Run Wizard Steps**:

```
Step 1: Welcome
  - Show feature overview
  - Check Voicemeeter installation
  - Button: "Download Voicemeeter" if missing

Step 2: Audio Device Selection
  - Enumerate available devices
  - Show "Test" button (play tone)
  - Show virtual cable devices separately

Step 3: Hotkey Configuration (optional)
  - Explain global hotkeys
  - Test hotkey binding
  - Permission warning (admin may needed)

Step 4: Completion
  - Mark wizard as complete in config
  - Don't show again unless reset by user
```

**Wizard State Management**:
```
Config.Json:
{
  "SetupComplete": true,
  "SetupCompletedVersion": "1.0.0",
  "LastSetupRun": "2026-07-09T..."
}

Logic:
- First run: Show wizard
- On upgrade: Show only new steps (if version changed)
- User can re-run via Settings → "Reset Setup"
```

### 5.3 Voicemeeter Auto-Download (Advanced Pattern)

**Pattern from installers**:
1. Detect if Voicemeeter installed (check registry or filesystem)
2. If missing: Offer download
3. Provide direct link to Voicemeeter installer
4. Option: Download during setup or later
5. Skip if already installed

**Detection code pattern**:
```csharp
bool IsVoicemeeterInstalled()
{
  // Check registry: HKLM\Software\VB-Audio\Voicemeeter
  // OR check for RemoteAPI.dll in Program Files
  // OR try to load DLL via P/Invoke
}
```

---

## 6. KEY IMPLEMENTATION INSIGHTS

### 6.1 Audio Stack (Recommended)
```
[User Audio] 
    ↓
[NAudio library]  ← Multi-device handling
    ↓
[WASAPI API]      ← Low-level Windows audio
    ↓
[Voicemeeter / Virtual Cable / Physical Output]
```

### 6.2 Voicemeeter Integration
**Important**: Always wrap calls in try-catch
```csharp
try 
{
  voicemeeterAPI.SetDeviceMute(bus, true);
}
catch (DllNotFoundException)
{
  // Voicemeeter not installed
  logger.Warn("Voicemeeter not found");
}
catch (Exception ex)
{
  logger.Error("Voicemeeter API error", ex);
}
```

### 6.3 Device Identification
**❌ WRONG**: Use device name
```csharp
string deviceName = "Voicemeeter Aux Input"; // Changes with language/updates!
```

**✅ RIGHT**: Use device GUID
```csharp
string deviceGuid = "\\?\WASAPI\{GUID-HERE}"; // Stable across reboots
```

### 6.4 Tray Icon Pattern (Modern WPF)
**Recommendation**: Use Hardcodet.NotifyIcon
```xml
<!-- NuGet: Hardcodet.Wpf.TaskbarNotification -->
<tb:TaskbarIcon IconSource="/Resources/TrayIcon.ico" 
                LeftClickCommand="{Binding ShowCommand}">
  <tb:TaskbarIcon.ContextMenu>
    <ContextMenu>
      <MenuItem Header="Quit" Command="{Binding ExitCommand}"/>
    </ContextMenu>
  </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

---

## 7. RED FLAGS TO AVOID

| ❌ Anti-Pattern | ✅ Solution |
|---|---|
| Assuming Voicemeeter installed | Always check, offer download |
| Using device names for identification | Use device GUIDs |
| Hardcoding device indices (0, 1, 2) | Enumerate and validate |
| Forgetting NAudio resource cleanup | Dispose `IWavePlayer` properly |
| Setup wizard showing after every upgrade | Mark completed + version check |
| Raw keyboard hooks without error handling | Use mrousavy/Hotkeys or wrap safely |
| Not handling device disconnect events | Subscribe to device change notifications |
| Sync audio playback without timestamps | Use NAudio sync patterns or latency control |
| Storing device config without validation | Validate device exists on app restart |

---

## 8. ACTIONABLE RECOMMENDATIONS FOR SOUNDFX STUDIO

### Priority 1: Immediate (Stabilization)
- [ ] Add Voicemeeter presence check in `HotkeyService` and `ConfigService`
- [ ] Replace device name references with GUIDs in `AppConfig.cs`
- [ ] Wrap P/Invoke calls in all services with try-catch + logging
- [ ] Test what happens when Voicemeeter is uninstalled mid-session

### Priority 2: Next Release (Robustness)
- [ ] Extend `SetupWizardWindow` with device enumeration + test tones
- [ ] Add Voicemeeter download link in wizard
- [ ] Implement device change event listeners (NAudio `DeviceChange` events)
- [ ] Fallback logic when preferred device disappears

### Priority 3: Polish (UX Improvements)
- [ ] Refactor tray icon to Hardcodet.NotifyIcon (like SoundSync)
- [ ] Add profile switching (learn from SonicBoard)
- [ ] Persistent setup state (don't re-show after upgrade)
- [ ] Visual feedback for device enumeration (spinner, status text)

### Priority 4: Future (Advanced)
- [ ] Consider mrousavy/Hotkeys library if custom hooks become problematic
- [ ] Multi-device audio routing (like SoundSync)
- [ ] Virtual cable auto-detection + preset profiles
- [ ] Installer dependency management (WiX or InnoSetup advanced)

---

## 9. EXTERNAL DEPENDENCIES TO CONSIDER

**Already Used (SoundFX)**:
- ✅ NAudio (audio device handling)
- ✅ Custom `KeyboardHookService` (hotkey triggering)

**Recommended NuGet Packages**:
- `Hardcodet.Wpf.TaskbarNotification` (modern tray icon)
- `mrousavy/Hotkeys` (if available; global hotkey alternative)

**System Dependencies**:
- Windows WASAPI (built-in)
- Voicemeeter (optional; should handle gracefully if missing)

---

## 10. SIMILAR PROJECTS ANALYSIS MATRIX

| Project | Focus | Tech | Status | Relevance | Notes |
|---------|-------|------|--------|-----------|-------|
| VoiceMeeterWrapper | Voicemeeter API | C# P/Invoke | Old (2020) | ⭐⭐⭐⭐⭐ | Best reference for Voicemeeter wrapping |
| SonicBoard | Soundboard + Profiles | .NET 8 WPF | Active (2026) | ⭐⭐⭐⭐ | Modern stack, multi-device patterns |
| SoundSync | Audio routing | WPF+NAudio | Active (2026) | ⭐⭐⭐⭐ | Real-time multi-device, tray icon patterns |
| Hotkeys | Hotkey library | .NET | Old (2020) | ⭐⭐⭐ | Useful if replacing keyboard hooks |
| VoiceSpritz | VAC effects | C# | WIP | ⭐⭐ | Reference only; not core SoundFX pattern |

---

## 11. LIMITATIONS OF THIS RESEARCH

1. **GitHub Rate Limiting**: Exceeded API limits after ~15 searches; couldn't explore all results
2. **Setup Wizard Scarcity**: Most open-source projects don't include fancy setup wizards
3. **Voicemeeter Niche**: Very few public projects integrate Voicemeeter (proprietary/paid market)
4. **CodeProject Blocked**: Anti-bot measures prevented secondary search
5. **Timestamp**: Based on June-July 2026 GitHub snapshots; newer repos may exist

---

## 12. CONCLUSION

**Best-In-Class References for SoundFX**:
1. **VoiceMeeterWrapper** (tocklime) → Voicemeeter integration pattern
2. **SonicBoard** (Kunal-CodeLab) → WPF architecture, profiles
3. **SoundSync** (sugumar247) → NAudio multi-device routing + tray icon patterns

**Immediate Next Steps**:
1. Review VoiceMeeterWrapper source for P/Invoke safety patterns
2. Study SoundSync's tray icon and device enumeration code
3. Extend setup wizard with device detection (see Section 5.2)
4. Add Voicemeeter presence checks throughout services

**Long-term Vision**:
- Combine SonicBoard's profile system with SoundFX's focus on hotkey efficiency
- Implement SoundSync-style multi-device routing for advanced users
- Build installer with optional Voicemeeter auto-download

---

**Research Compiled**: 2026-07-09  
**Researcher**: GitHub Copilot  
**Status**: Ready for implementation reference
