# SoundFX Studio: Implementation Checklist from Research

## 🎯 Quick Reference Checklist

### Voicemeeter Integration (From VoiceMeeterWrapper Pattern)

**Detection & Safety**
- [ ] Create `VoicemeeterDetection` utility class
- [ ] Check registry: `HKLM\Software\VB-Audio\Voicemeeter`
- [ ] Attempt P/Invoke RemoteAPI.dll load with try-catch
- [ ] Store result in app config (check on startup)
- [ ] Show warning if not installed on first use

**Service Hardening**
- [ ] Wrap all Voicemeeter API calls in try-catch
- [ ] Log every Voicemeeter operation (routing, mute, A1/B1 changes)
- [ ] Implement graceful degradation if Voicemeeter crashes
- [ ] Test: What happens when Voicemeeter is uninstalled mid-session?
- [ ] Test: What happens if Voicemeeter restarts?

---

### Audio Device Management (From SoundSync Pattern)

**Device Identification**
- [ ] Replace device name references with GUIDs in `AppConfig.cs`
- [ ] Update `AudioDeviceService` to store device GUIDs
- [ ] Validate device GUID exists on app restart
- [ ] Add fallback to default device if GUID not found
- [ ] Log all device fallback decisions

**Device Enumeration**
- [ ] Use WASAPI (via NAudio) to enumerate devices
- [ ] Separate physical outputs from virtual cables
- [ ] Identify Voicemeeter AUX input/output specifically
- [ ] Cache device list, refresh on user request
- [ ] Monitor for device change events (add/remove headsets, etc.)

**Refresh & Validation**
- [ ] Add "Refresh Audio Devices" button in UI
- [ ] Implement device change event listener
- [ ] Auto-validate device on app focus (in case device was unplugged)
- [ ] Show user notification on device fallback

---

### Global Hotkeys (Current + Optional Improvements)

**Current Implementation (KeyboardHookService)**
- [ ] Review error handling in keyboard hook
- [ ] Test with: locked screen, game windows, elevated apps
- [ ] Test with: multiple hotkeys simultaneously
- [ ] Document any Windows versions where it fails

**Optional: mrousavy/Hotkeys Migration**
- [ ] Check if NuGet package available
- [ ] If yes: Prototype using library
- [ ] Compare reliability vs. current implementation
- [ ] Benchmark performance (unlikely to be issue)

**Hotkey Validation**
- [ ] Display current hotkeys in UI
- [ ] Allow user to test hotkey binding
- [ ] Check for conflicts with system hotkeys
- [ ] Handle case: user disables hotkey, app doesn't respond

---

### Setup Wizard (SetupWizardWindow Enhancements)

**Step 1: Voicemeeter Check**
- [ ] Detect Voicemeeter installed
- [ ] If missing: Show "Download Voicemeeter" button
- [ ] Link to: https://vb-audio.com/Voicemeeter/
- [ ] Skip if already installed
- [ ] Option to skip/come back later

**Step 2: Audio Device Discovery**
- [ ] Enumerate all audio output devices
- [ ] Show: Name, format, channels
- [ ] Add "Test" button for each device (play 1sec tone)
- [ ] Highlight recommended device (default playback)
- [ ] Separate section for virtual cables
- [ ] Allow user to select default device

**Step 3: Hotkey Test (Optional)**
- [ ] Explain what global hotkeys do
- [ ] Permissions warning (may need admin on some Windows versions)
- [ ] Test hotkey binding in setup (press Ctrl+Shift+P to test)
- [ ] Show confirmation if hotkey works

**Step 4: Completion**
- [ ] Show summary of choices
- [ ] Write `SetupComplete` flag to config
- [ ] Store `SetupCompletedVersion` = current app version
- [ ] Allow "Back" to change choices
- [ ] Button: "Finish" → close wizard

**Persistent State**
- [ ] Mark setup as complete in `AppSettings`
- [ ] On app upgrade: Compare version, only re-show if new features
- [ ] Add "Reset Setup" option in Settings/About
- [ ] Never force re-run unless user requests

---

### Architecture Improvements (From SonicBoard/SoundSync)

**Service Layer Organization**
- [ ] Review current `Services/` folder structure
- [ ] Ensure separation: Audio, Device, Hotkey, Config
- [ ] Each service should have clear single responsibility
- [ ] Add unit tests for each service

**Profile/Preset System** (SonicBoard Pattern)
- [ ] Consider adding profile switching (advanced feature)
- [ ] Store multiple audio device configurations
- [ ] Allow quick switch between gaming/streaming/meeting profiles
- [ ] Not required for v1, but plan for it

**Tray Icon Modernization** (SoundSync Pattern)
- [ ] Replace current tray implementation with Hardcodet.NotifyIcon
- [ ] NuGet: `Hardcodet.Wpf.TaskbarNotification`
- [ ] Implement context menu: Show/Hide, Settings, Exit
- [ ] Show status icon (green = ready, gray = no Voicemeeter, etc.)
- [ ] Double-click to show main window

---

### Error Handling Patterns

