using System.Collections;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private BoardManager boardManager;

    [Header("Dropped Item Presentation")]
    [Min(0f)] [SerializeField] private float itemDropDuration = 0.45f;
    [Min(0f)] [SerializeField] private float itemDropArcHeight = 0.8f;
    [Min(0.01f)] [SerializeField] private float itemDropSpriteSize = 0.75f;
    [SerializeField] private Vector3 itemLandingOffset =
        new Vector3(0f, 0.12f, 0f);
    [SerializeField] private int itemSortingOrder = 25;

    private void Awake()
    {
        currencyManager ??= FindFirstObjectByType<CurrencyManager>();
        deckManager ??= FindFirstObjectByType<DeckManager>();
        playerInventory ??= FindFirstObjectByType<PlayerInventory>();
        playerMove ??= FindFirstObjectByType<PlayerMove>();
        boardManager ??= FindFirstObjectByType<BoardManager>();
    }

    public bool GrantEnemyDrop(EnemyData enemyData)
    {
        if (enemyData == null)
        {
            return false;
        }

        bool grantedGold = currencyManager != null
            && currencyManager.AddMoney(enemyData.RollGuaranteedGoldDrop());
        bool grantedItem = enemyData.TryRollDrop(
                out EnemyDropItemData dropItem)
            && GrantDrop(dropItem);
        return grantedGold || grantedItem;
    }

    public bool SpawnEnemyDrop(EnemyData enemyData, Vector3 defeatedPosition)
    {
        if (enemyData == null)
        {
            return false;
        }

        bool grantedGold = currencyManager != null
            && currencyManager.AddMoney(enemyData.RollGuaranteedGoldDrop());

        if (!enemyData.TryRollDrop(out EnemyDropItemData dropItem))
        {
            return grantedGold;
        }

        if (dropItem.DropType != EnemyDropType.InventoryItem)
        {
            return GrantDrop(dropItem) || grantedGold;
        }

        if (dropItem.ItemData == null || boardManager == null
            || playerInventory == null || playerMove == null
            || !boardManager.TryGetTileIndex(
                defeatedPosition,
                out int tileIndex)
            || !boardManager.TryGetTilePosition(
                tileIndex,
                out Vector3 tilePosition))
        {
            return grantedGold;
        }

        bool spawnedAny = false;

        for (int count = 0; count < dropItem.Amount; count++)
        {
            GameObject dropObject = new GameObject(
                $"Dropped Item | {dropItem.ItemData.DisplayName}");
            DroppedItemPickup pickup =
                dropObject.AddComponent<DroppedItemPickup>();
            pickup.Initialize(
                dropItem.ItemData,
                playerInventory,
                playerMove,
                boardManager,
                tileIndex,
                defeatedPosition,
                tilePosition + itemLandingOffset,
                itemDropDuration,
                itemDropArcHeight,
                itemDropSpriteSize,
                itemSortingOrder);
            spawnedAny = true;
        }

        return spawnedAny || grantedGold;
    }

    public bool GrantDrop(EnemyDropItemData dropItem)
    {
        if (dropItem == null || !dropItem.IsConfigured)
        {
            return false;
        }

        return dropItem.DropType switch
        {
            EnemyDropType.Gold => currencyManager != null
                && currencyManager.AddMoney(dropItem.Amount),
            EnemyDropType.InventoryItem => GrantInventoryItems(dropItem),
            EnemyDropType.Bullet => GrantBullets(dropItem),
            _ => false
        };
    }

    public void CollectAndDestroyAllDroppedItems()
    {
        DroppedItemPickup[] droppedItems =
            FindObjectsByType<DroppedItemPickup>(FindObjectsSortMode.None);

        foreach (DroppedItemPickup droppedItem in droppedItems)
        {
            if (droppedItem != null)
            {
                droppedItem.TryCollectForStageClear();
            }
        }
    }

    private bool GrantInventoryItems(EnemyDropItemData dropItem)
    {
        if (playerInventory == null || dropItem.ItemData == null)
        {
            return false;
        }

        bool grantedAny = false;

        for (int count = 0; count < dropItem.Amount; count++)
        {
            if (!playerInventory.TryAdd(dropItem.ItemData))
            {
                break;
            }

            grantedAny = true;
        }

        return grantedAny;
    }

    private bool GrantBullets(EnemyDropItemData dropItem)
    {
        if (deckManager == null || dropItem.BulletData == null)
        {
            return false;
        }

        bool grantedAny = false;

        for (int count = 0; count < dropItem.Amount; count++)
        {
            if (!deckManager.TryAddBullet(dropItem.BulletData))
            {
                break;
            }

            grantedAny = true;
        }

        return grantedAny;
    }
}

