# Duel Clock Prototype

## Scope

This document records the M0 through M44-2 implementation gates for GitHub
issues #44 and #45. The runtime core now connects authored data and the
deterministic clock rule to player action completion, natural Battle-scene
time, enemy-turn cycles, stun, active-battle save restoration, and a compact
Battle HUD. A dedicated half-speed spawn gauge schedules reinforcements.

Every current `BattleData` uses Duel Clock. The all-battle authoring command
flattens each asset's legacy waves into a weighted enemy-type pool and a total
spawn count. It also preserves the old duplicate list as hidden migration data
for active version 3 saves created before the weighted selector existed.

## Ownership

- `BattleData` owns authored pacing mode, natural and paid-action progress,
  total spawn count, and weighted enemy entries. Free actions
  are a fixed zero-progress rule rather than authored data.
- The scene-local `DuelClockController` owns the Duel Clock and spawn-gauge
  `DuelClockState` instances, natural progression gates, paid-action commits,
  preview queries, and save capture.
- `DuelClockState` owns deterministic progress and cumulative beat arithmetic.
- `DuelClockSnapshot` and `DuelClockAdvanceResult` are immutable values used by
  execution and preview paths.
- `WaveManager` remains the only owner of enemy-turn resolution, spawn-pool
  consumption, capacity enforcement, and battle completion. Legacy turns and
  Duel Clock beats enter the same sequential resolver.
- `DuelClockEnemySpawnPool` owns the scene-local remaining count, per-type
  spawn and missed-selection counts, and the last selected enemy. It never
  mutates authored `BattleData` entries.
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
| Runtime natural speed multiplier | `1.35x` |
| Paid action progress | `45` |
| Spawn gauge progress multiplier | `0.5x` |
| Free action progress | `0` (fixed rule) |
| Three-enemy natural rate | `1.0x` |
| Natural rate step | `0.3x` per enemy below/above three |
| Clock cycle length | `100` |

The cycle length is a rule constant in `DuelClockState`, not duplicated in
`BattleData`. Existing serialized assets are compatible because `Legacy` is
the zero enum value and all fields were added without renaming existing data.

Set the base charge speed on each `BattleData` asset under
`Combat Pacing > Duel Clock Natural Progress Per Second`. Runtime applies a
global `1.35x` increase to the authored value, so `4` becomes `5.4` before the
living-enemy multiplier, while `0` disables natural
charging while leaving paid-action progress available. All-battle authoring
preserves intentionally tuned per-battle charge values. Main Duel Clock
charging pauses for the full `PlayerShoot` firing sequence and resumes after
the cylinder has finished resolving. Every main-clock progress source stops at
the next `100` boundary and reserves exactly one enemy Beat. The HUD holds the
completed meter at `100%` until that enemy cycle starts. Additional natural
time and player actions do not create hidden main-clock progress or another
queued cycle while the reservation is held.

Every accepted positive paid-action Duel Clock increment also adds half of its
accepted amount to the spawn gauge. Natural time contributes at half speed
through an independent path, so the spawn gauge keeps charging while the
player fires and while the main Duel Clock holds a completed Beat at `100`.
Enemy-defeat reductions affect only the Duel Clock; they do not remove already-
earned spawn progress. Reaching `100` queues one random remaining enemy and
resets the spawn gauge. While the number of living enemies is at
`WaveManager.MaximumActiveEnemyCount`, only the spawn gauge pauses; the main
Duel Clock continues normally. The gauge resumes from its preserved progress
as soon as capacity returns. Its HUD label changes from `ENEMY SPAWN` to
`MAX ENEMIES` for the duration of that capacity pause. When the configured
enemy spawn pool has no enemies remaining, the spawn gauge stays at `0%` and
does not accumulate natural or paid-action progress; this includes the Big
Barrel boss battle after its single authored boss has spawned. The hidden serialized
`Duel Clock Enemy Wave Count` field remains only for older asset compatibility.

## All-battle authoring

