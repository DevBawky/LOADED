using System;
using System.Collections;
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
    public const int InventoryItemSellPrice = 3;
    private static readonly Color UnaffordableCostColor = Color.red;

    [Header("References")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private TMP_Text myBulletCountText;

    [Header("Bullet Offers")]
    [Tooltip("The configured candidate pool. Offers are drawn without replacement up to the number of connected slots.")]
    [SerializeField] private List<BulletData> bulletPool = new List<BulletData>();
    [SerializeField] private List<BulletGradeWeightData> gradeWeights =
        new List<BulletGradeWeightData>();
    [SerializeField] private List<ShopBulletSlot> slots =
        new List<ShopBulletSlot>();

    [Header("Bullet Offer Frames")]
    [SerializeField] private Sprite normalBulletFrame;
    [SerializeField] private Sprite rareBulletFrame;
    [SerializeField] private Sprite aceBulletFrame;
    [SerializeField] private Sprite legendaryBulletFrame;

    [Header("Item Offers")]
    [Tooltip("Every unique item has the same appearance probability.")]
    [SerializeField] private List<ItemData> itemPool = new List<ItemData>();
    [SerializeField] private List<ShopItemSlot> itemSlots =
        new List<ShopItemSlot>();

    [Header("Refresh")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private TMP_Text refreshCostText;
    [Min(0)]
    [SerializeField] private int initialRefreshCost;
    [Min(0)]
    [SerializeField] private int refreshCostIncrease = 1;

    [Header("Refresh Animation")]
    [Min(0f)]
    [SerializeField] private float refreshHideDuration = 0.12f;
    [Min(0f)]
    [SerializeField] private float refreshRevealDuration = 0.18f;
    [Min(0f)]
    [SerializeField] private float refreshRevealInterval = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float refreshHiddenScale = 0.75f;
    [Min(1f)]
    [SerializeField] private float refreshOvershootScale = 1.08f;
    [Min(0f)]
    [SerializeField] private float refreshRevealOffset = 20f;

    [Header("Runtime State")]
    [SerializeField] private int currentRefreshCost;

    private readonly List<BulletData> currentOffers =
        new List<BulletData>();
    private readonly List<bool> purchasedBulletOffers = new List<bool>();
    private readonly List<UnityAction> slotClickActions =
        new List<UnityAction>();
    private readonly List<ItemData> currentItemOffers =
        new List<ItemData>();
    private readonly List<bool> purchasedItemOffers = new List<bool>();
    private readonly List<UnityAction> itemSlotClickActions =
        new List<UnityAction>();
    private UnityAction refreshClickAction;
    private bool isRefreshing;

    private sealed class OfferVisualState
    {
        public Button Button;
        public CanvasGroup CanvasGroup;
        public RectTransform RectTransform;
        public Vector3 BaseScale;
        public Vector2 BasePosition;
        public bool DesiredInteractable;
    }

    public event Action OffersChanged;
    public event Action PurchaseCompleted;

    public IReadOnlyList<BulletData> CurrentOffers => currentOffers;
    public IReadOnlyList<ItemData> CurrentItemOffers => currentItemOffers;
    public int CurrentRefreshCost => currentRefreshCost;
    public bool IsRefreshing => isRefreshing;
    public bool CanSellInventoryItems => stateManager != null
        && stateManager.CurrentState == GameFlowState.Shop;

    public BulletData ResolveSavedBullet(RunBulletSaveData savedBullet)
    {
        if (savedBullet == null)
        {
            return null;
        }

        foreach (BulletData bulletData in bulletPool)
        {
            if (bulletData != null && string.Equals(
                    bulletData.name,
                    savedBullet.assetName,
                    StringComparison.Ordinal))
            {
                return bulletData;
            }
        }

        foreach (BulletData bulletData in bulletPool)
        {
            if (bulletData != null && !string.IsNullOrWhiteSpace(
                    savedBullet.bulletId)
                && string.Equals(
                    bulletData.BulletId,
                    savedBullet.bulletId,
                    StringComparison.Ordinal))
            {
                return bulletData;
            }
        }

        return null;
    }

    public ItemData ResolveSavedItem(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        foreach (ItemData itemData in itemPool)
        {
            if (itemData != null && string.Equals(
                    itemData.name,
                    assetName,
                    StringComparison.Ordinal))
            {
                return itemData;
            }
        }

        return null;
    }

    public void RestoreRunState(int savedRefreshCost)
    {
        currentRefreshCost = Mathf.Max(0, savedRefreshCost);
        ClearOffers();
        RefreshRefreshButton();
    }

    public void CaptureRunState(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.shop ??= new RunShopSaveData();
        saveData.shop.bulletOfferAssetNames.Clear();
        saveData.shop.purchasedBulletOffers.Clear();
        saveData.shop.itemOfferAssetNames.Clear();
        saveData.shop.purchasedItemOffers.Clear();

        for (int index = 0; index < currentOffers.Count; index++)
        {
            BulletData offer = currentOffers[index];
            saveData.shop.bulletOfferAssetNames.Add(
                offer == null ? string.Empty : offer.name);
            saveData.shop.purchasedBulletOffers.Add(
                index < purchasedBulletOffers.Count
                && purchasedBulletOffers[index]);
        }

        for (int index = 0; index < currentItemOffers.Count; index++)
        {
            ItemData offer = currentItemOffers[index];
            saveData.shop.itemOfferAssetNames.Add(
                offer == null ? string.Empty : offer.name);
            saveData.shop.purchasedItemOffers.Add(
                index < purchasedItemOffers.Count
                && purchasedItemOffers[index]);
        }
    }

    public bool RestoreShopRunState(
        RunShopSaveData savedShop,
        int savedRefreshCost)
    {
        if (savedShop == null)
        {
            return false;
        }

        StopAllCoroutines();
        isRefreshing = false;
        currentRefreshCost = Mathf.Max(0, savedRefreshCost);
        currentOffers.Clear();
        purchasedBulletOffers.Clear();
        currentItemOffers.Clear();
        purchasedItemOffers.Clear();

        if (savedShop.bulletOfferAssetNames != null)
        {
            for (int index = 0;
                 index < savedShop.bulletOfferAssetNames.Count;
                 index++)
            {
                BulletData offer = ResolveBulletByAssetName(
                    savedShop.bulletOfferAssetNames[index]);

                if (offer == null)
                {
                    return false;
                }

                currentOffers.Add(offer);
                purchasedBulletOffers.Add(
                    savedShop.purchasedBulletOffers != null
                    && index < savedShop.purchasedBulletOffers.Count
                    && savedShop.purchasedBulletOffers[index]);
            }
        }

        if (savedShop.itemOfferAssetNames != null)
        {
            for (int index = 0;
                 index < savedShop.itemOfferAssetNames.Count;
                 index++)
            {
                ItemData offer = ResolveSavedItem(
                    savedShop.itemOfferAssetNames[index]);

                if (offer == null)
                {
                    return false;
                }

                currentItemOffers.Add(offer);
                purchasedItemOffers.Add(
                    savedShop.purchasedItemOffers != null
                    && index < savedShop.purchasedItemOffers.Count
                    && savedShop.purchasedItemOffers[index]);
            }
        }

        RefreshSlots();
        RefreshItemSlots();
        RefreshRefreshButton();
        RefreshOwnedBulletCount();
        OffersChanged?.Invoke();
        return true;
    }

    private BulletData ResolveBulletByAssetName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        foreach (BulletData bulletData in bulletPool)
        {
            if (bulletData != null && string.Equals(
                    bulletData.name,
                    assetName,
                    StringComparison.Ordinal))
            {
                return bulletData;
            }
        }

        return null;
    }

    private void Awake()
    {
        ResolveReferences();
        BindSlotButtons();
        BindItemSlotButtons();
        BindRefreshButton();
        currentRefreshCost = Mathf.Max(0, initialRefreshCost);
        ClearOffers();

        if (currencyManager != null)
        {
            currencyManager.MoneyChanged += HandleMoneyChanged;
        }

        if (deckManager != null)
        {
            deckManager.StateChanged += HandleDeckStateChanged;
        }

        RefreshRefreshButton();
        RefreshOwnedBulletCount();

        if (playerInventory != null)
        {
            playerInventory.Changed += RefreshItemSlots;
        }
    }

    private void OnDestroy()
    {
        UnbindSlotButtons();
        UnbindItemSlotButtons();
        UnbindRefreshButton();

        if (currencyManager != null)
        {
            currencyManager.MoneyChanged -= HandleMoneyChanged;
        }

        if (deckManager != null)
        {
            deckManager.StateChanged -= HandleDeckStateChanged;
        }

        if (playerInventory != null)
        {
            playerInventory.Changed -= RefreshItemSlots;
        }
    }

    public void OpenShop()
    {
        currencyManager?.FlushPendingMoney();
        ResetOfferButtonsForNewVisit();
        GenerateOffers();
        GenerateItemOffers();
        RefreshRefreshButton();
        RefreshOwnedBulletCount();
    }

    public bool TrySellInventoryItem(int slotIndex)
    {
        if (!CanSellInventoryItems || playerInventory == null
            || currencyManager == null
            || !playerInventory.TryRemove(slotIndex))
        {
            return false;
        }

        currencyManager.AddMoney(InventoryItemSellPrice);
        return true;
    }

    public bool TryRefreshOffers()
    {
        if (isRefreshing || !isActiveAndEnabled || currencyManager == null
            || currencyManager.CurrentMoney < currentRefreshCost)
        {
            RefreshRefreshButton();
            return false;
        }

        isRefreshing = true;

        if (!currencyManager.TrySpendMoney(currentRefreshCost))
        {
            isRefreshing = false;
            RefreshRefreshButton();
            return false;
        }

        GameStatistics.RecordGoldSpent(currentRefreshCost);
        StartCoroutine(RefreshOffersSequence());
        RefreshRefreshButton();
        return true;
    }

    private IEnumerator RefreshOffersSequence()
    {
        List<OfferVisualState> visuals = BuildOfferVisualStates();
        SetOfferVisualInput(visuals, false);

        float elapsed = 0f;

        while (elapsed < refreshHideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = refreshHideDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / refreshHideDuration);
            float eased = SmoothStep(progress);

            foreach (OfferVisualState visual in visuals)
            {
                visual.CanvasGroup.alpha = 1f - eased;
                visual.RectTransform.localScale = visual.BaseScale
                    * Mathf.Lerp(1f, refreshHiddenScale, eased);
            }

            yield return null;
        }

        foreach (OfferVisualState visual in visuals)
        {
            visual.CanvasGroup.alpha = 0f;
            visual.RectTransform.localScale = visual.BaseScale
                * refreshHiddenScale;
        }

        ResetOfferButtonsForNewVisit();
        GenerateOffers();
        GenerateItemOffers();

        long nextCost = (long)currentRefreshCost
            + Mathf.Max(0, refreshCostIncrease);
        currentRefreshCost = (int)Math.Min(int.MaxValue, nextCost);
        RefreshRefreshButton();

        Canvas.ForceUpdateCanvases();

        foreach (OfferVisualState visual in visuals)
        {
            visual.DesiredInteractable = visual.Button.interactable;
            visual.Button.interactable = false;
            visual.BasePosition = visual.RectTransform.anchoredPosition;
            visual.RectTransform.anchoredPosition = visual.BasePosition
                + Vector2.down * refreshRevealOffset;
        }

        visuals.Sort(CompareOfferVisualPositions);
        Vector3 costTextScale = refreshCostText == null
            ? Vector3.one
            : refreshCostText.rectTransform.localScale;

        if (refreshCostText != null)
        {
            refreshCostText.rectTransform.localScale = costTextScale * 1.15f;
        }

        float revealTotalDuration = refreshRevealDuration
            + refreshRevealInterval * Mathf.Max(0, visuals.Count - 1);
        elapsed = 0f;

        while (elapsed < revealTotalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int index = 0; index < visuals.Count; index++)
            {
                OfferVisualState visual = visuals[index];

                if (!visual.Button.gameObject.activeSelf)
                {
                    continue;
                }

                float localElapsed = elapsed - refreshRevealInterval * index;
                float progress = refreshRevealDuration <= 0f
                    ? (localElapsed >= 0f ? 1f : 0f)
                    : Mathf.Clamp01(localElapsed / refreshRevealDuration);
                float eased = SmoothStep(progress);
                visual.CanvasGroup.alpha = eased;
                visual.RectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    visual.BasePosition + Vector2.down * refreshRevealOffset,
                    visual.BasePosition,
                    eased);
                visual.RectTransform.localScale = visual.BaseScale
                    * EvaluateRevealScale(progress);
            }

            if (refreshCostText != null)
            {
                float costProgress = Mathf.Clamp01(elapsed / 0.16f);
                refreshCostText.rectTransform.localScale = costTextScale
                    * Mathf.Lerp(1.15f, 1f, SmoothStep(costProgress));
            }

            yield return null;
        }

        foreach (OfferVisualState visual in visuals)
        {
            visual.CanvasGroup.alpha = 1f;
            visual.CanvasGroup.blocksRaycasts = true;
            visual.CanvasGroup.interactable = true;
            visual.RectTransform.localScale = visual.BaseScale;
            visual.RectTransform.anchoredPosition = visual.BasePosition;
            visual.Button.interactable = visual.DesiredInteractable;
        }

        if (refreshCostText != null)
        {
            refreshCostText.rectTransform.localScale = costTextScale;
        }

        isRefreshing = false;
        RefreshRefreshButton();
    }

    private List<OfferVisualState> BuildOfferVisualStates()
    {
        List<OfferVisualState> visuals = new List<OfferVisualState>();
        HashSet<Button> collectedButtons = new HashSet<Button>();

        foreach (ShopBulletSlot slot in slots)
        {
            AddOfferVisualState(slot?.Button, visuals, collectedButtons);
        }

        foreach (ShopItemSlot slot in itemSlots)
        {
            AddOfferVisualState(slot?.Button, visuals, collectedButtons);
        }

        return visuals;
    }

    private static void AddOfferVisualState(
        Button button,
        List<OfferVisualState> visuals,
        HashSet<Button> collectedButtons)
    {
        if (button == null || !collectedButtons.Add(button))
        {
            return;
        }

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform rectTransform = button.transform as RectTransform;

        if (rectTransform == null)
        {
            return;
        }

        visuals.Add(new OfferVisualState
        {
            Button = button,
            CanvasGroup = canvasGroup,
            RectTransform = rectTransform,
            BaseScale = rectTransform.localScale,
            BasePosition = rectTransform.anchoredPosition,
            DesiredInteractable = button.interactable
        });
    }

    private static void SetOfferVisualInput(
        List<OfferVisualState> visuals,
        bool enabled)
    {
        foreach (OfferVisualState visual in visuals)
        {
            visual.Button.interactable = enabled
                && visual.DesiredInteractable;
            visual.CanvasGroup.blocksRaycasts = enabled;
            visual.CanvasGroup.interactable = enabled;
        }
    }

    private static int CompareOfferVisualPositions(
        OfferVisualState left,
        OfferVisualState right)
    {
        int horizontalComparison = left.RectTransform.position.x.CompareTo(
            right.RectTransform.position.x);

        return horizontalComparison != 0
            ? horizontalComparison
            : right.RectTransform.position.y.CompareTo(
                left.RectTransform.position.y);
    }

    private float EvaluateRevealScale(float progress)
    {
        const float overshootPoint = 0.7f;

        if (progress <= overshootPoint)
        {
            float firstProgress = SmoothStep(progress / overshootPoint);
            return Mathf.Lerp(
                refreshHiddenScale,
                refreshOvershootScale,
                firstProgress);
        }

        float settleProgress = SmoothStep(
            (progress - overshootPoint) / (1f - overshootPoint));
        return Mathf.Lerp(refreshOvershootScale, 1f, settleProgress);
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void ResetOfferButtonsForNewVisit()
    {
        foreach (ShopBulletSlot slot in slots)
        {
            if (slot?.Button != null)
            {
                slot.Button.gameObject.SetActive(true);
                slot.Button.interactable = true;
            }
        }

        foreach (ShopItemSlot slot in itemSlots)
        {
            if (slot?.Button != null)
            {
                slot.Button.gameObject.SetActive(true);
                slot.Button.interactable = true;
            }
        }

        purchasedItemOffers.Clear();
        purchasedBulletOffers.Clear();
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
            || !deckManager.CanAddBullet(bulletData)
            || !currencyManager.TrySpendMoney(bulletData.Price))
        {
            return false;
        }

        if (!deckManager.TryAddBullet(bulletData))
        {
            currencyManager.AddMoney(bulletData.Price);
            return false;
        }

        GameStatistics.RecordGoldSpent(bulletData.Price);
        purchasedBulletOffers[slotIndex] = true;
        RefreshSlots();
        OffersChanged?.Invoke();
        SoundManager.PlaySfx("SFX_GainGold");
        PurchaseCompleted?.Invoke();
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

        GameStatistics.RecordGoldSpent(itemData.Price);
        purchasedItemOffers[slotIndex] = true;
        RefreshItemSlots();
        OffersChanged?.Invoke();
        SoundManager.PlaySfx("SFX_GainGold");
        PurchaseCompleted?.Invoke();
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
        purchasedBulletOffers.Clear();

        int offerCount = Mathf.Min(slots.Count, candidates.Count);

        for (int slotIndex = 0; slotIndex < offerCount; slotIndex++)
        {
            int candidateIndex = SelectWeightedCandidateIndex(candidates);

            if (candidateIndex < 0)
            {
                break;
            }

            currentOffers.Add(candidates[candidateIndex]);
            purchasedBulletOffers.Add(false);
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
            bool purchased = slotIndex < purchasedBulletOffers.Count
                && purchasedBulletOffers[slotIndex];
            RefreshSlot(slots[slotIndex], offer, purchased);
        }
    }

    private void RefreshSlot(
        ShopBulletSlot slot,
        BulletData offer,
        bool purchased)
    {
        if (slot == null)
        {
            return;
        }

        bool canAfford = offer != null && currencyManager != null
            && currencyManager.CurrentMoney >= offer.Price;

        if (slot.Button != null)
        {
            slot.Button.gameObject.SetActive(offer != null);
            slot.Button.interactable = offer != null && !purchased
                && canAfford
                && (deckManager == null || deckManager.CanAddBullet(offer));

            if (offer != null && slot.Button.image != null)
            {
                Sprite gradeFrame = GetBulletFrame(offer.Grade);

                if (gradeFrame != null)
                {
                    slot.Button.image.sprite = gradeFrame;
                }
            }
        }

        if (slot.BulletIcon != null)
        {
            slot.BulletIcon.gameObject.SetActive(offer != null);
            slot.BulletIcon.sprite = offer == null ? null : offer.CylinderIcon;
            slot.BulletIcon.enabled = offer != null
                && offer.CylinderIcon != null;
            slot.BulletIcon.color = Color.white;
            slot.BulletIcon.preserveAspect = true;
        }

        if (slot.CostText != null)
        {
            slot.CostText.text = offer == null ? string.Empty : $"${offer.Price}";
            slot.CostText.color = offer != null && !purchased && !canAfford
                ? UnaffordableCostColor
                : Color.white;
        }
    }

    private Sprite GetBulletFrame(BulletGrade grade)
    {
        return grade switch
        {
            BulletGrade.Normal => normalBulletFrame,
            BulletGrade.Rare => rareBulletFrame,
            BulletGrade.Ace => aceBulletFrame,
            BulletGrade.Legendary => legendaryBulletFrame,
            _ => normalBulletFrame
        };
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

        bool canAfford = offer != null && currencyManager != null
            && currencyManager.CurrentMoney >= offer.Price;

        if (slot.Button != null)
        {
            slot.Button.gameObject.SetActive(offer != null);
            slot.Button.interactable = offer != null && !purchased
                && canAfford
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
            slot.CostText.color = offer != null && !purchased && !canAfford
                ? UnaffordableCostColor
                : Color.white;
        }
    }

    private void ClearOffers()
    {
        currentOffers.Clear();
        purchasedBulletOffers.Clear();
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

    private void BindRefreshButton()
    {
        if (refreshButton == null)
        {
            return;
        }

        refreshClickAction = () => TryRefreshOffers();
        refreshButton.onClick.AddListener(refreshClickAction);
    }

    private void UnbindRefreshButton()
    {
        if (refreshButton != null && refreshClickAction != null)
        {
            refreshButton.onClick.RemoveListener(refreshClickAction);
        }

        refreshClickAction = null;
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshRefreshButton();
        RefreshSlots();
        RefreshItemSlots();
    }

    private void HandleDeckStateChanged()
    {
        RefreshOwnedBulletCount();
        RefreshSlots();
    }

    private void RefreshOwnedBulletCount()
    {
        if (myBulletCountText == null)
        {
            return;
        }

        int bulletCount = deckManager == null
            ? 0
            : deckManager.TotalBulletCount;
        myBulletCountText.text = $"탄환 보유 개수: {bulletCount}/"
            + DeckManager.MaximumOwnedBulletCount;
    }

    private void RefreshRefreshButton()
    {
        if (refreshButton != null)
        {
            refreshButton.interactable = currencyManager != null
                && !isRefreshing
                && currencyManager.CurrentMoney >= currentRefreshCost;
        }

        if (refreshCostText != null)
        {
            refreshCostText.text =
                $"새로고침 비용: ${currentRefreshCost}";
        }
    }

    private void ResolveReferences()
    {
        currencyManager ??= FindSceneObject<CurrencyManager>();
        deckManager ??= FindSceneObject<DeckManager>();
        playerInventory ??= FindSceneObject<PlayerInventory>();
        stateManager ??= FindSceneObject<StateManager>();

        ResolveRefreshButton();
        ResolveBulletCountText();

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

    private void ResolveRefreshButton()
    {
        if (refreshButton == null)
        {
            Button[] allButtons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Button button in allButtons)
            {
                if (button != null && button.gameObject.scene.IsValid()
                    && button.name == "Button | Refresh"
                    && HasNamedAncestor(button.transform, "Panel | Shop"))
                {
                    refreshButton = button;
                    break;
                }
            }
        }

        if (refreshCostText == null)
        {
            foreach (TMP_Text text in FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (text != null
                    && text.gameObject.scene.IsValid()
                    && text.name == "Text | Refresh Cost"
                    && HasNamedAncestor(text.transform, "Panel | Shop"))
                {
                    refreshCostText = text;
                    break;
                }
            }
        }
    }

    private void ResolveBulletCountText()
    {
        if (myBulletCountText != null)
        {
            return;
        }

        TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (TMP_Text textComponent in allTexts)
        {
            if (textComponent != null
                && textComponent.gameObject.scene.IsValid()
                && textComponent.name == "Text | My Bullet Count"
                && HasNamedAncestor(textComponent.transform, "Panel | Shop"))
            {
                myBulletCountText = textComponent;
                break;
            }
        }
    }

    private static bool HasNamedAncestor(Transform transform, string objectName)
    {
        Transform current = transform == null ? null : transform.parent;

        while (current != null)
        {
            if (current.name == objectName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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
