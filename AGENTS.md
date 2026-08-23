# AGENTS.md

## Scope and instruction priority

This file applies to the entire repository. If a more specific `AGENTS.md`
is added below a directory, follow that file for work in its subtree.

User instructions take priority over this document. Preserve intentional
uncommitted work and do not revert, regenerate, or reformat unrelated files.

## Project snapshot

LOADED is a 2D turn-based roguelike built with Unity `6000.3.21f1`, URP 2D,
the Unity Input System, uGUI/TMP, and Cinemachine. The player builds and
upgrades a deck of physical bullet instances, loads them into a cylinder, and
resolves movement, reload, fire, and enemy actions on a board.

The current run normally flows through:

```text
MainMenu -> NodeMap -> Battle / Shop / Treasure / Event -> NodeMap -> Ending
```

Treat `ProjectSettings/EditorBuildSettings.asset` as the source of truth for
enabled scene names and order. Some older docs and serialized defaults still
refer to `Stage 1`; do not revive that obsolete scene name without tracing the
current navigation code.

The repository contains locally managed Asset Store dependencies. A fresh
checkout may need these packages imported before Unity can compile or render
the project:

- Damage Numbers Pro
- Old Movie - Old Film Screen Effect
- the locally ignored `Assets/Package/` content

Do not copy, replace, publish, or edit third-party package sources unless the
task explicitly requires it.

## Start every task with context

Before changing code or serialized assets:

1. Read the complete target file and its direct callers/collaborators.
2. Search for events, serialized references, string IDs, scene object names,
   save fields, and tests affected by the change.
3. Read the relevant document under `Docs/` when one exists.
4. Identify the authoritative owner of the affected runtime state.
5. Check the working tree and preserve unrelated changes.
6. Make the smallest coherent change that fits the existing design.

Do not assume a common Unity pattern is appropriate until the current data and
execution flow has been traced. Do not begin a feature by adding a new global
manager, singleton, service locator, or speculative abstraction.

## Project-local skills

Project-specific Codex skills live under `.agents/skills/<skill-name>/SKILL.md`.
The filename is singular by convention. Use the smallest matching set; combine
skills when a change crosses authoritative state boundaries.

- `loaded-refactor-systems`: SOLID review and responsibility extraction.
- `loaded-edit-audio`: playback, soundtrack, volume, and sound-ID wiring.
- `loaded-edit-bullet-deck`: bullet data/instances, deck lifecycle, and cylinder.
- `loaded-edit-player-combat`: movement, health, firing, targeting, and previews.
- `loaded-edit-enemies`: enemy data/AI, telegraphs, waves, bosses, and hazards.
- `loaded-edit-relics`: relic data/runtime state, triggers, inventory, and restore.
- `loaded-edit-shop-inventory`: offers, purchases, currency, items, and rewards.
- `loaded-edit-run-save`: run/session/node-map persistence and restore.
- `loaded-edit-statistics`: statistic production, aggregation, persistence, and UI.
- `loaded-edit-settings`: pause, accessibility, graphics, audio controls, and video.
- `loaded-edit-world-flow`: state, scenes, node map, stages, events, and ending.
- `loaded-edit-ui-guides`: tutorials, tooltips, HUD, and visual feedback.

Examples of intentional combinations: a saved bullet effect uses bullet/deck,
player combat, and run-save; a relic granted by a treasure scene uses relics,
shop/inventory, and world-flow; a structural rewrite also uses the refactoring
skill. Domain skills supplement rather than replace this repository-wide file.

## Repository map and ownership

- `Assets/Scripts/Bullet/`
  - `BulletData` and related serializable types are authored configuration.
  - `BulletInstance` owns per-run mutable bullet state.
  - `BulletEffectUtility` owns shared, stateless bullet-effect lookup and
    classification used by execution and preview paths.
  - Bullet UI scripts present and manipulate state through the owning systems.