Run `Tools > LOADED > Author Duel Clock All Battles`, or use the command-line
execute method `DuelClockPilotAuthoring.ApplyFromCommandLine`. The idempotent
command discovers every `BattleData` below `Assets/Scripts/Manager/Battle SO`,
enables Duel Clock, and updates the hidden legacy migration pool from every
legacy wave entry. When weighted entries are still empty, duplicate entries
of one enemy become one weighted entry whose base weight is the old duplicate
count. The command sets the total spawn count to the flattened count and
initializes minimum count to `0`, missed-selection weight increase to `0.25`,
and previous-spawn multiplier to `0.35`. Existing weighted entries and their total
spawn count are preserved so rerunning the command does not erase encounter
tuning.

## Weighted enemy selection

Each `Duel Clock Enemy Spawn Entry` authors an enemy type, base weight,
minimum spawn count, missed-selection weight increase, and previous-spawn
weight multiplier. Effective weight starts as
`base * (1 + missed count * missed increase)`, then applies the previous-spawn
multiplier when it was the last selected type. Selecting an entry resets its
missed count to zero and increments every other entry's count. If a zero
previous-spawn multiplier would leave no selectable type, selection retries
without that multiplier so a single-type pool cannot deadlock.

When the remaining spawn slots equal the sum of unmet minimum counts, only
entries that have not met their minimum remain eligible. A configuration is
invalid when entries are null or duplicated, a base weight is non-positive,
or minimum counts exceed the total spawn count. Random selection does not
consume the spawn budget until `WaveManager` has successfully created and
initialized the enemy.

## Duel Clock HUD

`Canvas > Panel | Floating > Layout | Duel Clock` presents the active Duel
Clock without owning or mutating combat state. Its recommended hierarchy is:

```text
Layout | Duel Clock
|- Layout | Header
|  |- Text | Title
|  `- Text | Progress
|- Layout | Meter
|  |- Image | Track
|  |- Image | Progress Fill
|  `- Image | Beat Marker
|- Layout | Spawn Meter
|  |- Image | Spawn Track
|  |- Image | Spawn Progress Fill
|  `- Text | Spawn Gauge Label
`- Panel | Remaining Enemies
   |- Text | Remaining Enemy Label
   `- Text | Unspawned Enemy Count