**Try-Catch Everywhere**
```csharp
// Pattern for all external API calls
try
{
  // Voicemeeter, NAudio, or system calls
  result = externalAPI.DoSomething();
}
catch (DllNotFoundException ex)
{
  // Handle missing library (Voicemeeter not installed)
  logger.Error("Voicemeeter not found", ex);
  notifyUser("Voicemeeter not installed. Download from vb-audio.com");
}
catch (AudioException ex)
{
  // Handle audio-specific errors
  logger.Error("Audio device error", ex);
  fallbackToDefaultDevice();
}
catch (Exception ex)
{
  // Catch-all
  logger.Error("Unexpected error", ex);
  notifyUser("An error occurred. Check logs.");
}
```

**Logging**
- [ ] Log all Voicemeeter operations
- [ ] Log all device enumerations
- [ ] Log all hotkey registrations/triggers
- [ ] Log all device fallbacks
- [ ] Include timestamps, device IDs, operation names

**User Notifications**
- [ ] Show toast notifications for device changes
- [ ] Show status in UI for current device
- [ ] Show warning if Voicemeeter not installed (once per session)
- [ ] Provide actionable next steps (download link, etc.)

---

### Testing Checklist

**Voicemeeter Scenarios**
- [ ] App starts, Voicemeeter not installed
- [ ] App starts, Voicemeeter installed
- [ ] Voicemeeter is running, then crashes
- [ ] Voicemeeter is uninstalled while app is running
- [ ] Voicemeeter is upgraded while app is running
- [ ] All Voicemeeter routing operations work (A1/B1 mute, etc.)

**Device Scenarios**
- [ ] App starts with last device connected
- [ ] App starts with last device disconnected (fallback)
- [ ] Headphones plugged in during app running
- [ ] Headphones unplugged during app running
- [ ] USB audio device disconnected
- [ ] Virtual cable disconnected
- [ ] Multiple device changes rapidly (stress test)

**Hotkey Scenarios**
- [ ] Hotkey works on desktop
- [ ] Hotkey works in game (fullscreen)
- [ ] Hotkey works when app is minimized
- [ ] Hotkey works when screen is locked
- [ ] Hotkey works with VirtualBox/VM running
- [ ] Multiple hotkeys triggered in quick succession

**Setup Wizard**
- [ ] First run shows wizard
- [ ] Closing wizard without completing doesn't mark complete
- [ ] Completing wizard marks complete + stores version
- [ ] App upgrade: if version changed, show new steps
- [ ] "Reset Setup" in settings re-shows wizard
- [ ] Wizard skipped on app restart (after completion)

---

### Performance & Resource Management

**Audio Streaming**
- [ ] Dispose NAudio `IWavePlayer` instances properly
- [ ] Don't keep audio devices open unnecessarily
- [ ] Monitor for memory leaks during long sessions
- [ ] Test with: multiple rapid sound triggers

**Keyboard Hook**
- [ ] Hook should not block UI thread
- [ ] Hook should not consume excessive CPU
- [ ] Unregister hook on app shutdown
- [ ] Test with: 100+ hotkey presses per minute

**Device Enumeration**
- [ ] Enumeration should be cached
- [ ] Background refresh thread safe
- [ ] Don't enumerate on every hotkey trigger
- [ ] Batch device changes (don't notify UI for each change)

---

### Documentation to Add

**In Code**
- [ ] XML comments on all public methods
- [ ] Explain why we use GUIDs not device names
- [ ] Explain Voicemeeter dependency and error handling
- [ ] Explain hotkey threading model

**In README**
- [ ] System requirements (Windows 10+, .NET 8)
- [ ] Optional dependency: Voicemeeter
- [ ] First-run setup instructions
- [ ] Troubleshooting guide (Voicemeeter not found, etc.)
- [ ] Known limitations (e.g., doesn't work on locked screen if Windows 11 security policy)

**In RESEARCH_FINDINGS.md** (already created)
- [ ] Links to reference projects
- [ ] Implementation patterns
- [ ] Known gotchas

---

### Priority Order for Implementation

**Week 1: Foundation**
1. Voicemeeter detection + error handling
2. Device GUID refactoring
3. Enhanced logging

**Week 2: Setup Wizard**
4. Device enumeration + test UI
5. Voicemeeter check + download link
6. Completion state persistence

**Week 3: Polish**
7. Tray icon modernization (Hardcodet)
8. Device change event listeners
9. User notifications

**Week 4: Testing**
10. Comprehensive test matrix
11. Documentation
12. Release

---

### Reference Links

- **VoiceMeeterWrapper**: https://github.com/tocklime/VoiceMeeterWrapper
- **SonicBoard**: https://github.com/Kunal-CodeLab/SonicBoard
- **SoundSync**: https://github.com/sugumar247/SoundSync
- **Voicemeeter Official**: https://vb-audio.com/Voicemeeter/
- **NAudio Docs**: https://github.com/naudio/NAudio
- **Hardcodet.NotifyIcon**: https://www.nuget.org/packages/Hardcodet.Wpf.TaskbarNotification

---

**Last Updated**: 2026-07-09  
**Status**: Ready for implementation sprint planning
