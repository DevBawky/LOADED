using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField, Min(1)] private int slotCount = 5;
    [SerializeField] private ItemData[] startingItems;

    private ItemData[] items;

    public event Action Changed;

    public int SlotCount => slotCount;

    private void Awake()
    {
        items = new ItemData[slotCount];

        int count = startingItems == null ? 0 : Mathf.Min(slotCount, startingItems.Length);

        for (int index = 0; index < count; index++)
        {
            items[index] = startingItems[index];
        }
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
        if (item == null)
        {
            return false;
        }

        for (int index = 0; index < items.Length; index++)
        {
            if (items[index] != null)
            {
                continue;
            }

            items[index] = item;
            Changed?.Invoke();
            return true;
        }

        return false;
    }
}
