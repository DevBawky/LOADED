using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private RelicTooltipUI relicTooltip;
    private bool ownsClosedChestSprite;
    private bool ownsOpenedChestSprite;
    private bool initialized;
    private bool leaving;

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeUi();
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
        DestroyGeneratedSprite(closedChestSprite, ownsClosedChestSprite);
        DestroyGeneratedSprite(openedChestSprite, ownsOpenedChestSprite);
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
        ShowOpenedChest();
        PopulateOffers();
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
            if (instructionText != null)
            {
                instructionText.text = result == RelicAcquireResult.InventoryFull
                    ? "유물 보관함이 가득 찼습니다."
                    : "이 유물을 획득할 수 없습니다.";
            }
            return;
        }

        runData.treasureChoiceResolved = true;
        if (instructionText != null)
        {
            instructionText.text = $"{selected.DisplayName}을(를) 획득했습니다.";
        }
        SetChoiceButtonsInteractable(false);
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
            continueButton.transform.SetAsLastSibling();
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

        int cumulativeCount = Mathf.Max(
            0,
            runData.cumulativeBattleTurnCount);
        foreach (TurnCountText countText in FindObjectsByType<TurnCountText>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            countText.SetExternalCount(cumulativeCount);
        }

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
            if (instructionText != null)
            {
                instructionText.text = "획득할 수 있는 유물이 없습니다.";
            }
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.transform.SetAsLastSibling();
            }
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
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.transform.SetAsLastSibling();
            }
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
            if (button == null)
            {
                Debug.LogWarning(
                    $"Treasure relic choice button {index + 1} is missing.",
                    this);
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

            TreasureRelicChoiceUI interaction =
                button.GetComponent<TreasureRelicChoiceUI>();
            interaction ??= button.gameObject.AddComponent<
                TreasureRelicChoiceUI>();
            TMP_Text fontSource = index < relicNames.Length
                ? relicNames[index]
                : null;
            relicTooltip ??= RelicTooltipUI.GetOrCreate(button, fontSource);
            interaction.Initialize(relicTooltip, relic);
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
        choicesPanel ??= FindNamed<RectTransform>("Panel | Relic Choice")
            ?.gameObject;
        choicesPanel ??= FindNamed<RectTransform>("Panel | Relic Choices")
            ?.gameObject;
        instructionText ??= FindNamed<TMP_Text>("Text | Treasure Instruction");
        continueButton ??= FindNamed<Button>("Button | Treasure Continue");

        relicButtons = FindRelicButtons(choicesPanel);
        relicIcons = FindIndexed<Image>("Image | Relic Icon ");
        relicNames = FindIndexed<TMP_Text>("Text | Relic Name ");
        relicDescriptions = FindIndexed<TMP_Text>("Text | Relic Description ");
        for (int index = 0; index < relicButtons.Length; index++)
        {
            if (relicIcons[index] == null && relicButtons[index] != null)
            {
                relicIcons[index] = relicButtons[index].image;
            }
        }
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

    private void EnsureRuntimeUi()
    {
        Transform parent = choicesPanel == null
            ? FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)
                ?.rootCanvas.transform
            : choicesPanel.transform.parent;
        if (parent == null)
        {
            return;
        }

        if (chestButton == null)
        {
            chestButton = CreateRuntimeButton(
                "Button | Treasure Chest",
                parent,
                new Vector2(0.38f, 0.28f),
                new Vector2(0.62f, 0.72f),
                "보물 상자");
            chestImage = chestButton.image;
            chestLabel = chestButton.GetComponentInChildren<TMP_Text>(true);
            if (choicesPanel != null)
            {
                chestButton.transform.SetSiblingIndex(
                    choicesPanel.transform.GetSiblingIndex());
            }
        }

        if (continueButton == null)
        {
            continueButton = CreateRuntimeButton(
                "Button | Treasure Continue",
                parent,
                new Vector2(0.42f, 0.07f),
                new Vector2(0.58f, 0.15f),
                "노드맵으로");
            continueButton.gameObject.SetActive(false);
        }

        if (closedChestSprite == null)
        {
            closedChestSprite = CreateFallbackChestSprite(false);
            ownsClosedChestSprite = true;
        }
        if (openedChestSprite == null)
        {
            openedChestSprite = CreateFallbackChestSprite(true);
            ownsOpenedChestSprite = true;
        }
    }

    private static Button[] FindRelicButtons(GameObject panel)
    {
        Button[] results = new Button[RewardChoiceCount];

        // The current Treasure scene renames the three prefab buttons through
        // scene overrides. Resolve those final scene names first so an inactive
        // choice panel does not leave the controller with an empty button list.
        for (int index = 0; index < results.Length; index++)
        {
            int number = index + 1;
            results[index] = FindNamed<Button>($"Button | Relic {number}");
            results[index] ??= FindNamed<Button>(
                $"Button | Relic Choice {number}");
        }
        if (Array.TrueForAll(results, button => button != null))
        {
            return results;
        }

        List<Button> candidates = new List<Button>();
        Button[] availableButtons = panel == null
            ? FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            : panel.GetComponentsInChildren<Button>(true);
        foreach (Button button in availableButtons)
        {
            if (button != null && button.name.StartsWith(
                    "Button | Relic",
                    StringComparison.Ordinal)
                && button.name != "Button | Relic Dictionary")
            {
                candidates.Add(button);
            }
        }
        candidates.Sort((left, right) => left.transform.GetSiblingIndex()
            .CompareTo(right.transform.GetSiblingIndex()));
        for (int index = 0; index < results.Length && index < candidates.Count;
             index++)
        {
            results[index] ??= candidates[index];
        }
        return results;
    }

    private static Button CreateRuntimeButton(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject(
            objectName == "Button | Treasure Chest"
                ? "Text | Chest Label"
                : "Text | Continue",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = buttonObject.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = new Vector2(0.05f, 0.05f);
        labelRect.anchorMax = new Vector2(0.95f, 0.95f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.86f, 0.48f, 1f);
        text.raycastTarget = false;
        return button;
    }

    private static Sprite CreateFallbackChestSprite(bool opened)
    {
        const int width = 32;
        const int height = 24;
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false)
        {
            name = opened
                ? "Generated Treasure Chest Open"
                : "Generated Treasure Chest Closed",
            filterMode = FilterMode.Point
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color outline = new Color(0.16f, 0.07f, 0.025f, 1f);
        Color wood = new Color(0.52f, 0.23f, 0.06f, 1f);
        Color gold = new Color(0.92f, 0.64f, 0.12f, 1f);
        Color[] pixels = new Color[width * height];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = clear;
        }

        DrawRect(pixels, width, 3, 2, 29, 13, outline);
        DrawRect(pixels, width, 5, 4, 27, 12, wood);
        DrawRect(pixels, width, 14, 2, 18, 13, gold);
        if (opened)
        {
            DrawRect(pixels, width, 4, 15, 28, 19, outline);
            DrawRect(pixels, width, 6, 17, 27, 22, wood);
            DrawRect(pixels, width, 14, 17, 18, 22, gold);
        }
        else
        {
            DrawRect(pixels, width, 3, 13, 29, 20, outline);
            DrawRect(pixels, width, 5, 14, 27, 19, wood);
            DrawRect(pixels, width, 14, 14, 18, 20, gold);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            32f);
    }

    private static void DrawRect(
        Color[] pixels,
        int width,
        int minX,
        int minY,
        int maxX,
        int maxY,
        Color color)
    {
        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                pixels[y * width + x] = color;
            }
        }
    }

    private static void DestroyGeneratedSprite(Sprite sprite, bool owned)
    {
        if (!owned || sprite == null)
        {
            return;
        }
        Texture texture = sprite.texture;
        Destroy(sprite);
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}

[DisallowMultipleComponent]
public sealed class TreasureRelicChoiceUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    private RelicTooltipUI tooltip;
    private RelicData relic;

    public void Initialize(RelicTooltipUI value, RelicData relicData)
    {
        tooltip = value;
        relic = relicData;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltip?.Show(relic, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide(relic);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        tooltip?.Move(relic, eventData.position);
    }
}
