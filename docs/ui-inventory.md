# UI inventory

## MainWindow
- Purpose: Primary shell for soundboard, library, effects, settings, and navigation.
- Current layout: Hero section at the top, followed by page-specific content with a left-side navigation experience.
- Main controls: Navigation buttons, hero action button, soundboard content, library tiles, voice changer controls, settings sections.
- Important user actions: Open keyboard, browse and trigger sounds, edit library entries, configure voice changer, open settings.
- Existing navigation: In-window page switching with button-based navigation.
- Existing keyboard interaction: Keyboard window launch and sound triggering through keyboard mappings.
- Current visual problems: Page sections feel loosely connected, hero content is visually dominant without a clear relationship to the rest of the shell, and the layout is still more collage-like than guided.
- Current UX problems: The primary action hierarchy is not immediately obvious for first-time users.
- Proposed redesign: Improve page grouping, strengthen the hero-to-content relationship, make primary actions more obvious, and introduce more consistent section structure throughout the shell.
- Must remain untouched: Sound playback, keyboard-launch behavior, settings persistence, and existing commands.

## KeyboardWindow
- Purpose: Floating keyboard for assigning and triggering sounds from the physical keyboard layout.
- Current layout: Large keyboard surface with visible key states and overlay interactions.
- Main controls: Keyboard keys, assignment/selection affordances, status indicators.
- Important user actions: View assigned sounds, select/edit keys, trigger sounds.
- Existing navigation: Driven from the main app and selection state.
- Existing keyboard interaction: Core interaction surface for key assignment and playback.
- Current visual problems: The keyboard is functional but lacks a strong visual hierarchy for empty vs assigned keys and selected state.
- Current UX problems: It is harder than it should be to understand what is assigned, what is empty, and what is currently selected.
- Proposed redesign: Improve key state clarity, make selected/editing state more obvious, and strengthen empty vs populated distinctions.
- Must remain untouched: Key assignment behavior, playback behavior, and all mapping logic.

## SetupWizardWindow
- Purpose: First-run audio setup and Voicemeeter/device routing guidance.
- Current layout: Header, description, device selection panels, status area, footer actions.
- Main controls: Device combo boxes, setup actions, status text, finish/close controls.
- Important user actions: Choose hear/talk devices, run auto setup, confirm setup.
- Existing navigation: Linear wizard flow with footer actions.
- Existing keyboard interaction: Standard dialog interaction and focus traversal.
- Current visual problems: The flow is functional but not yet structured like a guided workflow.
- Current UX problems: The intent of each step and the current state of the setup are not as clear as they could be.
- Proposed redesign: Introduce stronger step hierarchy, clearer status, and better grouping of selection controls.
- Must remain untouched: Device selection logic, routing auto-setup, and persistence behavior.

## VoicemeeterPanel
- Purpose: Advanced mixer and audio routing surface for Voicemeeter.
- Current layout: Header, quick mute bar, auto-setup toolbar, dynamically generated strip cards, footer actions.
- Main controls: Mute buttons, strip cards, volume sliders, status text, action buttons.
- Important user actions: Mute hear/team/all, adjust strip gain, rename strips, remove device assignments, open Voicemeeter, test hear.
- Existing navigation: Modal dialog with footer actions.
- Existing keyboard interaction: Standard focus navigation and text rename entry.
- Current visual problems: The interface is technically useful but visually dense, with weak grouping and unclear state hierarchy.
- Current UX problems: The user has to infer what each section means and how the routing controls relate to each other.
- Proposed redesign: Create clearer summary/status structure, improve grouping for mute actions and strip cards, strengthen affordances, and reduce visual clutter.
- Must remain untouched: Voicemeeter routing behavior, strip state updates, and the underlying control logic.

## Voice changer UI
- Purpose: Control real-time voice effects and preset selection.
- Current layout: Preset picker, pitch slider, formant slider, and status/context content.
- Main controls: Preset combo box, sliders, status text, on/off actions.
- Important user actions: Choose preset, adjust pitch/formant, enable or disable processing.
- Existing navigation: Part of the main window pages.
- Existing keyboard interaction: Standard keyboard and mouse control.
- Current visual problems: The content is stacked but not clearly grouped around a simple mental model.
- Current UX problems: The flow from input to effect to output is not explicit enough.
- Proposed redesign: Introduce clearer grouping around an input-effect-output flow and strengthen the active state.
- Must remain untouched: Voice processing behavior, preset selection logic, and underlying audio pipeline.