```

The header pairs the system title with a large whole-number percentage. The
main meter is followed by a blue spawn meter that fills from the same accepted
progress at half speed. The single status card below both gauges displays only
the number of enemies that have not spawned yet; living enemies are excluded.
Its label and number form one compact centered group instead of occupying
opposite edges. A dark high-contrast backdrop, thicker primary meter, and
outlined key values keep the clock, spawn state, and remaining count readable
as three distinct information levels at a glance.
The HUD queries `WaveManager` and never owns or mutates competing combat state.

The meter follows ordinary progress with an unscaled-time exponential Lerp.
Its fill color also interpolates from yellow at zero progress to red at 100
percent, using the same displayed fill amount as the meter animation.
When a commit crosses `100`, it rapidly Lerps to a full meter, holds while the
single next enemy cycle is reserved, and pulses the complete
`Layout | Duel Clock` for `0.2` seconds. It resets to zero when that cycle
starts. The animation freezes for the game pause
menu and an open first-run guide card, and remains presentation-only.
`DuelClockHUD` hides itself in
Legacy battles, never blocks raycasts, binds after the runtime-added controller
becomes available, and unsubscribes at matching lifecycle boundaries. The
serialized `Canvas.prefab` hierarchy is the authoritative layout and is never
generated or rearranged at runtime. `Tools > LOADED > Validate Duel Clock HUD`
and `DuelClockHudAuthoring.ApplyFromCommandLine` are read-only checks for the
required serialized references; neither command writes the prefab or Battle
scene. Visual edits are authored directly in the prefab.

The former `Turn N` run display now renders `COUNT N` from completed enemy
cycles. The persistent combo window starts consuming one of eight gauge cells
as soon as the Duel Clock commits a Beat at 100%, using its existing
`0.1`-second drain timing instead of waiting for enemy turn resolution. Player
actions do not consume combo gauge cells directly.
Legacy pacing retains its enemy-cycle-completion countdown.

## Rule contract

- The underlying `DuelClockState` progress is always normalized to `[0, 100)`;
  the controller presents `100` while one completed Beat is reserved.
- Reaching exactly `100` produces one beat and resets progress to `0`.
- `DuelClockState.Commit` retains its normalized overflow arithmetic, while the
  runtime controller uses `CommitUntilNextBeat` for every progress source.
  Runtime overflow is discarded and at most one Beat can be reserved.
- While a Beat is reserved, the presented progress is `100` and further
  progress commits are ignored. The underlying normalized state resumes from
  `0` when `WaveManager` starts that enemy cycle.
- `Preview` performs the same calculation as `Commit` without changing state.
- Progress inputs must be finite and nonnegative.
- Restored progress is normalized, adding every completed cycle to the saved
  cumulative beat count.
- Beat-count overflow is rejected instead of wrapping.

## Runtime progression and pause contract

`PlayerMove.TurnCompleted` represents a finalized paid action and is normally
committed exactly once by the active controller. Shooting is the exception:
its paid-action progress is committed as soon as a valid firing sequence
starts, and its later `TurnCompleted` event does not commit the cost again.
Non-shoot actions that do not publish `TurnCompleted` remain free, and the
shared free-action preview always evaluates `state.Preview(0)` with no
authored override. Natural progress runs while the
active Duel Clock controller, player, and wave references are valid and the
battle is not complete. `GamePauseController` and an open first-run guide card
pause it. A `PlayerShoot` cylinder firing sequence also pauses natural progress
from the moment the sequence starts until its gameplay settlement completes.
Enemy resolution does not pause natural progress. The clock can therefore fill
during enemy presentation and reserve one following cycle. Once that
reservation reaches `100`, further natural and paid progress is held until the
reserved cycle starts; no additional cycle debt is accumulated. Loading
transitions, input locks, player motion, tooltips, reload punch, and other
presentation continue clock progress until that reservation limit is reached.
Natural progress uses unscaled frame time, so hit stop and slow motion do not
pause or slow the clock. Presentation never gates action completion or beat
dispatch.

When a player movement action successfully dodges an enemy attack or a boss
bomb, that action's pending paid-action progress is suppressed. The movement
still completes and publishes `TurnCompleted`, but `DuelClockController` does
not commit `Duel Clock Paid Action Progress` for that one action. The
suppression is reset at completion or when another action starts, so it cannot
make a later action free. It also never refunds shooting progress that was
already committed at firing-sequence start, remove natural progress, or undo a
COUNT that already completed. `PlayerMove` publishes the gameplay dodge
outcome before the optional feedback controller renders it, so missing or
disabled presentation cannot change the clock rule.

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

The natural rate uses three living enemies as its `1.0x` baseline. Each enemy
below three adds 30 percentage points, while each enemy above three subtracts
30 percentage points. The resulting rates are `1.9x`, `1.6x`, `1.3x`, `1.0x`,
`0.7x`, and `0.4x` for zero through five living enemies respectively. The
controller queries `WaveManager` when advancing natural time, so defeats and
reinforcements affect the next frame without adding saved or duplicated enemy
count state.

## Enemy-cycle ordering

`WaveManager` resolves committed beats in one resolver coroutine. Shooting
locks the firing sequence before it commits paid-action progress. Therefore a
Beat that reaches `100` from that shot may enter the resolver queue immediately,
but enemy execution waits until the complete cylinder sequence settles. The
ordering contract is player firing first, then queued enemy attacks. Main-clock
natural progress pauses for the same firing interval, while spawn-gauge natural
progress continues. During an active enemy cycle, natural or paid progress may
reserve one following cycle. `WaveManager` keeps
at most one pending Duel Clock cycle in addition to the cycle currently being
resolved. In Duel Clock mode,
`IsResolvingTurn` remains true for save/exit settlement but no longer blocks a
new player action after the action that preceded the beat has settled. Legacy
mode retains its enemy-turn input lock. Tooltip, explicit input-lock, and reload
presentation state do not delay the resolver. There is no fixed interval
between consecutive Duel Clock enemy cycles. The resolver only waits for an
already-running player action to settle before starting the next enemy cycle.
There is no Duel Clock-specific player input barrier. Only successful wait and
reload actions share the authored `PlayerMove > Instant Action Cooldown`, which
defaults to `0.16` seconds. Movement, rotation, and shooting retain their own
execution gates, so movement remains immediately available for the dodge system.
Actions performed while the meter is already reserved at `100` do not advance
the clock. When the reserved cycle starts, the meter resets to `0` and can fill
again while that cycle is resolving. Each beat:

1. processes one COUNT of player status effects, including one stun stack when
   present;
2. snapshots and resolves the current enemies in existing order;
3. publishes `EnemyTurnCycleCompleted` exactly once;
4. records any spawn-gauge completion;
5. spawns each requested enemy when the battle's capacity and a tile are
   available. A request completed alongside a Duel Clock Beat waits until that
   Beat ends so the new enemy joins the following snapshot.

`WaveManager.MaximumActiveEnemyCount` is calculated for each battle by rounding
`BattleData.BoardCount * 0.35` to the nearest integer, with a minimum of one.
This yields capacities of two, four, and five enemies for board counts seven,
eleven, and thirteen respectively. Spawn-tile selection and the final spawn
commit both enforce the current battle's result. A scheduled reinforcement is
deferred while that capacity is full or no spawn tile is available. Deferred
reinforcements do not consume the authored pool or RNG. When a
defeat leaves no living enemy while the pool still has a remaining spawn, one
enemy is spawned immediately if a tile is available; this prevents an empty
stage from waiting for the next gauge completion. Active-
battle saves containing more living enemies than the restored battle's
calculated capacity are rejected. Deferred requests are captured in
`duelClockPendingEnemySpawns` and restored with the active battle.

One random enemy is spawned immediately at fresh battle start. Empty boards
continue accumulating natural clock progress. Battle clear occurs only after
the authored pool is exhausted and all living enemies are defeated. Enemies
spawned after a cycle join the following Beat rather than the cycle snapshot
already being resolved.

Battle completion or player defeat clears the remaining queue. Legacy
`TurnCompleted` still dispatches one cycle directly; in Duel Clock mode only
the controller's committed beats dispatch cycles.

Every enemy defeat reduces the current Duel Clock progress by one quarter of
the active battle's `Duel Clock Paid Action Progress` without changing
completed COUNT. With the default value of 45, shooting immediately adds 45
and each defeat removes 11.25, so four defeats exactly offset that shot's
progress. Reduction clamps at zero, does not cancel a COUNT that was already
committed, and has no separate combat text. Defeats outside a firing sequence
use the same fixed reduction.

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
the projectile arrives instead of locking the player hit at launch. Dodge
windows compare the complete player cell `(tile, lane)`, so leaving the warned
cell through either horizontal or vertical movement resolves the same dodge
rule.

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
- normalized spawn-gauge progress and cumulative completed spawn cycles;
- a spawn-pool initialization flag;
- remaining enemy asset names with duplicate entries preserved;
- a weighted-selector initialization flag and remaining spawn count;
- per-entry spawned and missed-selection counts;
- the last selected enemy asset name for the repeat penalty;
- the capacity-deferred spawn-request count.

The old initialization flag and duplicate remaining-name list stay in version
3 JSON for migration. On first restore after this change, `WaveManager` infers
per-entry spawned counts from the preserved authored legacy pool and then
captures the weighted state on the next checkpoint. Desktop and WebGL continue
through the same `RunSaveSystem` normalization and `RunSession` JSON clone
paths; the save version, filename, and PlayerPrefs key are unchanged.

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
