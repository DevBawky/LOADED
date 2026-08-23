# Duel Clock Prototype

## Scope

This document records the M0 through M44-2 implementation gates for GitHub
issues #44 and #45. The runtime core now connects authored data and the
deterministic clock rule to player action completion, natural Battle-scene
time, enemy-turn cycles, stun, active-battle save restoration, and a compact
Battle HUD. Time-based single-enemy reinforcement scheduling is connected.

Every current `BattleData` uses Duel Clock. The all-battle authoring command
flattens each asset's legacy waves into one finite enemy pool while preserving
duplicate entries for authored counts.

## Ownership

- `BattleData` owns authored pacing mode, natural and paid-action progress,
  spawn interval, and the finite enemy pool. Free actions are a fixed
  zero-progress rule rather than authored data.
- The scene-local `DuelClockController` owns one `DuelClockState`, natural
  progression gates, paid-action commits, preview queries, and save capture.
- `DuelClockState` owns deterministic progress and cumulative beat arithmetic.
- `DuelClockSnapshot` and `DuelClockAdvanceResult` are immutable values used by
  execution and preview paths.
- `WaveManager` remains the only owner of enemy-turn resolution, spawn-pool
  consumption, capacity deferral, and battle completion. Legacy turns and
  Duel Clock beats enter the same sequential resolver.
- `DuelClockEnemySpawnPool` owns the scene-local remaining enemy references.
  It never mutates the authored `BattleData` list.
- `PlayerMove` owns player action gating and exposes the dedicated Duel Clock
  stun consumption that does not publish another `TurnCompleted` event.
- `RunSaveSystem` owns normalization of the additive clock fields while
  `RunSession` continues cloning the complete JSON DTO.

`DuelClockState` and `DuelClockEnemySpawnPool` are plain C# types with no Unity
lifecycle or presentation dependency. The controller uses the clock's shared
`Preview` and `Commit` calculation
instead of duplicating overflow arithmetic. `WaveManager` adds the controller
at runtime only for a Duel Clock battle; no scene or prefab reference is
required.

`WaveManager.CurrentEnemyTurnCycle` is also the authoritative completed
`COUNT` for run-wide presentation. `StateManager` combines the completed count
from earlier battles with the current battle count, while the version 3 save
keeps the legacy `cumulativeBattleTurnCount` JSON field name for compatibility.

## Authored prototype defaults

| Setting | Default |
|---|---:|
| Combat pacing mode | `Legacy` |
| Natural progress per second | `4` |
| Paid action progress | `45` |
| Enemy wave Beat count | `5` |
| Free action progress | `0` (fixed rule) |
| Empty-board natural rate | `1.0x` (same fixed rate) |
| Clock cycle length | `100` |

The cycle length is a rule constant in `DuelClockState`, not duplicated in
`BattleData`. Existing serialized assets are compatible because `Legacy` is
the zero enum value and all fields were added without renaming existing data.

Set the base charge speed on each `BattleData` asset under
`Combat Pacing > Duel Clock Natural Progress Per Second`. The value is the
percentage points added per real-time second, so `4` fills an empty clock in
`100 / 4 = 25` seconds, `10` fills it in 10 seconds, and `0` disables natural
charging while leaving paid-action progress available. All-battle authoring
preserves intentionally tuned per-battle charge and interval values.

Set `Combat Pacing > Duel Clock Enemy Wave Count` to change the reinforcement
interval and HUD denominator. A value of `5` displays completed Beats as
`0/5` through `4/5`; the fifth resolved Beat requests one random remaining
enemy and resets the counter.

## All-battle authoring

Run `Tools > LOADED > Author Duel Clock All Battles`, or use the command-line
execute method `DuelClockPilotAuthoring.ApplyFromCommandLine`. The idempotent
command discovers every `BattleData` below `Assets/Scripts/Manager/Battle SO`,
enables Duel Clock, and rebuilds `Duel Clock Enemy Pool` from every legacy wave
entry. Duplicate list entries preserve the old authored counts.

## Duel Clock HUD

`Canvas > Panel | Floating > Layout | Duel Clock` presents the active Duel
Clock without owning or mutating combat state. Its recommended hierarchy is:

