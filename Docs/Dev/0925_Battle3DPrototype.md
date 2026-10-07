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
- The HD-style environment pass remains deliberately small: the sky renders
  its sun, halo, atmospheric gradient, and moving cloud layers procedurally.
  The ground blends three generated 128×128 style layers (dry soil, packed
  duel-track dirt, and faceted rock). Compact mipmapped textures use
  deterministic multi-scale crust, clod, grit, and pebble noise instead of
  directional sine bands, with stronger normals for a visibly broken surface.
  No panoramic sky, cloud, scanned material, or high-resolution terrain
  texture is added to the build. Terrain layers use zero metallic/smoothness,
  suppress specular highlights, and use generated irregular normals so the
  ground reads as coarse, dry wasteland rather than wet clay. Generated
  normals are packed for Unity Terrain's AG normal sampling, avoiding the
  false one-direction highlight that made the surface appear reflective. The
  generated diffuse alpha is zero because Terrain Lit can source smoothness
  from albedo alpha even when each TerrainLayer's smoothness is zero.
  The final soil palette uses a neutral Terrain-layer remap and a sampled pale
  sand/ochre range so the rendered ground stays beige instead of shifting to
  saturated orange. The zero-smoothness response and surface breakup remain
  unchanged.
- The default lighting balance uses a warm 1.65 key light, 1.25 sky ambient
  intensity, and four shadow cascades. Shadow strength is limited to 0.82 and
  SSAO to 0.65 so low-poly contact detail remains visible without crushing the
  unlit faces of large props. No secondary directional fill is required.
- The HD-2D direction pass keeps actors and the duel board inside the sharp
  camera range. A low-cost Gaussian far-depth blur begins behind them and
  derives its maximum distance from the transformed far boundary of the
  authored Battle Terrain, rather than from a hard-coded cliff distance.
  Motion blur and baseline chromatic aberration remain disabled so pixel
  silhouettes do not smear.
- Church and saloon windows use a tiny project-owned additive glow material
  and one shadowless warm point light each. These localized accents feed the
  existing restrained Bloom without adding another full-screen brightness
  effect. A deterministic, low-density 32px dust-mote particle field adds slow
  atmospheric motion, and a small set of existing low-poly Western props
  breaks up the building/terrain seams. No new high-resolution texture set is
  introduced.
- Player and shared Enemy prefabs align their visible foot sprites to the
  current Terrain height and project every animated sprite part into an
  alpha-cut silhouette shadow. This preserves the existing 2D sprite shaders,
  animation, and horizontal flip behavior while grounding them in the 3D set.
- Battle avatars use `LOADED/Battle Lit Sprite` under the Universal 3D
  Renderer. It derives shallow per-pixel normals from the sprite luminance,
  receives the main light and its shadows, samples ambient probes, and adds a
  restrained rim/specular response without requiring normal maps for every
  animation frame. GPU instancing is deliberately disabled for this material:
  player and enemy avatars are assembled from independently animated
  SpriteRenderers, and instancing can reuse an incorrect part transform under
  the Universal 3D renderer. Explicit enemy material overrides remain
  authoritative.

## Authored data

`BattleEnvironmentProfile` owns the reusable Battle world presentation:

- board position and rotation;
- legacy camera lens defaults retained for setup-data compatibility;
- URP renderer index;
- directional light color, strength, and direction.
- URP gradient Skybox, ambient/reflection strength, distance fog, and the Battle
  Volume Profile.

The Battle scene's `Main Camera` component and `CinemachineCamera` lens are the
authoritative source for projection mode, FOV, orthographic size, and clipping
planes. This lets an artist tune the camera directly in the scene without the
environment profile replacing those values when Play Mode starts. The profile
keeps its former lens fields only as legacy setup data.
The Cinemachine camera follows `Player/Avatar`; its follow offset places that
target at screen center instead of trying to keep the whole battlefield in a
fixed frame.

The Battle Terrain is a persistent object under `##--ENVIRONMENT--##` in
`Battle.unity`. Terrain sculpting, painting, trees, details, transform, and
material changes are authored directly in the scene and TerrainData asset.
They are not created or reset at runtime.

`Props | Battle` is the authoring root for 3D set dressing. Its
`Architecture`, `Ground Detail`, `Background`, `Foreground`, and
`Environment FX` children separate buildings and terrain dressing by visual
depth without giving them gameplay responsibility. The setup pass moves the
current church/saloon under `Architecture` and the current cliff silhouettes
under `Background` while preserving world transforms. `Lighting | Battle`
owns a 128px baked arena reflection probe and an 18-point light-probe grid.
Static environment renderers contribute to low-resolution baked indirect
lighting and retain realtime main-light shadows. Existing children are
preserved when the builder is rerun.

