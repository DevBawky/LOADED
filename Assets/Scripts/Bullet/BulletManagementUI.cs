using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum EventBulletSelectionMode
{
    None,
    Remove,
    Upgrade
}

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

    [Header("Bullet Button Visual")]
    [SerializeField] private Color hoverIndicatorColor = Color.white;
    [SerializeField] private Color selectedIndicatorColor =
        new Color(1f, 0.5f, 0f, 1f);

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
    [SerializeField] private TextMeshProUGUI warningText;

    [Header("Upgrade Tooltip")]
    [SerializeField] private RectTransform upgradeTooltip;
    [SerializeField] private TextMeshProUGUI upgradeTooltipDescriptionText;

    private readonly List<BulletInstance> ownedBullets =
        new List<BulletInstance>();
    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly List<UnityAction> spawnedClickActions =
        new List<UnityAction>();
    private readonly List<BulletInstance> spawnedButtonBullets =
        new List<BulletInstance>();
    private readonly List<BulletButtonVisualState> spawnedButtonVisuals =
        new List<BulletButtonVisualState>();
    private readonly List<System.Action<bool>> spawnedHoverActions =
        new List<System.Action<bool>>();
    private readonly List<BulletInstance> eventSelectedBullets =
        new List<BulletInstance>();
    private readonly Vector3[] tooltipWorldCorners = new Vector3[4];
    private BulletInstance selectedBullet;
    private BulletInstance hoveredBullet;
    private bool wasShopActive;
    private EventBulletSelectionMode eventSelectionMode;
    private int eventRequiredSelectionCount = 1;
    private System.Func<BulletInstance, bool> eventSelectionPredicate;
    private System.Func<IReadOnlyList<BulletInstance>, bool>
        eventSelectionValidator;
    private System.Action<IReadOnlyList<BulletInstance>>
        eventConfirmCallback;
    private System.Action eventCancelCallback;

    public BulletInstance SelectedBullet => selectedBullet;
    public BulletInstance TooltipBullet => hoveredBullet ?? selectedBullet;
    public bool IsOpen => manageBulletsPanel != null
        && manageBulletsPanel.activeInHierarchy;

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

        if (wasShopActive != isShopActive)
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

    public bool OpenEventSelection(
        Transform runtimeRoot,
        DeckManager runtimeDeckManager,
        EventBulletSelectionMode mode,
        System.Action<BulletInstance> onConfirm,
        System.Action onCancel)
    {
        return OpenEventSelection(
            runtimeRoot,
            runtimeDeckManager,
            mode,
            1,
            null,
            null,
            bullets =>
            {
                if (bullets != null && bullets.Count > 0)
                {
                    onConfirm?.Invoke(bullets[0]);
                }
            },
            onCancel);
    }

    public bool OpenEventSelection(
        Transform runtimeRoot,
        DeckManager runtimeDeckManager,
        EventBulletSelectionMode mode,
        int requiredSelectionCount,
        System.Func<BulletInstance, bool> selectionPredicate,
        System.Func<IReadOnlyList<BulletInstance>, bool> selectionValidator,
        System.Action<IReadOnlyList<BulletInstance>> onConfirm,
        System.Action onCancel)
    {
        if (mode == EventBulletSelectionMode.None
            || requiredSelectionCount <= 0)
        {
            return false;
        }

        UnbindEvents();
        deckManager = runtimeDeckManager;
        currencyManager = null;
        shopPanel = runtimeRoot == null ? null : runtimeRoot.gameObject;
        wasShopActive = shopPanel != null && shopPanel.activeInHierarchy;
        manageBulletsPanel = FindNamedGameObject(
            runtimeRoot,
            "Panel | Manage Bullets");
        closeButton = FindNamedChild<Button>(
            manageBulletsPanel == null ? null : manageBulletsPanel.transform,
            "Button | Close");
        removeButton = FindNamedChild<Button>(
            manageBulletsPanel == null ? null : manageBulletsPanel.transform,
            "Button | Remove");
        upgradeButton = FindNamedChild<Button>(
            manageBulletsPanel == null ? null : manageBulletsPanel.transform,
            "Button | Upgrade");
        upgradeTooltip = FindNamedChild<RectTransform>(
            manageBulletsPanel == null ? null : manageBulletsPanel.transform,
            "Panel | Upgrade Tooltip");
        upgradeTooltipDescriptionText = FindNamedChild<TextMeshProUGUI>(
            upgradeTooltip,
            "Text | Bullet Description");
        ResolveReferences();
        BindEvents();
        DisableRaycasts(upgradeTooltip);
        HideUpgradeTooltip();

        eventSelectionMode = mode;
        eventRequiredSelectionCount = requiredSelectionCount;
        eventSelectionPredicate = selectionPredicate;
        eventSelectionValidator = selectionValidator;
        eventConfirmCallback = onConfirm;
        eventCancelCallback = onCancel;
        SetManagementView(true);
        RefreshOwnedBullets();
        return manageBulletsPanel != null;
    }

    public bool ConfigureDedicatedShop(
        Transform runtimeShopRoot,
        DeckManager runtimeDeckManager,
        CurrencyManager runtimeCurrencyManager)
    {
        // The dedicated Shop canvas and its nested panel can deserialize in a
        // different order from the legacy Battle canvas. Rebind both manager
        // events and UI button events after every required object exists.
        UnbindEvents();
        deckManager = runtimeDeckManager;
        currencyManager = runtimeCurrencyManager;
        shopPanel = runtimeShopRoot == null
            ? null
            : runtimeShopRoot.gameObject;
        shopItemsLayout = FindShopItemsLayout(runtimeShopRoot);
        manageBulletsPanel = FindNamedGameObject(
            runtimeShopRoot,
            "Panel | Manage Bullets");
        manageBulletsButton = FindNamedChild<Button>(
            runtimeShopRoot,
            "Button | Manage Bullet");
        closeButton = FindNamedChild<Button>(
            manageBulletsPanel == null
                ? null
                : manageBulletsPanel.transform,
            "Button | Close");
        removeButton = FindNamedChild<Button>(
            manageBulletsPanel == null
                ? null
                : manageBulletsPanel.transform,
            "Button | Remove");
        upgradeButton = FindNamedChild<Button>(
            manageBulletsPanel == null
                ? null
                : manageBulletsPanel.transform,
            "Button | Upgrade");
        upgradeTooltip = FindNamedChild<RectTransform>(
            runtimeShopRoot,
            "Panel | Upgrade Tooltip");
        ResolveReferences();
        BindEvents();
        DisableRaycasts(upgradeTooltip);
        HideUpgradeTooltip();

        bool configured = manageBulletsButton != null
            && closeButton != null
            && manageBulletsPanel != null;

        if (!configured)
        {
            Debug.LogError(
                "Dedicated Shop bullet management UI is missing its open button, close button, or management panel.",
                this);
        }

        if (IsOpen)
        {
            RefreshOwnedBullets();
        }

        return configured;
    }

    public void Close()
    {
        bool notifyEventCancel = eventSelectionMode
            != EventBulletSelectionMode.None;
        System.Action cancelCallback = eventCancelCallback;
        ResetEventSelection();
        SetManagementView(false);
        HideUpgradeTooltip();
        ClearSpawnedButtons();
        ClearSelection();

        if (notifyEventCancel)
        {
            cancelCallback?.Invoke();
        }
    }

    public bool TryCloseFromEscape()
    {
        if (manageBulletsPanel == null
            || !manageBulletsPanel.activeInHierarchy)
        {
            return false;
        }

        Close();
        return true;
    }

    public void RemoveSelectedBullet()
    {
        if (eventSelectionMode == EventBulletSelectionMode.Remove)
        {
            ConfirmEventSelection();
            return;
        }

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
        if (eventSelectionMode == EventBulletSelectionMode.Upgrade)
        {
            ConfirmEventSelection();
            return;
        }

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
        SoundManager.PlaySfxNonOverlapping("UI_Upgrade");
        RefreshOwnedBullets();
    }

    private void SelectBullet(BulletInstance bullet)
    {
        if (eventSelectionMode != EventBulletSelectionMode.None)
        {
            if (bullet == null || eventSelectionPredicate != null
                && !eventSelectionPredicate(bullet))
            {
                return;
            }

            if (eventSelectedBullets.Contains(bullet))
            {
                eventSelectedBullets.Remove(bullet);
            }
            else if (eventSelectedBullets.Count
                     < eventRequiredSelectionCount)
            {
                eventSelectedBullets.Add(bullet);
            }

            selectedBullet = bullet;
            RefreshSelection();
            return;
        }

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

        if (selectedBullet == null && ownedBullets.Count > 0
            && eventSelectionMode == EventBulletSelectionMode.None)
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
        button.transition = Selectable.Transition.None;
        Image icon = FindNamedChild<Image>(
            button.transform,
            "Image | Bullet Sprite");
        ApplyIcon(icon, GetPreferredIcon(bullet));
        if (icon != null)
        {
            icon.raycastTarget = true;
        }

        TextMeshProUGUI levelText = FindNamedChild<TextMeshProUGUI>(
            button.transform,
            "Text | Level");
        ApplyUpgradeLevel(levelText, bullet);

        TextMeshProUGUI stackText = FindNamedChild<TextMeshProUGUI>(
            button.transform,
            "Text | Stack");
        ApplyStackCount(
            stackText,
            bullet,
            CreateBulletTooltipContext());

        Image indicator = button.GetComponent<Image>();
        BulletButtonVisualState visual =
            button.GetComponent<BulletButtonVisualState>();
        if (visual == null)
        {
            visual = button.gameObject.AddComponent<BulletButtonVisualState>();
        }

        visual.Initialize(
            indicator,
            hoverIndicatorColor,
            selectedIndicatorColor);
        System.Action<bool> hoverAction = isHovered =>
            HandleBulletHover(bullet, isHovered);
        visual.HoverChanged += hoverAction;
        UnityAction clickAction = () => SelectBullet(bullet);
        button.onClick.AddListener(clickAction);
        if (eventSelectionMode != EventBulletSelectionMode.None
            && eventSelectionPredicate != null
            && !eventSelectionPredicate(bullet))
        {
            button.interactable = false;
        }
        SoundManager.BindUiButtonSfx(button);
        spawnedButtons.Add(button);
        spawnedClickActions.Add(clickAction);
        spawnedButtonBullets.Add(bullet);
        spawnedButtonVisuals.Add(visual);
        spawnedHoverActions.Add(hoverAction);
    }

    private void HandleBulletHover(BulletInstance bullet, bool isHovered)
    {
        if (isHovered)
        {
            hoveredBullet = bullet;
        }
        else if (hoveredBullet == bullet)
        {
            hoveredBullet = null;
        }
    }

    private void RefreshSelection()
    {
        RefreshButtonSelection();

        if (selectedBullet == null || selectedBullet.Data == null)
        {
            ClearSelection();
            return;
        }

        if (eventSelectionMode != EventBulletSelectionMode.None)
        {
            RefreshEventSelection();
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
            removeButtonText.text =
                $"제거  {FormatCost(removeCost, currentMoney)}";
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
                ? $"강화  {FormatCost(selectedBullet.UpgradeCost, currentMoney)}"
                : "강화";
        }

        string warning = GetManagementWarning(
            canManageSelectedBullet,
            canRemoveSelectedBullet,
            currentMoney,
            removeCost);
        SetWarning(warning);
    }

    private string GetManagementWarning(
        bool canManageSelectedBullet,
        bool canRemoveSelectedBullet,
        int currentMoney,
        int removeCost)
    {
        if (!canManageSelectedBullet)
        {
            return "선택한 탄환을 관리할 수 없습니다.";
        }

        if (!canRemoveSelectedBullet)
        {
            return "최소 1개의 탄환은 보유해야 합니다.";
        }

        if (!selectedBullet.CanUpgrade)
        {
            return "이미 최대 강화 단계입니다.";
        }

        if (currentMoney < removeCost
            || currentMoney < selectedBullet.UpgradeCost)
        {
            return "골드가 부족합니다.";
        }

        return string.Empty;
    }

    private void SetWarning(string message)
    {
        if (warningText == null)
        {
            return;
        }

        bool hasWarning = !string.IsNullOrWhiteSpace(message);
        warningText.text = hasWarning ? message : string.Empty;
        warningText.gameObject.SetActive(hasWarning);
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
        RefreshButtonSelection();
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

        if (eventSelectionMode != EventBulletSelectionMode.None)
        {
            bool isRemove = eventSelectionMode
                == EventBulletSelectionMode.Remove;
            if (removeButton != null)
            {
                removeButton.gameObject.SetActive(isRemove);
                removeButton.interactable = IsEventSelectionValid();
            }
            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(!isRemove);
                upgradeButton.interactable = IsEventSelectionValid();
            }
            if (removeButtonText != null)
            {
                removeButtonText.text =
                    $"선택 완료 ({eventSelectedBullets.Count}/{eventRequiredSelectionCount})";
            }
            if (upgradeButtonText != null)
            {
                upgradeButtonText.text =
                    $"선택 완료 ({eventSelectedBullets.Count}/{eventRequiredSelectionCount})";
            }
            SetWarning(
                $"조건에 맞는 탄환을 {eventRequiredSelectionCount}개 선택하세요.");
            return;
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
            removeButtonText.text = "제거";
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = "강화";
        }

        SetWarning(string.Empty);
    }

    private void RefreshEventSelection()
    {
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

        bool isRemove = eventSelectionMode == EventBulletSelectionMode.Remove;
        bool valid = IsEventSelectionValid();

        if (removeButton != null)
        {
            removeButton.gameObject.SetActive(isRemove);
            removeButton.interactable = valid;
        }

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(!isRemove);
            upgradeButton.interactable = valid;
        }

        if (removeButtonText != null)
        {
            removeButtonText.text =
                $"선택 완료 ({eventSelectedBullets.Count}/{eventRequiredSelectionCount})";
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text =
                $"선택 완료 ({eventSelectedBullets.Count}/{eventRequiredSelectionCount})";
        }

        SetWarning(valid ? string.Empty
            : $"조건에 맞는 탄환을 {eventRequiredSelectionCount}개 선택하세요.");
    }

    private void ConfirmEventSelection()
    {
        if (!IsEventSelectionValid())
        {
            RefreshSelection();
            return;
        }

        List<BulletInstance> confirmed =
            new List<BulletInstance>(eventSelectedBullets);
        System.Action<IReadOnlyList<BulletInstance>> callback =
            eventConfirmCallback;
        ResetEventSelection();
        SetManagementView(false);
        HideUpgradeTooltip();
        ClearSpawnedButtons();
        ClearSelection();
        callback?.Invoke(confirmed);
    }

    private bool IsEventSelectionValid()
    {
        if (deckManager == null
            || eventSelectedBullets.Count != eventRequiredSelectionCount)
        {
            return false;
        }

        foreach (BulletInstance bullet in eventSelectedBullets)
        {
            if (bullet == null || !deckManager.Contains(bullet)
                || eventSelectionPredicate != null
                && !eventSelectionPredicate(bullet)
                || eventSelectionMode == EventBulletSelectionMode.Upgrade
                && !bullet.CanUpgrade)
            {
                return false;
            }
        }

        if (eventSelectionMode == EventBulletSelectionMode.Remove
            && deckManager.OwnedBulletCount
                - eventSelectedBullets.Count
                < DeckManager.MinimumOwnedBulletCount)
        {
            return false;
        }

        return eventSelectionValidator == null
            || eventSelectionValidator(eventSelectedBullets);
    }

    private void ResetEventSelection()
    {
        eventSelectionMode = EventBulletSelectionMode.None;
        eventRequiredSelectionCount = 1;
        eventSelectionPredicate = null;
        eventSelectionValidator = null;
        eventConfirmCallback = null;
        eventCancelCallback = null;
        eventSelectedBullets.Clear();

        if (removeButton != null)
        {
            removeButton.gameObject.SetActive(true);
        }

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(true);
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
        hoveredBullet = null;

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

            if (index < spawnedButtonVisuals.Count
                && index < spawnedHoverActions.Count
                && spawnedButtonVisuals[index] != null)
            {
                spawnedButtonVisuals[index].HoverChanged -=
                    spawnedHoverActions[index];
            }

            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }

        spawnedButtons.Clear();
        spawnedClickActions.Clear();
        spawnedButtonBullets.Clear();
        spawnedButtonVisuals.Clear();
        spawnedHoverActions.Clear();
    }

    private void RefreshButtonSelection()
    {
        int count = Mathf.Min(
            spawnedButtonBullets.Count,
            spawnedButtonVisuals.Count);

        for (int index = 0; index < count; index++)
        {
            BulletButtonVisualState visual = spawnedButtonVisuals[index];
            if (visual != null)
            {
                visual.SetSelected(eventSelectionMode
                    != EventBulletSelectionMode.None
                        ? eventSelectedBullets.Contains(
                            spawnedButtonBullets[index])
                        : spawnedButtonBullets[index] == selectedBullet);
            }
        }
    }

    private void ResolveReferences()
    {
        deckManager ??= FindSceneObject<DeckManager>();
        currencyManager ??= FindSceneObject<CurrencyManager>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
        playerShoot ??= FindSceneObject<PlayerShoot>();
        shopPanel ??= FindGameObject("Panel | Shop");
        shopPanel ??= FindGameObject("Panel_Shop");
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
        warningText ??= FindNamedChild<TextMeshProUGUI>(
            manageBulletsPanel == null ? null : manageBulletsPanel.transform,
            "Text | Warning");
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

    private static GameObject FindNamedGameObject(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform candidate in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindShopItemsLayout(Transform shopRoot)
    {
        if (shopRoot == null)
        {
            return null;
        }

        GameObject fallback = null;

        foreach (Transform candidate in
                 shopRoot.GetComponentsInChildren<Transform>(true))
        {
            if (candidate == null || candidate.name != "Layout | Shop Items")
            {
                continue;
            }

            fallback ??= candidate.gameObject;

            foreach (Button button in
                     candidate.GetComponentsInChildren<Button>(true))
            {
                if (button != null
                    && (button.name == "Button | Bullet Item"
                        || button.name == "Button | Shop Item"))
                {
                    return candidate.gameObject;
                }
            }
        }

        return fallback;
    }

    private static Sprite GetPreferredIcon(BulletInstance bullet)
    {
        if (bullet == null)
        {
            return null;
        }

        return bullet.CylinderIcon;
    }

    private static void ApplyUpgradeLevel(
        TextMeshProUGUI levelText,
        BulletInstance bullet)
    {
        if (levelText == null)
        {
            return;
        }

        bool hasUpgrade = bullet != null
            && bullet.Data != null
            && bullet.Level > 0;
        levelText.gameObject.SetActive(hasUpgrade);

        if (!hasUpgrade)
        {
            levelText.text = string.Empty;
            return;
        }

        levelText.text = $"+{bullet.Level}";
        levelText.color = bullet.Data.GetUpgradeLevelColor(bullet.Level);
    }

    private static void ApplyStackCount(
        TextMeshProUGUI stackText,
        BulletInstance bullet,
        BulletTooltipContext context)
    {
        if (stackText == null)
        {
            return;
        }

        string statusText = bullet == null
            ? string.Empty
            : bullet.GetStatusDisplayText(context);
        stackText.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
        stackText.text = statusText;
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
