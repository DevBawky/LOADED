---
name: loaded-edit-statistics
description: Change LOADED lifetime or run statistics, combat counters, record aggregation, statistics persistence, event-condition statistics, or statistics UI. Use for GameStatistics, GameStatisticsData, StatisticsPanelController, and statistic producers/consumers; do not use for general run-save fields with no statistic meaning.
---

# Edit LOADED Statistics

## Ownership

- `GameStatistics` owns live aggregation and lifetime persistence under `loaded.statistics.v1`.
- `GameStatisticsData` is the serializable statistics shape.
- `StatisticsPanelController` renders statistics.
- Combat, player, enemy, shop, event, and result systems publish facts; they do not independently maintain duplicate totals.
- Run-save snapshot fields used to resume an unfinished aggregate are distinct from lifetime persistence.

## Change workflow

Define the event being counted and its exact commit point before adding a field. Count resolved domain outcomes, not animation callbacks or UI updates. Search all producers and consumers, including event weighting/requirements and ending or battle-report screens.

Prevent double counting across retries, extra shots, chained damage, scene restoration, and duplicate event subscriptions. Preserve saturating `long` arithmetic for damage and totals. Treat per-cylinder, per-battle, per-run, and lifetime values as separate scopes and reset them only at their owning boundary.

If a field must resume mid-run, update the run-save capture, normalization, and restore path in addition to statistics persistence. Do not silently repurpose an existing statistic whose historical meaning would change.

## Verify

Add focused tests for the producing event, aggregation, reset boundary, overflow saturation, persistence round trip, and restore without double counting. In Play Mode compare the authoritative event with the statistics panel and any event-condition or result-screen consumer.

Use `Docs/Dev/0805_CombatFeedback_ComboGold_Kick_CameraShake.md` and `Docs/Dev/0808_Conversation_Implementation_Log.md` when changing combat or report statistics.
