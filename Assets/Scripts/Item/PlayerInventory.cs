using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int MaximumSlotCount = 3;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private DeckManager deckManager;

    [Header("Inventory")]
    [SerializeField, Range(1, MaximumSlotCount)] private int slotCount =
        MaximumSlotCount;
    [SerializeField] private ItemData[] startingItems;

    private ItemData[] items;

    public event Action Changed;

    public int SlotCount => slotCount;
    public bool IsFull => FindEmptySlotIndex() < 0;

    private void Awake()
    {
        slotCount = Mathf.Clamp(slotCount, 1, MaximumSlotCount);

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (deckManager == null)
        {
            deckManager = FindFirstObjectByType<DeckManager>();
        }

        items = new ItemData[slotCount];

        int count = startingItems == null ? 0 : Mathf.Min(slotCount, startingItems.Length);

        for (int index = 0; index < count; index++)
        {
            ItemData startingItem = startingItems[index];

            if (startingItem != null && !Contains(startingItem))
            {
                items[index] = startingItem;
            }
        }
    }

    private void OnValidate()
    {
        slotCount = Mathf.Clamp(slotCount, 1, MaximumSlotCount);
    }

    public ItemData GetItem(int slotIndex)
    {
        if (items == null || slotIndex < 0 || slotIndex >= items.Length)
        {
            return null;
        }

        return items[slotIndex];
    }

    public bool TryAdd(ItemData item)
    {
        if (!CanAdd(item))
        {
            return false;
        }

        int emptySlotIndex = FindEmptySlotIndex();

        if (emptySlotIndex < 0)
        {
            return false;
        }

        items[emptySlotIndex] = item;
        Changed?.Invoke();
        return true;
    }

    public bool CanAdd(ItemData item)
    {
        return item != null && !Contains(item) && FindEmptySlotIndex() >= 0;
    }

    public bool Contains(ItemData item)
    {
        if (item == null || items == null)
        {
            return false;
        }

        foreach (ItemData storedItem in items)
        {
            if (storedItem == item)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryUse(int slotIndex)
    {
        ItemData item = GetItem(slotIndex);

        if (item == null || !item.TryApply(playerHealth, deckManager))
        {
            return false;
        }

        items[slotIndex] = null;
        Changed?.Invoke();
        return true;
    }

    private int FindEmptySlotIndex()
    {
        if (items == null)
        {
            return -1;
        }

        for (int index = 0; index < items.Length; index++)
        {
            if (items[index] == null)
            {
                return index;
            }
        }

        return -1;
    }
}
