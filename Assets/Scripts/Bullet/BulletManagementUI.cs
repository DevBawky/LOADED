using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BulletManagementUI : MonoBehaviour
{
    private const int BulletsPerRow = 5;
    private const float TooltipPointerGap = 12f;
    private const float TooltipScreenPadding = 8f;
    private const string RedCostColorTag = "#FF0000";

    [Header("Managers")]
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShoot playerShoot;

    [Header("Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject shopItemsLayout;
    [SerializeField] private GameObject manageBulletsPanel;
    [SerializeField] private GameObject bulletManageLayout;
    [SerializeField] private Button manageBulletsButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button myBulletButtonPrefab;
    [SerializeField] private RectTransform[] bulletRows;

    [Header("Selected Bullet")]
    [SerializeField] private Image bulletIcon;
    [SerializeField] private Image cylinderIcon;
    [SerializeField] private TextMeshProUGUI bulletNameText;
    [SerializeField] private TextMeshProUGUI bulletGradeText;
    [SerializeField] private TextMeshProUGUI bulletDescriptionText;
    [SerializeField] private Button removeButton;
    [SerializeField] private TextMeshProUGUI removeButtonText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    [Header("Upgrade Tooltip")]
    [SerializeField] private RectTransform upgradeTooltip;
    [SerializeField] private TextMeshProUGUI upgradeTooltipDescriptionText;

    private readonly List<BulletInstance> ownedBullets =
        new List<BulletInstance>();
    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly List<UnityAction> spawnedClickActions =
        new List<UnityAction>();
    private readonly Vector3[] tooltipWorldCorners = new Vector3[4];
    private BulletInstance selectedBullet;
    private bool wasShopActive;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
        DisableRaycasts(upgradeTooltip);
        HideUpgradeTooltip();
        SetManagementView(false);

        wasShopActive = shopPanel != null
            && shopPanel.activeInHierarchy;
        ClearSelection();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        ClearSpawnedButtons();
    }

    private void Update()
    {
        bool isShopActive = shopPanel != null
            && shopPanel.activeInHierarchy;

        if (wasShopActive && !isShopActive)
        {
            Close();
        }

        wasShopActive = isShopActive;
        RefreshUpgradeTooltip();
    }

    public void Open()
    {
        if (manageBulletsPanel == null)
        {
            return;
        }

        SetManagementView(true);
        RefreshOwnedBullets();
    }

    public void Close()
    {
        SetManagementView(false);
        HideUpgradeTooltip();
        ClearSpawnedButtons();
        ClearSelection();
    }

    public void RemoveSelectedBullet()
    {
        if (selectedBullet == null || deckManager == null
            || currencyManager == null
            || !deckManager.CanRemoveBullet(selectedBullet))
        {
            RefreshSelection();
            return;
        }

        int cost = deckManager.CurrentBulletRemovalCost;

        if (!currencyManager.TrySpendMoney(cost))
        {
            RefreshSelection();
            return;
        }

        if (!deckManager.TryRemoveBullet(selectedBullet))
        {
            currencyManager.AddMoney(cost);
            return;
        }

        GameStatistics.RecordGoldSpent(cost);
        deckManager.RegisterPaidBulletRemoval();
        SoundManager.PlaySfx("SFX_Bullet_Destroy");
        selectedBullet = null;
        RefreshOwnedBullets();
    }

    public void UpgradeSelectedBullet()
    {
        if (selectedBullet == null || !selectedBullet.CanUpgrade
            || deckManager == null || currencyManager == null)
        {
            return;
        }

        int cost = selectedBullet.UpgradeCost;

        if (!currencyManager.TrySpendMoney(cost))
        {
            RefreshSelection();
            return;
        }

        if (!deckManager.TryUpgradeBullet(selectedBullet))
        {
            currencyManager.AddMoney(cost);
            return;
        }

        GameStatistics.RecordGoldSpent(cost);
        SoundManager.PlaySfx("UI_Upgrade");
        RefreshOwnedBullets();
    }

    private void SelectBullet(BulletInstance bullet)
    {
        selectedBullet = bullet;
        RefreshSelection();
    }

    private void HandleDeckStateChanged()
    {
        if (manageBulletsPanel != null
            && manageBulletsPanel.activeInHierarchy)
        {
            RefreshOwnedBullets();
        }
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshSelection();
    }

    private void RefreshOwnedBullets()
    {
        ClearSpawnedButtons();

        if (deckManager == null)
        {
            ClearSelection();
            return;
        }

        deckManager.GetOwnedBullets(ownedBullets);

        if (selectedBullet != null && !ownedBullets.Contains(selectedBullet))
        {
            selectedBullet = null;
        }

        int capacity = bulletRows == null
            ? 0
            : bulletRows.Length * BulletsPerRow;
        int visibleCount = Mathf.Min(capacity, ownedBullets.Count);

        for (int index = 0; index < visibleCount; index++)
        {
            CreateBulletButton(ownedBullets[index], index);
        }

        if (ownedBullets.Count > capacity)
        {
            Debug.LogWarning(
                $"Bullet management UI can display {capacity} bullets, "
                + $"but the player owns {ownedBullets.Count}.",
                this);
        }

        if (selectedBullet == null && ownedBullets.Count > 0)
        {
            selectedBullet = ownedBullets[0];
        }

        RefreshSelection();
    }

    private void CreateBulletButton(BulletInstance bullet, int index)
    {
        if (myBulletButtonPrefab == null || bulletRows == null)
        {
            return;
        }

        int rowNumber = index / BulletsPerRow + 1;
        RectTransform targetRow = FindBulletRow(rowNumber);

        if (targetRow == null)
        {
            return;
        }

        Button button = Instantiate(
            myBulletButtonPrefab,
            targetRow);
        button.name = $"Button _ My Bullet {index + 1}";
        Image icon = FindNamedChild<Image>(
            button.transform,
            "Image | Bullet Sprite");
        ApplyIcon(icon, GetPreferredIcon(bullet));
        UnityAction clickAction = () => SelectBullet(bullet);
        button.onClick.AddListener(clickAction);
        SoundManager.BindUiButtonSfx(button);
        spawnedButtons.Add(button);
        spawnedClickActions.Add(clickAction);
    }

    private void RefreshSelection()
    {
        if (selectedBullet == null || selectedBullet.Data == null)
        {
            ClearSelection();
            return;
        }

        ApplyIcon(bulletIcon, null);
        ApplyIcon(cylinderIcon, selectedBullet.CylinderIcon);

        if (bulletNameText != null)
        {
            bulletNameText.richText = true;
            bulletNameText.color = selectedBullet.GradeNameColor;
            bulletNameText.text = selectedBullet.RichDisplayName;
        }

        if (bulletGradeText != null)
        {
            bulletGradeText.text = selectedBullet.Grade.ToString();
            bulletGradeText.color = selectedBullet.GradeNameColor;
        }

        if (bulletDescriptionText != null)
        {
            bulletDescriptionText.richText = true;
            bulletDescriptionText.text = selectedBullet.GetDetailedDescription(
                CreateBulletTooltipContext());
        }

        int currentMoney = currencyManager == null
            ? 0
            : currencyManager.CurrentMoney;
        bool canManageSelectedBullet = deckManager != null
            && currencyManager != null
            && deckManager.Contains(selectedBullet);
        bool canRemoveSelectedBullet = canManageSelectedBullet
            && deckManager.CanRemoveBullet(selectedBullet);
        int removeCost = deckManager == null
            ? 1
            : deckManager.CurrentBulletRemovalCost;

        if (removeButton != null)
        {
            removeButton.interactable = canRemoveSelectedBullet
                && currentMoney >= removeCost;
        }

        if (removeButtonText != null)
        {
            removeButtonText.richText = true;
            removeButtonText.text = canManageSelectedBullet
                && !canRemoveSelectedBullet
                    ? "At least 1 bullet required"
                    : $"Remove  {FormatCost(removeCost, currentMoney)}";
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = canManageSelectedBullet
                && selectedBullet.CanUpgrade
                && currentMoney >= selectedBullet.UpgradeCost;
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.richText = true;
            upgradeButtonText.text = selectedBullet.CanUpgrade
                ? $"Upgrade  {FormatCost(selectedBullet.UpgradeCost, currentMoney)}"
                : "MAX LEVEL";
        }
    }

    private static string FormatCost(int cost, int currentMoney)
    {
        return currentMoney >= cost
            ? $"${cost}"
            : $"<color={RedCostColorTag}>${cost}</color>";
    }

    private void ClearSelection()
    {
        selectedBullet = null;
        HideUpgradeTooltip();
        ApplyIcon(bulletIcon, null);
        ApplyIcon(cylinderIcon, null);

        if (bulletNameText != null)
        {
            bulletNameText.text = string.Empty;
        }

        if (bulletGradeText != null)
        {
            bulletGradeText.text = string.Empty;
        }

        if (bulletDescriptionText != null)
        {
            bulletDescriptionText.text = string.Empty;
        }

        if (removeButton != null)
        {
            removeButton.interactable = false;
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = false;
        }

        if (removeButtonText != null)
        {
            removeButtonText.text = "Remove";
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = "Upgrade";
        }
    }

    private void BindEvents()
    {
        if (manageBulletsButton != null)
        {
            manageBulletsButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (removeButton != null)
        {
            removeButton.onClick.AddListener(RemoveSelectedBullet);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(UpgradeSelectedBullet);
        }

        if (deckManager != null)
        {
            deckManager.StateChanged += HandleDeckStateChanged;
        }

        if (currencyManager != null)
        {
            currencyManager.MoneyChanged += HandleMoneyChanged;
        }
    }

    private void UnbindEvents()
    {
        if (manageBulletsButton != null)
        {
            manageBulletsButton.onClick.RemoveListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(RemoveSelectedBullet);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(UpgradeSelectedBullet);
        }

        if (deckManager != null)
        {
            deckManager.StateChanged -= HandleDeckStateChanged;
        }

        if (currencyManager != null)
        {
            currencyManager.MoneyChanged -= HandleMoneyChanged;
        }
    }

    private void ClearSpawnedButtons()
    {
        for (int index = 0; index < spawnedButtons.Count; index++)
        {
            Button button = spawnedButtons[index];

            if (button == null)
            {
                continue;
            }

            if (index < spawnedClickActions.Count)
            {
                button.onClick.RemoveListener(spawnedClickActions[index]);
            }

            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }

        spawnedButtons.Clear();
        spawnedClickActions.Clear();
    }

    private void ResolveReferences()
    {
        deckManager ??= FindSceneObject<DeckManager>();
        currencyManager ??= FindSceneObject<CurrencyManager>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
        playerShoot ??= FindSceneObject<PlayerShoot>();
        shopPanel ??= FindGameObject("Panel | Shop");
        shopItemsLayout ??= FindGameObject("Layout | Shop Items");
        manageBulletsPanel ??= FindGameObject("Panel | Manage Bullets");
        manageBulletsButton ??= FindButton("Button | Manage Bullet");
        closeButton ??= FindButton("Button | Close", manageBulletsPanel);
        removeButton ??= FindButton("Button | Remove", manageBulletsPanel);
        upgradeButton ??= FindButton("Button | Upgrade", manageBulletsPanel);
        upgradeTooltip ??= FindRectTransform(
            "Panel | Upgrade Tooltip",
            null);

        RectTransform currentBullets = FindRectTransform(
            "Layout | Current Bullets",
            manageBulletsPanel);

        RectTransform[] discoveredRows = FindDirectChildren(
            currentBullets,
            "Layout | ");

        if (discoveredRows.Length > 0)
        {
            bulletRows = discoveredRows;
        }

        SortBulletRows(bulletRows);

        RectTransform detail = FindRectTransform(
            "Layout | Bullet Manage",
            manageBulletsPanel);
        bulletManageLayout ??= detail == null ? null : detail.gameObject;
        bulletIcon ??= FindNamedChild<Image>(detail, "Image | Bullet Sprite");
        cylinderIcon ??= FindNamedChild<Image>(
            detail,
            "Image | Bullet Cylinder Sprite");
        bulletNameText ??= FindNamedChild<TextMeshProUGUI>(
            detail,
            "Text | Bullet Name");
        bulletGradeText ??= FindNamedChild<TextMeshProUGUI>(
            detail,
            "Text | Bullet Grade");
        bulletDescriptionText ??= FindNamedChild<TextMeshProUGUI>(
            detail,
            "Text | Bullet Description");
        removeButtonText ??= removeButton == null
            ? null
            : removeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        upgradeButtonText ??= upgradeButton == null
            ? null
            : upgradeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        upgradeTooltipDescriptionText ??= FindNamedChild<TextMeshProUGUI>(
            upgradeTooltip,
            "Text | Bullet Description");
    }

    private BulletTooltipContext CreateBulletTooltipContext()
    {
        return BulletTooltipContext.Create(
            deckManager,
            currencyManager,
            playerHealth,
            playerShoot);
    }

    private void RefreshUpgradeTooltip()
    {
        Mouse mouse = Mouse.current;
        RectTransform upgradeButtonRect = upgradeButton == null
            ? null
            : upgradeButton.transform as RectTransform;

        if (LoadingTransitionController.IsTransitioning
            || mouse == null || upgradeButtonRect == null
            || upgradeTooltip == null
            || upgradeTooltipDescriptionText == null
            || manageBulletsPanel == null
            || !manageBulletsPanel.activeInHierarchy
            || selectedBullet == null
            || selectedBullet.Data == null
            || !selectedBullet.CanUpgrade
            || !RectTransformUtility.RectangleContainsScreenPoint(
                upgradeButtonRect,
                mouse.position.ReadValue(),
                GetCanvasCamera(upgradeButtonRect)))
        {
            HideUpgradeTooltip();
            return;
        }

        int nextLevel = selectedBullet.Level + 1;
        upgradeTooltipDescriptionText.richText = true;
        upgradeTooltipDescriptionText.text =
            selectedBullet.Data.GetDetailedDescription(nextLevel);
        upgradeTooltip.gameObject.SetActive(true);
        PositionUpgradeTooltip(mouse.position.ReadValue());
    }

    private void PositionUpgradeTooltip(Vector2 pointerPosition)
    {
        if (upgradeTooltip == null)
        {
            return;
        }

        Canvas canvas = upgradeTooltip.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas == null ? null : canvas.rootCanvas;
        RectTransform canvasRect = rootCanvas == null
            ? null
            : rootCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
        Canvas.ForceUpdateCanvases();
        upgradeTooltip.GetWorldCorners(tooltipWorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            tooltipWorldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            tooltipWorldCorners[2]);
        Vector2 tooltipSize = topRight - bottomLeft;
        Rect screenRect = rootCanvas.pixelRect;
        float minimumX = screenRect.xMin + TooltipScreenPadding;
        float minimumY = screenRect.yMin + TooltipScreenPadding;
        float maximumX = Mathf.Max(
            minimumX,
            screenRect.xMax - TooltipScreenPadding - tooltipSize.x);
        float maximumY = Mathf.Max(
            minimumY,
            screenRect.yMax - TooltipScreenPadding - tooltipSize.y);
        Vector2 desiredBottomLeft = new Vector2(
            Mathf.Clamp(
                pointerPosition.x + TooltipPointerGap,
                minimumX,
                maximumX),
            Mathf.Clamp(
                pointerPosition.y - tooltipSize.y * 0.5f,
                minimumY,
                maximumY));
        Vector2 targetPivotPosition = desiredBottomLeft + new Vector2(
            tooltipSize.x * upgradeTooltip.pivot.x,
            tooltipSize.y * upgradeTooltip.pivot.y);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                targetPivotPosition,
                eventCamera,
                out Vector3 worldPosition))
        {
            upgradeTooltip.position = worldPosition;
        }
    }

    private void HideUpgradeTooltip()
    {
        if (upgradeTooltip != null && upgradeTooltip.gameObject.activeSelf)
        {
            upgradeTooltip.gameObject.SetActive(false);
        }
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
    }

    private static T FindSceneObject<T>() where T : Object
    {
        T[] objects = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return objects.Length == 0 ? null : objects[0];
    }

    private static GameObject FindGameObject(string objectName)
    {
        RectTransform rect = FindRectTransform(objectName, null);
        return rect == null ? null : rect.gameObject;
    }

    private static Button FindButton(
        string objectName,
        GameObject requiredAncestor = null)
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.gameObject.scene.IsValid()
                && button.name == objectName
                && (requiredAncestor == null
                    || button.transform.IsChildOf(requiredAncestor.transform)))
            {
                return button;
            }
        }

        return null;
    }

    private static RectTransform FindRectTransform(
        string objectName,
        GameObject requiredAncestor)
    {
        RectTransform[] transforms = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (RectTransform rectTransform in transforms)
        {
            if (rectTransform.gameObject.scene.IsValid()
                && rectTransform.name == objectName
                && (requiredAncestor == null
                    || rectTransform.IsChildOf(requiredAncestor.transform)))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static RectTransform[] FindDirectChildren(
        RectTransform parent,
        string namePrefix)
    {
        if (parent == null)
        {
            return System.Array.Empty<RectTransform>();
        }

        List<RectTransform> matches = new List<RectTransform>();

        for (int index = 0; index < parent.childCount; index++)
        {
            if (parent.GetChild(index) is RectTransform child
                && child.name.StartsWith(namePrefix))
            {
                matches.Add(child);
            }
        }

        matches.Sort(CompareBulletRows);
        return matches.ToArray();
    }

    private RectTransform FindBulletRow(int rowNumber)
    {
        if (bulletRows == null)
        {
            return null;
        }

        foreach (RectTransform row in bulletRows)
        {
            if (GetBulletRowNumber(row) == rowNumber)
            {
                return row;
            }
        }

        int fallbackIndex = rowNumber - 1;
        return fallbackIndex >= 0 && fallbackIndex < bulletRows.Length
            ? bulletRows[fallbackIndex]
            : null;
    }

    private void SetManagementView(bool isOpen)
    {
        if (shopItemsLayout != null)
        {
            shopItemsLayout.SetActive(!isOpen);
        }

        if (manageBulletsPanel != null)
        {
            manageBulletsPanel.SetActive(isOpen);
        }

        if (bulletManageLayout != null)
        {
            bulletManageLayout.SetActive(isOpen);
        }
    }

    private static void SortBulletRows(RectTransform[] rows)
    {
        if (rows != null)
        {
            System.Array.Sort(rows, CompareBulletRows);
        }
    }

    private static int CompareBulletRows(
        RectTransform left,
        RectTransform right)
    {
        int numberComparison = GetBulletRowNumber(left).CompareTo(
            GetBulletRowNumber(right));

        if (numberComparison != 0)
        {
            return numberComparison;
        }

        int leftSibling = left == null ? int.MaxValue : left.GetSiblingIndex();
        int rightSibling = right == null
            ? int.MaxValue
            : right.GetSiblingIndex();
        return leftSibling.CompareTo(rightSibling);
    }

    private static int GetBulletRowNumber(RectTransform row)
    {
        if (row == null)
        {
            return int.MaxValue;
        }

        const string Prefix = "Layout | ";
        string suffix = row.name.StartsWith(Prefix)
            ? row.name.Substring(Prefix.Length).Trim()
            : string.Empty;
        return int.TryParse(suffix, out int rowNumber)
            ? rowNumber
            : int.MaxValue;
    }

    private static T FindNamedChild<T>(Transform root, string objectName)
        where T : Component
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

    private static Sprite GetPreferredIcon(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return null;
        }

        return bullet.CylinderIcon;
    }

    private static void ApplyIcon(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }
}
