# Battle 3D Prototype

## Scope

- `NodeMap` and the other non-battle scenes keep their current 2D presentation.
- Only `Battle` uses the 2.5D world setup.
- Player and enemy artwork remains sprite based and faces the battle camera.
- The board, ground, lighting, camera framing, and projectile presentation use
  3D space.
- The existing `##--BACKGROUNDS--##` active state is preserved by the setup
  builder. It is currently disabled.
- Existing combat feedback remains in its current pipeline. The 3D renderer
  reuses its fullscreen impact feature and URP post-processing data instead of
  adding a separate world-space impact layer.
- The project remains on URP rather than migrating to HDRP. Battle uses an HDR
  camera, ACES tonemapping, SMAA, full-resolution SSAO, soft main-light
  shadows, environment fog, reflection probes, and light probes as its HD
  presentation baseline.
- The default lighting balance uses a warm 1.65 key light, a cool 0.38 fill,
  1.12 ambient intensity, and four shadow cascades. SSAO is limited to 0.85 so
  low-poly contact detail remains visible without crushing the whole arena.
- Player and shared Enemy prefabs align their visible foot sprites to the
  current Terrain height and project every animated sprite part into an
  alpha-cut silhouette shadow. This preserves the existing 2D sprite shaders,
  animation, and horizontal flip behavior while grounding them in the 3D set.

## Authored data

`BattleEnvironmentProfile` owns the reusable Battle world presentation:

- board position and rotation;
- Perspective camera transform, FOV, and legacy orthographic fallback framing;
- URP renderer index;
- directional light color, strength, and direction.
- URP gradient Skybox, ambient/reflection strength, distance fog, and the Battle
  Volume Profile.

The default camera uses a 40-degree Perspective lens at a 35-degree pitch.
This introduces visible lane and prop depth while keeping the full seven-tile
board safely inside a 16:9 frame. The previous orthographic size remains as a
fallback and as the reference framing value for presentation tests.
The Cinemachine camera follows `Player/Avatar`; its follow offset places that
target at screen center instead of trying to keep the whole battlefield in a
fixed frame.

The Battle Terrain is a persistent object under `##--ENVIRONMENT--##` in
`Battle.unity`. Terrain sculpting, painting, trees, details, transform, and
material changes are authored directly in the scene and TerrainData asset.
They are not created or reset at runtime.

`Props | Battle` is the authoring root for future 3D set dressing. Its
`Architecture`, `Ground Detail`, `Background`, `Foreground`, and
`Environment FX` children separate buildings and terrain dressing by visual
depth without giving them gameplay responsibility. `Lighting | Battle` owns
the cool fill light, realtime arena reflection probe, and an 18-point light
probe grid. Existing children are preserved when the builder is rerun.

`Assets/Materials/Battle3DSkybox.mat` is a neutral dusk Procedural Skybox.
`Assets/Settings/BattleEnvironmentVolume.asset` preserves the existing Old
Movie override and adds ACES tonemapping, restrained HDR Bloom, color balance,
and vignette. The camera Volume uses this shared asset in edit mode and clones
it at runtime when combat feedback temporarily pulses its overrides.

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
   player-following Battle camera and Universal Renderer, and applies one soft
   shadow-casting directional key light. Skybox ambient/reflection lighting,
   exponential-squared fog, and the global URP Volume provide the remaining
   environment illumination; no legacy 2D global light or secondary
   directional fill light is used. The forward 3D renderer is also the URP
   default so the Unity Scene View can draw the Terrain; the Battle camera
   keeps an explicit renderer assignment. The Main Camera's scene-authored
   Transform and world-space Cinemachine follow offset own the initial view;
   environment profiles may change lens and rendering settings but never
   replace that authored pose. The Terrain remains scene-authored.
3. `BoardManager` continues to generate cells in its own local XY coordinates.
   `TransformPoint` maps those cells onto the XZ ground plane, so save indices,
   lane rules, targeting, and movement stay unchanged.
