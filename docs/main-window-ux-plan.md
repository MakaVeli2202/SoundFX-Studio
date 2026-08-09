# MainWindow UX plan

## Current regions

1. Title bar
   - Purpose: window chrome and quick access to calibration.
   - Controls: app title, calibration button, minimize/close.
   - State: static shell chrome.
   - Problems: visually separate from the rest of the app and not tied to the page context.

2. Left navigation rail
   - Purpose: move between Home, Library, Effects, and Settings.
   - Controls: nav buttons and audio status summary.
   - State: current page, output/input devices, soundboard active state.
   - Problems: useful but visually dense and competing with the main content.

3. Main content surface
   - Purpose: page-specific work area.
   - Current pages: Home hero, Library, Effects, Settings.
   - Controls: page-specific content plus status text in the footer.
   - Problems: the home surface is more decorative than operational, and the page hierarchy is not obvious at a glance.

4. Library workspace
   - Purpose: browse, search, add, edit, and trigger sounds.
   - Controls: add buttons, search, tile grid, context menu.
   - State: search text, selected category, favorites filter, visible sounds.
   - Problems: the main task is present, but the surrounding surface does not explain what the user should do first.

5. Effects / voice changer workspace
   - Purpose: expose voice changer controls.
   - Controls: start/stop, preset dropdown, pitch/formant sliders.
   - State: runtime status, preset selection, monitor toggle.
   - Problems: advanced controls are present but not grouped into a clear primary/secondary hierarchy.

6. Settings workspace
   - Purpose: audio, keybinds, advanced routing.
   - Controls: section rail, content cards, combo boxes, toggle buttons.
   - State: selected section and persisted settings.
   - Problems: powerful, but dense and technical.

## New hierarchy

### What should the user see first?
The home surface should feel like a launchpad for the most common actions: open the keyboard, browse sounds, check audio, and reach voice changer or settings quickly.

### What is the primary action?
Triggering or finding a sound quickly remains the most important task. The experience should make that path obvious without overloading the page.

### What is secondary?
Voice changer controls, settings, and advanced routing should remain available but appear as supporting actions rather than the dominant visual focus.

### What information should always be visible?
The current page context, audio status, and the app’s primary action path should be visible at a glance.

### What information should be contextual?
Detail-heavy controls, advanced routing, and larger configuration sections should stay in their own pages and appear only when needed.

### What can be moved into secondary views?
The most technical routing and deep settings details can remain on the dedicated Settings and Effects areas rather than competing with the home experience.

### What should be accessible in one click?
- Open keyboard
- Sound library
- Voice changer
- Settings
- Audio status

### What should require a dialog/settings page?
- device configuration
- keybinding editing
- advanced routing
- calibration

## Stage A and B direction

- Preserve the existing hero image, keyboard-open button, and keyboard command.
- Improve the hero composition with stronger contrast, more deliberate spacing, and a better relationship to the content below.
- Keep the existing navigation model, but make it easier to scan and understand.
- Add a lightweight set of home actions that guide the user toward the main workflows without changing underlying functionality.
