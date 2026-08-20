---
name: loaded-edit-shop-inventory
description: Change LOADED shop offers and purchases, currency, items, inventory, rewards, treasure choices, shop persistence, or related tooltips. Use for ShopManager, ShopOfferGenerator, CurrencyManager, RewardManager, TreasureSceneController, and Assets/Scripts/Item; use relic or bullet skills for the acquired object's internal rules.
---

# Edit LOADED Shop, Economy, and Inventory

## Ownership

- `ShopOfferGenerator` owns unique weighted offer selection.
- `ShopManager` owns shop offer state, validation, purchase orchestration, and shop presentation coordination.
- `CurrencyManager` owns currency balances and changes.
- `PlayerInventory` owns runtime item inventory; `ItemData` is authored configuration.
- `RewardManager` coordinates battle drops and pickup application.
- `TreasureSceneController` owns treasure-scene choice flow.
- Tooltip and inventory UI render data; labels such as purchased state are not transaction authority.

Bullet ownership remains with `DeckManager`; relic ownership and capacity remain with `RelicManager`.

## Preserve transaction integrity

Validate offer availability, price, capacity, and target eligibility before charging currency. Apply payment and acquisition as one coherent transaction; if an expected validation fails, leave currency, offer state, and inventory unchanged. Mark an offer purchased and raise completion events only after ownership succeeds.

Preserve deterministic weighted selection when a seed or current RNG sequence is expected. Avoid duplicate offers unless the authored rule explicitly allows them. Do not consume selection RNG from UI preview or layout code.

Trace shop save capture/restore and checkpoint timing for any offer or purchase change. Changes to bullet capacity, relic capacity, or item persistence also require their domain and run-save checks.

## Verify

Add or run focused EditMode tests for weighted selection, uniqueness, insufficient currency, full capacity, duplicate acquisition, and transaction atomicity. In Play Mode check shop entry/exit, reroll or refresh if applicable, purchase feedback, currency display, inventory/tooltips, reward pickup, treasure choices, scene return, and continue-game restoration.

Read `Docs/Dev/0718_Shop_Reward_StageSystem.md`, `Docs/Dev/0719_ItemShopAndTooltipSystem.md`, and `Docs/Dev/0719_BulletManagementUpgradeSystem.md` only for the affected flow.
