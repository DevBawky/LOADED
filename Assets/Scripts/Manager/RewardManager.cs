using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerInventory playerInventory;

    public bool GrantEnemyDrop(EnemyData enemyData)
    {
        return enemyData != null
            && enemyData.TryRollDrop(out EnemyDropItemData dropItem)
            && GrantDrop(dropItem);
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
