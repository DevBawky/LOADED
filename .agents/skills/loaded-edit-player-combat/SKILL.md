---
name: loaded-edit-player-combat
description: Change LOADED player movement, health, input gating, shooting orchestration, firing sequences, targeting, damage preview, cylinder interaction, or player combat feedback. Use for Assets/Scripts/Player and player-facing battle control; use the bullet-deck skill too when bullet ownership or effects change.
---

# Edit LOADED Player Combat

## Ownership

- `PlayerMove` owns player movement and its turn/position events.
- `PlayerHealth` owns player health, defeat, and received status effects.
- `PlayerShoot` is the Unity-facing facade for shooting lifecycle, public combat entry points, and collaborator wiring.
- `PlayerShootInputReader` reads input; it does not decide whether gameplay permits the action.
- `FiringSequenceController` executes a cylinder sequence.
- `DamagePreviewController` and `PlayerAttackDamageCalculator` calculate non-mutating previews and shared damage rules.
- `PlayerShotRangePreview` and `BulletShotFeedbackView` present range and shot feedback.
- `BehaviourTileActionUI` and combat controls forward intent rather than own turn state.

Do not move collaborator behavior back into the `PlayerShoot` facade merely because it needs serialized references. Prefer explicit dependency setup and a small delegating surface.

## Preserve combat sequencing

- Validate input and game-flow gates before mutating bullets, movement, health, or turn state.
- Complete a player action and publish turn completion exactly once.
- Keep gameplay completion independent of optional animation, camera, audio, or UI listeners.
- Make coroutines and delayed callbacks safe when the component, target, or scene is disabled or destroyed.
- Preview from copied/read-only state. It must not consume RNG, trigger relics, change counters, or subscribe persistent listeners.
- Actual fire and preview must agree on target order, range, critical/status modifiers, extra shots, and effect classification.

Search `StateManager`, `WaveManager`, target/enemy APIs, relic hooks, statistics, and save capture before changing a public player event or action boundary.

## Verify

Run `PlayerAttackDamageCalculatorTests` plus relevant deck and relic tests. In Play Mode cover valid and rejected input, movement/kick/reload/fire/wait, pause and transition locks, cylinder reordering, range preview, damage preview parity, player defeat, action cancellation, and exact turn completion count.

Use `Docs/Dev/0715_Player_PlayerMove.md`, `Docs/Dev/0717_Combat_DeckManager_PlayerShoot.md`, `Docs/Dev/0718_Combat_PushSystem.md`, and `Docs/Dev/0803_CylinderBulletDamagePreview.md` selectively for the affected path.
