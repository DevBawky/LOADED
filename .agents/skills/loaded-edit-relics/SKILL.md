---
name: loaded-edit-relics
description: Change LOADED relic data, runtime relic state, acquisition, stacking, charges, combat triggers, effect ordering, relic inventory UI, or relic save restoration. Use when RelicData, RelicInstance, RelicManager, or RelicCombatEvents are involved.
---

# Edit LOADED Relics

## Ownership

- `RelicData` and `RelicEffectData` are authored configuration.
- `RelicInstance` owns mutable stack, charge, stored-value, counter, and acquisition-order state.
- `RelicManager` owns the collection, capacity, trigger coordination, and combat-event integration.
- `RelicCombatEvents` defines event contracts.
- Relic inventory and tooltip components render state and forward selection intent.

The current inventory limit is `RelicManager.MaximumRelicCount` (8). Stacking rules come from each relic's authored data. Do not represent run mutation by editing a shared `RelicData` asset.

## Change workflow

Trace acquisition, duplicate/stack handling, effect eligibility, trigger order, charge consumption, removal, UI notification, run capture, normalization, and restore. Use acquisition order as the deterministic tie-breaker where the existing pipeline does so. Emit one authoritative state change after a coherent mutation rather than having UI infer it.

Keep damage preview non-consuming: it may calculate deterministic relic modifiers but must not roll probability, spend charges, advance counters, or raise gameplay events. Guard recursive effects and extra attacks with the existing source/context flags so one trigger cannot loop indefinitely. Use saturating arithmetic and existing large-number helpers for damage and stored totals.

When a relic changes bullet ownership, player action flow, enemy damage, shop rewards, or persistence, use the corresponding domain skill as well.

## Verify

Run `RelicManagerTests` and related damage/deck tests. Add focused coverage for a new effect's eligibility, ordering, stack/charge behavior, removal, recursion guard, preview purity, and save round trip. In Play Mode check the HUD/tooltip, trigger feedback, full inventory, replacement/removal, battle transition, and continue-game restore.

Read `Docs/Dev/0814_Relic_System_Design_Issue.md` for event, damage, numeric, and persistence design before changing relic mechanics.
