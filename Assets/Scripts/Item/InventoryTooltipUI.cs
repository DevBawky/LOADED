using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryTooltipUI : MonoBehaviour
{
    private const int BulletStacksPerRow = 4;
    private const int BulletStackRowCount = 5;

    public static event System.Action BulletInspected;

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
    [SerializeField] private StateManager stateManager;

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

    [Header("Debuff Description")]
    [SerializeField] private RectTransform debuffDescriptionPanel;
    [SerializeField] private Image debuffDescriptionIcon;
    [SerializeField] private TextMeshProUGUI debuffDescriptionNameText;
    [SerializeField] private TextMeshProUGUI debuffDescriptionBodyText;
    [SerializeField] private Sprite poisonDescriptionIcon;
    [SerializeField] private Sprite stunDescriptionIcon;
    [SerializeField] private Sprite weaknessDescriptionIcon;
    [SerializeField] private Sprite markDescriptionIcon;
    [TextArea(2, 6)]
    [SerializeField] private string poisonDescription;
    [TextArea(2, 6)]
    [SerializeField] private string stunDescription;
    [TextArea(2, 6)]
    [SerializeField] private string weaknessDescription;
    [TextArea(2, 6)]
    [SerializeField] private string markDescription;

    [Header("Bullet Stack Status")]
    [SerializeField] private RectTransform bulletStatusLayout;
    [SerializeField] private Image bulletStackPrefab;

    private readonly Vector3[] tooltipCorners = new Vector3[4];
    private readonly List<BulletInstance> ownedBullets =
        new List<BulletInstance>();
    private readonly List<BulletInstance> displayedStackBullets =
        new List<BulletInstance>();
    private readonly List<Image> bulletStackImages = new List<Image>();
    private readonly List<TextMeshProUGUI> bulletStackCountTexts =
        new List<TextMeshProUGUI>();
    private readonly RectTransform[] bulletStackRows =
        new RectTransform[BulletStackRowCount];
    private Canvas rootCanvas;
    private BulletManagementUI bulletManagementUI;
    private BulletInstance previewedCylinderBullet;
    private int previewedCylinderBulletIndex = -1;
    private Vector2 debuffDescriptionInitialPosition;
    private bool hasDebuffDescriptionInitialPosition;

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
        DisableRaycasts(debuffDescriptionPanel);

        if (debuffDescriptionPanel != null
            && !hasDebuffDescriptionInitialPosition)
        {
            debuffDescriptionInitialPosition =
                debuffDescriptionPanel.anchoredPosition;
            hasDebuffDescriptionInitialPosition = true;
        }

        if (deckManager != null)
        {
            deckManager.StateChanged += RefreshNextChip;
        }

        if (stateManager != null)
        {
            stateManager.StateChanged += HandleFlowStateChanged;
        }

        RefreshNextChip();
        RefreshBulletStatusVisibility();
        HideAll();
    }

    private void OnDisable()
    {
        if (deckManager != null)
        {
            deckManager.StateChanged -= RefreshNextChip;
        }

        if (stateManager != null)
        {
            stateManager.StateChanged -= HandleFlowStateChanged;
        }

        HideAll();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || cylinderUI != null && cylinderUI.IsDragging)
        {
            HideAll();
            return;
        }

        RefreshBulletStackStatus();

        if (bulletManagementUI != null && bulletManagementUI.IsOpen)
        {
            HideItemTooltip();
            HideBulletTooltip();
            HideCylinderBulletTooltip();
            ShowDebuffDescription(
                TryGetDebuff(
                    bulletManagementUI.SelectedBullet,
                    out StatusEffectType selectedDebuff)
                    ? selectedDebuff
                    : (StatusEffectType?)null,
                null,
                true);
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            HideAll();
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (TryShowInventoryItem(pointerPosition)
            || TryShowShopItem(pointerPosition)
            || TryShowShopBullet(pointerPosition)
            || TryShowLoadedBullet(pointerPosition)
            || TryShowBulletStack(pointerPosition)
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

    private bool TryShowBulletStack(Vector2 pointerPosition)
    {
        for (int index = 0; index < displayedStackBullets.Count; index++)
        {
            Image stackImage = index < bulletStackImages.Count
                ? bulletStackImages[index]
                : null;

            if (stackImage == null
                || !stackImage.gameObject.activeInHierarchy
                || !IsHovered(stackImage.rectTransform, pointerPosition))
            {
                continue;
            }

            ShowCylinderBullet(
                displayedStackBullets[index],
                -1,
                pointerPosition,
                TooltipPointerAnchor.BottomRight);
            return true;
        }

        return false;
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
        ShowDebuffDescription(
            TryGetDebuff(item, out StatusEffectType debuff)
                ? debuff
                : (StatusEffectType?)null,
            tooltip,
            false);
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
        ShowDebuffDescription(
            TryGetDebuff(bullet, level, out StatusEffectType debuff)
                ? debuff
                : (StatusEffectType?)null,
            bulletTooltip,
            false);
        BulletInspected?.Invoke();
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
        ShowDebuffDescription(
            TryGetDebuff(bullet, out StatusEffectType debuff)
                ? debuff
                : (StatusEffectType?)null,
            cylinderBulletTooltip,
            false);
        BulletInspected?.Invoke();

        if (loadedBulletIndex < 0)
        {
            playerShoot?.ClearLoadedBulletDamagePreview();
            previewedCylinderBullet = null;
            previewedCylinderBulletIndex = -1;
        }
        else if (!ReferenceEquals(previewedCylinderBullet, bullet)
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

    private void RefreshBulletStackStatus()
    {
        if (bulletStatusLayout == null
            || !bulletStatusLayout.gameObject.activeInHierarchy)
        {
            return;
        }

        displayedStackBullets.Clear();
        BulletTooltipContext context = CreateBulletTooltipContext();

        if (deckManager != null)
        {
            deckManager.GetOwnedBullets(ownedBullets);

            foreach (BulletInstance bullet in ownedBullets)
            {
                if (!string.IsNullOrEmpty(GetBulletStatusText(bullet, context)))
                {
                    displayedStackBullets.Add(bullet);
                }
            }
        }

        int capacity = BulletStackRowCount * BulletStacksPerRow;
        int visibleCount = Mathf.Min(capacity, displayedStackBullets.Count);

        for (int index = 0; index < visibleCount; index++)
        {
            EnsureBulletStackImage(index);

            if (index >= bulletStackImages.Count
                || bulletStackImages[index] == null)
            {
                continue;
            }

            BulletInstance bullet = displayedStackBullets[index];
            Image stackImage = bulletStackImages[index];
            stackImage.gameObject.SetActive(true);
            ApplyIcon(stackImage, GetPreferredIcon(bullet));

            TextMeshProUGUI countText = bulletStackCountTexts[index];
            if (countText != null)
            {
                countText.text = GetBulletStatusText(bullet, context);
            }
        }

        for (int index = visibleCount; index < bulletStackImages.Count; index++)
        {
            if (bulletStackImages[index] != null)
            {
                bulletStackImages[index].gameObject.SetActive(false);
            }
        }

        if (displayedStackBullets.Count > visibleCount)
        {
            displayedStackBullets.RemoveRange(
                visibleCount,
                displayedStackBullets.Count - visibleCount);
        }
    }

    private static string GetBulletStatusText(
        BulletInstance bullet,
        BulletTooltipContext context)
    {
        return bullet == null
            ? string.Empty
            : bullet.GetStatusDisplayText(context);
    }

    private void EnsureBulletStackImage(int index)
    {
        while (bulletStackImages.Count <= index)
        {
            int newIndex = bulletStackImages.Count;
            int rowIndex = newIndex / BulletStacksPerRow;
            RectTransform row = rowIndex < bulletStackRows.Length
                ? bulletStackRows[rowIndex]
                : null;

            if (bulletStackPrefab == null || row == null)
            {
                return;
            }

            Image stackImage = Instantiate(bulletStackPrefab, row);
            stackImage.name = $"Image _ Bullet Stack {newIndex + 1}";
            stackImage.raycastTarget = true;
            bulletStackImages.Add(stackImage);
            bulletStackCountTexts.Add(FindNamedChild<TextMeshProUGUI>(
                stackImage.rectTransform,
                "Text | Stack Count"));
        }
    }

    private void HandleFlowStateChanged()
    {
        ResolveReferences();
        RefreshBulletStatusVisibility();
        HideAll();
    }

    private void RefreshBulletStatusVisibility()
    {
        if (bulletStatusLayout == null)
        {
            return;
        }

        bool shouldShow = stateManager != null
            && stateManager.CurrentState == GameFlowState.Battle;
        bulletStatusLayout.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            RefreshBulletStackStatus();
        }
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

    private void ShowDebuffDescription(
        StatusEffectType? debuff,
        RectTransform adjacentTooltip,
        bool useInitialPosition)
    {
        if (!debuff.HasValue
            || debuffDescriptionPanel == null
            || debuffDescriptionIcon == null
            || debuffDescriptionNameText == null
            || debuffDescriptionBodyText == null)
        {
            HideDebuffDescription();
            return;
        }

        StatusEffectType type = debuff.Value;
        ApplyIcon(debuffDescriptionIcon, GetDebuffIcon(type));
        debuffDescriptionNameText.text = GetDebuffName(type);
        debuffDescriptionNameText.color = GetDebuffColor(type);
        debuffDescriptionBodyText.richText = true;
        debuffDescriptionBodyText.text = TooltipTextFormatter.Format(
            GetDebuffDescription(type));
        debuffDescriptionPanel.gameObject.SetActive(true);

        if (useInitialPosition && hasDebuffDescriptionInitialPosition)
        {
            debuffDescriptionPanel.anchoredPosition =
                debuffDescriptionInitialPosition;
        }
        else
        {
            PositionBesideTooltip(debuffDescriptionPanel, adjacentTooltip);
        }
    }

    private void PositionBesideTooltip(
        RectTransform target,
        RectTransform adjacentTooltip)
    {
        if (target == null || adjacentTooltip == null || canvasRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Camera canvasCamera = GetCanvasCamera();
        adjacentTooltip.GetWorldCorners(tooltipCorners);
        Vector2 sourceLowerLeft = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[0]);
        Vector2 sourceUpperRight = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[2]);

        target.GetWorldCorners(tooltipCorners);
        Vector2 targetLowerLeft = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[0]);
        Vector2 targetUpperRight = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            tooltipCorners[2]);
        Vector2 targetSize = targetUpperRight - targetLowerLeft;
        Rect screenRect = rootCanvas == null
            ? new Rect(0f, 0f, Screen.width, Screen.height)
            : rootCanvas.pixelRect;

        float rightX = sourceUpperRight.x + pointerGap;
        float leftX = sourceLowerLeft.x - pointerGap - targetSize.x;
        float desiredX = rightX + targetSize.x
                <= screenRect.xMax - screenPadding
            ? rightX
            : leftX;
        Vector2 desiredLowerLeft = new Vector2(
            desiredX,
            sourceUpperRight.y - targetSize.y);
        desiredLowerLeft.x = Mathf.Clamp(
            desiredLowerLeft.x,
            screenRect.xMin + screenPadding,
            screenRect.xMax - screenPadding - targetSize.x);
        desiredLowerLeft.y = Mathf.Clamp(
            desiredLowerLeft.y,
            screenRect.yMin + screenPadding,
            screenRect.yMax - screenPadding - targetSize.y);

        SetScreenPosition(
            target,
            desiredLowerLeft + new Vector2(
                targetSize.x * target.pivot.x,
                targetSize.y * target.pivot.y));
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

    private static bool TryGetDebuff(
        ItemData item,
        out StatusEffectType debuff)
    {
        if (item != null)
        {
            switch (item.EffectType)
            {
                case ItemEffectType.PoisonAllEnemies:
                    debuff = StatusEffectType.Poison;
                    return true;
                case ItemEffectType.StunAllEnemies:
                    debuff = StatusEffectType.Stun;
                    return true;
            }

            if (TryGetDebuffFromText(item.Description, out debuff))
            {
                return true;
            }
        }

        debuff = default;
        return false;
    }

    private static bool TryGetDebuff(
        BulletInstance bullet,
        out StatusEffectType debuff)
    {
        if (bullet != null)
        {
            return TryGetDebuff(
                bullet.Data,
                bullet.Level,
                out debuff);
        }

        debuff = default;
        return false;
    }

    private static bool TryGetDebuff(
        BulletData bullet,
        int level,
        out StatusEffectType debuff)
    {
        bool poison = false;
        bool stun = false;
        bool weakness = false;
        bool mark = false;

        if (bullet != null)
        {
            AddDebuffs(
                bullet.GetEffects(level),
                ref poison,
                ref stun,
                ref weakness,
                ref mark);

            foreach (BulletConditionalEventData conditionalEvent
                     in bullet.GetConditionalEvents(level))
            {
                if (conditionalEvent != null)
                {
                    AddDebuffs(
                        conditionalEvent.Events,
                        ref poison,
                        ref stun,
                        ref weakness,
                        ref mark);
                }
            }

            if (!poison && !stun && !weakness && !mark
                && TryGetDebuffFromText(
                    bullet.GetDescription(level),
                    out debuff))
            {
                return true;
            }
        }

        if (poison)
        {
            debuff = StatusEffectType.Poison;
            return true;
        }

        if (stun)
        {
            debuff = StatusEffectType.Stun;
            return true;
        }

        if (weakness)
        {
            debuff = StatusEffectType.Weakness;
            return true;
        }

        if (mark)
        {
            debuff = StatusEffectType.Mark;
            return true;
        }

        debuff = default;
        return false;
    }

    private static void AddDebuffs(
        IReadOnlyList<BulletEffectData> effects,
        ref bool poison,
        ref bool stun,
        ref bool weakness,
        ref bool mark)
    {
        if (effects == null)
        {
            return;
        }

        foreach (BulletEffectData effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case BulletEffectType.Poison:
                    poison = true;
                    break;
                case BulletEffectType.Stun:
                    stun = true;
                    break;
                case BulletEffectType.Weakness:
                    weakness = true;
                    break;
                case BulletEffectType.Mark:
                    mark = true;
                    break;
            }
        }
    }

    private static bool TryGetDebuffFromText(
        string description,
        out StatusEffectType debuff)
    {
        if (!string.IsNullOrEmpty(description))
        {
            if (description.Contains("독"))
            {
                debuff = StatusEffectType.Poison;
                return true;
            }

            if (description.Contains("기절"))
            {
                debuff = StatusEffectType.Stun;
                return true;
            }

            if (description.Contains("약화"))
            {
                debuff = StatusEffectType.Weakness;
                return true;
            }

            if (description.Contains("표식"))
            {
                debuff = StatusEffectType.Mark;
                return true;
            }
        }

        debuff = default;
        return false;
    }

    private Sprite GetDebuffIcon(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Poison => poisonDescriptionIcon,
            StatusEffectType.Stun => stunDescriptionIcon,
            StatusEffectType.Weakness => weaknessDescriptionIcon,
            StatusEffectType.Mark => markDescriptionIcon,
            _ => null
        };
    }

    private string GetDebuffDescription(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Poison => poisonDescription,
            StatusEffectType.Stun => stunDescription,
            StatusEffectType.Weakness => weaknessDescription,
            StatusEffectType.Mark => markDescription,
            _ => string.Empty
        };
    }

    private static string GetDebuffName(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Poison => "독",
            StatusEffectType.Stun => "기절",
            StatusEffectType.Weakness => "약화",
            StatusEffectType.Mark => "표식",
            _ => string.Empty
        };
    }

    private static Color GetDebuffColor(StatusEffectType type)
    {
        string htmlColor = type switch
        {
            StatusEffectType.Poison => TooltipTextFormatter.PoisonColor,
            StatusEffectType.Stun => TooltipTextFormatter.StunColor,
            StatusEffectType.Weakness => TooltipTextFormatter.WeaknessColor,
            StatusEffectType.Mark => TooltipTextFormatter.MarkColor,
            _ => "#FFFFFF"
        };
        return ColorUtility.TryParseHtmlString(htmlColor, out Color color)
            ? color
            : Color.white;
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
        HideDebuffDescription();
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

    private void HideDebuffDescription()
    {
        if (debuffDescriptionPanel != null
            && debuffDescriptionPanel.gameObject.activeSelf)
        {
            debuffDescriptionPanel.gameObject.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        playerInventory ??= FindSceneObject<PlayerInventory>();
        shopManager ??= FindSceneObject<ShopManager>();
        deckManager ??= FindSceneObject<DeckManager>();
        currencyManager ??= FindSceneObject<CurrencyManager>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
        playerShoot ??= FindSceneObject<PlayerShoot>();
        stateManager ??= FindSceneObject<StateManager>();
        cylinderUI ??= FindSceneObject<PlayerCylinderUI>();
        bulletManagementUI ??= FindSceneObject<BulletManagementUI>();

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
        debuffDescriptionPanel ??= FindRectTransform(
            "Panel | Debuff Desciption");
        bulletStatusLayout ??= FindRectTransform("Layout | Bullet Status");
        nextChip ??= FindRectTransform("Next Chip", "Panel | MainGame");

        for (int index = 0; index < bulletStackRows.Length; index++)
        {
            bulletStackRows[index] ??= FindNamedChild<RectTransform>(
                bulletStatusLayout,
                $"Layout | Stack {index + 1}");
        }

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
        debuffDescriptionIcon ??= FindNamedChild<Image>(
            debuffDescriptionPanel,
            "Image | Debuff Icon");
        debuffDescriptionNameText ??= FindNamedChild<TextMeshProUGUI>(
            debuffDescriptionPanel,
            "Text | Bullet Name");
        debuffDescriptionBodyText ??= FindNamedChild<TextMeshProUGUI>(
            debuffDescriptionPanel,
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
