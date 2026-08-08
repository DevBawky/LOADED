using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    public const int MaximumSlotCount = 3;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private WaveManager waveManager;

    [Header("Inventory")]
    [SerializeField, Range(1, MaximumSlotCount)] private int slotCount =
        MaximumSlotCount;
    [SerializeField] private ItemData[] startingItems;

    private ItemData[] items;
    private InputAction useItemHotkeyAction;

    public event Action Changed;
    public event Action<int, ItemData> ItemUsed;

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

        playerMove ??= GetComponent<PlayerMove>();
        waveManager ??= FindFirstObjectByType<WaveManager>();

        items = new ItemData[slotCount];

        int count = startingItems == null ? 0 : Mathf.Min(slotCount, startingItems.Length);

        for (int index = 0; index < count; index++)
        {
            ItemData startingItem = startingItems[index];

            if (startingItem != null)
            {
                items[index] = startingItem;
            }
        }

        CreateUseItemHotkeyAction();
    }

    private void OnEnable()
    {
        useItemHotkeyAction?.Enable();
    }

    private void OnDisable()
    {
        useItemHotkeyAction?.Disable();
    }

    private void OnDestroy()
    {
        if (useItemHotkeyAction == null)
        {
            return;
        }

        useItemHotkeyAction.performed -= HandleUseItemHotkey;
        useItemHotkeyAction.Dispose();
        useItemHotkeyAction = null;
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
        return item != null && FindEmptySlotIndex() >= 0;
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

        if (item == null || !item.TryApply(
                playerHealth,
                deckManager,
                playerMove,
                waveManager))
        {
            return false;
        }

        items[slotIndex] = null;
        Changed?.Invoke();
        ItemUsed?.Invoke(slotIndex, item);
        return true;
    }

    public bool TryRemove(int slotIndex)
    {
        if (GetItem(slotIndex) == null)
        {
            return false;
        }

        items[slotIndex] = null;
        Changed?.Invoke();
        return true;
    }

    public void CaptureRunState(System.Collections.Generic.List<string> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();

        for (int index = 0; index < slotCount; index++)
        {
            ItemData item = GetItem(index);
            results.Add(item == null ? string.Empty : item.name);
        }
    }

    public void RestoreRunState(
        System.Collections.Generic.IReadOnlyList<string> savedItemNames,
        Func<string, ItemData> resolveItemData)
    {
        items = new ItemData[slotCount];

        if (savedItemNames != null && resolveItemData != null)
        {
            int count = Mathf.Min(slotCount, savedItemNames.Count);

            for (int index = 0; index < count; index++)
            {
                string assetName = savedItemNames[index];

                if (!string.IsNullOrWhiteSpace(assetName))
                {
                    items[index] = resolveItemData(assetName);
                }
            }
        }

        Changed?.Invoke();
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

    private void CreateUseItemHotkeyAction()
    {
        useItemHotkeyAction = new InputAction(
            "Use Inventory Item",
            InputActionType.Button);
        useItemHotkeyAction.AddBinding("<Keyboard>/1");
        useItemHotkeyAction.AddBinding("<Keyboard>/2");
        useItemHotkeyAction.AddBinding("<Keyboard>/3");
        useItemHotkeyAction.AddBinding("<Keyboard>/numpad1");
        useItemHotkeyAction.AddBinding("<Keyboard>/numpad2");
        useItemHotkeyAction.AddBinding("<Keyboard>/numpad3");
        useItemHotkeyAction.performed += HandleUseItemHotkey;
    }

    private void HandleUseItemHotkey(InputAction.CallbackContext context)
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        int slotIndex = context.control == keyboard.digit1Key
            || context.control == keyboard.numpad1Key
                ? 0
                : context.control == keyboard.digit2Key
                    || context.control == keyboard.numpad2Key
                    ? 1
                    : context.control == keyboard.digit3Key
                        || context.control == keyboard.numpad3Key
                        ? 2
                        : -1;

        if (slotIndex >= 0 && slotIndex < slotCount)
        {
            TryUse(slotIndex);
        }
    }
}