## Library
- Purpose: Browse, search, edit, and manage stored sounds.
- Current layout: Toolbar, search bar, tile-based sound list.
- Main controls: Add, URL, microphone, delete, search, edit actions.
- Important user actions: Add sounds, delete marked sounds, search, edit or select sounds.
- Existing navigation: Library page within the main shell.
- Existing keyboard interaction: Search input, selection, and keyboard shortcuts for editing.
- Current visual problems: Tiles are visually strong but the page does not yet guide the user toward quick actions and the selected/editing state is not always obvious.
- Current UX problems: The page needs clearer action hierarchy and more purposeful empty-state handling.
- Proposed redesign: Improve the layout around the primary actions, strengthen selection/editing feedback, and create more refined empty and search states.
- Must remain untouched: Sound import, deletion, editing, and persistence.

## Soundboard
- Purpose: Present the active soundboard experience for rapid sound playback.
- Current layout: Page content focused on sound triggers and available actions.
- Main controls: Sound buttons, volume or state indicators, category/selection affordances.
- Important user actions: Find and trigger sounds quickly.
- Existing navigation: Main shell page content.
- Existing keyboard interaction: Keyboard-triggered and direct interaction-driven playback.
- Current visual problems: The current visuals are somewhat decorative and don’t yet clearly prioritize quick action and readability.
- Current UX problems: The interface should better emphasize speed, discoverability, and state clarity.
- Proposed redesign: Develop a reusable, purpose-driven sound button treatment with clear default, hover, pressed, playing, selected, and disabled states.
- Must remain untouched: Playback and trigger behavior.

## Presets
- Purpose: Manage and apply preset configurations.
- Current layout: Preset list or preset-related content within the main shell.
- Main controls: Preset selection and actions.
- Important user actions: Create, load, and manage presets.
- Existing navigation: Main shell page/view.
- Existing keyboard interaction: Standard interaction and selection.
- Current visual problems: Preset interfaces need stronger hierarchy and clearer action emphasis.
- Current UX problems: The user should more clearly understand what a preset contains and how it is applied.
- Proposed redesign: Use more structured content blocks and clearer primary/secondary actions.
- Must remain untouched: Preset management logic and persistence.

## Settings
- Purpose: Configure general, audio, keybinding, and advanced options.
- Current layout: Category rail with content area.
- Main controls: Section nav buttons, toggles, buttons, lists.
- Important user actions: View/edit settings, open audio setup, configure keybindings.
- Existing navigation: Settings section switching in the main shell.
- Existing keyboard interaction: Standard navigation and form controls.
- Current visual problems: The category rail and content area need stronger grouping and clearer spacing.
- Current UX problems: Some settings could be clearer and more scannable.
- Proposed redesign: Introduce more deliberate section cards, stronger labels, and cleaner grouping for each setting category.
- Must remain untouched: Settings values, persistence, and behavior.

## Team Monitor
- Purpose: Provide a preview of the current team mix and the monitored audio path.
- Current layout: Monitor content with device and route information.
- Main controls: Device information, status text, action controls.
- Important user actions: Review the current monitoring setup.
- Existing navigation: Modal dialog from the Voicemeeter workflow.
- Existing keyboard interaction: Standard dialog interaction.
- Current visual problems: It is functional but still reads as a technical panel rather than a guided status screen.
- Current UX problems: The status should be easier to understand at a glance.
- Proposed redesign: Clarify its purpose as a monitoring summary and present route/status information more clearly.
- Must remain untouched: Monitoring logic and device lookup behavior.

## Toast/notification windows
- Purpose: Surface transient feedback and states.
- Current layout: Small notification-style windows.
- Main controls: Message content and action buttons where needed.
- Important user actions: Acknowledge or act on notifications.
- Existing navigation: Modal or transient in-app feedback.
- Existing keyboard interaction: Standard close and action input.
- Current visual problems: Notification surfaces should feel lightweight and focused.
- Current UX problems: They should be easier to read and less visually noisy.
- Proposed redesign: Keep them compact, focused, and consistent with the larger dialog system.
- Must remain untouched: Triggering and dismissal behavior.

## Dialogs
- Purpose: Support focused tasks such as adding sounds, editing names, capturing keys, changing volume, and confirming actions.
- Current layout: Windowed dialogs with title bar, body, and footer actions.
- Main controls: Form inputs, buttons, list selectors, capture controls.
- Important user actions: Enter text, choose values, save or cancel, capture inputs.
- Existing navigation: Modal and centered over the parent window.
- Existing keyboard interaction: Standard dialog and input controls.
- Current visual problems: Many dialogs feel similar in shape and spacing, even when their purpose differs.
- Current UX problems: They should communicate their intent more clearly through hierarchy and purpose-specific layout.
- Proposed redesign: Introduce clearly differentiated variants for information, input, selection, confirmation, and progress-oriented dialogs.
- Must remain untouched: Their data entry and command behavior.
