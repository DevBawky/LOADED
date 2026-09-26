# Battle 3D Prototype

## Scope

- `NodeMap` and the other non-battle scenes keep their current 2D presentation.
- Only `Battle` uses the 2.5D world setup.
- Player and enemy artwork remains sprite based and faces the battle camera.
- The board, ground, lighting, camera framing, and projectile presentation use
  3D space.
- The existing `##--BACKGROUNDS--##` active state is preserved by the setup
  builder. It is currently disabled.
- Existing combat feedback remains in its current pipeline. A separate 3D
  feedback pass is outside this prototype.

## Authored data

`BattleEnvironmentProfile` owns the reusable Battle world presentation:

- board position and rotation;
- orthographic camera transform and framing;
- URP renderer index;
- directional light color, strength, and direction.

The default camera pitch is 35 degrees. This increases the screen-space depth
of each lane while keeping the board coordinates and tile spacing unchanged.
The Cinemachine camera follows `Player/Avatar`; its follow offset places that
target at screen center instead of trying to keep the whole battlefield in a
fixed frame.

The Battle Terrain is a persistent object under `##--ENVIRONMENT--##` in
`Battle.unity`. Terrain sculpting, painting, trees, details, transform, and
material changes are authored directly in the scene and TerrainData asset.
They are not created or reset at runtime.

Every current `BattleData` points to
`Resources/Battle/DefaultBattleEnvironment.asset`.

`ProjectileVisualProfile` owns the visual prefab, arc, and scale of a
projectile. Every current `BulletData` points to the shared
`Resources/ProjectileVisuals/DefaultProjectileVisual.asset`. The default
prefab is a collider-free Sphere. Bullet gameplay rules and damage resolution
remain owned by the existing bullet and firing systems.

Player projectiles travel from the fire point to the target impact point using
a per-shot interval resolved from board tile distance:
`Clamp(0.04 + tileDistance * 0.02, 0.06, 0.14)` seconds. This produces 0.06
seconds at one tile, 0.08 at two, 0.10 at three, 0.12 at four, and 0.14 at five
or more tiles. An unresolved distance uses the maximum `0.14` second delay.
This duration controls only projectile arrival and damage timing.
`PlayerShoot.shotInterval` independently keeps sequential shot launches at a
fixed `0.15` second cadence. Projectile travel and cadence both use unscaled
time while still stopping for an explicit game pause, so muzzle and impact
hit-stop do not lengthen either timer. `PlayerShoot` applies damage only after
the projectile reaches the target and waits out any cadence time remaining
after the shot's rule resolution. A missing or destroyed visual does not
cancel or delay gameplay resolution.

The current Player prefab uses a `0.15` second shot interval. The shared
projectile profile uses scale `0.17` and arc height `0.05` so the sphere reads
as a fast bullet instead of a large floating orb.

## Runtime flow

1. `StateManager` applies the selected battle's environment profile before
   configuring the board.
2. `BattleWorld3DController` rotates the board plane onto XZ, configures the
   player-following Battle camera and Universal Renderer, and applies the
   directional light. The Terrain remains scene-authored.
3. `BoardManager` continues to generate cells in its own local XY coordinates.
   `TransformPoint` maps those cells onto the XZ ground plane, so save indices,
   lane rules, targeting, and movement stay unchanged.
4. Player and spawned enemy avatar roots use `BattleSpriteBillboard` to face
   the camera while their owning gameplay objects stay at exact board world
   positions. The billboard aligns its horizontal axis with the camera so the
   gameplay facing sign is preserved on screen.
5. `PlayerShoot` creates `BulletProjectileView` as presentation for each shot,
   advances it to the target over the distance-resolved travel duration, and
   then resolves damage and effects. A separate fixed `0.15` second cadence
   controls the next sequential shot. Both timings remain authoritative even
   if the optional projectile object is unavailable.
6. Enemy world-space HUDs retain their authored lane layout: health and action
   UI stay below lower-lane enemies and above upper-lane enemies. Lane changes
   continue to interpolate those positions in `EnemyActionQueueUI`.
   `BattleWorldCanvasDepthOffset` moves only the Canvas render depth along the
   camera ray and compensates its horizontal scale for camera pitch. This
   preserves its screen position and authored lane layout, keeps it in front
   of the 3D Terrain depth buffer, and avoids horizontal stretching.
7. Damage Numbers Pro popups and procedural SpriteRenderer combat effects face
   the pitched Battle camera. Their authored screen proportions are preserved
   instead of being vertically foreshortened on the world XY plane.

## Rebuilding

Run `Tools > LOADED > Apply Battle 3D Prototype` after adding new BulletData or
BattleData assets. The builder is idempotent and updates:

- the Universal 3D Renderer entry;
- the directional light and, only when absent, the initial flat Terrain;
- Battle camera and board scene wiring;
- the shared Sphere projectile prefab and profile;
- all BulletData and BattleData references.

Once the Terrain assets and scene object exist, rerunning the builder preserves
their authored contents and placement so the battlefield can be decorated in
the Terrain tools.
