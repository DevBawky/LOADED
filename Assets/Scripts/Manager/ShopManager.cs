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

public class ShopManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private DeckManager deckManager;

    [Header("Offers")]
    [Tooltip("The configured candidate pool. Offers are drawn without replacement up to the number of connected slots.")]
    [SerializeField] private List<BulletData> bulletPool = new List<BulletData>();
    [SerializeField] private List<BulletGradeWeightData> gradeWeights =
        new List<BulletGradeWeightData>();
    [SerializeField] private List<ShopBulletSlot> slots =
        new List<ShopBulletSlot>();

    private readonly List<BulletData> currentOffers =
        new List<BulletData>();
    private readonly List<UnityAction> slotClickActions =
        new List<UnityAction>();

    public event Action OffersChanged;

    public IReadOnlyList<BulletData> CurrentOffers => currentOffers;

    private void Awake()
    {
        BindSlotButtons();
        ClearOffers();
    }

    private void OnDestroy()
    {
        UnbindSlotButtons();
    }

    public void OpenShop()
    {
        GenerateOffers();
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
        slot.Button.gameObject.SetActive(false);
        OffersChanged?.Invoke();
        return true;
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
            BulletGrade.Common => 100f,
            BulletGrade.Uncommon => 60f,
            BulletGrade.Rare => 25f,
            BulletGrade.Epic => 10f,
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

    private void ClearOffers()
    {
        currentOffers.Clear();
        RefreshSlots();
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
}
