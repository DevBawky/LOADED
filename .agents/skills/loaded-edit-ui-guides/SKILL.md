---
name: loaded-edit-ui-guides
description: Change LOADED first-run guides, gameplay HUD, tooltips, dictionaries, combat presentation, camera feedback, or non-domain UI composition. Use for FirstRunGuideController, FirstRunGuideContent, CombatPresentation, CombatFeedbackController, dictionary panels, and cross-system UI; use a domain skill too when UI changes gameplay state.
---

# Edit LOADED UI, Feedback, and Guides

## Responsibility boundary

UI reads authoritative state, renders it, and forwards user intent. It must not own bullets, relics, inventory, currency, enemy decisions, turn state, saves, or statistics. Presentation callbacks may react to a result but must not decide whether that result occurred.

- `FirstRunGuideController` coordinates tutorial progress and UI; `FirstRunGuideContent` holds immutable guide content.
- `CombatPresentation`, `CombatFeedbackController`, camera feedback, health feedback, and damage numbers render combat outcomes.
- Dictionary, tooltip, inventory, action queue, and result panels project existing data and events.

Do not split a large UI class solely by widget count. Extract layout calculation, immutable content, formatting, animation/presentation, or input adaptation when that responsibility changes independently and can receive explicit state.

Prefer plain C# formatters and presenters that receive explicit display state.
Keep serialized view references and lifecycle wiring in the existing Unity
facade, and keep authoritative manager mutation outside the presenter. UI and
preview code must call shared domain rules rather than recreating eligibility,
damage, reward, or progression logic.

## Change workflow

Search exact hierarchy names, resource paths, animation states/events, sound IDs, and builder scripts before modifying a view. Prefer serialized references; preserve established bootstrap fallbacks. Match every event subscription with unsubscription and avoid anonymous handlers that cannot be removed.

Make animations, coroutines, and delayed callbacks tolerate disabled or destroyed objects. Use unscaled time for pause/loading presentation where required. Never make turn completion, damage application, rewards, or guide progress depend on an optional visual listener finishing.

If a generated scene or prefab changes, update the relevant `Assets/Editor/*Builder.cs` source and run it only when the task authorizes serialized asset regeneration.

## Verify

Compile, run `SceneIntegrityTests` when scene wiring is affected, and manually inspect every affected resolution/state. Check missing references, duplicated listeners, rapid open/close, scene transition, pause, object disable/destruction, localization/TMP overflow, hover/selection, and feedback ordering.

Read `Docs/Dev/0809_FirstRunGuide.md` for guide work and the specific presentation document under `Docs/Dev/` for combat feedback changes.
