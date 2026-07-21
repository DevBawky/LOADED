using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryTooltipUI : MonoBehaviour
{
    [Header("Data Sources")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private DeckManager deckManager;

    [Header("Canvas")]
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Vector2 pointerOffset = new Vector2(20f, -20f);
    [Min(0f)]
    [SerializeField] private float screenPadding = 8f;

    [Header("Item Hover Targets")]
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private RectTransform[] itemSlots;
    [SerializeField] private RectTransform[] shopItemSlots;

    [Header("Item Tooltip")]
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;

    [Header("Bullet Hover Targets")]
    [SerializeField] private RectTransform[] shopBulletSlots;
    [SerializeField] private RectTransform nextChip;
    [SerializeField] private Image nextChipIcon;
    [SerializeField] private PlayerCylinderUI cylinderUI;

    [Header("Bullet Tooltip")]
    [SerializeField] private RectTransform bulletTooltip;
    [SerializeField] private Image bulletIcon;
    [SerializeField] private Image bulletCylinderIcon;
    [SerializeField] private TextMeshProUGUI bulletNameText;
    [SerializeField] private TextMeshProUGUI bulletGradeText;
    [SerializeField] private TextMeshProUGUI bulletDescriptionText;

    [Header("Cylinder Bullet Tooltip")]
    [SerializeField] private RectTransform cylinderBulletTooltip;
    [SerializeField] private TextMeshProUGUI cylinderBulletNameText;
    [SerializeField] private TextMeshProUGUI cylinderBulletGradeText;
    [SerializeField] private TextMeshProUGUI cylinderBulletDescriptionText;

    private readonly Vector3[] tooltipCorners = new Vector3[4];
    private Canvas rootCanvas;

    private void OnEnable()
    {
        ResolveReferences();
        rootCanvas = canvasRect == null
            ? GetComponentInParent<Canvas>()
            : canvasRect.GetComponent<Canvas>();

        if (rootCanvas != null)
        {
            rootCanvas = rootCanvas.rootCanvas;
        }

        DisableRaycasts(tooltip);
        DisableRaycasts(bulletTooltip);
        DisableRaycasts(cylinderBulletTooltip);

        if (deckManager != null)
        {
            deckManager.StateChanged += RefreshNextChip;
        }

        RefreshNextChip();
        HideAll();
    }

    private void OnDisable()
    {
        if (deckManager != null)
        {
            deckManager.StateChanged -= RefreshNextChip;
        }

        HideAll();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (GamePauseController.IsPaused || mouse == null
            || cylinderUI != null && cylinderUI.IsDragging)
        {
            HideAll();
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (TryShowInventoryItem(pointerPosition)
            || TryShowShopItem(pointerPosition)
            || TryShowShopBullet(pointerPosition)
            || TryShowLoadedBullet(pointerPosition)
            || TryShowNextBullet(pointerPosition))
        {
            return;
        }

        HideAll();
    }

    private bool TryShowInventoryItem(Vector2 pointerPosition)
    {
        if (playerInventory == null || inventoryPanel == null
            || itemSlots == null
            || !inventoryPanel.gameObject.activeInHierarchy)
        {
            return false;
        }

        for (int index = 0; index < itemSlots.Length; index++)
        {
            ItemData item = playerInventory.GetItem(index);

            if (item != null && IsHovered(itemSlots[index], pointerPosition))
            {
                ShowItem(item, pointerPosition);
                return true;
            }
        }

        return false;
    }

    private bool TryShowShopItem(Vector2 pointerPosition)
    {
        if (shopManager == null || shopItemSlots == null)
        {
            return false;
        }

        for (int index = 0; index < shopItemSlots.Length; index++)
        {
            ItemData item = shopManager.GetItemOffer(index);

            if (item != null
                && IsHovered(shopItemSlots[index], pointerPosition))
            {
                ShowItem(item, pointerPosition);
                return true;
            }
        }

        return false;
    }

    private bool TryShowShopBullet(Vector2 pointerPosition)
    {
        if (shopManager == null || shopBulletSlots == null)
        {
            return false;
        }

        for (int index = 0; index < shopBulletSlots.Length; index++)
        {
            BulletData bullet = shopManager.GetBulletOffer(index);

            if (bullet != null
                && IsHovered(shopBulletSlots[index], pointerPosition))
            {
                ShowBullet(bullet, pointerPosition);
                return true;
            }
        }

        return false;
    }

    private bool TryShowLoadedBullet(Vector2 pointerPosition)
    {
        if (cylinderUI == null
            || !cylinderUI.TryGetLoadedBulletAtScreenPosition(
                pointerPosition,
                GetCanvasCamera(),
                out BulletInstance bullet)
            || bullet == null)
        {
            return false;
        }

        ShowCylinderBullet(bullet, pointerPosition);
        return true;
    }

    private bool TryShowNextBullet(Vector2 pointerPosition)
    {
        if (deckManager == null || !IsHovered(nextChip, pointerPosition))
        {
            return false;
        }

        BulletInstance bullet = deckManager.PeekNextBullet();

        if (bullet == null)
        {
            return false;
        }

        ShowBullet(bullet.Data, bullet.Level, pointerPosition);
        return true;
    }

    private void ShowItem(ItemData item, Vector2 pointerPosition)
    {
        HideBulletTooltip();
        HideCylinderBulletTooltip();

        if (tooltip == null || itemNameText == null
            || itemDescriptionText == null)
        {
            return;
        }

        itemNameText.text = GetDisplayName(item.DisplayName, item.name);
        itemDescriptionText.text = item.Description;
        ApplyIcon(itemIcon, item.Icon);
        tooltip.gameObject.SetActive(true);
        tooltip.SetAsLastSibling();
        PositionInsideScreen(tooltip, pointerPosition);
    }

    private void ShowBullet(BulletData bullet, Vector2 pointerPosition)
    {
        ShowBullet(bullet, 0, pointerPosition);
    }

    private void ShowBullet(
        BulletData bullet,
        int level,
        Vector2 pointerPosition)
    {
        HideItemTooltip();
        HideCylinderBulletTooltip();

        if (bullet == null || bulletTooltip == null || bulletNameText == null
            || bulletDescriptionText == null)
        {
            return;
        }

        bulletNameText.richText = true;
        bulletNameText.color = bullet.GradeNameColor;
        bulletNameText.text = bullet.GetRichDisplayName(level);

        if (bulletGradeText != null)
        {
            bulletGradeText.text = bullet.Grade.ToString();
            bulletGradeText.color = bullet.GradeNameColor;
        }

        bulletDescriptionText.text = bullet.GetDetailedDescription(level);
        ApplyIcon(bulletIcon, bullet.BulletIcon);
        ApplyIcon(bulletCylinderIcon, bullet.CylinderIcon);
        bulletTooltip.gameObject.SetActive(true);
        bulletTooltip.SetAsLastSibling();
        PositionInsideScreen(bulletTooltip, pointerPosition);
    }

    private void ShowCylinderBullet(
        BulletInstance bullet,
        Vector2 pointerPosition)
    {
        HideItemTooltip();
        HideBulletTooltip();

        if (bullet == null || bullet.Data == null
            || cylinderBulletTooltip == null
            || cylinderBulletNameText == null
            || cylinderBulletDescriptionText == null)
        {
            HideCylinderBulletTooltip();
            return;
        }

        cylinderBulletNameText.richText = true;
        cylinderBulletNameText.color = bullet.GradeNameColor;
        cylinderBulletNameText.text = bullet.RichDisplayName;

        if (cylinderBulletGradeText != null)
        {
            cylinderBulletGradeText.text = bullet.Grade.ToString();
            cylinderBulletGradeText.color = bullet.GradeNameColor;
        }

        cylinderBulletDescriptionText.text = bullet.DetailedDescription;
        cylinderBulletTooltip.gameObject.SetActive(true);
        cylinderBulletTooltip.SetAsLastSibling();
        PositionInsideScreen(cylinderBulletTooltip, pointerPosition);
    }

    private void RefreshNextChip()
    {
        BulletInstance nextBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();
        ApplyIcon(nextChipIcon, GetPreferredIcon(nextBullet));
    }

    private void PositionInsideScreen(
        RectTransform targetTooltip,
        Vector2 pointerPosition)
    {
        if (canvasRect == null || targetTooltip == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Vector3 tooltipScale = targetTooltip.lossyScale;
        Vector2 tooltipScreenSize = new Vector2(
            targetTooltip.rect.width * Mathf.Abs(tooltipScale.x),
            targetTooltip.rect.height * Mathf.Abs(tooltipScale.y));
        Vector2 pivotOffset = new Vector2(
            tooltipScreenSize.x * targetTooltip.pivot.x,
            -tooltipScreenSize.y * (1f - targetTooltip.pivot.y));
        Vector2 targetScreenPosition = pointerPosition + pointerOffset
            + pivotOffset;
        SetScreenPosition(targetTooltip, targetScreenPosition);
        targetTooltip.GetWorldCorners(tooltipCorners);

        Camera canvasCamera = GetCanvasCamera();
        Rect screenRect = rootCanvas == null
            ? new Rect(0f, 0f, Screen.width, Screen.height)
            : rootCanvas.pixelRect;
        Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[0]);
        Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[2]);
        Vector2 correction = Vector2.zero;

        if (lowerLeft.x < screenRect.xMin + screenPadding)
        {
            correction.x = screenRect.xMin + screenPadding - lowerLeft.x;
        }
        else if (upperRight.x > screenRect.xMax - screenPadding)
        {
            correction.x = screenRect.xMax - screenPadding - upperRight.x;
        }

        if (lowerLeft.y < screenRect.yMin + screenPadding)
        {
            correction.y = screenRect.yMin + screenPadding - lowerLeft.y;
        }
        else if (upperRight.y > screenRect.yMax - screenPadding)
        {
            correction.y = screenRect.yMax - screenPadding - upperRight.y;
        }

        SetScreenPosition(targetTooltip, targetScreenPosition + correction);
    }

    private void SetScreenPosition(
        RectTransform targetTooltip,
        Vector2 screenPosition)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPosition,
                GetCanvasCamera(),
                out Vector3 worldPosition))
        {
            targetTooltip.position = worldPosition;
        }
    }

    private Camera GetCanvasCamera()
    {
        return rootCanvas == null
            || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
    }

    private static bool IsHovered(
        RectTransform target,
        Vector2 pointerPosition)
    {
        return target != null && target.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(
                target,
                pointerPosition);
    }

    private static string GetDisplayName(string displayName, string fallback)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
    }

    private static void ApplyIcon(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
    }

    private static Sprite GetPreferredIcon(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return null;
        }

        return bullet.BulletIcon != null
            ? bullet.BulletIcon
            : bullet.CylinderIcon;
    }

    private static void DisableRaycasts(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void HideAll()
    {
        HideItemTooltip();
        HideBulletTooltip();
        HideCylinderBulletTooltip();
    }

    private void HideItemTooltip()
    {
        if (tooltip != null && tooltip.gameObject.activeSelf)
        {
            tooltip.gameObject.SetActive(false);
        }
    }

    private void HideBulletTooltip()
    {
        if (bulletTooltip != null && bulletTooltip.gameObject.activeSelf)
        {
            bulletTooltip.gameObject.SetActive(false);
        }
    }

    private void HideCylinderBulletTooltip()
    {
        if (cylinderBulletTooltip != null
            && cylinderBulletTooltip.gameObject.activeSelf)
        {
            cylinderBulletTooltip.gameObject.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        playerInventory ??= FindSceneObject<PlayerInventory>();
        shopManager ??= FindSceneObject<ShopManager>();
        deckManager ??= FindSceneObject<DeckManager>();
        cylinderUI ??= FindSceneObject<PlayerCylinderUI>();

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvasRect == null && canvas != null)
        {
            canvasRect = canvas.rootCanvas.transform as RectTransform;
        }

        inventoryPanel ??= FindRectTransform("Panel | Inventory");
        tooltip ??= FindRectTransform("Panel | Item Tooltip");
        bulletTooltip ??= FindRectTransform("Panel | Bullet Tooltip");
        cylinderBulletTooltip ??= FindRectTransform(
            "Panel | Cylinder Bullet Tooltip");
        nextChip ??= FindRectTransform("Next Chip", "Panel | MainGame");

        if (itemSlots == null || itemSlots.Length == 0)
        {
            itemSlots = FindRectTransforms("Image | ItemSlot", "Layout | Inventory");
        }

        if (shopItemSlots == null || shopItemSlots.Length == 0)
        {
            shopItemSlots = FindRectTransforms(
                "Button | Shop Item",
                "Layout | Shop Items");
        }

        if (shopBulletSlots == null || shopBulletSlots.Length == 0)
        {
            shopBulletSlots = FindRectTransforms(
                "Button | Bullet Item",
                "Layout | Shop Items");
        }

        itemIcon ??= FindNamedChild<Image>(tooltip, "Image | Item Sprite");
        itemNameText ??= FindNamedChild<TextMeshProUGUI>(
            tooltip,
            "Text | Item Name");
        itemDescriptionText ??= FindNamedChild<TextMeshProUGUI>(
            tooltip,
            "Text | Item Description");
        bulletIcon ??= FindNamedChild<Image>(
            bulletTooltip,
            "Image | Bullet Sprite");
        bulletCylinderIcon ??= FindNamedChild<Image>(
            bulletTooltip,
            "Image | Bullet Cylinder Sprite");
        bulletNameText ??= FindNamedChild<TextMeshProUGUI>(
            bulletTooltip,
            "Text | Bullet Name");
        bulletGradeText ??= FindNamedChild<TextMeshProUGUI>(
            bulletTooltip,
            "Text | Bullet Grade");
        bulletDescriptionText ??= FindNamedChild<TextMeshProUGUI>(
            bulletTooltip,
            "Text | Bullet Description");
        cylinderBulletNameText ??= FindNamedChild<TextMeshProUGUI>(
            cylinderBulletTooltip,
            "Text | Bullet Name");
        cylinderBulletGradeText ??= FindNamedChild<TextMeshProUGUI>(
            cylinderBulletTooltip,
            "Text | Bullet Grade");
        cylinderBulletDescriptionText ??= FindNamedChild<TextMeshProUGUI>(
            cylinderBulletTooltip,
            "Text | Bullet Description");
        nextChipIcon ??= FindNamedChild<Image>(nextChip, "Image | Next Chip");
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return objects.Length == 0 ? null : objects[0];
    }

    private static RectTransform FindRectTransform(
        string objectName,
        string parentName = null)
    {
        RectTransform[] transforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (RectTransform rectTransform in transforms)
        {
            if (rectTransform.gameObject.scene.IsValid()
                && rectTransform.name == objectName
                && (string.IsNullOrEmpty(parentName)
                    || rectTransform.parent != null
                    && rectTransform.parent.name == parentName))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static RectTransform[] FindRectTransforms(
        string namePrefix,
        string parentName)
    {
        RectTransform[] transforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<RectTransform> matches = new List<RectTransform>();

        foreach (RectTransform rectTransform in transforms)
        {
            if (rectTransform.gameObject.scene.IsValid()
                && rectTransform.name.StartsWith(namePrefix)
                && rectTransform.parent != null
                && rectTransform.parent.name == parentName)
            {
                matches.Add(rectTransform);
            }
        }

        matches.Sort((left, right) =>
            left.GetSiblingIndex().CompareTo(right.GetSiblingIndex()));
        return matches.ToArray();
    }

    private static T FindNamedChild<T>(
        RectTransform root,
        string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
