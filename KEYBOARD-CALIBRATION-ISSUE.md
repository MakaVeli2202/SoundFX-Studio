# Bottom Row Calibration Issue

## Problem
The bottom row keys (Z/X/C/V/B/N/M/,/./right Shift) are misaligned — wrong size and position compared to other rows. The `.` and `/` keys on the main keyboard were also misclassified as NumpadCluster.

## Root Cause

### 1. `GetCluster` misclassification (FIXED)
`KeyboardLayoutPanel.cs:351` — `-`, `.`, `/` on main keyboard rows were assigned to `NumpadCluster` because `KeyName` matched numpad symbols. Added `ColumnIndex >= 20.25` guard to only match actual numpad keys.

**Side effect:** `.` and `/` on bottom row now in `MainTypingCluster` (X offset ~191px shift). Their per-key calibration offsets were calibrated for `NumpadCluster` position, so they'll be displaced until recalibrated.

### 2. Project calibration OVERRIDES config.json
`ConfigService.cs:209` — `ApplyProjectCalibrationIfAvailable` replaces the entire `KeyboardCalibration` object with `keyboard-calibration.json` contents. Config.json `KeyOverrides` are ignored at runtime.

### 3. `ResolveKeyboardLayoutMode()` is hardcoded
`MainViewModel.cs:2155` — Always returns `KeyboardLayoutMode.EnglishUS` regardless of config setting. Layout is always ANSI (no OEM102 key). ISO key IDs in calibration won't match.

## Files Responsible

| File | Role |
|------|------|
| `Controls/KeyboardLayoutPanel.cs` | Key positioning, cluster assignment, per-key calibration |
| `Controls/KeyboardClusterLayout.cs` | Cluster definitions and offset calibration |
| `Controls/KeyboardControl.xaml` | Key button template (size, image fill, inner section) |
| `Converters/InnerSectionMarginConverter.cs` | Inner image margin from per-key insets |
| `Models/KeyboardKey.cs` | Key model (RowIndex, ColumnIndex, WidthUnits) |
| `ViewModels/MainViewModel.cs:2155` | `ResolveKeyboardLayoutMode()` hardcoded to EnglishUS |
| `Services/ConfigService.cs:189-234` | `ApplyProjectCalibrationIfAvailable` overrides config |
| `Services/KeyboardLayoutService.cs` | Key layout generation (ANSI vs ISO) |
| `keyboard-calibration.json` | Project-level per-key calibration (OVERRIDES config.json) |
| `config.json` | User config KeyOverrides (ignored at runtime) |

## To Fix

1. Recalibrate `.` and `/` per-key offsets in `keyboard-calibration.json` for `MainTypingCluster` position (reduce OffsetX by ~191)
2. Visually verify all bottom row keys align with other rows
3. Consider fixing `ResolveKeyboardLayoutMode()` to read from config instead of hardcoding EnglishUS