- `Assets/Scripts/Player/`
  - `PlayerMove`, `PlayerShoot`, and `PlayerHealth` expose player combat
    behavior. `PlayerShoot` is the Unity-facing input/orchestration facade.
  - `PlayerShootInputReader`, `PlayerShotRangePreview`,
    `BulletShotFeedbackView`, `PlayerAttackDamageCalculator`, and the nested
    firing/damage-preview collaborators own their named responsibilities.
    Trace the full firing sequence before changing effect behavior.
  - `CylinderBulletEffectPolicy` decides whether a loaded bullet has an active
    effect indicator. `PlayerCylinderUI` only renders that decision.
- `Assets/Scripts/Enemy/`
  - `EnemyData`, `EnemyActionData`, and `EnemyAttackData` are authored data.
  - `EnemyController` resolves live enemy behavior. Its run-state serializer
    and telegraph presenter are separate collaborators; do not move save or
    telegraph implementation back into the controller facade.
  - `WaveManager` owns enemy/wave creation and progression.
- `Assets/Scripts/Manager/`
  - `StateManager` orchestrates battle flow and scene exits.
  - `DeckManager` is the authoritative owner of bullet locations and deck
    cycling.
  - `NodeMapSystem` owns map interaction/orchestration,
    `NodeMapGenerator` owns deterministic generation, and
    `NodeMapSaveSystem` owns node-map persistence.
  - `GameStatistics` owns live counters, `RunDataModels` defines save DTOs,
    and `RunSaveSystem` owns run serialization.
  - `RelicManager`, `ShopManager`, `RewardManager`, and the other focused
    managers own their named runtime systems. Their `Manager` suffix is not
    permission to add unrelated behavior.
  - `ShopOfferGenerator` owns unique weighted bullet/item offer selection;
    `ShopManager` owns purchase flow and shop presentation.
- `Assets/Scripts/Common/`
  - Shared combat presentation, status effects, relic models, and camera
    feedback. Keep presentation separate from rule decisions.
- `Assets/Scripts/Event/`
  - `EventDefinition` is authored event content; `EventSceneController`
    coordinates scene interaction, applies effects, saves checkpoints, and
    routes follow-up destinations.
  - `EventChoiceAvailabilityEvaluator` owns deterministic choice eligibility;
    `EventChoiceTextFormatter` owns choice-label highlighting. UI and execution
    paths must reuse `EventRuntimeRules` for repeat/effect calculations.
  - Follow `Docs/EventAuthoringGuide.md` when adding or changing events.
- `Assets/Scripts/Sound/`
  - `SoundManager` is the persistent audio playback and volume owner.
  - `SoundtrackDirector` selects scene music. UI feedback installers and
    components own button sound/hover decoration.
  - `SoundClipLibrary` and `Assets/Resources/Sound/SoundClipLibrary.asset` map
    string IDs to clips and playlists.
- `Assets/Editor/`
  - Project-specific scene/prefab setup builders and WebGL build tooling.
    Generated scenes may be overwritten by these tools, so inspect and update
    the relevant builder when a task changes generated structure. Never run a
    builder casually; it can rewrite serialized assets.
- `Assets/Editor/Tests/`
  - NUnit coverage for deck lifecycle, node-map generation, events, and relics.
  - Tests intentionally live under `Editor` so Unity Test Framework discovers
    them in the predefined `Assembly-CSharp-Editor` assembly. Runtime internals
    needed by tests are exposed only through `AssemblyInfo.cs`.
- `Docs/`
  - Design decisions and authoring/implementation guides. Keep the directly
    relevant document in sync when a rule, invariant, or authoring workflow
    changes.
- `WebBuild/`
  - Tracked GitHub Pages output. Do not modify or rebuild it unless the user
    explicitly requests a WebGL build or deployment-related change.

## Runtime-state boundaries

Use the existing ownership model:

