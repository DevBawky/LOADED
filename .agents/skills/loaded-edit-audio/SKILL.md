---
name: loaded-edit-audio
description: Change LOADED audio playback, BGM selection, volume preferences, sound IDs, or UI and enemy sound feedback. Use for Assets/Scripts/Sound and audio asset wiring; do not use for visual-only combat feedback.
---

# Edit LOADED Audio

## Ownership

- `SoundManager` owns persistent playback, source lifetime, BGM/SFX volume, and audio preference keys.
- `SoundtrackDirector` chooses soundtrack intent from the active scene.
- `SoundClipLibrary` and `Assets/Resources/Sound/SoundClipLibrary.asset` own clip, playlist, and string-ID mappings.
- `UiButtonFeedbackInstaller`, `UiButtonAudioFeedback`, and `UiButtonSpriteHoverScale` own button decoration and feedback.
- `EnemyAnimationSfx` bridges animation events to sound playback; enemy rules stay in enemy code.

Keep scene selection, UI discovery, animation timing, and gameplay decisions outside `SoundManager`. It may execute audio intent but must not own the originating rule.

## Change workflow

Search every consumer of an affected sound ID, scene name, button name, and preference key before editing. For a new sound, update both the library asset and every ID consumer; do not silently fall back to a similarly named clip.

Preserve `Audio.BGM.Volume` and `Audio.SFX.Volume` compatibility. Volume controls should delegate to `SoundManager` rather than create another persistence path. Avoid overlapping persistent audio objects across scene loads, and make fades/coroutines tolerate scene changes or destroyed objects.

## Verify

Compile in Unity, then check affected scenes in Play Mode for BGM selection and transition, SFX routing, mute/volume persistence, duplicate sources, missing-ID warnings, and UI hover/click behavior. If animation events changed, test all affected clips and interrupted death/disable paths.

Read audio-related sections in `Docs/Dev/0727_CombatPresentation.md`, `Docs/Dev/0802_EnemyData_Template_and_ActionPresentation.md`, or `Docs/Dev/0803_CombatFeedback_FullscreenImpact.md` only when that presentation path is involved.