```text
Layout | Duel Clock
|- Layout | Header
|  |- Text | Title
|  `- Text | Enemy Count
|- Layout | Meter
|  |- Image | Track
|  |- Image | Progress Fill
|  `- Image | Beat Marker
`- Layout | Footer
   |- Text | Progress
   `- Text | Action Preview
```

The header identifies the system and shows the completed Beat count toward the
next reinforcement, for example `적 스폰까지 (0/5)`. `BattleData`
authors the interval and `DuelClockController` derives the current count from
the already-saved cumulative Beat count. When the pool is empty, the label
changes to gray `모든 적 스폰됨`. The footer shows whole clock progress as a
percentage and the combined number of living and unspawned enemies. The HUD
queries `WaveManager` and never owns or mutates competing combat state.

The meter follows ordinary progress with an unscaled-time exponential Lerp.
When a commit crosses `100`, it rapidly Lerps to a full meter, briefly holds
and pulses the complete `Layout | Duel Clock`, resets to zero, and rapidly
Lerps to the preserved overflow. Multiple Beats queue the same visual sequence
without delaying enemy resolution. The animation freezes for the game pause
menu and an open first-run guide card, and remains presentation-only.
`DuelClockHUD` hides itself in
Legacy battles, never blocks raycasts, binds after the runtime-added controller
becomes available, and unsubscribes at matching lifecycle boundaries. Run
`Tools > LOADED > Build Duel Clock HUD`, or use
`DuelClockHudAuthoring.ApplyFromCommandLine`, to rebuild only the existing HUD
root in `Canvas.prefab` and reconcile the Battle scene instance.

The former `Turn N` run display now renders `COUNT N` from completed enemy
cycles. The persistent combo window also consumes one of eight gauge cells on
each `EnemyTurnCycleCompleted` event. A defeat during the resolving count
refreshes the window without consuming that same count; otherwise the eighth
count without another defeat expires the combo.

## Rule contract

- Progress is always normalized to `[0, 100)`.
- Reaching exactly `100` produces one beat and resets progress to `0`.
- Overflow is preserved, and one commit may produce multiple beats.
- `Preview` performs the same calculation as `Commit` without changing state.
- Progress inputs must be finite and nonnegative.
- Restored progress is normalized, adding every completed cycle to the saved
  cumulative beat count.
- Beat-count overflow is rejected instead of wrapping.

## Runtime progression and pause contract

`PlayerMove.TurnCompleted` represents a finalized paid action and is committed
exactly once by the active controller. Actions that do not publish that event
remain free, and the shared free-action preview always evaluates
`state.Preview(0)` with no authored override. Natural progress runs while the
active Duel Clock controller, player, and wave references are valid and the
battle is not complete. `GamePauseController` and an open first-run guide card
pause it. Loading transitions, input locks, player shooting or motion, enemy
resolution, tooltips, reload punch, and other presentation continue clock
progress.
Natural progress uses unscaled frame time, so hit stop and slow motion do not
pause or slow the clock. Presentation never gates action completion or beat
dispatch.

Stun does not pause natural time. While the player has stun stacks, Duel Clock
mode blocks player actions. Each resolved clock beat consumes at most one stun
stack through `ProcessDuelClockStatusBeat` without incrementing `TurnCount` or
publishing `TurnCompleted`. Legacy mode retains its existing recursive skipped
turn behavior.

In Duel Clock mode, paid player actions no longer advance player status-effect
durations. Each resolved clock beat processes the player's Mark, Poison, Stun,
and Weakness exactly once. Poison deals its current stack damage before losing
one stack. Enemies continue processing the same effects when their action in
that beat completes, so enemy effects are not decremented a second time by the
central beat resolver. Legacy mode keeps processing player effects at ordinary
turn completion.

The natural rate does not inspect the active enemy count. An empty Duel Clock
board therefore advances at the same authored rate as a board with enemies.

## Enemy-cycle ordering

`WaveManager` queues every triggered beat in one resolver coroutine. Beats may
be committed while the player is shooting or moving, but enemy execution waits
until that player action settles. Natural and paid-action beats committed
during the same firing sequence therefore join the same queue. New beats that
arrive during enemy resolution also join that queue. In Duel Clock mode,
`IsResolvingTurn` remains true for save/exit settlement but no longer blocks a
new player action after the action that preceded the beat has settled. Legacy
mode retains its enemy-turn input lock. Tooltip, explicit input-lock, and reload
presentation state do not delay the resolver. Each beat:

