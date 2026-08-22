using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private Image[] itemImages;

    private readonly List<int> eventSelectedSlots = new List<int>();
    private int eventRequiredSelectionCount;
    private Func<ItemData, bool> eventSelectionPredicate;
    private Action<IReadOnlyList<int>> eventConfirmCallback;
    private Action eventCancelCallback;

    public event Action<int, int> EventSelectionChanged;
    public bool IsEventSelectionActive => eventRequiredSelectionCount > 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (playerInventory != null)
        {
            playerInventory.Changed += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.Changed -= Refresh;
        }

        if (IsEventSelectionActive)
        {
            CancelEventSelection();
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        bool useRequested = mouse != null
            && mouse.leftButton.wasPressedThisFrame;
        bool sellRequested = mouse != null
            && mouse.rightButton.wasPressedThisFrame
            && shopManager != null
            && shopManager.CanSellInventoryItems;

        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || mouse == null
            || !useRequested && !sellRequested
            || playerInventory == null || itemImages == null)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        for (int index = 0; index < itemImages.Length; index++)
        {
            Image itemImage = itemImages[index];
            RectTransform slot = itemImage == null
                ? null
                : itemImage.transform.parent as RectTransform;

            if (slot != null && playerInventory.GetItem(index) != null
                && RectTransformUtility.RectangleContainsScreenPoint(
                    slot,
                    pointerPosition,
                    GetCanvasCamera(slot)))
            {
                if (IsEventSelectionActive)
                {
                    ToggleEventSelection(index);
                    return;
                }

                if (sellRequested)
                {
                    shopManager.TrySellInventoryItem(index);
                }
                else
                {
                    playerInventory.TryUse(index);
                }

                return;
            }
        }
    }

    public bool BeginEventSelection(
        int requiredSelectionCount,
        Func<ItemData, bool> selectionPredicate,
        Action<IReadOnlyList<int>> onConfirm,
        Action onCancel)
    {
        if (playerInventory == null || requiredSelectionCount <= 0)
        {
            return false;
        }

        int eligibleCount = 0;
        for (int index = 0; index < playerInventory.SlotCount; index++)
        {
            ItemData item = playerInventory.GetItem(index);
            if (item != null && (selectionPredicate == null
                || selectionPredicate(item)))
            {
                eligibleCount++;
            }
        }

        if (eligibleCount < requiredSelectionCount)
        {
            return false;
        }

        eventSelectedSlots.Clear();
        eventRequiredSelectionCount = requiredSelectionCount;
        eventSelectionPredicate = selectionPredicate;
        eventConfirmCallback = onConfirm;
        eventCancelCallback = onCancel;
        EventSelectionChanged?.Invoke(0, eventRequiredSelectionCount);
        Refresh();
        return true;
    }

    public bool ConfirmEventSelection()
    {
        if (!IsEventSelectionActive
            || eventSelectedSlots.Count != eventRequiredSelectionCount)
        {
            return false;
        }

        List<int> confirmed = new List<int>(eventSelectedSlots);
        Action<IReadOnlyList<int>> callback = eventConfirmCallback;
        ResetEventSelection();
        callback?.Invoke(confirmed);
        return true;
    }

    public void CancelEventSelection()
    {
        if (!IsEventSelectionActive)
        {
            return;
        }

        Action callback = eventCancelCallback;
        ResetEventSelection();
        callback?.Invoke();
    }

    private void ToggleEventSelection(int slotIndex)
    {
        ItemData item = playerInventory.GetItem(slotIndex);
        if (item == null || eventSelectionPredicate != null
            && !eventSelectionPredicate(item))
        {
            return;
        }

        if (eventSelectedSlots.Contains(slotIndex))
        {
            eventSelectedSlots.Remove(slotIndex);
        }
        else if (eventSelectedSlots.Count < eventRequiredSelectionCount)
        {
            eventSelectedSlots.Add(slotIndex);
        }

        EventSelectionChanged?.Invoke(
            eventSelectedSlots.Count,
            eventRequiredSelectionCount);
        Refresh();
    }

    private void ResetEventSelection()
    {
        eventSelectedSlots.Clear();
        eventRequiredSelectionCount = 0;
        eventSelectionPredicate = null;
        eventConfirmCallback = null;
        eventCancelCallback = null;
        Refresh();
    }

    private void ResolveReferences()
    {
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        shopManager ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
    }

    private static Camera GetCanvasCamera(RectTransform target)
    {
        Canvas canvas = target == null
            ? null
            : target.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return null;
        }

        Canvas rootCanvas = canvas.rootCanvas;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
    }

    private void Refresh()
    {
        RefreshShortcutLabels();

        if (playerInventory == null || itemImages == null)
        {
            return;
        }

        for (int index = 0; index < itemImages.Length; index++)
        {
            Image itemImage = itemImages[index];

            if (itemImage == null)
            {
                continue;
            }

            ItemData item = playerInventory.GetItem(index);
            itemImage.preserveAspect = true;
            itemImage.sprite = item != null ? item.Icon : null;
            itemImage.color = IsEventSelectionActive
                && eventSelectedSlots.Contains(index)
                    ? new Color(1f, 0.72f, 0.2f, 1f)
                    : Color.white;
            itemImage.gameObject.SetActive(item != null && item.Icon != null);
        }
    }

    private void RefreshShortcutLabels()
    {
        if (itemImages == null)
        {
            return;
        }

        int count = Mathf.Min(
            itemImages.Length,
            PlayerInventory.MaximumSlotCount);

        for (int index = 0; index < count; index++)
        {
            Image itemImage = itemImages[index];
            Transform slot = itemImage == null
                ? null
                : itemImage.transform.parent;
            Transform shortcutBadge = FindShortcutBadge(slot);

            if (shortcutBadge == null)
            {
                continue;
            }

            shortcutBadge.gameObject.SetActive(true);
            shortcutBadge.SetAsLastSibling();

            TMP_Text shortcutLabel =
                shortcutBadge.GetComponentInChildren<TMP_Text>(true);
            if (shortcutLabel == null)
            {
                continue;
            }

            shortcutLabel.gameObject.SetActive(true);
            shortcutLabel.enabled = true;
            shortcutLabel.text = (index + 1).ToString();
            shortcutLabel.color = Color.white;
            shortcutLabel.enableAutoSizing = true;
            shortcutLabel.fontSizeMin = 8f;
            shortcutLabel.fontSizeMax = 24f;
            shortcutLabel.alignment = TextAlignmentOptions.Center;
        }
    }

    private static Transform FindShortcutBadge(Transform slot)
    {
        if (slot == null)
        {
            return null;
        }

        for (int index = 0; index < slot.childCount; index++)
        {
            Transform child = slot.GetChild(index);
            if (child.name.StartsWith("Image | Inventory Num"))
            {
                return child;
            }
        }

        return null;
    }
}
