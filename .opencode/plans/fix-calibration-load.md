# Fix keyboard calibration not loading + window scale

## Problem
1. `ApplyProjectCalibrationIfAvailable()` is missing from the normal config load path — all key overrides in `keyboard-calibration.json` silently ignored
2. `KeyboardWindowScale` defaults to `1.0` but should be `0.85`
3. Migration check `<= 0` never catches `1.0` default

## Changes

### 1. `SoundFXStudio/Services/ConfigService.cs` — add project calibration to normal load path

At line 47 (after `if (migrated) Save(normalized);` and before `_logService?.Info("Config Loaded");`):

```cs
            ApplyProjectCalibrationIfAvailable(normalized);
```

**Result (around line 46-50):**
```cs
            if (migrated)
            {
                _logService?.Info("Config Migration Executed");
                Save(normalized);
            }
            ApplyProjectCalibrationIfAvailable(normalized);  // ← ADD
            _logService?.Info("Config Loaded");
            return normalized;
```

### 2. `SoundFXStudio/Models/KeyboardCalibrationSettings.cs:17` — fix default scale

```cs
// Before:
public double KeyboardWindowScale { get; set; } = 1.0;
// After:
public double KeyboardWindowScale { get; set; } = 0.85;
```

### 3. `SoundFXStudio/Services/ConfigService.cs:165` — fix migration check

```cs
// Before:
if (config.Settings.KeyboardCalibration.KeyboardWindowScale <= 0)
// After:
if (config.Settings.KeyboardCalibration.KeyboardWindowScale < 0.5)
```

### 4. `SoundFXStudio/Services/ConfigService.cs:205` — same check in ApplyProjectCalibrationIfAvailable

```cs
// Before:
if (calibration.KeyboardWindowScale <= 0)
// After:
if (calibration.KeyboardWindowScale < 0.5)
```

### 5. `SoundFXStudio/keyboard-calibration.json` — fix scale value

Find `"KeyboardWindowScale": 1` and change to `"KeyboardWindowScale": 0.85`

## After applying
1. Restart app
2. Keyboard should open at 85%
3. Key overrides from `keyboard-calibration.json` should apply
4. If buttons still look wrong, recalibrate via Advanced → Calibration
