---
name: loaded-edit-enemies
description: Change LOADED enemy authored data, AI decisions, actions, telegraphs, attacks, waves, enemy turns, boss bombs, or enemy combat presentation. Use for Assets/Scripts/Enemy and WaveManager; do not use for player-only targeting rules.
---

# Edit LOADED Enemies and Waves

## Ownership

- `EnemyData`, `EnemyActionData`, and `EnemyAttackData` are authored configuration.
- `EnemyController` owns one live enemy's state and coordinates behavior.
- `EnemyRunStateSerializer` captures and restores enemy runtime state.
- `EnemyTelegraphPresenter` and `BoardTelegraphUtility` present intent without deciding it.
- `WaveManager` owns spawning, wave progression, enemy turn cycles, battle completion, and battle failure.
- `BossBomb` owns one bomb; `BossBombManager` coordinates bomb lifecycle.
- `EnemyActionQueueUI`, tooltips, health feedback, and damage numbers are presentation.

Keep per-enemy mutable state out of `EnemyData`. Do not fold run serialization or telegraph rendering back into `EnemyController`. If a behavior branch is extracted, give it a cohesive decision/execution boundary rather than splitting by line count.

## Preserve behavior contracts

- Determine intent from stable board state, then present the same intent that will execute.
- Preserve seeded/random call order and enemy action ordering.
- Keep stun, attack preparation, acting duration, animation completion, and turn completion distinct.
- Publish an enemy turn cycle exactly once after all required enemies and spawned hazards settle.
- A visual listener or missing animator must not prevent rules from completing.
- Maintain `IPlayerBulletBlocker` behavior for player targeting and previews.
- Save/restore must reproduce health, position, status, prepared action, behavior counters, boss phase, and hazards relevant to the active battle.

## Verify

Compile and run applicable EditMode tests, then exercise each affected behavior in Play Mode. Check intent/telegraph agreement, movement blocking, melee/ranged/thrower/porter decisions, stun and status timing, death during actions, wave boundaries, boss phase/bomb restore, final-enemy battle completion, and scene exit.

Read `Docs/Dev/0717_Enemy_EnemyController_WaveManager.md` for the base flow, `Docs/Dev/0727_EnemyAI.md` for behavior rules, `Docs/Dev/0802_EnemyData_Template_and_ActionPresentation.md` for authoring/presentation, and `Docs/Dev/0803_Stage1_Boss_BigBarrel.md` for boss work.
