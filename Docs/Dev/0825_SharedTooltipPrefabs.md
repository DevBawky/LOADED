# Shared Tooltip Prefabs

## Ownership

All gameplay tooltip layouts are authored under
`Assets/Prefabs/UI/Shared/Tooltips/`.

`Panel_Tooltips.prefab` is only a full-canvas container. Its children are
nested instances of the individual tooltip prefabs; it does not own copied
tooltip hierarchies.

Battle, Shop, Treasure, and Event each use the same nested
`Panel_Tooltips.prefab` instance. The upgrade tooltip remains under
`Layout | Bullet Manage` so its anchors retain the intended parent, but every
management screen references the same `Panel_UpgradeTooltip.prefab` asset.

## Authoring

Edit the individual prefab in `Assets/Prefabs/UI/Shared/Tooltips/` when changing
a tooltip. The change propagates through `Panel_Tooltips.prefab` to every
gameplay Canvas.

Use `Tools > LOADED > Build Shared Tooltip Prefabs` only to migrate or repair
the nested prefab wiring. Existing individual tooltip assets remain the
authoritative source and are not overwritten from a scene instance.

Do not add a scene-specific copy of a tooltip hierarchy. Add a new individual
tooltip prefab definition to `ShopSceneSetupBuilder`, include it in the shared
container when appropriate, and extend the prefab-link tests.
