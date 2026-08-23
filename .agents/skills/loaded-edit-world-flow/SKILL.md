---
name: loaded-edit-world-flow
description: Change LOADED game-flow states, scene navigation, loading transitions, node-map generation and progress, stages, battle selection, events, shop/treasure scene routing, or ending transitions. Use for StateManager, LoadingTransitionController, NodeMap systems, StageData, and Assets/Scripts/Event.
---

# Edit LOADED World and Scene Flow

## Ownership

- `StateManager` coordinates battle flow and exits.
- `LoadingTransitionController` owns normal asynchronous scene navigation and its persistent transition UI.
- `NodeMapSystem` coordinates map interaction; `NodeMapGenerator` owns deterministic generation; `NodeMapSaveSystem` owns map persistence.
- `StageData` and `BattleData` are authored progression/configuration.
- `EventDefinition` is authored content; `EventSelector` chooses from run context; `EventSceneController` applies choices and effects.
- Shop and treasure controllers own their scene-local choice UI, then return through the shared transition path.

`ProjectSettings/EditorBuildSettings.asset` is the source of truth for enabled scene names and order. Do not revive the obsolete `Stage 1` route without tracing and intentionally migrating current navigation.

## Preserve flow invariants

- Use `LoadingTransitionController` for normal navigation and keep the established direct `SceneManager.LoadScene` fallback.
- Commit gameplay, reward, statistics, and save state before beginning a scene transition.
- Prevent duplicate transition requests and callbacks after the source scene is destroyed.
- Preserve node-map seed and RNG consumption; UI preview must not alter future generation or event selection.
- Keep node reachability, completed/selected state, stage/battle indices, and saved map consistent.
- Event choice requirements and effects must share the same run context and update persistence after a successful choice.

## Resumable interaction states

Model multi-step event, shop, treasure, and transition interactions as explicit
runtime states with legal transitions instead of scattering magic stage values
and related fields across UI methods. Keep the scene controller as the
orchestrator, keep presentation state non-authoritative, and save only after a
coherent transition has committed.

When compatibility requires existing integer or primitive save fields, map
those values to meaningful runtime states without renaming or retyping the DTO.
Test restoration at every resumable state and confirm that re-rendering a state
does not consume RNG or reapply costs and rewards.

When an `EventDefinition` asset is added, moved, or removed, pool registration is part of the same task. Follow `Docs/EventAuthoringGuide.md` and run `Tools > LOADED > Refresh Event Definition Pool`; do not stop after creating the asset. Verify `Assets/Prefabs/UI/Event/EventSceneManagers.prefab` contains every authored event exactly once, with no null reference or duplicate `StableId`. Runtime discovery under `Resources/Events` is a safety net, not a substitute for completing the serialized pool. Rebuild the dedicated scene only when its generated structure actually changed.

## Verify

Run `NodeMapGeneratorTests`, `EventSelectorTests`, and `SceneIntegrityTests`. For event additions, also compare the authored `EventDefinition` set with the serialized Event pool after refreshing it. Add focused deterministic and transition-state coverage. In Play Mode traverse MainMenu, NodeMap, Battle, Shop, Treasure, Event, Ending, back/continue paths, and interrupted or repeated clicks. Confirm exact build-scene names, checkpoint timing, map restoration, and no missing scripts.

Use `Docs/EventAuthoringGuide.md`, `Docs/Dev/0804_LoadingTransition_GameStartUI_BulletIcon.md`, and relevant node/stage documents for the affected path.
