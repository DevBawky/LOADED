# Treasure combat experiment baseline

## Shared starting state

The Treasure scene is currently a combat-shaped scaffold for the planned AI
implementation comparison video. It reuses the Battle environment, player,
deck, cylinder HUD, movement, reload, and firing pipeline while suppressing
normal battle startup, waves, enemy turns, and the battle report.

- The board has one lane and ten tiles.
- Tile spacing is `2`, matching the Battle scene. The Treasure camera follows
  a board-centre anchor so all ten full-size tiles share one stable frame.
- The player starts on tile 1 and faces right.
- Three fixed chest targets occupy tiles 3, 5, and 7.
- Chests do not reserve or block movement tiles.
- Tile 10 has a persistent green highlight and the label
  `다음 지역으로 이동`, centred over the tile.
- Chests use the thick-outline American-cartoon sprite at
  `Assets/Sprites/Environment/Treasure/TreasureChest.png`, cropped to its
  visible alpha and authored with a bottom-centre pivot. Their world transform
  is the board tile position, so the sprite baseline sits on the tile floor.
- Chest maximum health is the rounded ADPC captured on first entry. ADPC is
  the sum of the current owned bullets' authored/upgraded base damage divided
  by the number of cylinders needed at the current cylinder capacity. The
  result is rounded down to one leading digit (`9786 -> 9000`). Chance,
  critical, status, and random effect outcomes do not affect this entry-time
  value.
- Each chest displays current and maximum health. Chest health, destroyed
  state, pending rewards, and the active offer are checkpointed in the run
  save, so reopening the scene neither heals chests nor rerolls an offer.
- Hovering a loaded cylinder bullet runs the same deterministic cumulative
  damage preview used in Battle. Each chest health bar overlays the guaranteed
  damage segments up to the hovered bullet and shows the predicted remaining
  durability; ending the hover restores the authoritative health display.
- Chests participate in directional, penetrating, repeated, and board-wide
  player shots through `PlayerAttackTargetRegistry`.
- Chests do not expose an enemy status controller. Debuffs, knockback, and
  position swap therefore cannot mutate them. Shot-wide damage calculation
  and player-side effects still resolve through the existing firing sequence.
- Every destroyed chest queues one relic reward. The current firing sequence
  settles before the modal opens, allowing one chained attack to destroy
  several chests. When that firing sequence ends, every chest that survived
  the sequence withdraws without granting a reward and cannot be targeted
  again during the visit. Each queued modal offers up to three uniform
  available relics and can be resolved by acquisition or skip. Combat input
  remains locked until all earned reward modals are resolved.
- The combat HUD remains visible in Treasure, including the cylinder and its
  currently loaded bullet icons. The battle-only cylinder tempo panel remains
  hidden.
- Successful chest hits reuse the shared combat contact flare, sparks,
  optical impact, camera feedback, and hit stop. The chest also performs a
  short local squash-and-rebound, while enemy defeat combo/status presentation
  remains excluded.
- Reaching tile 10 after the reward modal is closed checkpoints the run,
  completes the active node, clears the Treasure visit state, and returns to
  NodeMap through `LoadingTransitionController`.

The runtime fallback chest sprite remains only as missing-reference safety;
the authored Treasure sprite is the normal scene asset.

## Deliberately deferred to the video experiment

The proposed treasure-specialist bullet remains deliberately excluded from
this implementation. It can be evaluated later without changing the chest
target or reward queue contract.

## Filming checklist

Immediately before recording, create one fixed run snapshot/test fixture and
reuse it for all three implementations. Record at least the bullet instances,
acquisition order, cylinder order, relics, items, money, health, RNG state,
node-map state, and Treasure entry checkpoint. Do not author this fixture in
the baseline branch: create it after the final feature requirements and test
bullet set are frozen.

Use the same camera framing and the same action script for every comparison,
including movement, reload timing, shot order, chest choice/skip, and exit.
