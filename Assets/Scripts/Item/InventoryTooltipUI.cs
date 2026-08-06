using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryTooltipUI : MonoBehaviour
{
    private enum TooltipPointerAnchor
    {
        LeftCenter,
        TopLeft,
        BottomLeft,
        BottomRight
    }

    [Header("Data Sources")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShoot playerShoot;

    [Header("Canvas")]
    [SerializeField] private RectTransform canvasRect;
    [Min(0f)]
    [SerializeField] private float pointerGap = 12f;
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
    private BulletInstance previewedCylinderBullet;
    private int previewedCylinderBulletIndex = -1;

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

        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || mouse == null
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
                ShowItem(
                    item,
                    pointerPosition,
                    TooltipPointerAnchor.BottomLeft,
                    shopManager != null
                        && shopManager.CanSellInventoryItems);
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
                ShowItem(
                    item,
                    pointerPosition,
                    TooltipPointerAnchor.TopLeft,
                    false);
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
                ShowBullet(
                    bullet,
                    pointerPosition,
                    TooltipPointerAnchor.TopLeft);
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
                out BulletInstance bullet,
                out int loadedBulletIndex)
            || bullet == null)
        {
            return false;
        }

        ShowCylinderBullet(
            bullet,
            loadedBulletIndex,
            pointerPosition,
            TooltipPointerAnchor.BottomRight);
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

        ShowBullet(
            bullet,
            pointerPosition,
            TooltipPointerAnchor.LeftCenter);
        return true;
    }

    private void ShowItem(
        ItemData item,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor,
        bool showSellHint)
    {
        HideBulletTooltip();
        HideCylinderBulletTooltip();

        if (tooltip == null || itemNameText == null
            || itemDescriptionText == null)
        {
            return;
        }

        itemNameText.text = GetDisplayName(item.DisplayName, item.name);
        itemDescriptionText.richText = true;
        string description = item.Description == null
            ? string.Empty
            : item.Description.TrimEnd();

        if (showSellHint)
        {
            string separator = description.Length == 0 ? string.Empty : "\n\n";
            description += separator
                + $"우클릭을 통해 판매: ${ShopManager.InventoryItemSellPrice}";
        }

        itemDescriptionText.text = TooltipTextFormatter.Format(description);
        ApplyIcon(itemIcon, item.Icon);
        tooltip.gameObject.SetActive(true);
        PositionInsideScreen(tooltip, pointerPosition, pointerAnchor);
    }

    private void ShowBullet(
        BulletData bullet,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor)
    {
        ShowBullet(bullet, 0, pointerPosition, pointerAnchor);
    }

    private void ShowBullet(
        BulletInstance bullet,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor)
    {
        if (bullet == null || bullet.Data == null)
        {
            return;
        }

        ShowBullet(bullet.Data, bullet.Level, pointerPosition, pointerAnchor);
        bulletDescriptionText.text = bullet.GetDetailedDescription(
            CreateBulletTooltipContext());
        PositionInsideScreen(bulletTooltip, pointerPosition, pointerAnchor);
    }

    private void ShowBullet(
        BulletData bullet,
        int level,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor)
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

        bulletDescriptionText.richText = true;
        bulletDescriptionText.text = bullet.GetDetailedDescription(level);
        ApplyIcon(bulletIcon, null);
        ApplyIcon(bulletCylinderIcon, bullet.CylinderIcon);
        bulletTooltip.gameObject.SetActive(true);
        PositionInsideScreen(bulletTooltip, pointerPosition, pointerAnchor);
    }

    private void ShowCylinderBullet(
        BulletInstance bullet,
        int loadedBulletIndex,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor)
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

        cylinderBulletDescriptionText.richText = true;
        cylinderBulletDescriptionText.text = bullet.GetDetailedDescription(
            CreateBulletTooltipContext());
        cylinderBulletTooltip.gameObject.SetActive(true);
        PositionInsideScreen(
            cylinderBulletTooltip,
            pointerPosition,
            pointerAnchor);

        if (!ReferenceEquals(previewedCylinderBullet, bullet)
            || previewedCylinderBulletIndex != loadedBulletIndex)
        {
            playerShoot?.ClearLoadedBulletDamagePreview();
            playerShoot?.ShowLoadedBulletDamagePreview(loadedBulletIndex);
            previewedCylinderBullet = bullet;
            previewedCylinderBulletIndex = loadedBulletIndex;
        }
    }

    private void RefreshNextChip()
    {
        playerShoot?.ClearLoadedBulletDamagePreview();
        previewedCylinderBullet = null;
        previewedCylinderBulletIndex = -1;

        BulletInstance nextBullet = deckManager == null
            ? null
            : deckManager.PeekNextBullet();
        ApplyIcon(nextChipIcon, GetPreferredIcon(nextBullet));
    }

    private void PositionInsideScreen(
        RectTransform targetTooltip,
        Vector2 pointerPosition,
        TooltipPointerAnchor pointerAnchor)
    {
        if (canvasRect == null || targetTooltip == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
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
        Vector2 tooltipScreenSize = upperRight - lowerLeft;

        // Keep the cursor at the middle of the tooltip's left edge whenever
        // the available screen space allows it. Only move away from that
        // preferred position far enough to keep the whole tooltip visible.
        Vector2 desiredLowerLeft = GetPreferredLowerLeft(
            pointerPosition,
            tooltipScreenSize,
            pointerAnchor);
        float minimumX = screenRect.xMin + screenPadding;
        float minimumY = screenRect.yMin + screenPadding;
        float maximumX = Mathf.Max(
            minimumX,
            screenRect.xMax - screenPadding - tooltipScreenSize.x);
        float maximumY = Mathf.Max(
            minimumY,
            screenRect.yMax - screenPadding - tooltipScreenSize.y);
        desiredLowerLeft.x = Mathf.Clamp(desiredLowerLeft.x, minimumX, maximumX);
        desiredLowerLeft.y = Mathf.Clamp(desiredLowerLeft.y, minimumY, maximumY);

        Vector2 targetPivotPosition = desiredLowerLeft + new Vector2(
            tooltipScreenSize.x * targetTooltip.pivot.x,
            tooltipScreenSize.y * targetTooltip.pivot.y);
        SetScreenPosition(targetTooltip, targetPivotPosition);
    }

    private Vector2 GetPreferredLowerLeft(
        Vector2 pointerPosition,
        Vector2 tooltipSize,
        TooltipPointerAnchor pointerAnchor)
    {
        switch (pointerAnchor)
        {
            case TooltipPointerAnchor.TopLeft:
                return new Vector2(
                    pointerPosition.x + pointerGap,
                    pointerPosition.y - pointerGap - tooltipSize.y);

            case TooltipPointerAnchor.BottomLeft:
                return new Vector2(
                    pointerPosition.x + pointerGap,
                    pointerPosition.y + pointerGap);

            case TooltipPointerAnchor.BottomRight:
                return new Vector2(
                    pointerPosition.x - pointerGap - tooltipSize.x,
                    pointerPosition.y + pointerGap);

            default:
                return new Vector2(
                    pointerPosition.x + pointerGap,
                    pointerPosition.y - tooltipSize.y * 0.5f);
        }
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

    private bool IsHovered(
        RectTransform target,
        Vector2 pointerPosition)
    {
        return target != null && target.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(
                target,
                pointerPosition,
                GetCanvasCamera());
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

        return bullet.CylinderIcon;
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

        playerShoot?.ClearLoadedBulletDamagePreview();
        previewedCylinderBullet = null;
        previewedCylinderBulletIndex = -1;
    }

    private void ResolveReferences()
    {
        playerInventory ??= FindSceneObject<PlayerInventory>();
        shopManager ??= FindSceneObject<ShopManager>();
        deckManager ??= FindSceneObject<DeckManager>();
        currencyManager ??= FindSceneObject<CurrencyManager>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
        playerShoot ??= FindSceneObject<PlayerShoot>();
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

    private BulletTooltipContext CreateBulletTooltipContext()
    {
        return BulletTooltipContext.Create(
            deckManager,
            currencyManager,
            playerHealth,
            playerShoot);
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
