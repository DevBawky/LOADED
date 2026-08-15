using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class TreasureSceneController : MonoBehaviour
{
    private const string NodeMapSceneName = "NodeMap";
    private const int RewardChoiceCount = 3;

    [Header("Scene-local Managers")]
    [SerializeField] private ShopManager dataResolver;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RelicManager relicManager;
    [SerializeField] private StateManager stateManager;

    [Header("Chest")]
    [SerializeField] private Button chestButton;
    [SerializeField] private Image chestImage;
    [SerializeField] private Sprite closedChestSprite;
    [SerializeField] private Sprite openedChestSprite;
    [SerializeField] private TMP_Text chestLabel;

    [Header("Relic Choices")]
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private Button[] relicButtons = Array.Empty<Button>();
    [SerializeField] private Image[] relicIcons = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] relicNames = Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] relicDescriptions =
        Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button continueButton;

    [Header("Fallback Colors")]
    [SerializeField] private Color closedChestColor =
        new Color(0.42f, 0.24f, 0.10f, 1f);
    [SerializeField] private Color openedChestColor =
        new Color(0.72f, 0.48f, 0.16f, 1f);

    private readonly List<RelicData> offers = new List<RelicData>();
    private RunSaveData runData;
    private bool initialized;
    private bool leaving;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        ShowClosedChest();
    }

    private void Start()
    {
        if (!TryInitialize())
        {
            Debug.LogError(
                "Treasure scene could not restore the current run.",
                this);
            ShowUnavailableState();
        }
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void OnDisable()
    {
        if (!leaving)
        {
            SaveTreasureState();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveTreasureState();
        }
    }

    public void OpenChest()
    {
        if (!initialized || leaving || runData.treasureChestOpened)
        {
            return;
        }

        runData.treasureChestOpened = true;
        ResolveReferences();
        PopulateOffers();
        ShowOpenedChest();
        SaveTreasureState();
    }

    public void SelectRelic(int index)
    {
        if (!initialized || leaving || runData.treasureChoiceResolved
            || index < 0 || index >= offers.Count)
        {
            return;
        }

        RelicData selected = offers[index];
        RelicAcquireResult result = relicManager.TryAcquire(selected);
        if (result != RelicAcquireResult.Acquired
            && result != RelicAcquireResult.Stacked)
        {
            instructionText.text = result == RelicAcquireResult.InventoryFull
                ? "유물 보관함이 가득 찼습니다."
                : "이 유물을 획득할 수 없습니다.";
            return;
        }

        runData.treasureChoiceResolved = true;
        instructionText.text = $"{selected.DisplayName}을(를) 획득했습니다.";
        SetChoiceButtonsInteractable(false);
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
        }

        SaveTreasureState();
    }

    public void ReturnToNodeMap()
    {
        if (leaving || !initialized || !runData.treasureChoiceResolved)
        {
            return;
        }

        leaving = true;
        SaveTreasureState();
        NodeMapSaveSystem.CompleteActiveNode();
        runData.treasureVisitActive = false;
        runData.treasureChestOpened = false;
        runData.treasureChoiceResolved = false;
        runData.treasureOfferRelicIds.Clear();
        RunSaveSystem.Save(runData);

        if (!LoadingTransitionController.LoadScene(NodeMapSceneName))
        {
            SceneManager.LoadScene(NodeMapSceneName);
        }
    }

    private bool TryInitialize()
    {
        if (dataResolver == null || deckManager == null
            || currencyManager == null || playerInventory == null
            || relicManager == null || !RunSaveSystem.TryLoad(out runData)
            || !deckManager.RestoreRunState(
                runData.bullets,
                dataResolver.ResolveSavedBullet,
                runData.paidBulletRemovalCount,
                runData.nextCycleAcquisitionOrders)
            || !relicManager.RestoreRunState(runData.relics))
        {
            return false;
        }

        currencyManager.RestoreRunMoney(runData.money);
        playerInventory.RestoreRunState(
            runData.inventoryItemAssetNames,
            dataResolver.ResolveSavedItem);
        stateManager?.ConfigureExternalSceneState(
            runData.stageIndex,
            runData.battleIndex,
            GameFlowState.Treasure);

        bool resumeVisit = runData.treasureVisitActive
            && runData.flowState == (int)GameFlowState.Treasure
            && runData.treasureOfferRelicIds.Count > 0;

        if (resumeVisit)
        {
            foreach (string relicId in runData.treasureOfferRelicIds)
            {
                RelicData relic = relicManager.ResolveRelicData(relicId);
                if (relic != null)
                {
                    offers.Add(relic);
                }
            }
        }
        else
        {
            relicManager.GetUniformRewardChoices(RewardChoiceCount, offers);
            runData.treasureVisitActive = true;
            runData.treasureChestOpened = false;
            runData.treasureChoiceResolved = false;
            runData.treasureOfferRelicIds.Clear();
            foreach (RelicData offer in offers)
            {
                runData.treasureOfferRelicIds.Add(offer.Id);
            }
        }

        initialized = true;
        PopulateOffers();
        if (offers.Count == 0)
        {
            runData.treasureChoiceResolved = true;
            ShowOpenedChest();
            instructionText.text = "획득할 수 있는 유물이 없습니다.";
            continueButton.gameObject.SetActive(true);
        }
        else if (runData.treasureChestOpened)
        {
            ShowOpenedChest();
        }
        else
        {
            ShowClosedChest();
        }

        if (runData.treasureChoiceResolved)
        {
            SetChoiceButtonsInteractable(false);
            continueButton.gameObject.SetActive(true);
        }

        SaveTreasureState();
        return true;
    }

    private void PopulateOffers()
    {
        for (int index = 0; index < relicButtons.Length; index++)
        {
            bool visible = index < offers.Count;
            Button button = relicButtons[index];
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                continue;
            }

            RelicData relic = offers[index];
            if (index < relicIcons.Length && relicIcons[index] != null)
            {
                relicIcons[index].sprite = relic.Icon;
                relicIcons[index].enabled = relic.Icon != null;
                relicIcons[index].preserveAspect = true;
            }
            if (index < relicNames.Length && relicNames[index] != null)
            {
                relicNames[index].text = relic.DisplayName;
            }
            if (index < relicDescriptions.Length
                && relicDescriptions[index] != null)
            {
                string effect = relic.BuildEffectSummary();
                relicDescriptions[index].text = string.IsNullOrWhiteSpace(effect)
                    ? relic.Description
                    : effect;
            }
        }
    }

    private void ShowClosedChest()
    {
        if (chestButton != null)
        {
            chestButton.gameObject.SetActive(true);
            chestButton.interactable = initialized;
        }
        ApplyChestVisual(false);
        if (choicesPanel != null)
        {
            choicesPanel.SetActive(false);
        }
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
        if (instructionText != null)
        {
            instructionText.text = "상자를 클릭해 여십시오.";
        }
    }

    private void ShowOpenedChest()
    {
        ApplyChestVisual(true);
        if (chestButton != null)
        {
            chestButton.interactable = false;
        }
        if (choicesPanel != null)
        {
            choicesPanel.SetActive(true);
            choicesPanel.transform.SetAsLastSibling();
        }
        SetChoiceButtonsInteractable(!runData.treasureChoiceResolved);
        Canvas.ForceUpdateCanvases();
        if (instructionText != null && !runData.treasureChoiceResolved)
        {
            instructionText.text = "가져갈 유물 하나를 선택하십시오.";
        }
    }

    private void ApplyChestVisual(bool opened)
    {
        if (chestImage != null)
        {
            Sprite sprite = opened ? openedChestSprite : closedChestSprite;
            chestImage.sprite = sprite;
            chestImage.color = sprite == null
                ? opened ? openedChestColor : closedChestColor
                : Color.white;
            chestImage.preserveAspect = true;
        }
        if (chestLabel != null)
        {
            chestLabel.text = opened ? "열린 보물 상자" : "보물 상자";
        }
    }

    private bool SaveTreasureState()
    {
        if (!initialized || runData == null)
        {
            return false;
        }

        currencyManager.FlushPendingMoney();
        runData.flowState = (int)GameFlowState.Treasure;
        runData.startSelectedBattleFresh = false;
        runData.money = currencyManager.CurrentMoney;
        runData.paidBulletRemovalCount = deckManager.PaidBulletRemovalCount;
        deckManager.CaptureRunState(
            runData.bullets,
            runData.nextCycleAcquisitionOrders);
        playerInventory.CaptureRunState(runData.inventoryItemAssetNames);
        relicManager.CaptureRunState(runData.relics);
        return RunSaveSystem.Save(runData);
    }

    private void ResolveReferences()
    {
        dataResolver ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
        deckManager ??= FindFirstObjectByType<DeckManager>(
            FindObjectsInactive.Include);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>(
            FindObjectsInactive.Include);
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        chestButton ??= FindNamed<Button>("Button | Treasure Chest");
        chestImage ??= chestButton == null
            ? null
            : chestButton.GetComponent<Image>();
        chestLabel ??= FindNamed<TMP_Text>("Text | Chest Label");
        choicesPanel ??= FindNamed<RectTransform>("Panel | Relic Choices")
            ?.gameObject;
        instructionText ??= FindNamed<TMP_Text>("Text | Treasure Instruction");
        continueButton ??= FindNamed<Button>("Button | Treasure Continue");

        relicButtons = FindIndexed<Button>("Button | Relic Choice ");
        relicIcons = FindIndexed<Image>("Image | Relic Icon ");
        relicNames = FindIndexed<TMP_Text>("Text | Relic Name ");
        relicDescriptions = FindIndexed<TMP_Text>("Text | Relic Description ");
    }

    private void BindButtons()
    {
        chestButton?.onClick.AddListener(OpenChest);
        for (int index = 0; index < relicButtons.Length; index++)
        {
            int captured = index;
            relicButtons[index]?.onClick.AddListener(
                () => SelectRelic(captured));
        }
        continueButton?.onClick.AddListener(ReturnToNodeMap);
    }

    private void UnbindButtons()
    {
        chestButton?.onClick.RemoveAllListeners();
        foreach (Button button in relicButtons)
        {
            button?.onClick.RemoveAllListeners();
        }
        continueButton?.onClick.RemoveAllListeners();
    }

    private void SetChoiceButtonsInteractable(bool value)
    {
        foreach (Button button in relicButtons)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }
    }

    private void ShowUnavailableState()
    {
        if (instructionText != null)
        {
            instructionText.text = "보물 데이터를 불러올 수 없습니다.";
        }
        if (chestButton != null)
        {
            chestButton.interactable = false;
        }
    }

    private static T FindNamed<T>(string objectName) where T : Component
    {
        foreach (T component in FindObjectsByType<T>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (component != null && component.gameObject.scene.IsValid()
                && component.name == objectName)
            {
                return component;
            }
        }
        return null;
    }

    private static T[] FindIndexed<T>(string prefix) where T : Component
    {
        T[] results = new T[RewardChoiceCount];
        foreach (T component in FindObjectsByType<T>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (component == null || !component.gameObject.scene.IsValid())
            {
                continue;
            }
            for (int index = 0; index < results.Length; index++)
            {
                if (component.name == prefix + (index + 1))
                {
                    results[index] = component;
                }
            }
        }
        return results;
    }
}