- `ScriptableObject` assets (`BulletData`, `EnemyData`, `RelicData`,
  `ItemData`, `BattleData`, `StageData`, `EventDefinition`, and settings) hold
  authoring/configuration data. Do not put per-run mutation into shared assets.
- Runtime instances and scene components hold mutable combat state.
- `RunSession` is the scene-independent in-memory owner of the current cloned
  `RunSaveData` snapshot. Never store scene-object references in it.
- `RunSaveSystem` owns run serialization. Desktop saves use JSON under
  `Application.persistentDataPath`; WebGL uses the existing versioned
  `PlayerPrefs` key. `NodeMapSaveSystem` separately owns node-map progress.
- UI renders state and forwards player intent. Disabling a panel must not
  destroy authoritative gameplay state or decide game rules.
- Presentation, animation, camera feedback, and audio may react to outcomes;
  they must not determine those outcomes.

When changing save data, inspect capture, normalization, validation, restore,
new-game, continue-game, WebGL, and desktop paths. Do not rename fields, change
the save version, or change a `PlayerPrefs` key without an explicit migration
plan and backward-compatibility review.

## Bullet and combat invariants

Preserve the rules documented in `Docs/BulletDeckLifecycle.md` unless the user
explicitly requests a rule change:

```text
TotalBulletCount = deck.Count + loadedBullets.Count + graveyard.Count
0 <= TotalBulletCount <= 20
```

- A `BulletInstance` belongs to exactly one of `deck`, `loadedBullets`, or
  `graveyard`, except during a tightly scoped transfer in one operation.
- `nextCycleOrder` is an ordering preview, not another owner of instances.
- Manual removal must leave at least one owned bullet; combat effects may
  reduce the count to zero.
- Depletion is resolved after the firing/effect sequence, not in the middle of
  `TryDestroyBullet`.
- If the final enemy and final bullet are removed in the same shot, battle
  clear takes priority over bullet-depletion failure.
- Do not consume RNG early merely to preview a future result. Seeded node-map
  behavior and shuffle/effect call order must remain deterministic where the
  current design expects it.
- During firing, avoid transient next-bullet UI updates. Commit the final UI
  state after the sequence has settled.

Changes to bullet effects should trace at least `BulletData`, `BulletInstance`,
`DeckManager`, `PlayerShoot`, target/enemy state, relic hooks, save/restore,
and the related UI. Prefer extending an existing effect pipeline over adding a
parallel special-case path.

## Scene, lifecycle, and event safety

- Prefer serialized scene references and explicit initialization. Existing
  `FindFirstObjectByType` fallbacks are allowed where they are part of the
  established scene bootstrap, but do not add repeated per-frame hierarchy
  searches.
- Several systems rely on exact scene, GameObject, button, resource, animation,
  and sound ID strings. Search all usages before renaming hierarchy objects or
  IDs. In particular, `SoundManager` discovers and decorates some UI buttons by
  name.
- Use `LoadingTransitionController` for normal scene navigation and preserve
  the existing direct `SceneManager.LoadScene` fallback where applicable.
- Subscribe and unsubscribe events at matching lifecycle boundaries. Avoid
  anonymous handlers when they cannot be removed safely.
- Coroutines, delayed callbacks, tween-like presentation, and async scene loads
  must tolerate disabled or destroyed Unity objects.
- Account for Unity's destroyed-object null semantics.
- Gameplay correctness must not depend on whether a visual listener is active.

`SoundManager`, `RunSession`, and `LoadingTransitionController` already provide
intentional persistent behavior. Extend those owners when the task belongs to
their current responsibility; do not create competing persistent objects.
Preserve the existing audio preference keys and update both the clip-library
asset and its ID consumers when authoring a new sound.

## Architecture and refactoring policy

Follow SOLID principles pragmatically, with responsibility boundaries taking
priority over minimizing file or class count.

- `MonoBehaviour`: Unity lifecycle, scene references, input/presentation
  adapters, and orchestration that genuinely belongs to the scene component.