`Assets/Materials/Battle3DSkybox.mat` is a texture-free procedural sky with a
warm directional sun, broad atmospheric halo, and slowly moving multi-scale
cloud banks. Its sun direction matches the authored Battle key light.
`Assets/Settings/BattleEnvironmentVolume.asset` preserves the existing Old
Movie override at a reduced baseline grain/noise level and adds ACES
tonemapping, restrained HDR Bloom, warm-highlight and cool-shadow split
toning, color balance, and a light vignette. A small texture-free SRP lens
flare follows the authored sun and uses depth occlusion, avoiding a permanent
full-screen glare. The camera
Volume uses this shared asset in edit mode and clones it at runtime when combat
feedback temporarily pulses its overrides.

`Assets/Materials/BattleWindowGlow.mat` and
`Assets/Materials/BattleAtmosphericDust.mat` are lightweight project-owned
materials. The window pass is applied only to scene instances whose renderer
name contains `Glass`; the source Synty materials and prefabs remain untouched.
The dust texture is generated as a 32×32 mipmapped `.asset`, so the ambience
adds negligible distribution size compared with texture-based sky or VFX
packages. Decorative prefab instances have their colliders disabled and do not
participate in board rules, targeting, or movement.

The authored `Train` under `##--ENVIRONMENT--##` uses
`BattleTrainRouteController` with four Inspector-assigned route Transforms.
It starts at `Train_StartPoint`, travels to `Train_FirstDestination` in 35
seconds, waits 10 seconds, teleports to `Train_SecondPoint`, flips its local X
scale negative, and travels to `Train_SecondDestination` in 35 seconds. After
another 10-second wait it restores positive X scale, returns to the start, and
repeats. Route motion and dwell time pause with `GamePauseController`.

The generated Battle Terrain layers combine tileable multi-frequency crust,
grit, and pebble color maps with matte PBR color remapping. Initial blend weights emphasize
packed dirt near the duel lanes, dry soil across open ground, and rock only on
steep/high ground. The one-time upgrade replaces the older rock-heavy blend;
afterward rerunning the setup preserves artist-painted blend weights.

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
fixed `0.05` second cadence. Projectile travel and cadence both use unscaled
time while still stopping for an explicit game pause, so muzzle and impact
hit-stop do not lengthen either timer. `PlayerShoot` applies damage only after
the projectile reaches the target and waits out any cadence time remaining
after the shot's rule resolution. A missing or destroyed visual does not
cancel or delay gameplay resolution.

The current Player prefab uses a `0.05` second shot interval. The shared
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
   Transform, lens values, clipping planes, and world-space Cinemachine follow
   offset own the initial view. Environment profiles apply renderer, color,
   post-processing, and world-lighting settings but never replace that authored
   camera state. The Terrain remains scene-authored.
   Distant renderers under `Props | Battle/Background` receive a non-destructive
   per-renderer fog tint based on camera distance, so low-poly mountain shapes
   separate into atmospheric depth bands without cloned materials.
3. `BoardManager` continues to generate cells in its own local XY coordinates.
   `TransformPoint` maps those cells onto the XZ ground plane, so save indices,
   lane rules, targeting, and movement stay unchanged.
4. Player and spawned enemy avatar roots use `BattleSpriteBillboard` to face
   the camera while their owning gameplay objects stay at exact board world
   positions. The billboard aligns its horizontal axis with the camera so the
   gameplay facing sign is preserved on screen. `BattleContactShadow` moves
   only that visual root so the midpoint of the visible foot sprites lands on
   the Terrain point seen through the projected center of its owning grid
   cell. The character therefore reads as centered in Game View under a
   Perspective camera even when the grid floats slightly above uneven ground;
   gameplay roots, board indices, and `ActorMotion` jumps remain authoritative.
   Every visible body-part SpriteRenderer is also
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
   then resolves damage and effects. A separate fixed `0.05` second cadence
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
   The Main Camera excludes the built-in `UI` layer. A child URP Overlay Camera
   renders only that layer after the Base Camera post-process pass, preserving
   the same perspective transform while keeping enemy health/action UI and
   other world-space UI perfectly sharp under Depth of Field. Screen Space
   Overlay canvases remain on Unity's normal overlay path.
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
  subtle sun flare, probes, low-resolution baked-lighting settings, Prop
  authoring roots, and only when absent, the initial flat Terrain;
- the generated three-layer Terrain style set and shared Battle Lit Sprite
  material;
- Battle camera and board scene wiring while preserving the camera's authored
  Transform and lens values;
- the shared Sphere projectile prefab and profile;
- ground-projected contact shadows on the Player and shared Enemy prefabs;
- all BulletData and BattleData references.

Run `Tools > LOADED > Apply Battle HD-2D Direction Pass` when only the current
lighting/depth/set-dressing presentation needs to be refreshed. The pass is
idempotent: it updates named lights, materials, dust, and named prop instances
without duplicating them or editing the source Asset Store prefabs.

Once the Terrain assets and scene object exist, rerunning the builder preserves
their authored contents and placement so the battlefield can be decorated in
the Terrain tools.