4. Player and spawned enemy avatar roots use `BattleSpriteBillboard` to face
   the camera while their owning gameplay objects stay at exact board world
   positions. The billboard aligns its horizontal axis with the camera so the
   gameplay facing sign is preserved on screen. `BattleContactShadow` moves
   only that visual root so the midpoint of the visible foot sprites matches
   the owning grid cell center on X/Z and their lowest edge stays on the active
   Terrain; gameplay roots, board indices, and `ActorMotion` jumps remain
   authoritative. Every visible body-part SpriteRenderer is also
   projected onto the Terrain along the directional key light and rendered
   through the sprite's alpha, producing the current animated character
   silhouette instead of a generic oval contact blob. The foot anchor is
   cached in avatar-local space, and camera shake, billboard facing, grounding,
   screen-space optical centering, then world-space HUD projection run in that
   order. The stable visual center excludes held weapons, so asymmetric props
   and animation poses do not pull the character away from the projected grid
   center. Shot animation and camera shake therefore cannot make actor roots
   hop between visual offsets.
5. `PlayerShoot` creates `BulletProjectileView` as presentation for each shot,
   advances it to the target over the distance-resolved travel duration, and
   then resolves damage and effects. A separate fixed `0.15` second cadence
   controls the next sequential shot. Both timings remain authoritative even
   if the optional projectile object is unavailable.
6. Enemy world-space HUDs retain their authored lane layout: health and action
   UI stay below lower-lane enemies and above upper-lane enemies. Lane changes
   interpolate those positions with the same duration and easing as the enemy
   move. The lane sorting commit does not restart or snap that interpolation.
   `BattleWorldCanvasDepthOffset` moves only the Canvas render depth along the
   camera ray, turns the Canvas fully toward the camera, and compensates its
   scale for Perspective depth and camera pitch. This preserves its screen
   position and apparent size, keeps it in front of the 3D Terrain depth
   buffer, and avoids perspective skew.
   The compensation preserves the Canvas runtime X sign so the enemy's
   counter-flip continues to cancel actor facing and UI never mirrors. Canvas
   projection runs after combat camera shake so a hit cannot leave the HUD one
   camera frame behind. The health-bar impact keeps its horizontal anchor and
   uses only vertical micro-shake, squash, flash, and shards for local feedback.
7. Damage Numbers Pro popups and procedural SpriteRenderer combat effects face
   the pitched Battle camera. Damage text uses the camera's plane rotation
   instead of pointing individually at the camera position, so every popup is
   perfectly front-facing across the screen. Their authored screen proportions
   remain intact while the camera naturally gives the two lanes a small depth
   difference. Impact snapshots reuse the character's stable, weapon-excluded
   optical center and pin it to the projected grid center line. Procedural
   spark, streak, and popup offsets also use the current camera right/up axes
   instead of world X/Y. Camera position, rotation, and FOV can therefore be
   authored without retuning per-effect offsets. Popup anchors begin at the
   camera-aligned Avatar root, and overlap checks use the same camera plane, so
   lane Z separation is reflected on screen. Repeated numbers may spread only
   two compact rows from their target; a saturated layout reuses the clearest
   bounded slot instead of drifting farther away. The fullscreen kill
   shockwave continues to derive its center with `WorldToViewportPoint`, and
   passes that normalized viewport coordinate directly to URP's fullscreen
   blit without an additional platform Y flip. Its hit position therefore
   follows the same projection-independent impact anchor.
8. `BattleCameraEdgeHoverController` calculates visible board width from the
   active lens. Edge hover therefore keeps its authored viewport inset in
   both Perspective and the orthographic fallback.

## Rebuilding

Run `Tools > LOADED > Apply Battle 3D Prototype` after adding new BulletData or
BattleData assets. The builder is idempotent and updates:

- the Universal 3D Renderer entry, combat fullscreen feature, and post-process
  data, plus Battle-only SSAO;
- the URP gradient Skybox, Battle Volume Profile, soft directional lighting,
  fill light, probes, Prop authoring roots, and only when absent, the initial
  flat Terrain;
- Battle camera and board scene wiring;
- the shared Sphere projectile prefab and profile;
- ground-projected contact shadows on the Player and shared Enemy prefabs;
- all BulletData and BattleData references.

Once the Terrain assets and scene object exist, rerunning the builder preserves
their authored contents and placement so the battlefield can be decorated in
the Terrain tools.