public class DroppedItemPickup : MonoBehaviour
{
    private ItemData itemData;
    private PlayerInventory playerInventory;
    private PlayerMove playerMove;
    private BoardManager boardManager;
    private int tileIndex = -1;
    private bool isLanded;
    private bool isCollecting;

    public void Initialize(
        ItemData assignedItemData,
        PlayerInventory assignedInventory,
        PlayerMove assignedPlayerMove,
        BoardManager assignedBoardManager,
        int assignedTileIndex,
        Vector3 startPosition,
        Vector3 landingPosition,
        float duration,
        float arcHeight,
        float spriteSize,
        int sortingOrder)
    {
        itemData = assignedItemData;
        playerInventory = assignedInventory;
        playerMove = assignedPlayerMove;
        boardManager = assignedBoardManager;
        tileIndex = assignedTileIndex;
        transform.position = startPosition;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, spriteSize);

        SpriteRenderer spriteRenderer =
            gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = itemData == null ? null : itemData.Icon;
        spriteRenderer.sortingOrder = sortingOrder;

        SpriteRenderer playerRenderer =
            playerMove.GetComponentInChildren<SpriteRenderer>();

        if (playerRenderer != null)
        {
            spriteRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        }

        playerInventory.Changed += TryCollect;
        playerMove.PositionChanged += TryCollect;
        StartCoroutine(DropRoutine(
            startPosition,
            landingPosition,
            duration,
            arcHeight));
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.Changed -= TryCollect;
        }

        if (playerMove != null)
        {
            playerMove.PositionChanged -= TryCollect;
        }
    }

    private IEnumerator DropRoutine(
        Vector3 startPosition,
        Vector3 landingPosition,
        float duration,
        float arcHeight)
    {
        duration = Mathf.Max(0f, duration);
        arcHeight = Mathf.Max(0f, arcHeight);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 position = Vector3.Lerp(
                startPosition,
                landingPosition,
                smoothProgress);
            position += Vector3.up
                * (Mathf.Sin(progress * Mathf.PI) * arcHeight);
            transform.position = position;
        }

        transform.position = landingPosition;
        isLanded = true;
        TryCollect();
    }

    private void TryCollect()
    {
        if (!isLanded || isCollecting || itemData == null
            || playerInventory == null || playerMove == null
            || boardManager == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            || playerTileIndex != tileIndex)
        {
            return;
        }

        isCollecting = true;

        if (playerInventory.TryAdd(itemData))
        {
            Destroy(gameObject);
            return;
        }

        isCollecting = false;
    }

    public bool TryCollectForStageClear()
    {
        if (isCollecting)
        {
            Destroy(gameObject);
            return false;
        }

        isCollecting = true;

        if (playerInventory != null)
        {
            playerInventory.Changed -= TryCollect;
        }

        if (playerMove != null)
        {
            playerMove.PositionChanged -= TryCollect;
        }

        bool collected = itemData != null && playerInventory != null
            && playerInventory.TryAdd(itemData);
        Destroy(gameObject);
        return collected;
    }
}
