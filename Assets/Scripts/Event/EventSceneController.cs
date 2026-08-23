using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private const string BattleSceneName = "Battle";
    private const string ShopSceneName = "Shop";

    [Header("Event Pool")]
    [Tooltip("비어 있으면 Resources/Events의 모든 EventDefinition을 사용합니다.")]
    [SerializeField] private EventDefinition[] eventPool =
        Array.Empty<EventDefinition>();

    [Header("Scene-local Managers")]
    [SerializeField] private ShopManager dataResolver;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RelicManager relicManager;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private BulletManagementUI bulletSelectionUI;
    [SerializeField] private InventoryUI inventorySelectionUI;
    [SerializeField] private RelicInventoryUI relicSelectionUI;

    [Header("Event UI")]
    [SerializeField] private Transform eventCanvasRoot;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Image[] reelResultImages = Array.Empty<Image>();
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
    private readonly List<BulletData> bulletOfferBuffer =
        new List<BulletData>();
    private RunSaveData runData;
    private EventDefinition currentEvent;
    private EventChoiceData pendingChoice;
    private IReadOnlyList<BulletInstance> pendingSelectedBullets =
        Array.Empty<BulletInstance>();
    private IReadOnlyList<int> pendingSelectedItemSlots = Array.Empty<int>();
    private IReadOnlyList<RelicInstance> pendingSelectedRelics =
        Array.Empty<RelicInstance>();
    private InventoryTooltipUI eventTooltipUI;
    private EventChoiceTextFormatter choiceTextFormatter;
    private EventChoiceButtonPresenter choiceButtonPresenter;
    private EventResultPresenter resultPresenter;
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
            || currencyManager == null || playerInventory == null
            || relicManager == null)
        {
            Debug.LogError(
                "Event initialization is missing managers. "
                + $"Resolver={dataResolver != null}, "
                + $"Deck={deckManager != null}, "
                + $"Currency={currencyManager != null}, "
                + $"Inventory={playerInventory != null}, "
                + $"Relics={relicManager != null}.",
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
        if (!relicManager.RestoreRunState(runData.relics))
        {
            Debug.LogError(
                "Event initialization could not restore saved relics.",
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
            runData.eventResultText = string.Empty;
            runData.eventReelSymbolKeys.Clear();
            runData.eventChoiceSelectionCounts.Clear();
            runData.eventChoiceFailureCounts.Clear();
            ClearPendingInteractionState();
            runData.eventFollowUpDestination =
                (int)EventFollowUpDestination.NodeMap;
            runData.eventFollowUpBattleIndex = -1;
        }

        EnsureChoiceProgressCapacity();

        ConfigureSharedPresentation();
        initialized = true;

        if (runData.eventInteractionStage == 1)
        {
            ShowRandomBulletOffer();
        }
        else if (runData.eventInteractionStage == 2)
        {
            ShowQuizAnswers();
        }
        else if (runData.eventChoiceResolved)
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
        IEnumerable<EventDefinition> source =
            (eventPool ?? Array.Empty<EventDefinition>())
            .Concat(Resources.LoadAll<EventDefinition>("Events"));
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
        SetResultText(runData.eventResultText);

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
        SetResultText(runData.eventResultText);
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
        EventChoiceData[] visibleChoices = new EventChoiceData[choiceCount];
        EventChoiceButtonState[] states =
            new EventChoiceButtonState[choiceCount];
        for (int index = 0; index < choiceCount; index++)
        {
            EventChoiceData choice = currentEvent.choices[index];
            if (choice == null)
            {
                states[index] = new EventChoiceButtonState(
                    string.Empty,
                    false,
                    true);
                continue;
            }

            visibleChoices[index] = choice;
            bool available = IsChoiceAvailable(choice, out string reason);
            states[index] = new EventChoiceButtonState(
                FormatChoiceText(choice, available, reason),
                available,
                true);
        }

        EnsurePresenters();
        choiceButtonPresenter.PresentChoices(
            states,
            index => SelectChoice(visibleChoices[index]),
            (button, index) => ConfigureChoiceRewardPreview(
                button,
                visibleChoices[index]));
    }

    private bool IsChoiceAvailable(
        EventChoiceData choice,
        out string unavailableReason)
    {
        int choiceIndex = GetChoiceIndex(choice);
        int previousSelections = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        EventChoiceAvailabilityResult result =
            EventChoiceAvailabilityEvaluator.Evaluate(
                choice,
                choiceIndex,
                previousSelections,
                CreateChoiceAvailabilityContext());
        unavailableReason = result.UnavailableReason;
        return result.IsAvailable;
    }

    private EventChoiceAvailabilityContext CreateChoiceAvailabilityContext()
    {
        deckManager.GetOwnedBullets(bulletBuffer);
        int ownedItemCount = 0;
        int emptyItemSlotCount = 0;
        for (int index = 0; index < playerInventory.SlotCount; index++)
        {
            if (playerInventory.GetItem(index) == null)
            {
                emptyItemSlotCount++;
            }
            else
            {
                ownedItemCount++;
            }
        }

        return new EventChoiceAvailabilityContext(
            currencyManager.CurrentMoney,
            deckManager.CanRemoveOwnedBullet,
            deckManager.OwnedBulletCount,
            bulletBuffer,
            ownedItemCount,
            emptyItemSlotCount,
            relicManager.OwnedRelics.Count,
            dataResolver.BulletCatalog.Count,
            dataResolver.BulletCatalog.Any(bullet => bullet != null
                && bullet.BulletId != "bullet_jackpot"),
            dataResolver.ItemCatalog.Any(item => item != null));
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
        pendingSelectedBullets = Array.Empty<BulletInstance>();
        pendingSelectedItemSlots = Array.Empty<int>();
        pendingSelectedRelics = Array.Empty<RelicInstance>();

        if (choice.specialAction == EventSpecialAction.BulletQuiz)
        {
            BeginBulletQuiz(choice);
            return;
        }

        int previousSelections = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            GetChoiceIndex(choice));
        EventEffect targetedEffect = EventRuntimeRules.GetActiveEffects(
                choice.attemptEffects,
                previousSelections)
            .Concat(EventRuntimeRules.GetActiveEffects(
                choice.effects,
                previousSelections))
            .Concat(EventRuntimeRules.GetActiveEffects(
                choice.failureEffects,
                previousSelections))
            .FirstOrDefault(effect => effect != null
                && (effect.type == EventEffectType.RemoveChosenBullet
                    || effect.type == EventEffectType.UpgradeChosenBullet
                    || effect.type == EventEffectType.RemoveChosenItem
                    || effect.type == EventEffectType.RemoveChosenRelic));

        if (targetedEffect == null)
        {
            ResolveChoice(
                choice,
                pendingSelectedBullets,
                pendingSelectedItemSlots,
                pendingSelectedRelics);
            return;
        }


        if (targetedEffect.type == EventEffectType.RemoveChosenItem)
        {
            if (inventorySelectionUI == null
                || !inventorySelectionUI.BeginEventSelection(
                    Mathf.Max(1, choice.itemSelectionCount),
                    null,
                    HandleItemsSelected,
                    HandleTargetSelectionCancelled))
            {
                pendingChoice = null;
                return;
            }

            inventorySelectionUI.EventSelectionChanged +=
                HandleInventorySelectionChanged;
            ShowExternalSelectionControls(
                "아이템 선택 완료",
                inventorySelectionUI.ConfirmEventSelection,
                inventorySelectionUI.CancelEventSelection);
            return;
        }

        if (targetedEffect.type == EventEffectType.RemoveChosenRelic)
        {
            if (relicSelectionUI == null
                || !relicSelectionUI.BeginEventSelection(
                    Mathf.Max(1, choice.relicSelectionCount),
                    null,
                    HandleRelicsSelected,
                    HandleTargetSelectionCancelled))
            {
                pendingChoice = null;
                return;
            }

            relicSelectionUI.EventSelectionChanged +=
                HandleRelicSelectionChanged;
            ShowExternalSelectionControls(
                "유물 선택 완료",
                relicSelectionUI.ConfirmEventSelection,
                relicSelectionUI.CancelEventSelection);
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
                Mathf.Max(1, choice.bulletSelectionCount),
                bullet => EventChoiceAvailabilityEvaluator
                    .IsBulletEligibleForChoice(
                    bullet,
                    choice,
                    mode == EventBulletSelectionMode.Upgrade),
                bullets => EventRuntimeRules.IsValidBulletGroup(
                    bullets,
                    Mathf.Max(1, choice.bulletSelectionCount),
                    choice.requireDistinctBulletTypes,
                    choice.requireSameBulletGrade),
                HandleBulletsSelected,
                HandleTargetSelectionCancelled))
        {
            Debug.LogError(
                "The Event scene is missing a configured bullet management panel.",
                this);
            pendingChoice = null;
        }
    }

    private void HandleBulletsSelected(
        IReadOnlyList<BulletInstance> bullets)
    {
        EventChoiceData choice = pendingChoice;
        pendingChoice = null;
        if (choice != null)
        {
            ResolveChoice(
                choice,
                bullets,
                pendingSelectedItemSlots,
                pendingSelectedRelics);
        }
    }

    private void HandleItemsSelected(IReadOnlyList<int> slots)
    {
        inventorySelectionUI.EventSelectionChanged -=
            HandleInventorySelectionChanged;
        EventChoiceData choice = pendingChoice;
        pendingChoice = null;
        if (choice != null)
        {
            ResolveChoice(
                choice,
                pendingSelectedBullets,
                slots,
                pendingSelectedRelics);
        }
    }

    private void HandleRelicsSelected(IReadOnlyList<RelicInstance> relics)
    {
        relicSelectionUI.EventSelectionChanged -=
            HandleRelicSelectionChanged;
        EventChoiceData choice = pendingChoice;
        pendingChoice = null;
        if (choice != null)
        {
            ResolveChoice(
                choice,
                pendingSelectedBullets,
                pendingSelectedItemSlots,
                relics);
        }
    }

    private void HandleTargetSelectionCancelled()
    {
        if (inventorySelectionUI != null)
        {
            inventorySelectionUI.EventSelectionChanged -=
                HandleInventorySelectionChanged;
        }
        if (relicSelectionUI != null)
        {
            relicSelectionUI.EventSelectionChanged -=
                HandleRelicSelectionChanged;
        }
        pendingChoice = null;
        ShowEventChoices();
    }

    private void ResolveChoice(
        EventChoiceData choice,
        IReadOnlyList<BulletInstance> chosenBullets,
        IReadOnlyList<int> chosenItemSlots,
        IReadOnlyList<RelicInstance> chosenRelics)
    {
        int choiceIndex = GetChoiceIndex(choice);
        if (choiceIndex < 0)
        {
            Debug.LogError(
                "The selected event choice does not belong to the active event.",
                this);
            return;
        }

        int previousSelections = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        int previousFailures = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceFailureCounts,
            choiceIndex);

        if (choice.specialAction == EventSpecialAction.RandomBulletOffer)
        {
            BeginRandomBulletOffer(
                choice,
                chosenBullets,
                chosenItemSlots,
                chosenRelics,
                previousSelections);
            return;
        }

        if (choice.specialAction == EventSpecialAction.SlotMachine)
        {
            ResolveSlotMachine(choice, previousSelections);
            return;
        }

        ApplyEffects(
            EventRuntimeRules.GetActiveEffects(
                choice.attemptEffects,
                previousSelections),
            chosenBullets,
            chosenItemSlots,
            chosenRelics,
            previousSelections);

        float successChance = EventRuntimeRules.GetSuccessChance(
            choice,
            previousFailures);
        bool succeeded = !choice.useSuccessChance
            || UnityEngine.Random.value * 100f < successChance;
        EventEffect[] branchEffects = succeeded
            ? choice.effects
            : choice.failureEffects;
        ApplyEffects(
            EventRuntimeRules.GetActiveEffects(
                branchEffects,
                previousSelections),
            chosenBullets,
            chosenItemSlots,
            chosenRelics,
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

        CompleteEvent(runData.eventOutcomeText);
    }

    private void ApplyEffects(
        IEnumerable<EventEffect> effects,
        IReadOnlyList<BulletInstance> chosenBullets,
        IReadOnlyList<int> chosenItemSlots,
        IReadOnlyList<RelicInstance> chosenRelics,
        int previousSelections)
    {
        foreach (EventEffect effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            int amount = EventRuntimeRules.GetEffectAmount(
                effect,
                previousSelections);

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
                    AddBulletReward(effect, chosenBullets);
                    break;
                case EventEffectType.RemoveChosenBullet:
                    foreach (BulletInstance bullet in chosenBullets
                                 ?? Array.Empty<BulletInstance>())
                    {
                        deckManager.TryRemoveBullet(bullet);
                    }
                    break;
                case EventEffectType.UpgradeChosenBullet:
                    foreach (BulletInstance bullet in chosenBullets
                                 ?? Array.Empty<BulletInstance>())
                    {
                        deckManager.TryUpgradeBullet(bullet);
                    }
                    break;
                case EventEffectType.AddItem:
                    playerInventory.TryAdd(effect.item);
                    break;
                case EventEffectType.IncreaseMaxHealthPercent:
                    IncreaseRunMaxHealthPercent(amount);
                    break;
                case EventEffectType.LoseCurrentHealthPercent:
                    LoseCurrentHealthPercent(amount);
                    break;
                case EventEffectType.AddPendingStatusEffect:
                    AddPendingStatusEffect(effect.statusEffectType, amount);
                    break;
                case EventEffectType.RemoveChosenItem:
                    foreach (int slotIndex in chosenItemSlots
                                 ?? Array.Empty<int>())
                    {
                        playerInventory.TryRemove(slotIndex);
                    }
                    break;
                case EventEffectType.RemoveChosenRelic:
                    RemoveChosenRelics(chosenRelics);
                    break;
            }
        }
    }

    private void AddBulletReward(
        EventEffect effect,
        IReadOnlyList<BulletInstance> chosenBullets)
    {
        if (effect.bullet != null)
        {
            deckManager.TryAddBullet(effect.bullet, effect.bulletLevel);
            return;
        }

        EventRuntimeRules.GenerateBulletOffers(
            dataResolver.BulletCatalog,
            dataResolver.BulletGradeWeights,
            1,
            effect.randomBulletGradeMode,
            effect.fixedBulletGrade,
            effect.oneGradeHigherChancePercent,
            chosenBullets,
            bulletOfferBuffer);
        if (bulletOfferBuffer.Count > 0)
        {
            deckManager.TryAddBullet(
                bulletOfferBuffer[0],
                effect.bulletLevel);
        }
    }

    private void IncreaseRunMaxHealthPercent(int percent)
    {
        if (percent <= 0)
        {
            return;
        }

        int previousMaximum = Mathf.Max(1, runData.maxHealth);
        int increase = Mathf.Max(
            1,
            Mathf.CeilToInt(previousMaximum * percent / 100f));
        runData.maxHealth = (int)Math.Min(
            int.MaxValue,
            (long)previousMaximum + increase);
        runData.currentHealth = (int)Math.Min(
            runData.maxHealth,
            (long)runData.currentHealth + increase);
    }

    private void LoseCurrentHealthPercent(int percent)
    {
        int loss = Mathf.CeilToInt(
            Mathf.Max(1, runData.currentHealth) * Mathf.Max(0, percent)
            / 100f);
        ChangeRunHealth(-loss);
    }

    private void AddPendingStatusEffect(StatusEffectType type, int stacks)
    {
        if (stacks <= 0)
        {
            return;
        }

        runData.pendingNextBattlePlayerStatusEffects ??=
            new RunStatusEffectSaveData();
        RunStatusEffectSaveData pending =
            runData.pendingNextBattlePlayerStatusEffects;
        switch (type)
        {
            case StatusEffectType.Mark:
                pending.markStacks = SaturatingAdd(
                    pending.markStacks,
                    stacks);
                break;
            case StatusEffectType.Poison:
                pending.poisonStacks = SaturatingAdd(
                    pending.poisonStacks,
                    stacks);
                pending.poisonCreditedToPlayer = false;
                break;
            case StatusEffectType.Stun:
                pending.stunStacks = SaturatingAdd(
                    pending.stunStacks,
                    stacks);
                break;
            case StatusEffectType.Weakness:
                pending.weaknessStacks = SaturatingAdd(
                    pending.weaknessStacks,
                    stacks);
                break;
        }
    }

    private static int SaturatingAdd(int current, int added)
    {
        return (int)Math.Min(
            int.MaxValue,
            (long)Mathf.Max(0, current) + Mathf.Max(0, added));
    }

    private void RemoveChosenRelics(
        IReadOnlyList<RelicInstance> chosenRelics)
    {
        if (chosenRelics == null)
        {
            return;
        }

        List<int> indices = chosenRelics
            .Select(FindOwnedRelicIndex)
            .Where(index => index >= 0)
            .Distinct()
            .OrderByDescending(index => index)
            .ToList();
        foreach (int index in indices)
        {
            relicManager.TryRemoveAt(index, RelicRemovalReason.Removed);
        }
    }

    private int FindOwnedRelicIndex(RelicInstance relic)
    {
        for (int index = 0; index < relicManager.OwnedRelics.Count; index++)
        {
            if (ReferenceEquals(relicManager.OwnedRelics[index], relic))
            {
                return index;
            }
        }

        return -1;
    }

    private void BeginRandomBulletOffer(
        EventChoiceData choice,
        IReadOnlyList<BulletInstance> chosenBullets,
        IReadOnlyList<int> chosenItemSlots,
        IReadOnlyList<RelicInstance> chosenRelics,
        int previousSelections)
    {
        EventRuntimeRules.GenerateBulletOffers(
            dataResolver.BulletCatalog,
            dataResolver.BulletGradeWeights,
            Mathf.Clamp(choice.randomBulletOfferCount, 1, 3),
            choice.offerGradeMode,
            choice.fixedOfferGrade,
            choice.offerOneGradeHigherChancePercent,
            chosenBullets,
            bulletOfferBuffer);
        if (bulletOfferBuffer.Count == 0)
        {
            pendingChoice = null;
            ShowEventChoices();
            return;
        }

        ApplyEffects(
            EventRuntimeRules.GetActiveEffects(
                choice.attemptEffects,
                previousSelections),
            chosenBullets,
            chosenItemSlots,
            chosenRelics,
            previousSelections);
        ApplyEffects(
            EventRuntimeRules.GetActiveEffects(
                choice.effects,
                previousSelections)
                .Where(effect => effect.type != EventEffectType.AddBullet),
            chosenBullets,
            chosenItemSlots,
            chosenRelics,
            previousSelections);
        int choiceIndex = GetChoiceIndex(choice);
        EnsureChoiceProgressCapacity();
        runData.eventChoiceSelectionCounts[choiceIndex] =
            previousSelections + 1;
        runData.eventPendingChoiceIndex = choiceIndex;
        runData.eventInteractionStage = 1;
        runData.eventOfferAssetNames.Clear();
        runData.eventOfferAssetNames.AddRange(
            bulletOfferBuffer.Select(bullet => bullet.name));
        runData.eventOutcomeText = string.IsNullOrWhiteSpace(choice.outcomeText)
            ? "대가를 치렀다. 이제 하나를 골라야 한다."
            : choice.outcomeText;
        pendingChoice = null;
        RefreshHealthPresentation();
        SaveEventState();
        ShowRandomBulletOffer();
    }

    private void ShowRandomBulletOffer()
    {
        int choiceIndex = runData.eventPendingChoiceIndex;
        EventChoiceData choice = currentEvent?.choices != null
            && choiceIndex >= 0 && choiceIndex < currentEvent.choices.Length
                ? currentEvent.choices[choiceIndex]
                : null;
        if (choice == null)
        {
            ClearPendingInteractionState();
            ShowEventChoices();
            return;
        }

        dialogueText.text = runData.eventOutcomeText;
        bulletOfferBuffer.Clear();
        foreach (string assetName in runData.eventOfferAssetNames)
        {
            BulletData bullet = EventRuntimeRules.FindBulletByAssetName(
                dataResolver.BulletCatalog,
                assetName);
            if (bullet != null)
            {
                bulletOfferBuffer.Add(bullet);
            }
        }

        ConfigureDynamicChoices(
            bulletOfferBuffer.Select(bullet => bullet.GetDisplayName(
                    choice.offeredBulletLevel)).ToList(),
            index => SelectRandomBulletOffer(index, choice));
    }

    private void SelectRandomBulletOffer(
        int index,
        EventChoiceData choice)
    {
        if (index < 0 || index >= bulletOfferBuffer.Count
            || !deckManager.TryAddBullet(
                bulletOfferBuffer[index],
                choice.offeredBulletLevel))
        {
            return;
        }

        string acquiredName = bulletOfferBuffer[index].GetDisplayName(
            choice.offeredBulletLevel);
        string outcome = $"{runData.eventOutcomeText}\n{acquiredName}을(를) 챙겼다.";
        ClearPendingInteractionState();
        CompleteEvent(outcome);
    }

    private void BeginBulletQuiz(EventChoiceData choice)
    {
        deckManager.GetOwnedBullets(bulletBuffer);
        BulletInstance target = bulletBuffer.Count == 0
            ? null
            : bulletBuffer[UnityEngine.Random.Range(0, bulletBuffer.Count)];
        if (target?.Data == null)
        {
            pendingChoice = null;
            ShowEventChoices();
            return;
        }

        List<BulletData> answers = dataResolver.BulletCatalog
            .Where(bullet => bullet != null && bullet != target.Data)
            .OrderBy(bullet => bullet.Grade == target.Grade ? 0 : 1)
            .Take(2)
            .ToList();
        answers.Add(target.Data);
        for (int index = answers.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (answers[index], answers[swapIndex]) =
                (answers[swapIndex], answers[index]);
        }

        runData.eventPendingChoiceIndex = GetChoiceIndex(choice);
        runData.eventQuizCorrectAssetName = target.Data.name;
        runData.eventOfferAssetNames.Clear();
        runData.eventOfferAssetNames.AddRange(
            answers.Select(answer => answer.name));
        runData.eventOutcomeText =
            $"노인은 탄환의 내용물을 가리고 {target.Grade} 등급 테두리만 내밀었다.";
        runData.eventResultText =
            $"<color=#{ColorUtility.ToHtmlStringRGB(target.GradeNameColor)}>◆ {target.Grade} ◆</color>";
        runData.eventInteractionStage = 2;
        pendingChoice = null;
        SaveEventState();
        ShowQuizAnswers();
    }

    private void ShowQuizAnswers()
    {
        dialogueText.text = runData.eventOutcomeText;
        SetResultText(runData.eventResultText);
        List<string> labels = runData.eventOfferAssetNames
            .Select(assetName => EventRuntimeRules.FindBulletByAssetName(
                dataResolver.BulletCatalog,
                assetName))
            .Where(bullet => bullet != null)
            .Select(bullet => bullet.GetDisplayName(0))
            .ToList();
        ConfigureDynamicChoices(labels, ResolveQuizAnswer);
    }

    private void ResolveQuizAnswer(int index)
    {
        if (index < 0 || index >= runData.eventOfferAssetNames.Count)
        {
            return;
        }

        bool correct = runData.eventOfferAssetNames[index]
            == runData.eventQuizCorrectAssetName;
        if (correct)
        {
            currencyManager.AddMoney(30);
        }

        string outcome = correct
            ? "노인이 무릎을 치며 30 골드를 쏟아 놓았다."
            : "노인은 틀렸다며 낄낄댔다. 보상은 없었다.";
        ClearPendingInteractionState();
        CompleteEvent(outcome);
    }

    private void ResolveSlotMachine(
        EventChoiceData choice,
        int previousSelections)
    {
        ApplyEffects(
            EventRuntimeRules.GetActiveEffects(
                choice.attemptEffects,
                previousSelections),
            Array.Empty<BulletInstance>(),
            Array.Empty<int>(),
            Array.Empty<RelicInstance>(),
            previousSelections);
        List<string> symbols = BuildEligibleSlotSymbols();
        if (symbols.Count == 0)
        {
            return;
        }

        string[] reels = new string[3];
        for (int index = 0; index < reels.Length; index++)
        {
            reels[index] = symbols[UnityEngine.Random.Range(0, symbols.Count)];
        }

        string[] displayNames = reels.Select(GetSlotSymbolDisplayName).ToArray();
        runData.eventResultText =
            $"[ {displayNames[0]} ]  [ {displayNames[1]} ]  [ {displayNames[2]} ]";
        runData.eventReelSymbolKeys.Clear();
        runData.eventReelSymbolKeys.AddRange(reels);
        bool triple = reels[0] == reels[1] && reels[1] == reels[2];
        bool pair = !triple && (reels[0] == reels[1]
            || reels[0] == reels[2] || reels[1] == reels[2]);
        string outcome;
        if (triple)
        {
            GrantSlotSymbol(reels[0]);
            BulletData jackpot = EventRuntimeRules.FindBulletById(
                dataResolver.BulletCatalog,
                "bullet_jackpot");
            if (jackpot != null)
            {
                deckManager.TryAddBullet(
                    jackpot,
                    BulletData.MaximumUpgradeLevel);
            }

            outcome = "세 릴이 맞물렸다. 그림의 전리품과 잭팟탄(+3)이 튀어나왔다.";
        }
        else if (pair)
        {
            int cost = EventRuntimeRules.GetActiveEffects(
                    choice.attemptEffects,
                    previousSelections)
                .Where(effect => effect.type == EventEffectType.LoseMoney)
                .Sum(effect => EventRuntimeRules.GetEffectAmount(
                    effect,
                    previousSelections));
            currencyManager.AddMoney(cost * 3);
            outcome = $"두 릴이 맞았다. {cost * 3} 골드를 받았다.";
        }
        else
        {
            outcome = "릴은 서로 다른 그림에서 멎었다. 도박사는 돈을 쓸어 갔다.";
        }

        int choiceIndex = GetChoiceIndex(choice);
        EnsureChoiceProgressCapacity();
        runData.eventChoiceSelectionCounts[choiceIndex] =
            previousSelections + 1;
        runData.eventOutcomeText = outcome;
        runData.eventChoiceResolved = !choice.continueAfterSuccess;
        RefreshHealthPresentation();
        if (choice.continueAfterSuccess)
        {
            SaveEventState();
            ShowEventChoices();
            return;
        }

        CompleteEvent(outcome);
    }

    private List<string> BuildEligibleSlotSymbols()
    {
        int bulletSpaces = DeckManager.MaximumOwnedBulletCount
            - deckManager.OwnedBulletCount;
        List<string> symbols = new List<string>();
        if (bulletSpaces >= 2)
        {
            symbols.AddRange(dataResolver.BulletCatalog
                .Where(bullet => bullet != null
                    && bullet.BulletId != "bullet_jackpot")
                .Take(6)
                .Select(bullet => "B:" + bullet.name));
        }
        if (bulletSpaces >= 1 && !playerInventory.IsFull)
        {
            symbols.AddRange(dataResolver.ItemCatalog
                .Where(item => item != null)
                .Take(4)
                .Select(item => "I:" + item.name));
        }

        return symbols;
    }

    private string GetSlotSymbolDisplayName(string symbol)
    {
        if (symbol.StartsWith("B:"))
        {
            BulletData bullet = EventRuntimeRules.FindBulletByAssetName(
                dataResolver.BulletCatalog,
                symbol.Substring(2));
            return bullet == null ? "?" : bullet.GetDisplayName(0);
        }

        ItemData item = EventRuntimeRules.FindItemByAssetName(
            dataResolver.ItemCatalog,
            symbol.Substring(2));
        return item == null || string.IsNullOrWhiteSpace(item.DisplayName)
            ? item?.name ?? "?"
            : item.DisplayName;
    }

    private void GrantSlotSymbol(string symbol)
    {
        if (symbol.StartsWith("B:"))
        {
            BulletData bullet = EventRuntimeRules.FindBulletByAssetName(
                dataResolver.BulletCatalog,
                symbol.Substring(2));
            deckManager.TryAddBullet(bullet);
            return;
        }

        playerInventory.TryAdd(EventRuntimeRules.FindItemByAssetName(
            dataResolver.ItemCatalog,
            symbol.Substring(2)));
    }

    private void CompleteEvent(string outcome)
    {
        runData.eventChoiceResolved = true;
        runData.eventOutcomeText = string.IsNullOrWhiteSpace(outcome)
            ? fallbackOutcome
            : outcome;
        if (!runData.completedEventIds.Contains(currentEvent.StableId))
        {
            runData.completedEventIds.Add(currentEvent.StableId);
        }

        SelectAndStoreFollowUp();
        SaveEventState();
        ShowOutcome(runData.eventOutcomeText);
    }

    private void SelectAndStoreFollowUp()
    {
        EventFollowUpDestination destination =
            EventRuntimeRules.SelectFollowUp(
                currentEvent.normalBattleChancePercent,
                currentEvent.eliteBattleChancePercent,
                currentEvent.shopChancePercent);
        int battleIndex = -1;
        if (destination == EventFollowUpDestination.NormalBattle
            || destination == EventFollowUpDestination.EliteBattle)
        {
            BattleType requestedType = destination
                == EventFollowUpDestination.EliteBattle
                    ? BattleType.Elite
                    : BattleType.Normal;
            battleIndex = SelectFollowUpBattleIndex(requestedType);
            if (battleIndex < 0)
            {
                destination = EventFollowUpDestination.NodeMap;
            }
        }

        runData.eventFollowUpDestination = (int)destination;
        runData.eventFollowUpBattleIndex = battleIndex;
    }

    private int SelectFollowUpBattleIndex(BattleType requestedType)
    {
        StageData stage = stateManager == null ? null : stateManager.CurrentStage;
        if (stage == null)
        {
            return -1;
        }

        List<int> candidates = new List<int>();
        for (int index = 0; index < stage.Battles.Count; index++)
        {
            BattleData battle = stage.Battles[index];
            if (battle != null && battle.BattleType == requestedType)
            {
                candidates.Add(index);
            }
        }

        return candidates.Count == 0
            ? -1
            : candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void ConfigureDynamicChoices(
        IReadOnlyList<string> labels,
        Action<int> onSelected)
    {
        EnsurePresenters();
        choiceButtonPresenter.ShowDynamicChoices(labels, onSelected);
    }

    private void ShowExternalSelectionControls(
        string confirmLabel,
        Func<bool> confirmAction,
        Action cancelAction)
    {
        EnsurePresenters();
        choiceButtonPresenter.ShowExternalSelectionControls(
            confirmLabel,
            confirmAction,
            cancelAction);
    }

    private void HandleInventorySelectionChanged(int selected, int required)
    {
        SetExternalConfirmLabel($"아이템 선택 완료 ({selected}/{required})");
    }

    private void HandleRelicSelectionChanged(int selected, int required)
    {
        SetExternalConfirmLabel($"유물 선택 완료 ({selected}/{required})");
    }

    private void SetExternalConfirmLabel(string value)
    {
        EnsurePresenters();
        choiceButtonPresenter.SetPrimaryLabel(value);
    }

    private void EnsurePresenters()
    {
        choiceButtonPresenter ??= new EventChoiceButtonPresenter(
            choiceButtons,
            choiceTexts,
            ClearChoiceRewardPreview,
            SoundManager.BindUiButtonSfx);
        resultPresenter ??= new EventResultPresenter(
            dialogueText,
            resultText,
            reelResultImages);
    }

    private void ClearPendingInteractionState()
    {
        runData.eventInteractionStage = 0;
        runData.eventPendingChoiceIndex = -1;
        runData.eventOfferAssetNames.Clear();
        runData.eventQuizCorrectAssetName = string.Empty;
    }

    private void SetResultText(string value)
    {
        EnsurePresenters();
        resultPresenter.Present(
            value,
            runData?.eventReelSymbolKeys,
            GetSlotSymbolSprite);
        resultText = resultPresenter.ResultText;
        reelResultImages = resultPresenter.ReelResultImages;
    }

    private Sprite GetSlotSymbolSprite(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length <= 2)
        {
            return null;
        }

        if (symbol.StartsWith("B:"))
        {
            BulletData bullet = EventRuntimeRules.FindBulletByAssetName(
                dataResolver.BulletCatalog,
                symbol.Substring(2));
            return bullet == null ? null : bullet.CylinderIcon;
        }

        ItemData item = EventRuntimeRules.FindItemByAssetName(
            dataResolver.ItemCatalog,
            symbol.Substring(2));
        return item == null ? null : item.Icon;
    }

    private void ShowOutcome(string outcome)
    {
        eventTooltipUI?.HideEventRewardPreview();
        SetResultText(runData?.eventResultText);
        if (dialogueText != null)
        {
            dialogueText.text = string.IsNullOrWhiteSpace(outcome)
                ? fallbackOutcome
                : outcome;
        }

        EnsurePresenters();
        choiceButtonPresenter.ShowSingleAction(
            GetFollowUpLabel(),
            ContinueFromEvent);
    }

    private string GetFollowUpLabel()
    {
        EventFollowUpDestination destination = runData == null
            ? EventFollowUpDestination.NodeMap
            : (EventFollowUpDestination)runData.eventFollowUpDestination;
        return destination switch
        {
            EventFollowUpDestination.NormalBattle => "일반 전투로",
            EventFollowUpDestination.EliteBattle => "엘리트 전투로",
            EventFollowUpDestination.Shop => "상점으로",
            _ => continueLabel
        };
    }

    private void ContinueFromEvent()
    {
        if (leaving || runData == null)
        {
            return;
        }

        EventFollowUpDestination destination =
            (EventFollowUpDestination)runData.eventFollowUpDestination;
        switch (destination)
        {
            case EventFollowUpDestination.NormalBattle:
            case EventFollowUpDestination.EliteBattle:
                ContinueToFollowUpBattle();
                break;
            case EventFollowUpDestination.Shop:
                ContinueToFollowUpShop();
                break;
            default:
                ReturnToNodeMap();
                break;
        }
    }

    private void ContinueToFollowUpBattle()
    {
        int battleIndex = runData.eventFollowUpBattleIndex;
        if (battleIndex < 0
            || !NodeMapSaveSystem.SetSelectedBattleIndex(battleIndex))
        {
            ReturnToNodeMap();
            return;
        }

        leaving = true;
        SaveEventState();
        ClearCompletedEventState();
        RunSaveSystem.Save(runData);
        if (!RunSaveSystem.PrepareForSelectedBattle(
                runData.stageIndex,
                battleIndex))
        {
            leaving = false;
            ReturnToNodeMap();
            return;
        }

        RunSaveSystem.RequestStart(RunStartMode.Continue);
        if (!LoadingTransitionController.LoadScene(BattleSceneName))
        {
            SceneManager.LoadScene(BattleSceneName);
        }
    }

    private void ContinueToFollowUpShop()
    {
        leaving = true;
        SaveEventState();
        ClearCompletedEventState();
        runData.flowState = (int)GameFlowState.Shop;
        runData.shopVisitActive = false;
        RunSaveSystem.Save(runData);
        if (!LoadingTransitionController.LoadScene(ShopSceneName))
        {
            SceneManager.LoadScene(ShopSceneName);
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
            ClearCompletedEventState();
            RunSaveSystem.Save(runData);
        }

        if (!LoadingTransitionController.LoadScene(NodeMapSceneName))
        {
            SceneManager.LoadScene(NodeMapSceneName);
        }
    }

    private void ClearCompletedEventState()
    {
        runData.activeEventId = string.Empty;
        runData.eventChoiceResolved = false;
        runData.eventOutcomeText = string.Empty;
        runData.eventResultText = string.Empty;
        runData.eventReelSymbolKeys.Clear();
        runData.eventChoiceSelectionCounts.Clear();
        runData.eventChoiceFailureCounts.Clear();
        runData.eventFollowUpDestination =
            (int)EventFollowUpDestination.NodeMap;
        runData.eventFollowUpBattleIndex = -1;
        ClearPendingInteractionState();
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
        relicManager?.CaptureRunState(runData.relics);
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
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        relicManager ??= gameObject.AddComponent<RelicManager>();
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        bulletSelectionUI ??= FindFirstObjectByType<BulletManagementUI>(
            FindObjectsInactive.Include);
        inventorySelectionUI ??= FindFirstObjectByType<InventoryUI>(
            FindObjectsInactive.Include);
        relicSelectionUI ??= FindFirstObjectByType<RelicInventoryUI>(
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
        resultText ??= FindNamedComponent<TMP_Text>(
            eventCanvasRoot,
            "Text | Event Result");

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

    private string ExpandChoiceTokens(
        string source,
        EventChoiceData choice)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        int choiceIndex = GetChoiceIndex(choice);
        int selections = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            choiceIndex);
        int failures = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceFailureCounts,
            choiceIndex);
        float chance = EventRuntimeRules.GetSuccessChance(choice, failures);
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
        choiceTextFormatter ??= new EventChoiceTextFormatter(
            actionNameColor,
            upgradeKeywordColor,
            removeKeywordColor,
            freeKeywordColor,
            costKeywordColor,
            rewardNameColor,
            unavailableReasonColor);
        return choiceTextFormatter.Format(
            source,
            choice,
            available,
            unavailableReason);
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
        int previousSelections = EventRuntimeRules.GetChoiceProgress(
            runData.eventChoiceSelectionCounts,
            GetChoiceIndex(choice));
        EventEffect reward = EventRuntimeRules.GetActiveEffects(
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
