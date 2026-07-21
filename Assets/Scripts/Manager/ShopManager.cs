using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class BulletGradeWeightData
{
    [SerializeField] private BulletGrade grade;
    [Min(0f)]
    [SerializeField] private float appearanceWeight = 1f;

    public BulletGrade Grade => grade;
    public float AppearanceWeight => Mathf.Max(0f, appearanceWeight);
}

[Serializable]
public class ShopBulletSlot
{
    [SerializeField] private Button button;
    [SerializeField] private Image bulletIcon;
    [SerializeField] private TMP_Text costText;

    public Button Button => button;
    public Image BulletIcon => bulletIcon;
    public TMP_Text CostText => costText;
}

[Serializable]
public class ShopItemSlot
{
    [SerializeField] private Button button;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text costText;

    public Button Button => button;
    public Image ItemIcon => itemIcon;
    public TMP_Text CostText => costText;

    public ShopItemSlot()
    {
    }

    public ShopItemSlot(Button button, Image itemIcon, TMP_Text costText)
    {
        this.button = button;
        this.itemIcon = itemIcon;
        this.costText = costText;
    }
}

public class ShopManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Bullet Offers")]
    [Tooltip("The configured candidate pool. Offers are drawn without replacement up to the number of connected slots.")]
    [SerializeField] private List<BulletData> bulletPool = new List<BulletData>();
    [SerializeField] private List<BulletGradeWeightData> gradeWeights =
        new List<BulletGradeWeightData>();
    [SerializeField] private List<ShopBulletSlot> slots =
        new List<ShopBulletSlot>();

    [Header("Item Offers")]
    [Tooltip("Every unique item has the same appearance probability.")]
    [SerializeField] private List<ItemData> itemPool = new List<ItemData>();
    [SerializeField] private List<ShopItemSlot> itemSlots =
        new List<ShopItemSlot>();

    private readonly List<BulletData> currentOffers =
        new List<BulletData>();
    private readonly List<UnityAction> slotClickActions =
        new List<UnityAction>();
    private readonly List<ItemData> currentItemOffers =
        new List<ItemData>();
    private readonly List<bool> purchasedItemOffers = new List<bool>();
    private readonly List<UnityAction> itemSlotClickActions =
        new List<UnityAction>();

    public event Action OffersChanged;

    public IReadOnlyList<BulletData> CurrentOffers => currentOffers;
    public IReadOnlyList<ItemData> CurrentItemOffers => currentItemOffers;

    private void Awake()
    {
        ResolveReferences();
        BindSlotButtons();
        BindItemSlotButtons();
        ClearOffers();

        if (playerInventory != null)
        {
            playerInventory.Changed += RefreshItemSlots;
        }
    }

    private void OnDestroy()
    {
        UnbindSlotButtons();
        UnbindItemSlotButtons();

        if (playerInventory != null)
        {
            playerInventory.Changed -= RefreshItemSlots;
        }
    }

    public void OpenShop()
    {
        GenerateOffers();
        GenerateItemOffers();
    }

    public bool TryPurchase(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentOffers.Count
            || slotIndex >= slots.Count)
        {
            return false;
        }

        ShopBulletSlot slot = slots[slotIndex];
        BulletData bulletData = currentOffers[slotIndex];

        if (slot == null || slot.Button == null || !slot.Button.interactable
            || bulletData == null || currencyManager == null
            || deckManager == null
            || !currencyManager.TrySpendMoney(bulletData.Price))
        {
            return false;
        }

        if (!deckManager.TryAddBullet(bulletData))
        {
            currencyManager.AddMoney(bulletData.Price);
            return false;
        }

        slot.Button.interactable = false;
        OffersChanged?.Invoke();
        return true;
    }

    public bool TryPurchaseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentItemOffers.Count
            || slotIndex >= itemSlots.Count
            || slotIndex >= purchasedItemOffers.Count)
        {
            return false;
        }

        ShopItemSlot slot = itemSlots[slotIndex];
        ItemData itemData = currentItemOffers[slotIndex];

        if (slot == null || slot.Button == null || !slot.Button.interactable
            || itemData == null || currencyManager == null
            || playerInventory == null || !playerInventory.CanAdd(itemData)
            || !currencyManager.TrySpendMoney(itemData.Price))
        {
            return false;
        }

        if (!playerInventory.TryAdd(itemData))
        {
            currencyManager.AddMoney(itemData.Price);
            return false;
        }

        purchasedItemOffers[slotIndex] = true;
        slot.Button.interactable = false;
        OffersChanged?.Invoke();
        return true;
    }

    public BulletData GetBulletOffer(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < currentOffers.Count
            ? currentOffers[slotIndex]
            : null;
    }

    public ItemData GetItemOffer(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < currentItemOffers.Count
            ? currentItemOffers[slotIndex]
            : null;
    }

    private void GenerateOffers()
    {
        List<BulletData> candidates = BuildCandidateList();
        currentOffers.Clear();

        int offerCount = Mathf.Min(slots.Count, candidates.Count);

        for (int slotIndex = 0; slotIndex < offerCount; slotIndex++)
        {
            int candidateIndex = SelectWeightedCandidateIndex(candidates);

            if (candidateIndex < 0)
            {
                break;
            }

            currentOffers.Add(candidates[candidateIndex]);
            candidates.RemoveAt(candidateIndex);
        }

        RefreshSlots();
        OffersChanged?.Invoke();
    }

    private void GenerateItemOffers()
    {
        List<ItemData> candidates = new List<ItemData>();

        foreach (ItemData itemData in itemPool)
        {
            if (itemData != null && !candidates.Contains(itemData))
            {
                candidates.Add(itemData);
            }
        }

        currentItemOffers.Clear();
        purchasedItemOffers.Clear();
        int offerCount = Mathf.Min(itemSlots.Count, candidates.Count);

        for (int slotIndex = 0; slotIndex < offerCount; slotIndex++)
        {
            int candidateIndex = UnityEngine.Random.Range(0, candidates.Count);
            currentItemOffers.Add(candidates[candidateIndex]);
            purchasedItemOffers.Add(false);
            candidates.RemoveAt(candidateIndex);
        }

        RefreshItemSlots();
        OffersChanged?.Invoke();
    }

    private List<BulletData> BuildCandidateList()
    {
        List<BulletData> candidates = new List<BulletData>();

        foreach (BulletData bulletData in bulletPool)
        {
            if (bulletData != null && !candidates.Contains(bulletData)
                && GetGradeWeight(bulletData.Grade) > 0f)
            {
                candidates.Add(bulletData);
            }
        }

        return candidates;
    }

    private int SelectWeightedCandidateIndex(List<BulletData> candidates)
    {
        List<BulletGrade> availableGrades = new List<BulletGrade>();

        foreach (BulletData candidate in candidates)
        {
            if (!availableGrades.Contains(candidate.Grade)
                && GetGradeWeight(candidate.Grade) > 0f)
            {
                availableGrades.Add(candidate.Grade);
            }
        }

        float totalWeight = 0f;

        foreach (BulletGrade grade in availableGrades)
        {
            totalWeight += GetGradeWeight(grade);
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        BulletGrade selectedGrade = availableGrades[availableGrades.Count - 1];

        foreach (BulletGrade grade in availableGrades)
        {
            roll -= GetGradeWeight(grade);

            if (roll <= 0f)
            {
                selectedGrade = grade;
                break;
            }
        }

        List<int> gradeCandidateIndices = new List<int>();

        for (int candidateIndex = 0;
             candidateIndex < candidates.Count;
             candidateIndex++)
        {
            if (candidates[candidateIndex].Grade == selectedGrade)
            {
                gradeCandidateIndices.Add(candidateIndex);
            }
        }

        return gradeCandidateIndices.Count == 0
            ? -1
            : gradeCandidateIndices[UnityEngine.Random.Range(
                0,
                gradeCandidateIndices.Count)];
    }

    private float GetGradeWeight(BulletGrade grade)
    {
        foreach (BulletGradeWeightData gradeWeight in gradeWeights)
        {
            if (gradeWeight != null && gradeWeight.Grade == grade)
            {
                return gradeWeight.AppearanceWeight;
            }
        }

        return grade switch
        {
            BulletGrade.Normal => 100f,
            BulletGrade.Rare => 85f,
            BulletGrade.Ace => 10f,
            BulletGrade.Legendary => 3f,
            _ => 0f
        };
    }

    private void RefreshSlots()
    {
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            BulletData offer = slotIndex < currentOffers.Count
                ? currentOffers[slotIndex]
                : null;
            RefreshSlot(slots[slotIndex], offer);
        }
    }

    private void RefreshSlot(ShopBulletSlot slot, BulletData offer)
    {
        if (slot == null)
        {
            return;
        }

        if (slot.Button != null)
        {
            slot.Button.gameObject.SetActive(offer != null);
            slot.Button.interactable = offer != null;
        }

        if (slot.BulletIcon != null)
        {
            slot.BulletIcon.gameObject.SetActive(offer != null);
            slot.BulletIcon.sprite = offer == null ? null : offer.BulletIcon;
            slot.BulletIcon.enabled = offer != null;
            slot.BulletIcon.color = offer == null || offer.BulletIcon != null
                ? Color.white
                : offer.PrimaryLineColor;
            slot.BulletIcon.preserveAspect = true;
        }

        if (slot.CostText != null)
        {
            slot.CostText.text = offer == null ? string.Empty : $"${offer.Price}";
        }
    }

    private void RefreshItemSlots()
    {
        for (int slotIndex = 0; slotIndex < itemSlots.Count; slotIndex++)
        {
            ItemData offer = GetItemOffer(slotIndex);
            bool purchased = slotIndex < purchasedItemOffers.Count
                && purchasedItemOffers[slotIndex];
            RefreshItemSlot(itemSlots[slotIndex], offer, purchased);
        }
    }

    private void RefreshItemSlot(
        ShopItemSlot slot,
        ItemData offer,
        bool purchased)
    {
        if (slot == null)
        {
            return;
        }

        if (slot.Button != null)
        {
            slot.Button.gameObject.SetActive(offer != null);
            slot.Button.interactable = offer != null && !purchased
                && playerInventory != null && playerInventory.CanAdd(offer);
        }

        if (slot.ItemIcon != null)
        {
            slot.ItemIcon.sprite = offer == null ? null : offer.Icon;
            slot.ItemIcon.enabled = offer != null && offer.Icon != null;
            slot.ItemIcon.preserveAspect = true;
        }

        if (slot.CostText != null)
        {
            slot.CostText.text = offer == null ? string.Empty : $"${offer.Price}";
        }
    }

    private void ClearOffers()
    {
        currentOffers.Clear();
        currentItemOffers.Clear();
        purchasedItemOffers.Clear();
        RefreshSlots();
        RefreshItemSlots();
    }

    private void BindSlotButtons()
    {
        slotClickActions.Clear();

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            int capturedSlotIndex = slotIndex;
            UnityAction clickAction = () => TryPurchase(capturedSlotIndex);
            slotClickActions.Add(clickAction);

            if (slots[slotIndex] != null && slots[slotIndex].Button != null)
            {
                slots[slotIndex].Button.onClick.AddListener(clickAction);
            }
        }
    }

    private void UnbindSlotButtons()
    {
        for (int slotIndex = 0;
             slotIndex < slots.Count && slotIndex < slotClickActions.Count;
             slotIndex++)
        {
            if (slots[slotIndex] != null && slots[slotIndex].Button != null)
            {
                slots[slotIndex].Button.onClick.RemoveListener(
                    slotClickActions[slotIndex]);
            }
        }

        slotClickActions.Clear();
    }

    private void BindItemSlotButtons()
    {
        itemSlotClickActions.Clear();

        for (int slotIndex = 0; slotIndex < itemSlots.Count; slotIndex++)
        {
            int capturedSlotIndex = slotIndex;
            UnityAction clickAction = () => TryPurchaseItem(capturedSlotIndex);
            itemSlotClickActions.Add(clickAction);

            if (itemSlots[slotIndex] != null
                && itemSlots[slotIndex].Button != null)
            {
                itemSlots[slotIndex].Button.onClick.AddListener(clickAction);
            }
        }
    }

    private void UnbindItemSlotButtons()
    {
        for (int slotIndex = 0;
             slotIndex < itemSlots.Count
             && slotIndex < itemSlotClickActions.Count;
             slotIndex++)
        {
            if (itemSlots[slotIndex] != null
                && itemSlots[slotIndex].Button != null)
            {
                itemSlots[slotIndex].Button.onClick.RemoveListener(
                    itemSlotClickActions[slotIndex]);
            }
        }

        itemSlotClickActions.Clear();
    }

    private void ResolveReferences()
    {
        currencyManager ??= FindSceneObject<CurrencyManager>();
        deckManager ??= FindSceneObject<DeckManager>();
        playerInventory ??= FindSceneObject<PlayerInventory>();

        if (itemPool.Count == 0)
        {
            itemPool.AddRange(Resources.LoadAll<ItemData>("Items"));
        }

        itemSlots.RemoveAll(slot => slot == null || slot.Button == null);

        if (itemSlots.Count > 0)
        {
            return;
        }

        Button[] allButtons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<Button> discoveredButtons = new List<Button>();

        foreach (Button button in allButtons)
        {
            if (button != null && button.gameObject.scene.IsValid()
                && button.name == "Button | Shop Item"
                && button.transform.parent != null
                && button.transform.parent.name == "Layout | Shop Items")
            {
                discoveredButtons.Add(button);
            }
        }

        discoveredButtons.Sort((left, right) =>
            left.transform.GetSiblingIndex().CompareTo(
                right.transform.GetSiblingIndex()));

        int slotCount = Mathf.Min(2, discoveredButtons.Count);

        for (int index = 0; index < slotCount; index++)
        {
            Button button = discoveredButtons[index];
            itemSlots.Add(new ShopItemSlot(
                button,
                FindNamedChild<Image>(button.transform, "Image | Sprite"),
                FindNamedChild<TMP_Text>(button.transform, "Text | Cost")));
        }
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return objects.Length == 0 ? null : objects[0];
    }

    private static T FindNamedChild<T>(Transform root, string objectName)
        where T : Component
    {
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
