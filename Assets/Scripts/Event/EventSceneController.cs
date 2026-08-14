using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class EventSceneController : MonoBehaviour
{
    private const string NodeMapSceneName = "NodeMap";

    [Header("Event Pool")]
    [Tooltip("비어 있으면 Resources/Events의 모든 EventDefinition을 사용합니다.")]
    [SerializeField] private EventDefinition[] eventPool =
        Array.Empty<EventDefinition>();

    [Header("Scene-local Managers")]
    [SerializeField] private ShopManager dataResolver;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private BulletManagementUI bulletSelectionUI;

    [Header("Event UI")]
    [SerializeField] private Transform eventCanvasRoot;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button[] choiceButtons = Array.Empty<Button>();
    [SerializeField] private TMP_Text[] choiceTexts = Array.Empty<TMP_Text>();

    [Header("Run HUD")]
    [SerializeField] private Image playerHealthFillImage;
    [SerializeField] private TMP_Text playerHealthText;

    [Header("Choice Text Colors")]
    [SerializeField] private Color actionNameColor =
        new Color(1f, 0.78f, 0.32f, 1f);
    [SerializeField] private Color upgradeKeywordColor =
        new Color(0.48f, 1f, 0.58f, 1f);
    [SerializeField] private Color removeKeywordColor =
        new Color(1f, 0.42f, 0.52f, 1f);
    [SerializeField] private Color freeKeywordColor =
        new Color(0.45f, 0.94f, 0.62f, 1f);
    [SerializeField] private Color costKeywordColor =
        new Color(1f, 0.82f, 0.34f, 1f);
    [SerializeField] private Color rewardNameColor =
        new Color(0.38f, 0.85f, 1f, 1f);
    [SerializeField] private Color unavailableReasonColor =
        new Color(1f, 0.28f, 0.28f, 1f);

    [Header("Labels")]
    [SerializeField] private string continueLabel = "계속";
    [TextArea] [SerializeField] private string fallbackOutcome =
        "선택의 결과가 길 위에 남았습니다.";

    private readonly List<BulletInstance> bulletBuffer =
        new List<BulletInstance>();
    private RunSaveData runData;
    private EventDefinition currentEvent;
    private EventChoiceData pendingChoice;
    private InventoryTooltipUI eventTooltipUI;
    private bool initialized;
    private bool leaving;
    private Coroutine initializationRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!initialized && initializationRoutine == null)
        {
            initializationRoutine = StartCoroutine(
                InitializeWhenSceneIsReady());
        }
    }

    private IEnumerator InitializeWhenSceneIsReady()
    {
        // Scene objects and managers can deserialize after this controller's
        // early Awake. Waiting one frame also makes this recover correctly
        // after an editor domain reload while the Event scene is open.
        yield return null;
        initializationRoutine = null;
        ResolveReferences();

        bool succeeded = false;
        try
        {
            succeeded = TryInitialize();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (!succeeded)
        {
            Debug.LogError(
                "Event scene could not initialize. See the preceding Event initialization error.",
                this);
            ShowFallbackReturn();
        }
    }

    private void OnDisable()
    {
        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }

        if (!leaving)
        {
            SaveEventState();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveEventState();
        }
    }

    private void OnApplicationQuit()
    {
        SaveEventState();
    }

    private bool TryInitialize()
    {
        if (dataResolver == null || deckManager == null
            || currencyManager == null || playerInventory == null)
        {
            Debug.LogError(
                "Event initialization is missing managers. "
                + $"Resolver={dataResolver != null}, "
                + $"Deck={deckManager != null}, "
                + $"Currency={currencyManager != null}, "
                + $"Inventory={playerInventory != null}.",
                this);
            return false;
        }

        if (eventCanvasRoot == null || titleText == null
            || dialogueText == null || choiceButtons == null
            || choiceButtons.Length == 0)
        {
            Debug.LogError(
                "Event initialization could not bind the Event UI. "
                + $"Canvas={eventCanvasRoot != null}, "
                + $"Title={titleText != null}, "
                + $"Dialogue={dialogueText != null}, "
                + $"Choices={choiceButtons?.Length ?? 0}.",
                this);
            return false;
        }

        if (!RunSaveSystem.TryLoad(out runData))
        {
            Debug.LogError(
                "Event initialization could not load a valid run save.",
                this);
            return false;
        }

        if (!deckManager.RestoreRunState(
                runData.bullets,
                dataResolver.ResolveSavedBullet,
                runData.paidBulletRemovalCount,
                runData.nextCycleAcquisitionOrders))
        {
            Debug.LogError(
                "Event initialization could not restore any saved bullets. "
                + "Check the ShopManager bullet pool.",
                this);
            return false;
        }

        currencyManager.RestoreRunMoney(runData.money);
        playerInventory.RestoreRunState(
            runData.inventoryItemAssetNames,
            dataResolver.ResolveSavedItem);
        stateManager?.ConfigureExternalSceneState(
            runData.stageIndex,
            runData.battleIndex,
            GameFlowState.Event);

        List<EventDefinition> availableEvents = BuildEventPool();
        currentEvent = availableEvents.FirstOrDefault(definition =>
            definition != null
            && definition.StableId == runData.activeEventId);

        if (currentEvent == null)
        {
            currentEvent = EventSelector.Select(
                availableEvents,
                CreateRunContext(),
                runData.completedEventIds);
            if (currentEvent == null)
            {
                Debug.LogError(
                    "Event initialization found no selectable EventDefinition. "
                    + $"Loaded candidates={availableEvents.Count}, "
                    + $"completed events={runData.completedEventIds.Count}. "
                    + "Populate Event Pool or place definitions under Resources/Events.",
                    this);
                return false;
            }

            runData.activeEventId = currentEvent.StableId;
            runData.eventChoiceResolved = false;
            runData.eventOutcomeText = string.Empty;
            runData.eventChoiceSelectionCounts.Clear();
            runData.eventChoiceFailureCounts.Clear();
        }

        EnsureChoiceProgressCapacity();

        ConfigureSharedPresentation();
        initialized = true;

        if (runData.eventChoiceResolved)
        {
            ShowOutcome(runData.eventOutcomeText);
        }
        else
        {
            ShowEventChoices();
        }

        SaveEventState();
        Debug.Log(
            $"Event initialized successfully: {currentEvent.StableId}",
            this);
        return true;
    }

    private List<EventDefinition> BuildEventPool()
    {
        IEnumerable<EventDefinition> source = eventPool != null
            && eventPool.Any(definition => definition != null)
                ? eventPool
                : Resources.LoadAll<EventDefinition>("Events");
        return source.Where(definition => definition != null)
            .GroupBy(definition => definition.StableId)
            .Select(group => group.First())
            .ToList();
    }

    private EventRunContext CreateRunContext()
    {
        int maxHealth = Mathf.Max(1, runData.maxHealth);
        return new EventRunContext(
            NodeMapSaveSystem.GetCompletedNodeCount(
                NodeMapNodeType.EliteBattle),
            NodeMapSaveSystem.GetCompletedNodeCount(NodeMapNodeType.Shop),
            NodeMapSaveSystem.GetCompletedNodeCount(NodeMapNodeType.Event),
            currencyManager.CurrentMoney,
            deckManager.OwnedBulletCount,
            runData.currentHealth * 100f / maxHealth,
            Mathf.Max(
                runData.cumulativeBattleTurnCount,
                runData.playerTurnCount));
    }

    private void ConfigureSharedPresentation()
    {
        RefreshHealthPresentation();

        if (artworkImage != null)
        {
            artworkImage.sprite = currentEvent.artwork;
            artworkImage.enabled = currentEvent.artwork != null;
            artworkImage.preserveAspect = true;
        }

        if (titleText != null)
        {
            titleText.text = currentEvent.displayName;
        }

        eventTooltipUI ??= eventCanvasRoot == null
            ? null
            : eventCanvasRoot.GetComponentInChildren<InventoryTooltipUI>(true);
        eventTooltipUI?.ConfigureEventScene(
            eventCanvasRoot,
            playerInventory,
            deckManager,
            currencyManager,
            stateManager);

        foreach (StageProgressUI progressUI in FindObjectsByType<StageProgressUI>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            progressUI.SetExternalStageTitle("마을. 이벤트");
        }

        int cumulativeTurns = Mathf.Max(
            runData.cumulativeBattleTurnCount,
            runData.playerTurnCount);
        foreach (TurnCountText turnText in FindObjectsByType<TurnCountText>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            turnText.SetExternalTurnCount(cumulativeTurns);
        }
    }

    private void ShowEventChoices()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrWhiteSpace(
                runData.eventOutcomeText)
                    ? currentEvent.dialogue
                    : runData.eventOutcomeText;
        }

        int choiceCount = Mathf.Min(
            3,
            currentEvent.choices == null ? 0 : currentEvent.choices.Length);
        for (int index = 0; index < choiceButtons.Length; index++)
        {
            Button button = choiceButtons[index];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            bool visible = index < choiceCount
                && currentEvent.choices[index] != null;
            button.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            EventChoiceData choice = currentEvent.choices[index];
            bool available = IsChoiceAvailable(choice, out string reason);
            button.interactable = available;
            TMP_Text label = index < choiceTexts.Length
                ? choiceTexts[index]
                : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.richText = true;
                label.text = FormatChoiceText(
                    choice,
                    available,
                    reason);
            }

            ConfigureChoiceRewardPreview(button, choice);
            button.onClick.AddListener(() => SelectChoice(choice));
            SoundManager.BindUiButtonSfx(button);
        }
    }

    private bool IsChoiceAvailable(
        EventChoiceData choice,
        out string unavailableReason)
    {
        unavailableReason = string.Empty;
        if (choice == null)
        {
            return false;
        }

        int choiceIndex = GetChoiceIndex(choice);
        int previousSelections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        if (choice.maximumSelections > 0
            && previousSelections >= choice.maximumSelections)
        {
            unavailableReason = string.IsNullOrWhiteSpace(
                choice.selectionLimitReason)
                    ? "더 이상 이 선택지를 고를 수 없습니다."
                    : choice.selectionLimitReason;
            return false;
        }

        IEnumerable<EventChoiceRequirement> requirements =
            choice.requirements ?? Array.Empty<EventChoiceRequirement>();
        foreach (EventChoiceRequirement requirement in requirements)
        {
            if (requirement == null)
            {
                continue;
            }

            bool valid = requirement.type switch
            {
                EventChoiceRequirementType.None => true,
                EventChoiceRequirementType.MoneyAtLeast =>
                    currencyManager.CurrentMoney >= requirement.amount,
                EventChoiceRequirementType.RemovableBulletExists =>
                    deckManager.CanRemoveOwnedBullet,
                EventChoiceRequirementType.UpgradableBulletExists =>
                    HasUpgradableBullet(),
                EventChoiceRequirementType.BulletSpaceExists =>
                    deckManager.OwnedBulletCount
                        < DeckManager.MaximumOwnedBulletCount,
                EventChoiceRequirementType.ItemSpaceExists =>
                    !playerInventory.IsFull,
                _ => true
            };

            if (!valid)
            {
                unavailableReason = requirement.unavailableReason;
                return false;
            }
        }

        return AreEffectsAvailable(choice, out unavailableReason);
    }

    private bool AreEffectsAvailable(
        EventChoiceData choice,
        out string unavailableReason)
    {
        int choiceIndex = GetChoiceIndex(choice);
        if (choiceIndex < 0)
        {
            unavailableReason = "유효하지 않은 이벤트 선택지입니다.";
            return false;
        }

        int previousSelections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        IEnumerable<EventEffect> attemptEffects = GetActiveEffects(
            choice.attemptEffects,
            previousSelections);
        IEnumerable<EventEffect> successEffects = GetActiveEffects(
            choice.effects,
            previousSelections);

        if (!ValidateEffectSet(
                attemptEffects.Concat(successEffects),
                previousSelections,
                out unavailableReason))
        {
            return false;
        }

        if (!choice.useSuccessChance)
        {
            return true;
        }

        IEnumerable<EventEffect> failureEffects = GetActiveEffects(
            choice.failureEffects,
            previousSelections);
        return ValidateEffectSet(
            GetActiveEffects(choice.attemptEffects, previousSelections)
                .Concat(failureEffects),
            previousSelections,
            out unavailableReason);
    }

    private bool ValidateEffectSet(
        IEnumerable<EventEffect> effects,
        int previousSelections,
        out string unavailableReason)
    {
        unavailableReason = string.Empty;
        long moneyCost = 0L;
        int bulletsToAdd = 0;
        int itemsToAdd = 0;

        foreach (EventEffect effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.type)
            {
                case EventEffectType.LoseMoney:
                    moneyCost += GetEffectAmount(
                        effect,
                        previousSelections);
                    break;
                case EventEffectType.AddBullet:
                    if (effect.bullet == null)
                    {
                        unavailableReason =
                            "획득할 탄환이 설정되지 않았습니다.";
                        return false;
                    }

                    bulletsToAdd++;
                    break;
                case EventEffectType.AddItem:
                    if (effect.item == null)
                    {
                        unavailableReason =
                            "획득할 아이템이 설정되지 않았습니다.";
                        return false;
                    }

                    itemsToAdd++;
                    break;
                case EventEffectType.RemoveChosenBullet:
                    if (!deckManager.CanRemoveOwnedBullet)
                    {
                        unavailableReason =
                            "제거할 수 있는 탄환이 없습니다.";
                        return false;
                    }

                    break;
                case EventEffectType.UpgradeChosenBullet:
                    if (!HasUpgradableBullet())
                    {
                        unavailableReason =
                            "강화할 수 있는 탄환이 없습니다.";
                        return false;
                    }

                    break;
            }
        }

        if (moneyCost > currencyManager.CurrentMoney)
        {
            unavailableReason = "골드가 부족합니다.";
            return false;
        }

        if ((long)deckManager.OwnedBulletCount + bulletsToAdd
            > DeckManager.MaximumOwnedBulletCount)
        {
            unavailableReason = "탄환 보유 공간이 부족합니다.";
            return false;
        }

        int emptyItemSlots = 0;
        for (int slotIndex = 0;
             slotIndex < playerInventory.SlotCount;
             slotIndex++)
        {
            if (playerInventory.GetItem(slotIndex) == null)
            {
                emptyItemSlots++;
            }
        }

        if (itemsToAdd > emptyItemSlots)
        {
            unavailableReason = "아이템 보유 공간이 부족합니다.";
            return false;
        }

        return true;
    }

    private bool HasUpgradableBullet()
    {
        deckManager.GetOwnedBullets(bulletBuffer);
        return bulletBuffer.Any(bullet => bullet != null && bullet.CanUpgrade);
    }

    private void SelectChoice(EventChoiceData choice)
    {
        eventTooltipUI?.HideEventRewardPreview();
        if (leaving || runData.eventChoiceResolved
            || !IsChoiceAvailable(choice, out _))
        {
            return;
        }

        pendingChoice = choice;
        int previousSelections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            GetChoiceIndex(choice));
        EventEffect targetedEffect = GetActiveEffects(
                choice.attemptEffects,
                previousSelections)
            .Concat(GetActiveEffects(choice.effects, previousSelections))
            .Concat(GetActiveEffects(
                choice.failureEffects,
                previousSelections))
            .FirstOrDefault(effect => effect != null
                && (effect.type == EventEffectType.RemoveChosenBullet
                    || effect.type == EventEffectType.UpgradeChosenBullet));

        if (targetedEffect == null)
        {
            ResolveChoice(choice, null);
            return;
        }

        EventBulletSelectionMode mode = targetedEffect.type
                == EventEffectType.RemoveChosenBullet
            ? EventBulletSelectionMode.Remove
            : EventBulletSelectionMode.Upgrade;
        if (bulletSelectionUI == null
            || !bulletSelectionUI.OpenEventSelection(
                eventCanvasRoot,
                deckManager,
                mode,
                HandleBulletSelected,
                HandleBulletSelectionCancelled))
        {
            Debug.LogError(
                "The Event scene is missing a configured bullet management panel.",
                this);
            pendingChoice = null;
        }
    }

    private void HandleBulletSelected(BulletInstance bullet)
    {
        EventChoiceData choice = pendingChoice;
        pendingChoice = null;
        if (choice != null)
        {
            ResolveChoice(choice, bullet);
        }
    }

    private void HandleBulletSelectionCancelled()
    {
        pendingChoice = null;
        ShowEventChoices();
    }

    private void ResolveChoice(
        EventChoiceData choice,
        BulletInstance chosenBullet)
    {
        int choiceIndex = GetChoiceIndex(choice);
        if (choiceIndex < 0)
        {
            Debug.LogError(
                "The selected event choice does not belong to the active event.",
                this);
            return;
        }

        int previousSelections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        int previousFailures = GetChoiceProgress(
            runData.eventChoiceFailureCounts,
            choiceIndex);

        ApplyEffects(
            GetActiveEffects(choice.attemptEffects, previousSelections),
            chosenBullet,
            previousSelections);

        float successChance = GetSuccessChance(choice, previousFailures);
        bool succeeded = !choice.useSuccessChance
            || UnityEngine.Random.value * 100f < successChance;
        EventEffect[] branchEffects = succeeded
            ? choice.effects
            : choice.failureEffects;
        ApplyEffects(
            GetActiveEffects(branchEffects, previousSelections),
            chosenBullet,
            previousSelections);

        EnsureChoiceProgressCapacity();
        runData.eventChoiceSelectionCounts[choiceIndex] =
            previousSelections + 1;
        if (!succeeded)
        {
            runData.eventChoiceFailureCounts[choiceIndex] =
                previousFailures + 1;
        }

        RefreshHealthPresentation();

        string resultText = succeeded
            ? choice.outcomeText
            : choice.failureOutcomeText;
        runData.eventOutcomeText = ExpandChoiceTokens(
            string.IsNullOrWhiteSpace(resultText)
                ? fallbackOutcome
                : resultText,
            choice);

        bool continueEvent = succeeded
            ? choice.continueAfterSuccess
            : choice.continueAfterFailure;
        runData.eventChoiceResolved = !continueEvent;
        if (continueEvent)
        {
            SaveEventState();
            ShowEventChoices();
            return;
        }

        if (!runData.completedEventIds.Contains(currentEvent.StableId))
        {
            runData.completedEventIds.Add(currentEvent.StableId);
        }

        SaveEventState();
        ShowOutcome(runData.eventOutcomeText);
    }

    private void ApplyEffects(
        IEnumerable<EventEffect> effects,
        BulletInstance chosenBullet,
        int previousSelections)
    {
        foreach (EventEffect effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            int amount = GetEffectAmount(effect, previousSelections);

            switch (effect.type)
            {
                case EventEffectType.GainMoney:
                    currencyManager.AddMoney(amount);
                    break;
                case EventEffectType.LoseMoney:
                    currencyManager.TrySpendMoney(amount);
                    break;
                case EventEffectType.Heal:
                    ChangeRunHealth(amount);
                    break;
                case EventEffectType.LoseHealth:
                    ChangeRunHealth(-(long)amount);
                    break;
                case EventEffectType.AddBullet:
                    deckManager.TryAddBullet(effect.bullet);
                    break;
                case EventEffectType.RemoveChosenBullet:
                    deckManager.TryRemoveBullet(chosenBullet);
                    break;
                case EventEffectType.UpgradeChosenBullet:
                    deckManager.TryUpgradeBullet(chosenBullet);
                    break;
                case EventEffectType.AddItem:
                    playerInventory.TryAdd(effect.item);
                    break;
            }
        }
    }

    private void ShowOutcome(string outcome)
    {
        eventTooltipUI?.HideEventRewardPreview();
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrWhiteSpace(outcome)
                ? fallbackOutcome
                : outcome;
        }

        for (int index = 0; index < choiceButtons.Length; index++)
        {
            Button button = choiceButtons[index];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            ClearChoiceRewardPreview(button);
            button.gameObject.SetActive(index == 0);
            if (index != 0)
            {
                continue;
            }

            button.interactable = true;
            TMP_Text label = index < choiceTexts.Length
                ? choiceTexts[index]
                : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = continueLabel;
            }

            button.onClick.AddListener(ReturnToNodeMap);
        }
    }

    public void ReturnToNodeMap()
    {
        if (leaving)
        {
            return;
        }

        leaving = true;
        SaveEventState();
        NodeMapSaveSystem.CompleteActiveNode();
        if (runData != null)
        {
            runData.activeEventId = string.Empty;
            runData.eventChoiceResolved = false;
            runData.eventOutcomeText = string.Empty;
            runData.eventChoiceSelectionCounts.Clear();
            runData.eventChoiceFailureCounts.Clear();
            RunSaveSystem.Save(runData);
        }

        if (!LoadingTransitionController.LoadScene(NodeMapSceneName))
        {
            SceneManager.LoadScene(NodeMapSceneName);
        }
    }

    private bool SaveEventState()
    {
        if (!initialized || runData == null || deckManager == null
            || currencyManager == null || playerInventory == null)
        {
            return false;
        }

        currencyManager.FlushPendingMoney();
        runData.flowState = (int)GameFlowState.Event;
        runData.startSelectedBattleFresh = false;
        runData.money = currencyManager.CurrentMoney;
        runData.paidBulletRemovalCount = deckManager.PaidBulletRemovalCount;
        deckManager.CaptureRunState(
            runData.bullets,
            runData.nextCycleAcquisitionOrders);
        playerInventory.CaptureRunState(runData.inventoryItemAssetNames);
        return RunSaveSystem.Save(runData);
    }

    private void ShowFallbackReturn()
    {
        eventTooltipUI?.HideEventRewardPreview();
        if (dialogueText != null)
        {
            dialogueText.text =
                "표시할 이벤트가 없습니다. EventDefinition을 이벤트 풀에 추가해 주세요.";
        }

        if (choiceButtons.Length > 0 && choiceButtons[0] != null)
        {
            choiceButtons[0].gameObject.SetActive(true);
            choiceButtons[0].interactable = true;
            choiceButtons[0].onClick.RemoveAllListeners();
            choiceButtons[0].onClick.AddListener(ReturnToNodeMap);
            TMP_Text label = choiceTexts.Length > 0
                ? choiceTexts[0]
                : choiceButtons[0].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "노드맵으로 돌아가기";
            }
        }
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
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        bulletSelectionUI ??= FindFirstObjectByType<BulletManagementUI>(
            FindObjectsInactive.Include);

        if (eventCanvasRoot == null)
        {
            Canvas canvas = FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && FindNamedComponent<TMP_Text>(
                        candidate.transform,
                        "Text | Event Dialogue") != null);
            eventCanvasRoot = canvas == null ? null : canvas.transform;
        }


        artworkImage ??= FindNamedComponent<Image>(
            eventCanvasRoot,
            "Image | Event Artwork");
        titleText ??= FindNamedComponent<TMP_Text>(
            eventCanvasRoot,
            "Text | Event Title");
        dialogueText ??= FindNamedComponent<TMP_Text>(
            eventCanvasRoot,
            "Text | Event Dialogue");

        if (choiceButtons == null || choiceButtons.Length == 0)
        {
            choiceButtons = Enumerable.Range(1, 3)
                .Select(index => FindNamedComponent<Button>(
                    eventCanvasRoot,
                    $"Button | Event Choice {index}"))
                .Where(button => button != null)
                .ToArray();
        }

        if (choiceTexts == null || choiceTexts.Length != choiceButtons.Length)
        {
            choiceTexts = choiceButtons
                .Select(button => button == null
                    ? null
                    : button.GetComponentInChildren<TMP_Text>(true))
                .ToArray();
        }

        eventTooltipUI ??= eventCanvasRoot == null
            ? null
            : eventCanvasRoot.GetComponentInChildren<InventoryTooltipUI>(true);

        playerHealthFillImage ??= FindNamedComponent<Image>(
            eventCanvasRoot,
            "Image | Fill Amount");
        playerHealthText ??= FindNamedComponent<TMP_Text>(
            eventCanvasRoot,
            "Text | Player HP");
    }

    private void EnsureChoiceProgressCapacity()
    {
        runData.eventChoiceSelectionCounts ??= new List<int>();
        runData.eventChoiceFailureCounts ??= new List<int>();
        int choiceCount = currentEvent?.choices?.Length ?? 0;
        while (runData.eventChoiceSelectionCounts.Count < choiceCount)
        {
            runData.eventChoiceSelectionCounts.Add(0);
        }

        while (runData.eventChoiceFailureCounts.Count < choiceCount)
        {
            runData.eventChoiceFailureCounts.Add(0);
        }
    }

    private int GetChoiceIndex(EventChoiceData choice)
    {
        if (currentEvent?.choices == null || choice == null)
        {
            return -1;
        }

        return Array.IndexOf(currentEvent.choices, choice);
    }

    private static int GetChoiceProgress(
        IReadOnlyList<int> progress,
        int choiceIndex)
    {
        return progress == null || choiceIndex < 0
            || choiceIndex >= progress.Count
                ? 0
                : Mathf.Max(0, progress[choiceIndex]);
    }

    private static IEnumerable<EventEffect> GetActiveEffects(
        EventEffect[] effects,
        int previousSelections)
    {
        return (effects ?? Array.Empty<EventEffect>()).Where(effect =>
            effect != null
            && (!effect.useSelectionRange
                || previousSelections >= effect.minimumPreviousSelections
                && (effect.maximumPreviousSelections < 0
                    || previousSelections
                        <= effect.maximumPreviousSelections)));
    }

    private static int GetEffectAmount(
        EventEffect effect,
        int previousSelections)
    {
        if (effect == null)
        {
            return 0;
        }

        long amount = Math.Max(0, effect.amount)
            + (long)Math.Max(0, effect.amountPerPreviousSelection)
                * Math.Max(0, previousSelections);
        return (int)Math.Min(int.MaxValue, amount);
    }

    private float GetSuccessChance(
        EventChoiceData choice,
        int failureCount)
    {
        if (choice == null || !choice.useSuccessChance)
        {
            return 100f;
        }

        return Mathf.Clamp(
            choice.baseSuccessChancePercent
                + Mathf.Max(0, failureCount)
                * Mathf.Max(
                    0f,
                    choice.successChanceIncreaseOnFailurePercent),
            0f,
            100f);
    }

    private string ExpandChoiceTokens(
        string source,
        EventChoiceData choice)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        int choiceIndex = GetChoiceIndex(choice);
        int selections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        int failures = GetChoiceProgress(
            runData.eventChoiceFailureCounts,
            choiceIndex);
        float chance = GetSuccessChance(choice, failures);
        return source
            .Replace("{attempt}", (selections + 1).ToString())
            .Replace("{selections}", selections.ToString())
            .Replace("{failures}", failures.ToString())
            .Replace("{chance}", chance.ToString("0.#"));
    }

    private void ChangeRunHealth(long delta)
    {
        int maximumHealth = Mathf.Max(1, runData.maxHealth);
        long changedHealth = (long)runData.currentHealth + delta;
        runData.currentHealth = (int)Math.Max(
            1L,
            Math.Min(maximumHealth, changedHealth));
    }

    private void RefreshHealthPresentation()
    {
        if (runData == null)
        {
            return;
        }

        int maximumHealth = Mathf.Max(1, runData.maxHealth);
        runData.maxHealth = maximumHealth;
        runData.currentHealth = Mathf.Clamp(
            runData.currentHealth,
            1,
            maximumHealth);

        if (playerHealthFillImage != null)
        {
            playerHealthFillImage.fillAmount =
                (float)runData.currentHealth / maximumHealth;
        }

        if (playerHealthText != null)
        {
            playerHealthText.text =
                $"{runData.currentHealth}/{maximumHealth}";
        }
    }

    private string FormatChoiceText(
        EventChoiceData choice,
        bool available,
        string unavailableReason)
    {
        string source = ExpandChoiceTokens(
            choice?.buttonText ?? string.Empty,
            choice);
        Match actionMatch = Regex.Match(source, @"^\s*(\[[^\]]+\])");
        string action = actionMatch.Success ? actionMatch.Groups[1].Value : string.Empty;
        string body = actionMatch.Success
            ? source.Substring(actionMatch.Length).TrimStart()
            : source;

        string formatted = string.IsNullOrEmpty(action)
            ? HighlightChoiceBody(body, choice)
            : $"{Colorize(action, actionNameColor)} {HighlightChoiceBody(body, choice)}";

        if (!available && !string.IsNullOrWhiteSpace(unavailableReason))
        {
            formatted += "\n<size=70%>"
                + Colorize(unavailableReason, unavailableReasonColor)
                + "</size>";
        }

        return formatted;
    }

    private string HighlightChoiceBody(
        string body,
        EventChoiceData choice)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        Dictionary<string, Color> namedRewards =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        foreach (EventEffect effect in choice?.effects
                     ?? Array.Empty<EventEffect>())
        {
            if (effect?.bullet != null)
            {
                AddHighlightName(
                    namedRewards,
                    effect.bullet.GetDisplayName(0),
                    rewardNameColor);
                AddHighlightName(
                    namedRewards,
                    effect.bullet.name,
                    rewardNameColor);
            }

            if (effect?.item != null)
            {
                AddHighlightName(
                    namedRewards,
                    string.IsNullOrWhiteSpace(effect.item.DisplayName)
                        ? effect.item.name
                        : effect.item.DisplayName,
                    rewardNameColor);
                AddHighlightName(
                    namedRewards,
                    effect.item.name,
                    rewardNameColor);
            }
        }

        List<string> patterns = namedRewards.Keys
            .OrderByDescending(value => value.Length)
            .Select(Regex.Escape)
            .ToList();
        patterns.Add(@"\d+\s*(?:골드|원)");
        patterns.Add(@"강화|제거|무료|비용|골드|탄환|아이템");
        Regex highlightPattern = new Regex(
            string.Join("|", patterns),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return highlightPattern.Replace(body, match =>
        {
            if (namedRewards.TryGetValue(match.Value, out Color rewardColor))
            {
                return Colorize(match.Value, rewardColor);
            }

            if (match.Value.Contains("강화"))
            {
                return Colorize(match.Value, upgradeKeywordColor);
            }

            if (match.Value.Contains("제거"))
            {
                return Colorize(match.Value, removeKeywordColor);
            }

            if (match.Value.Contains("무료"))
            {
                return Colorize(match.Value, freeKeywordColor);
            }

            if (Regex.IsMatch(match.Value, @"\d")
                || match.Value.Contains("비용")
                || match.Value.Contains("골드"))
            {
                return Colorize(match.Value, costKeywordColor);
            }

            return Colorize(match.Value, rewardNameColor);
        });
    }

    private static void AddHighlightName(
        IDictionary<string, Color> names,
        string value,
        Color color)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            names[value.Trim()] = color;
        }
    }

    private static string Colorize(string value, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";
    }

    private void ConfigureChoiceRewardPreview(
        Button button,
        EventChoiceData choice)
    {
        if (button == null)
        {
            return;
        }

        ClearChoiceRewardPreview(button);
        int previousSelections = GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            GetChoiceIndex(choice));
        EventEffect reward = GetActiveEffects(
                choice?.effects,
                previousSelections)
            .FirstOrDefault(effect => effect != null
                && (effect.type == EventEffectType.AddBullet
                    && effect.bullet != null
                    || effect.type == EventEffectType.AddItem
                    && effect.item != null));
        if (reward == null)
        {
            return;
        }

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        trigger ??= button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener(_ => eventTooltipUI?.ShowEventRewardPreview(
            reward.bullet,
            reward.item,
            button.transform as RectTransform));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exit.callback.AddListener(_ => eventTooltipUI?.HideEventRewardPreview());
        trigger.triggers.Add(exit);
    }

    private void ClearChoiceRewardPreview(Button button)
    {
        if (button == null)
        {
            return;
        }

        foreach (EventTrigger trigger in button.GetComponents<EventTrigger>())
        {
            trigger?.triggers?.Clear();
        }
    }

    private static T FindNamedComponent<T>(
        Transform root,
        string objectName)
        where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
