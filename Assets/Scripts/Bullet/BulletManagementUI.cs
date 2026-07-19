using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BulletManagementUI : MonoBehaviour
{
    private const int BulletsPerRow = 5;

    [Header("Managers")]
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;

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

    private readonly List<BulletInstance> ownedBullets =
        new List<BulletInstance>();
    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly List<UnityAction> spawnedClickActions =
        new List<UnityAction>();
    private BulletInstance selectedBullet;
    private bool wasShopActive;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
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
        ClearSpawnedButtons();
        ClearSelection();
    }

    public void RemoveSelectedBullet()
    {
        if (selectedBullet == null || deckManager == null
            || currencyManager == null)
        {
            return;
        }

        int cost = selectedBullet.RemoveCost;

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

        ApplyIcon(bulletIcon, selectedBullet.BulletIcon);
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
            bulletDescriptionText.text = BuildDescription(selectedBullet);
        }

        int currentMoney = currencyManager == null
            ? 0
            : currencyManager.CurrentMoney;
        bool canManageSelectedBullet = deckManager != null
            && currencyManager != null
            && deckManager.Contains(selectedBullet);

        if (removeButton != null)
        {
            removeButton.interactable = canManageSelectedBullet
                && currentMoney >= selectedBullet.RemoveCost;
        }

        if (removeButtonText != null)
        {
            removeButtonText.text = $"Remove  ${selectedBullet.RemoveCost}";
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = canManageSelectedBullet
                && selectedBullet.CanUpgrade
                && currentMoney >= selectedBullet.UpgradeCost;
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = selectedBullet.CanUpgrade
                ? $"Upgrade  ${selectedBullet.UpgradeCost}"
                : "MAX LEVEL";
        }
    }

    private void ClearSelection()
    {
        selectedBullet = null;
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

    private static string BuildDescription(BulletInstance bullet)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(bullet.Description);
        builder.AppendLine();
        builder.Append("Damage: ").AppendLine(bullet.Damage.ToString());
        builder.Append("Range: ").AppendLine(bullet.MaxRange.ToString());
        builder.Append("Critical: x")
            .AppendLine(bullet.CriticalDamageMultiplier.ToString("0.##"));
        builder.Append("Recoil: ")
            .AppendLine(bullet.RecoilStrength.ToString("0.##"));
        builder.Append("Turn Free: ")
            .AppendLine(bullet.DoesNotConsumeTurn ? "Yes" : "No");
        builder.Append("Effects: ")
            .AppendLine(FormatEffects(bullet.Effects));
        builder.Append("Penetration: ")
            .Append(FormatPenetration(bullet.PenetrationChances));
        return builder.ToString().Trim();
    }

    private static string FormatEffects(
        IReadOnlyList<BulletEffectData> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();

        for (int index = 0; index < effects.Count; index++)
        {
            BulletEffectData effect = effects[index];

            if (effect == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(effect.EffectType)
                .Append(' ')
                .Append(effect.ActivationChance.ToString("0.##"))
                .Append('%');
        }

        return builder.Length == 0 ? "None" : builder.ToString();
    }

    private static string FormatPenetration(
        IReadOnlyList<PenetrationChanceData> chances)
    {
        if (chances == null || chances.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();

        for (int index = 0; index < chances.Count; index++)
        {
            PenetrationChanceData chance = chances[index];

            if (chance == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(chance.Chance.ToString("0.##")).Append('%');
        }

        return builder.Length == 0 ? "None" : builder.ToString();
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
        shopPanel ??= FindGameObject("Panel | Shop");
        shopItemsLayout ??= FindGameObject("Layout | Shop Items");
        manageBulletsPanel ??= FindGameObject("Panel | Manage Bullets");
        manageBulletsButton ??= FindButton("Button | Manage Bullet");
        closeButton ??= FindButton("Button | Close", manageBulletsPanel);
        removeButton ??= FindButton("Button | Remove", manageBulletsPanel);
        upgradeButton ??= FindButton("Button | Upgrade", manageBulletsPanel);

        RectTransform currentBullets = FindRectTransform(
            "Layout | Current Bullets",
            manageBulletsPanel);

        if (bulletRows == null || bulletRows.Length == 0)
        {
            bulletRows = FindDirectChildren(currentBullets, "Layout | ");
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

        return bullet.BulletIcon != null
            ? bullet.BulletIcon
            : bullet.CylinderIcon;
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
