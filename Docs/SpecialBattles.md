# Special Battles

Special Battle nodes reuse a normal `BattleData` selected from the node's
progress section and apply exactly one `SpecialBattleRule`. The rule is saved
separately from `battleIndex`, so existing `StageData.Battles` ordering and old
version-3 saves remain valid.

The node map places two or three Special Battle nodes per generated stage,
never more than one in the same column and without repeating a rule in that
stage. The selected rule and normal-battle variant are deterministic for the
map seed.

## Rules

- `DeathBombs`: every non-final enemy death creates the dedicated special
  battle bomb on that cell. Cause of death is irrelevant, so bomb kills can
  create replacement bombs. Enemy spawning does not reserve or exclude bomb
  cells. Existing bombs are cleared when the battle completes.
- `DamageSurge`: all combat damage dealt to players or enemies is multiplied
  by 1.5 and rounded upward. Explicit health costs are not damage and are not
  multiplied.
- `ReverseCylinder`: the loaded-bullet collection and save order are preserved;
  only the firing-order policy changes from highest-to-lowest index to
  lowest-to-highest index. Preview, range, relic, tooltip, and active-effect
  queries use the same policy.
- `BloodReload`: each successful reload costs 1 current health. Reload is
  rejected before any bullet, turn, or presentation state changes when the
  player has only 1 health, so the cost cannot defeat the player. Every enemy
  death heals 3 health, regardless of its cause.

`WaveManager.ActiveRules` is the authoritative runtime context. A normal or
Battle Test encounter uses `SpecialBattleRule.None`, which preserves existing
combat behavior.

## Authoring

Run **Loaded > Setup Special Battles** after changing the special node icon or
the source Big Barrel bomb presentation. It creates or updates
`Resources/SpecialBattle/SpecialBattleBombProfile.asset`, assigns the Start,
Store, Reward, Event, and Special node icons, and ensures the node generation
and description entries exist.
