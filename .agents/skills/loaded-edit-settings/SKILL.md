---
name: loaded-edit-settings
description: Change LOADED settings, pause behavior, accessibility, graphics saturation, presentation intensity, audio controls, video playback preferences, or settings UI. Use for MainMenuSettingsController, GamePauseController, CombatAccessibilitySettings, GraphicsSaturationSettings, and related preference wiring.
---

# Edit LOADED Settings and Accessibility

## Ownership

- `MainMenuSettingsController` binds menu controls to existing setting owners.
- `GamePauseController` owns pause panels, time-scale behavior, and escape priority.
- `CombatAccessibilitySettings`, `GraphicsSaturationSettings`, and `OldMoviePresentationSettings` own their presentation preferences.
- `SoundManager` owns audio volumes and audio preference keys; use the audio skill for volume behavior.
- Video controllers own playback state, not gameplay state.

Keep settings UI as a binding layer. Do not duplicate preference persistence in each screen or let a disabled panel become the authoritative setting value.

## Change workflow

Search every reader and writer of an affected PlayerPrefs key before changing it. Preserve existing keys and defaults unless a migration is requested. Apply a setting immediately to current runtime components and ensure future scenes initialize from the same persisted owner.

Pause changes must preserve the established Escape-panel priority and restore the previous time scale and input availability exactly once. Use unscaled time for presentation that must continue while paused. Settings and pause overlays must tolerate scene loads and missing optional visual components.

## Verify

Compile and test the setting from main menu and battle contexts. Check first launch default, changed value, scene transition, application restart, reset behavior, pause/resume, Escape ordering, time scale, input lock, saturation/old-film intensity, audio volume, and WebGL behavior where relevant.

Read the settings, pause, and presentation sections of `Docs/Dev/0808_Conversation_Implementation_Log.md` and `Docs/Dev/0809_WebGL_Save_And_Loading.md` only when those paths are affected.
