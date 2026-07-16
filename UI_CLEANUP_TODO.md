# UI Cleanup TODO (Current Session)

## Done in this pass
- [x] Simplified calibration architecture: removed cluster/special-key logic from runtime.
- [x] Replaced with global controls: move all X, horizontal gap, vertical gap.
- [x] Kept and expanded per-key calibration.
- [x] Added inner section width/height controls (global + per-key adjustments).
- [x] Moved deep inner calibration to Calibration window.
- [x] Deleted obsolete cluster logic file.
- [x] Reorganized keyboard settings panel to compact quick controls.
- [x] Wired working Light/Dark theme application in main window.
- [x] Normalized saved theme values to avoid invalid legacy values.
- [x] Relabeled misleading "Language" selector to "Keyboard Layout".

## Remaining high-priority tasks
- [ ] Polish all production ComboBox/DropDown styles in main app windows.
- [ ] Verify/fix device dropdown behavior and selection rendering.
- [ ] Extend theme switching (light/dark) to all dialogs/windows, not only main window.
- [ ] Implement true UI language selection flow (currently keyboard layout selection only).
- [ ] Remove remaining dead code/files after theme/language pass.
- [ ] Final UX spacing/alignment pass and screenshot-based visual audit.

## Notes from user requirements
- Calibration flow should stay minimal and practical.
- Inner section must support width/height + X/Y movement globally and per-key.
- Keep software looking production-level (no clipping/misalignment/artifacts).