- Plain C# types: reusable rules, calculations, state transitions, and runtime
  data that do not require a Unity lifecycle.
- `ScriptableObject`: static authored configuration.
- UI components: rendering and forwarding intent, not owning domain state.

Refactor autonomously only when the refactor is directly required by the task,
keeps behavior stable, stays within touched code and immediate collaborators,
and makes the change easier to verify. Large existing classes are not by
themselves authorization for broad rewrites.

Ask before a change that would:

- replace a project-wide architecture pattern;
- change a public API used by several systems;
- change gameplay beyond the request;
- rename or change the type of serialized fields;
- alter prefab, scene, `ScriptableObject`, or save schemas;
- change packages, assembly definitions, build configuration, or many
  unrelated files;
- require a large asset/reference migration.

Introduce an interface only for a meaningful boundary, test seam, or plausible
multiple implementation. Do not create one mechanically for every class.

Using `partial` only to shorten a file does not separate responsibility. A
partial controller file may contain a nested collaborator during a
serialization-compatible refactor, but that collaborator must own cohesive
state/behavior and the `MonoBehaviour` part must remain a small delegating
facade. Prefer a standalone plain C# type when private Unity serialization
compatibility does not require nesting.

### Maintainability and extension workflow

Design changes around ownership and observable behavior rather than shorter
files:

- Name one authoritative owner for each mutable state before adding a feature.
  Other systems may query that owner or send intent, but must not cache a
  competing mutable copy.
- Separate queries, mutations, and presentation when they have different
  reasons to change. Preview and UI paths must reuse the same deterministic
  rules as execution instead of reimplementing them.
- Keep Unity facades responsible for lifecycle, serialized references, event
  wiring, and orchestration. Move deterministic formatting, calculations, and
  state transitions to plain C# collaborators with explicit inputs.
- Before extracting behavior, add characterization tests for public outcomes
  and sensitive ordering such as RNG consumption, effect resolution, save
  checkpoints, event emission, and turn completion.
- When an effect or behavior `switch` grows, extract a cohesive effect family
  only when it creates a real test seam or independent change boundary. The
  authoritative manager still validates and commits state changes.
- Represent resumable multi-step flows with explicit runtime states and legal
  transitions. Map them onto existing save primitives without changing the
  serialized schema unless a migration is explicitly planned.

A maintainability refactor is complete when ownership is unambiguous, new
behavior can be added without duplicating rules across execution and UI,
critical ordering is protected by tests, public/serialized contracts are
preserved, and disabling presentation cannot change gameplay outcomes.

## Serialized asset safety

Treat `.unity`, `.prefab`, `.asset`, `.inputactions`, project settings, and
`.meta` files as reference-bearing serialized data.

- Before any Unity batch-mode run, package import, builder execution, or other
  operation that can import or rewrite `Assets/`, inventory the locally managed
  ignored/untracked asset roots (`Assets/DamageNumbersPro/`, `Assets/OldMovie/`,
  `Assets/Package/`, custom fonts, and TMP essentials). Record their existence,
  file counts, and representative GUID-bearing `.meta` files. Do the same check
  immediately afterward.
- Never assume `git restore`, checkout, or a fresh clone can recover ignored or
  untracked Unity assets. Preserve those assets and their original `.meta` files
  in a verified backup before an operation with project-wide import or rewrite
  risk. Restoring an asset without its original `.meta` does not restore its
  serialized references.
- Never replace, clear, reconstruct, or bulk-restore the entire `Assets/`
  directory as a normal implementation step. Limit Editor builders and package
  recovery to explicit, verified target paths.
- If `Assets/` becomes empty, loses a locally managed root, or changes outside
  the expected diff, stop Unity and all further builders immediately. Do not
  continue with a tracked-only Git recovery. First locate a complete local
  backup/package cache, restore ignored assets with their `.meta` files, and
  verify serialized GUID resolution before reopening Unity.
