---
name: loaded-edit-bullet-deck
description: Change LOADED bullet definitions, mutable bullet instances, bullet effects, deck ownership and cycling, cylinder ordering, bullet management UI, upgrades, or bullet shop capacity. Use when BulletData, BulletInstance, DeckManager, or bullet lifecycle invariants are involved.
---

# Edit LOADED Bullet and Deck Systems

## Ownership chain

- `BulletData` is authored immutable configuration.
- `BulletInstance` owns one bullet's mutable run state and upgrade/effect counters.
- `DeckManager` is the authority for bullet locations, transfers, cycling, and ownership counts.
- `BulletEffectUtility` provides shared stateless classification and lookup.
- `PlayerCylinderUI`, `BulletManagementUI`, `NextBulletUI`, and `BulletLine` render or forward intent; they do not own bullets.
- `CylinderBulletEffectPolicy` decides effect-indicator visibility.

When firing, targeting, damage, or preview changes, also use the player-combat skill. When persistence fields change, also use the run-save skill.

## Preserve lifecycle invariants

```text
TotalBulletCount = deck.Count + loadedBullets.Count + graveyard.Count
0 <= TotalBulletCount <= 20
```

- Each `BulletInstance` has exactly one owner among deck, loaded cylinder, and graveyard except during one tightly scoped transfer.
- `nextCycleOrder` is a preview ordering, never a fourth owner.
- Manual removal leaves at least one owned bullet; combat effects may reduce the total to zero.
- Resolve depletion after the full firing/effect sequence.
- If the final enemy and final bullet disappear in the same shot, battle clear wins.
- Do not consume RNG to preview a future result.
- Suppress transient next-bullet UI churn during firing and publish the settled result once.

## Change workflow

Trace the entire path from authored data through runtime instance, deck transfer, player execution, target state, relic hooks, save/restore, and UI. Add state to `BulletInstance`, not `BulletData`, when it varies within a run. Extend the existing effect pipeline instead of adding a second special-case execution path. Keep actual firing and damage preview on the same rule/calculation path without mutating state during preview.

Read `Docs/BulletDeckLifecycle.md` for any ownership, capacity, cycling, destruction, or UI change. Use the relevant bullet document under `Docs/Dev/` for a named effect.

## Verify

Run `DeckManagerTests`, `PlayerAttackDamageCalculatorTests`, and affected refactoring-policy tests. Add focused EditMode coverage for deterministic rule changes. In Play Mode verify load, reorder, reload, fire, destroy, graveyard recovery, upgrade, capacity 20, final-bullet depletion, final-enemy priority, and next-bullet UI timing.
