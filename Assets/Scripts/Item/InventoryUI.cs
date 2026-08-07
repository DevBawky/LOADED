using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private Image[] itemImages;

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