- After restoring fonts, audio, packages, or other reference-bearing local
  assets, enumerate the GUIDs referenced by affected scenes/prefabs and confirm
  that every GUID resolves to exactly one `.meta` file. Unity compilation or
  EditMode tests alone are not proof that those references exist.

- Do not hand-edit scene or prefab YAML for ordinary hierarchy work.
- Do not invent, replace, or duplicate GUIDs.
- Never delete or regenerate `.meta` files casually.
- Preserve serialized references. When a serialized field must be renamed,
  use `FormerlySerializedAs` where compatibility is required and verify all
  affected prefabs/scenes/assets.
- Add the `.meta` file with every new Unity asset.
- Do not edit generated `.csproj`, `.sln`, `.slnx`, `Library/`, `Temp/`,
  `Logs/`, or `UserSettings/` as source changes.
- Inspect the final diff for unintended Unity rewrites, especially after any
  Editor automation.

## Code style

Match the surrounding file first. The established baseline is:

- 4-space indentation and opening braces on the next line;
- `PascalCase` for types, methods, properties, and events;
- `camelCase` for private fields, locals, and parameters; do not introduce an
  underscore prefix into files that do not use it;
- private `[SerializeField]` fields instead of public mutable Inspector state;
- expression-bodied members only when they remain easy to read;
- explicit success/failure (`Try...`, `bool`, or a meaningful result) for
  expected gameplay rejection;
- exceptions for exceptional failures, not normal outcomes such as insufficient
  currency or a full inventory;
- no per-frame or repeatedly emitted logs for expected conditions;
- comments that explain intent, ordering constraints, compatibility, or a
  non-obvious rule, not comments that narrate the code;
- no decorative comments or emoji in code comments.

The project currently uses the global namespace. Do not perform a namespace
migration incidentally. Keep `MonoBehaviour`/`ScriptableObject` class names and
file names aligned so Unity script references remain valid.

## Verification

Use verification proportional to the risk. For C# changes, Unity compilation
is the source of truth; generated IDE project files are secondary.

The installed editor for this workspace is:

```text
C:\6000.3.21f1\Editor\Unity.exe
```

Run EditMode tests from PowerShell when the project is not already open in a
conflicting Unity Editor process:

```powershell
& 'C:\6000.3.21f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'C:\Users\user\Desktop\LOADED' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\Users\user\Desktop\LOADED\Logs\EditModeResults.xml' `
  -logFile 'C:\Users\user\Desktop\LOADED\Logs\EditModeTests.log'
```

After a code change:

1. Run the most relevant existing tests; add focused EditMode tests for changed
   rules, state transitions, deterministic generation, and save restoration.
2. Check the Unity log for compiler errors and confirm the test-results file
   actually reports completion. A process exit code alone is insufficient.
3. Manually verify scene/prefab wiring when serialized fields, hierarchy names,
   resources, animation events, input bindings, or UI are affected.
4. For save changes, exercise new game, continue, scene transition, desktop
   persistence, and WebGL-specific branches as relevant.
5. For combat changes, verify invalid input, event balance, turn completion,
   battle-clear/failure ordering, and UI refresh timing.
6. Review the diff and status for unrelated asset or `.meta` changes.

Do not run a full WebGL build by default. When explicitly requested, use
`Tools > LOADED > Build WebGL` or the corresponding
`WebBuildCommand.BuildWebGL` editor method, then verify the tracked `WebBuild/`
output and browser behavior.

If Unity validation cannot run because a third-party asset is unavailable, the
project is already open, or the environment lacks graphics/editor support,
state exactly what was not verified and provide a short manual Editor checklist.
Never claim compilation, tests, Play Mode behavior, or a build succeeded unless
it was actually observed.

## Completion report

Report:

- what changed and which system now owns the behavior;
- any directly related refactor;
- tests and checks actually run, including failures;
- serialized assets or docs changed;
- remaining Unity Editor, scene, audio, WebGL, or manual verification.