1. processes one COUNT of player status effects, including one stun stack when
   present;
2. snapshots and resolves the current enemies in existing order;
3. publishes `EnemyTurnCycleCompleted` exactly once;
4. requests one enemy on each authored spawn interval;
5. spawns it immediately when a tile is free, or keeps the request pending
   until capacity becomes available.

`WaveManager.MaximumActiveEnemyCount` fixes encounter capacity at six living
enemies. Spawn-tile selection and the final spawn commit both enforce that
limit. A reinforcement requested while six enemies are alive remains pending
without consuming the authored pool or RNG, then fills the first available
slot after an enemy is defeated. Active-battle saves containing more than six
living enemies are rejected instead of restoring an invalid encounter state.

One random enemy is spawned immediately at fresh battle start. Empty boards
continue accumulating natural clock progress. Battle clear occurs only after
the authored pool is exhausted and all living enemies are defeated. Enemies
spawned after a cycle join the following Beat rather than the cycle snapshot
already being resolved.

Battle completion or player defeat clears the remaining queue. Legacy
`TurnCompleted` still dispatches one cycle directly; in Duel Clock mode only
the controller's committed beats dispatch cycles.

Because Duel Clock allows player input while an enemy cycle is resolving,
`WaveManager` also owns transient movement-tile reservations. Player movement,
enemy movement, bullet knockback, position swaps, and enemy spawning all check
the same registry. A mover reserves its complete path before visual
interpolation begins and releases it after arriving or when disabled. A
conflicting path fails as one operation, so concurrent actors cannot select the
same intermediate or destination tile. These reservations are runtime-only;
active-battle saves still require combat actions to be settled.

## Enemy attack active windows

Direct enemy attack animations may author one active hit window with paired
`BeginAttackActiveWindow` and `EndAttackActiveWindow` Animation Events. The
runtime samples the same event times while the animation plays, so a skipped
visual callback cannot suppress gameplay resolution. A target entering the
attack range during the window is hit once by that queued attack; remaining in
range cannot apply repeated damage.

Existing animations with no complete event pair use the compatibility rule:
the attack checks its live target and applies damage immediately when playback
starts. Melee, gunner, and Big Barrel shotgun attacks use this rule. Thrower
attacks retain their fixed warned tile, but determine its current occupant when
the projectile arrives instead of locking the player hit at launch.

The avatar Animator receives `EnemyAttackAnimationEvents` at runtime, so event
authors do not add scene or prefab references. Attack range and damage remain
owned by `EnemyController` and `EnemyAttackData`; animation events only define
the active timing window.

## Save compatibility

Run save version `3`, desktop filename, and WebGL key
`loaded.run.save.v3` remain unchanged. The DTO adds compatible fields:

- pacing mode as an integer;
- normalized clock progress as a double;
- cumulative completed beats as a long;
- a spawn-pool initialization flag;
- remaining enemy asset names with duplicate entries preserved;
- the number of capacity-deferred spawn requests.

Missing fields normalize to Legacy mode. Invalid modes fall back to Legacy;
negative, non-finite, or overflowing Duel Clock values reset to a safe zero
clock. Restore uses the saved pacing mode before the current authored mode so
an active battle continues with its original pacing. Selecting a fresh battle
clears the saved pacing fields and lets the selected `BattleData` decide its
new mode. Saves are rejected while an enemy-beat queue or another combat
action is unsettled, so pending beats do not need a serialized field.

## Legacy characterization

Focused EditMode coverage fixes the current player-action contracts before the
prototype is wired into combat:

- Wait completes exactly one turn.
- A successful paid reload completes exactly one turn.
- A failed reload and chamber ejection do not consume a turn.
- Destroying the final owned bullet raises depletion only after the firing
  sequence completes.

The legacy wave countdown and firing-sequence victory ordering remain owned by
`WaveManager` and the existing firing sequence. They are not changed or
duplicated by this gate.

## Remaining gates

- M44 UI follow-up: optional enemy-step details, Beat audio, responsive layout
  tuning, and accessibility variants. Inspection does not pause natural
  progress.
- M44 Play Mode tuning: validate per-battle clock values, fixed free action
  `0`, firing-sequence ordering, and accessibility behavior.
- Issue #45 follow-up: optional pre-spawn tile warnings and encounter-specific
  pool/interval balance passes.
