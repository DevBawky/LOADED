using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static FirstRunGuideContent;

[DisallowMultipleComponent]
public sealed class FirstRunGuideController : MonoBehaviour
{
    private const string CombatGuideKey = "loaded.guide.combat.v1";
    private const string ItemGuideKey = "loaded.guide.item.v1";
    private const string ShopGuideKey = "loaded.guide.shop.v1";
    private const string NodeMapGuideKey = "loaded.guide.node_map.v1";
    private const string EventGuideKey = "loaded.guide.event.v1";
    private const string TreasureGuideKey = "loaded.guide.treasure.v1";
    private const string GuideDisabledKey = "loaded.guide.disabled.v1";
    private const string FirstTutorialRunStartedKey =
        "loaded.guide.first_run_started.v1";
    private const string GuideContentVersionKey =
        "loaded.guide.content_version";
    private const int CurrentGuideContentVersion = 4;
    internal const int GuideSortingOrder = 30000;
    internal const int GuideTooltipSortingOrder = GuideSortingOrder + 1;
    private const float StepAdvanceDelay = 0.45f;
    private const string PreferredGuideFontName = "Bold_Ko SDF";
    private const string FallbackGuideFontName = "Galmuri9 SDF";

    private static FirstRunGuideController activeInstance;

    internal static bool IsGuidePanelOpen => activeInstance != null
        && activeInstance.isActiveAndEnabled
        && activeInstance.card != null
        && activeInstance.card.activeInHierarchy;

    internal static bool IsMandatoryGuideInputBlocked =>
        activeInstance != null
        && activeInstance.isActiveAndEnabled
        && activeInstance.isMandatoryGuideSession
        && activeInstance.card != null
        && activeInstance.card.activeInHierarchy;

    internal static bool IsGuideElement(Transform candidate)
    {
        return IsGuidePanelOpen && candidate != null
            && activeInstance.guideRoot != null
            && candidate.IsChildOf(activeInstance.guideRoot);
    }

    private enum GuideMode
    {
        None,
        Combat,
        Item,
        Shop,
        NodeMap,
        Event,
        Treasure
    }

    private StateManager stateManager;
    private PlayerMove playerMove;
    private PlayerShoot playerShoot;
    private PlayerCylinderUI cylinderUI;
    private PlayerInventory playerInventory;
    private DeckManager deckManager;
    private BoardManager boardManager;
    private WaveManager waveManager;
    private ShopManager shopManager;
    private Canvas rootCanvas;
    private TMP_FontAsset guideFont;

    private RectTransform guideRoot;
    private Image inputBlocker;
    private RectTransform highlight;
    private Image highlightImage;
    private FirstRunGuideHighlightPresenter highlightPresenter;
    private GameObject card;
    private TMP_Text cardStepText;
    private TMP_Text cardTitleText;
    private TMP_Text cardBodyText;
    private GameObject cardMissionPanel;
    private TMP_Text cardMissionText;
    private Button cardBackButton;
    private Button cardExitButton;
    private Toggle neverShowToggle;
    private Button continueButton;
    private TMP_Text continueButtonText;
    private Button missionGuideButton;
    private Button missionNextButton;
    private FirstRunGuideVideoPresenter videoPresenter;
    private GameObject missionBar;
    private TMP_Text missionText;
    private Coroutine missionScaleCoroutine;
    private GameObject warningDemoRoot;
    private Button warningSoundButton;
    private Image warningDemoTileImage;
    private Image warningDemoAttackIcon;
    private Image warningDemoReadyGlow;
    private Sprite warningDemoNormalSprite;
    private Sprite warningDemoPreparedSprite;
    private Material warningDemoReadyMaterial;
    private Coroutine warningDemoCoroutine;
    private GameObject debuffLegendRoot;
    private readonly Image[] debuffLegendIcons = new Image[4];

    private GuideMode mode;
    private int combatSystemPageIndex;
    private int combatStepIndex;
    private int combatReviewStepIndex = -1;
    private int shopPageIndex;
    private int nodeMapPageIndex;
    private int eventPageIndex;
    private int treasurePageIndex;
    private bool combatGuideStarted;
    private bool shopGuideStarted;
    private bool nodeMapGuideStarted;
    private bool eventGuideStarted;
    private bool treasureGuideStarted;
    private bool showingCombatSystemPages;
    private bool missionActive;
    private bool pendingAdvance;
    private float advanceAt;
    private bool completionCardOpen;
    private bool isMandatoryGuideSession;
    private string activeTargetName;
    private TargetKind activeTargetKind;
    private RectTransform activeTarget;
    private RectTransform activeSecondaryTarget;
    private EnemyController activeTutorialEnemy;

    private bool moved;
    private bool rotated;
    private bool waited;
    private bool enemyActionInspected;
    private int reloadCount;
    private bool chamberEjected;
    private bool bulletInfoInspected;
    private bool cylinderReordered;
    private bool damagePreviewInspected;
    private bool kickPerformed;
    private bool fired;
    private bool itemUsed;
    private bool tutorialStunItemGranted;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInLoadedScene()
    {
        InstallIfNeeded();
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        InstallIfNeeded();
    }

    private static void InstallIfNeeded()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool isNodeMapScene = sceneName == "NodeMap";
        bool isEventScene = sceneName == "Event";
        bool isTreasureScene = sceneName == "Treasure";
        StateManager manager = FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        if ((manager == null && !isNodeMapScene
                && !isEventScene && !isTreasureScene)
            || FindFirstObjectByType<FirstRunGuideController>(
                FindObjectsInactive.Include) != null)
        {
            return;
        }

        Canvas selectedCanvas = null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isRootCanvas)
            {
                continue;
            }

            RectTransform[] descendants =
                canvas.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform descendant in descendants)
            {
                if (IsGuideCanvasAnchor(sceneName, descendant.name))
                {
                    selectedCanvas = canvas;
                    break;
                }
            }

            if (selectedCanvas != null)
            {
                break;
            }
        }

        if (selectedCanvas != null)
        {
            selectedCanvas.gameObject.AddComponent<FirstRunGuideController>();
        }
    }

    internal static bool IsGuideCanvasAnchor(
        string sceneName,
        string objectName)
    {
        return objectName == "Panel | MainGame"
            || sceneName == "NodeMap"
                && objectName == "Scroll View | Map"
            || sceneName == "Shop"
                && objectName == "Panel | Shop"
            || sceneName == "Event"
                && objectName == "Text | Event Dialogue"
            || sceneName == "Treasure"
                && objectName == "Button | Treasure Chest";
    }

    public static bool TrySkipActiveGuide()
    {
        if (activeInstance == null || activeInstance.mode == GuideMode.None)
        {
            return false;
        }

        if (IsMandatoryGuideInputBlocked)
        {
            // Consume Escape without closing or opening another panel while
            // an automatically started early-game tutorial is in progress.
            return true;
        }

        activeInstance.SkipCurrentGuide();
        return true;
    }

    public static void ResetSavedProgress()
    {
        PlayerPrefs.DeleteKey(CombatGuideKey);
        PlayerPrefs.DeleteKey(ItemGuideKey);
        PlayerPrefs.DeleteKey(ShopGuideKey);
        PlayerPrefs.DeleteKey(NodeMapGuideKey);
        PlayerPrefs.DeleteKey(EventGuideKey);
        PlayerPrefs.DeleteKey(TreasureGuideKey);
        PlayerPrefs.DeleteKey(GuideDisabledKey);
        PlayerPrefs.DeleteKey(FirstTutorialRunStartedKey);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        activeInstance = this;
        EnsureCurrentGuideContentVersion();
        ResolveReferences();
        ResolveFont();
        BuildInterface();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopVideo();
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(HandleContinue);
        }

        cardBackButton?.onClick.RemoveListener(HandleBack);
        cardExitButton?.onClick.RemoveListener(SkipCurrentGuide);
        missionGuideButton?.onClick.RemoveListener(ShowCurrentMissionGuide);
        missionNextButton?.onClick.RemoveListener(AdvanceCurrentMission);
        warningSoundButton?.onClick.RemoveListener(PlayWarningDemo);

        videoPresenter?.Dispose();
        videoPresenter = null;
    }

    private void Update()
    {
        ResolveReferences();
        Subscribe();

        if (mode == GuideMode.None)
        {
            TryStartNodeMapGuide();
            TryStartCombatGuide();
            TryStartItemGuide();
            TryStartShopGuide();
            TryStartEventGuide();
            TryStartTreasureGuide();
        }
        else if ((mode == GuideMode.Combat || mode == GuideMode.Item)
            && stateManager != null
            && stateManager.CurrentState != GameFlowState.Battle)
        {
            if (mode == GuideMode.Combat
                && stateManager.CurrentState == GameFlowState.BattleClear)
            {
                SaveCompleted(CombatGuideKey);
            }

            HideGuide(false);
        }
        else if (mode == GuideMode.Shop
            && stateManager != null
            && stateManager.CurrentState != GameFlowState.Shop)
        {
            HideGuide(false);
        }

        if (pendingAdvance && Time.unscaledTime >= advanceAt
            && IsPresentationSettled())
        {
            pendingAdvance = false;

            if (mode == GuideMode.Combat)
            {
                combatStepIndex++;
                ShowNextCombatStep();
            }
            else if (mode == GuideMode.Item)
            {
                FinishItemGuide();
            }
        }

        if (missionActive && !pendingAdvance && IsPresentationSettled())
        {
            RefreshMissionBar();
        }

        UpdateHighlight();
    }

    private void TryStartCombatGuide()
    {
        if (combatGuideStarted || IsGuideDisabled()
            || stateManager == null || playerMove == null
            || stateManager.CurrentState != GameFlowState.Battle
            || stateManager.CurrentStageIndex != 0
            || !IsFirstBattleNode()
            || !stateManager.IsFreshRun
            || !playerMove.CanStartAction
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        combatGuideStarted = true;
        moved = false;
        rotated = false;
        waited = false;
        enemyActionInspected = false;
        reloadCount = 0;
        chamberEjected = false;
        bulletInfoInspected = false;
        cylinderReordered = false;
        damagePreviewInspected = false;
        kickPerformed = false;
        fired = false;
        itemUsed = false;
        tutorialStunItemGranted = false;
        mode = GuideMode.Combat;
        isMandatoryGuideSession = IsFirstGuideSession(CombatGuideKey);
        combatSystemPageIndex = 0;
        combatStepIndex = 0;
        combatReviewStepIndex = -1;
        showingCombatSystemPages = true;
        ShowCombatSystemPage();
    }

    private void TryStartNodeMapGuide()
    {
        if (nodeMapGuideStarted
            || SceneManager.GetActiveScene().name != "NodeMap")
        {
            return;
        }

        if (IsGuideDisabled() || RunSaveSystem.HasValidSave)
        {
            nodeMapGuideStarted = true;
            return;
        }

        if (!IsInitialNodeSelection()
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        nodeMapGuideStarted = true;
        mode = GuideMode.NodeMap;
        isMandatoryGuideSession = IsFirstGuideSession(NodeMapGuideKey);
        nodeMapPageIndex = 0;
        ShowNodeMapPage();
    }

    private void TryStartItemGuide()
    {
        if (IsGuideDisabled() || !IsCompleted(CombatGuideKey)
            || IsCompleted(ItemGuideKey)
            || stateManager == null || playerMove == null
            || playerInventory == null
            || stateManager.CurrentState != GameFlowState.Battle
            || !stateManager.IsFreshRun
            || !HasInventoryItem()
            || !playerMove.CanStartAction
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        itemUsed = false;
        mode = GuideMode.Item;
        isMandatoryGuideSession = IsFirstGuideSession(ItemGuideKey);
        SetActiveTarget("Layout | Inventory", TargetKind.Named);
        ShowCard(
            "ITEM GUIDE",
            "아이템 사용",
            "<color=#FF5757><b>1/2/3 키</b></color> 또는 <color=#FF5757><b>인벤토리 슬롯 클릭</b></color>으로 아이템을 사용합니다.\n<color=#FFD05A><b>사용 조건이 맞지 않으면 소비되지 않습니다.</b></color>\n적이 나온 뒤 다시 시도하세요.",
            "보유 아이템 한 번 사용",
            null,
            "미션 시작");
    }

    private void TryStartShopGuide()
    {
        if (shopGuideStarted || IsGuideDisabled() || stateManager == null
            || stateManager.CurrentState != GameFlowState.Shop
            || !stateManager.IsFreshRun
            || !IsFirstActiveNodeOfType(NodeMapNodeType.Shop)
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        shopGuideStarted = true;
        mode = GuideMode.Shop;
        isMandatoryGuideSession = IsFirstGuideSession(ShopGuideKey);
        shopPageIndex = 0;
        ShowShopPage();
    }

    private void TryStartEventGuide()
    {
        if (eventGuideStarted || IsGuideDisabled()
            || SceneManager.GetActiveScene().name != "Event"
            || !IsFirstActiveNodeOfType(NodeMapNodeType.Event)
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        eventGuideStarted = true;
        mode = GuideMode.Event;
        isMandatoryGuideSession = IsFirstGuideSession(EventGuideKey);
        eventPageIndex = 0;
        ShowEventPage();
    }

    private void TryStartTreasureGuide()
    {
        if (treasureGuideStarted || IsGuideDisabled()
            || SceneManager.GetActiveScene().name != "Treasure"
            || !IsFirstActiveNodeOfType(NodeMapNodeType.Treasure)
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        treasureGuideStarted = true;
        mode = GuideMode.Treasure;
        isMandatoryGuideSession = IsFirstGuideSession(TreasureGuideKey);
        treasurePageIndex = 0;
        ShowTreasurePage();
    }

    private void ShowCombatSystemPage()
    {
        if (combatSystemPageIndex >= CombatSystemPages.Length)
        {
            showingCombatSystemPages = false;
            combatStepIndex = 0;
            ShowNextCombatStep();
            return;
        }

        GuidePage page = CombatSystemPages[combatSystemPageIndex];
        SetActiveTarget(page.TargetName, page.TargetKind);
        ShowCard(
            $"SYSTEM GUIDE {combatSystemPageIndex + 1}/{CombatSystemPages.Length}",
            page.Title,
            page.Description,
            null,
            page.VideoPath,
            combatSystemPageIndex == CombatSystemPages.Length - 1
                ? "미션으로"
                : "다음");
    }

    private void ShowNextCombatStep()
    {
        combatReviewStepIndex = -1;

        while (combatStepIndex < CombatSteps.Length
            && IsCombatStepSatisfied(CombatSteps[combatStepIndex].Step))
        {
            combatStepIndex++;
        }

        if (combatStepIndex >= CombatSteps.Length)
        {
            FinishCombatGuide();
            return;
        }

        GuideStepDefinition step = CombatSteps[combatStepIndex];
        SetActiveTarget(step.TargetName, step.TargetKind);
        ShowCard(
            $"MISSION {combatStepIndex + 1}/{CombatSteps.Length}",
            step.Title,
            step.Description,
            GetMissionText(step),
            step.VideoPath,
            "미션 시작");
    }

    private void ShowCombatReviewStep()
    {
        if (combatReviewStepIndex < 0
            || combatReviewStepIndex >= combatStepIndex
            || combatReviewStepIndex >= CombatSteps.Length)
        {
            combatReviewStepIndex = -1;
            ShowNextCombatStep();
            return;
        }

        GuideStepDefinition step = CombatSteps[combatReviewStepIndex];
        SetActiveTarget(step.TargetName, step.TargetKind);
        ShowCard(
            $"MISSION REVIEW {combatReviewStepIndex + 1}/{CombatSteps.Length}",
            step.Title,
            step.Description,
            GetMissionText(step),
            step.VideoPath,
            "다음");
    }

    private void ShowShopPage()
    {
        if (shopPageIndex >= ShopPages.Length)
        {
            CommitNeverShowPreference();
            SaveCompleted(ShopGuideKey);
            HideGuide(false);
            return;
        }

        GuidePage page = ShopPages[shopPageIndex];
        SetActiveTarget(page.TargetName, page.TargetKind);
        ShowCard(
            $"SHOP GUIDE {shopPageIndex + 1}/{ShopPages.Length}",
            page.Title,
            page.Description,
            null,
            page.VideoPath,
            shopPageIndex == ShopPages.Length - 1 ? "완료" : "다음");
    }

    private void ShowNodeMapPage()
    {
        if (nodeMapPageIndex >= NodeMapPages.Length)
        {
            SaveCompleted(NodeMapGuideKey);
            HideGuide(false);
            return;
        }

        GuidePage page = NodeMapPages[nodeMapPageIndex];
        SetActiveTarget(page.TargetName, page.TargetKind);
        ShowCard(
            $"NODE MAP GUIDE {nodeMapPageIndex + 1}/{NodeMapPages.Length}",
            page.Title,
            page.Description,
            null,
            page.VideoPath,
            nodeMapPageIndex == NodeMapPages.Length - 1 ? "확인" : "다음");
    }

    private void ShowEventPage()
    {
        if (eventPageIndex >= EventPages.Length)
        {
            CommitNeverShowPreference();
            SaveCompleted(EventGuideKey);
            HideGuide(false);
            return;
        }

        GuidePage page = EventPages[eventPageIndex];
        SetActiveTarget(page.TargetName, page.TargetKind);
        ShowCard(
            $"EVENT GUIDE {eventPageIndex + 1}/{EventPages.Length}",
            page.Title,
            page.Description,
            null,
            page.VideoPath,
            eventPageIndex == EventPages.Length - 1 ? "확인" : "다음");
    }

    private void ShowTreasurePage()
    {
        if (treasurePageIndex >= TreasurePages.Length)
        {
            CommitNeverShowPreference();
            SaveCompleted(TreasureGuideKey);
            HideGuide(false);
            return;
        }

        GuidePage page = TreasurePages[treasurePageIndex];
        SetActiveTarget(page.TargetName, page.TargetKind);
        ShowCard(
            $"TREASURE GUIDE {treasurePageIndex + 1}/{TreasurePages.Length}",
            page.Title,
            page.Description,
            null,
            page.VideoPath,
            treasurePageIndex == TreasurePages.Length - 1
                ? "확인"
                : "다음");
    }

    private void ShowCard(
        string stepLabel,
        string title,
        string description,
        string mission,
        string videoPath,
        string continueLabel)
    {
        if (guideRoot == null)
        {
            return;
        }

        guideRoot.gameObject.SetActive(true);
        guideRoot.SetAsLastSibling();
        inputBlocker.gameObject.SetActive(true);
        inputBlocker.raycastTarget = true;
        card.SetActive(true);
        RefreshMandatoryDismissControls();
        missionBar.SetActive(false);
        missionActive = false;
        pendingAdvance = false;
        cardStepText.text = RemoveBoldTags(stepLabel);
        cardTitleText.text = RemoveBoldTags(title);
        cardBodyText.text = RemoveBoldTags(description);
        bool hasMission = !string.IsNullOrWhiteSpace(mission);
        cardMissionPanel.SetActive(hasMission);
        cardMissionText.text = RemoveBoldTags(hasMission
            ? "<color=#FFB347><b>MISSION</b></color>  "
                + $"<color=#8FE6FF><b>{mission}</b></color>"
            : string.Empty);
        continueButtonText.text = RemoveBoldTags(continueLabel);
        SetCardVideo(videoPath);
        bool hasVideo = !string.IsNullOrWhiteSpace(videoPath);
        bool showWarningDemo = mode == GuideMode.Combat
            && showingCombatSystemPages
            && combatSystemPageIndex == 1;
        bool showDebuffLegend = mode == GuideMode.Combat
            && showingCombatSystemPages
            && combatSystemPageIndex == CombatSystemPages.Length - 1;
        SetWarningDemoActive(showWarningDemo);
        SetDebuffLegendActive(showDebuffLegend);
        SetAnchors(
            cardBodyText.rectTransform,
            0.08f,
            showWarningDemo ? 0.29f
                : showDebuffLegend ? 0.38f
                : hasMission ? hasVideo ? 0.18f : 0.23f
                    : hasVideo ? 0.13f : 0.17f,
            0.92f,
            hasVideo ? 0.30f : 0.72f);
        SetAnchors(
            (RectTransform)cardMissionPanel.transform,
            0.12f,
            hasVideo ? 0.12f : 0.13f,
            0.88f,
            hasVideo ? 0.17f : 0.20f);
        RefreshBackButton();

        if (mode == GuideMode.Combat || mode == GuideMode.Item)
        {
            SetTutorialInputLocked(true);
        }
    }

    private void RefreshMandatoryDismissControls()
    {
        bool canDismiss = !isMandatoryGuideSession;

        if (cardExitButton != null)
        {
            cardExitButton.interactable = canDismiss;
            cardExitButton.gameObject.SetActive(canDismiss);
        }

        if (neverShowToggle != null)
        {
            if (!canDismiss)
            {
                neverShowToggle.SetIsOnWithoutNotify(false);
            }

            neverShowToggle.interactable = canDismiss;
            neverShowToggle.gameObject.SetActive(canDismiss);
        }
    }

    private void HandleContinue()
    {
        if (mode == GuideMode.NodeMap)
        {
            nodeMapPageIndex++;
            ShowNodeMapPage();
            return;
        }

        if (mode == GuideMode.Shop)
        {
            shopPageIndex++;
            ShowShopPage();
            return;
        }

        if (mode == GuideMode.Event)
        {
            eventPageIndex++;
            ShowEventPage();
            return;
        }

        if (mode == GuideMode.Treasure)
        {
            treasurePageIndex++;
            ShowTreasurePage();
            return;
        }

        if (mode == GuideMode.Combat && combatReviewStepIndex >= 0)
        {
            combatReviewStepIndex++;
            if (combatReviewStepIndex >= combatStepIndex)
            {
                combatReviewStepIndex = -1;
                ShowNextCombatStep();
            }
            else
            {
                ShowCombatReviewStep();
            }

            return;
        }

        if (mode == GuideMode.Combat && showingCombatSystemPages)
        {
            combatSystemPageIndex++;
            ShowCombatSystemPage();
            return;
        }

        if (mode != GuideMode.Combat && mode != GuideMode.Item)
        {
            return;
        }

        if (mode == GuideMode.Combat
            && combatStepIndex >= 0
            && combatStepIndex < CombatSteps.Length
            && CombatSteps[combatStepIndex].Step == CombatStep.UseItem)
        {
            EnsureTutorialStunItem();
        }

        card.SetActive(false);
        ResetWarningDemo();
        inputBlocker.gameObject.SetActive(false);
        inputBlocker.raycastTarget = false;
        missionBar.SetActive(true);
        missionActive = true;
        videoPresenter?.Pause();
        SetTutorialInputLocked(false);
        RefreshMissionBar();
        EvaluateMission();
    }

    private void HandleBack()
    {
        if (completionCardOpen || card == null || !card.activeSelf)
        {
            return;
        }

        if (mode == GuideMode.NodeMap)
        {
            if (nodeMapPageIndex > 0)
            {
                nodeMapPageIndex--;
                ShowNodeMapPage();
            }

            return;
        }

        if (mode == GuideMode.Shop)
        {
            if (shopPageIndex > 0)
            {
                shopPageIndex--;
                ShowShopPage();
            }

            return;
        }

        if (mode == GuideMode.Event)
        {
            if (eventPageIndex > 0)
            {
                eventPageIndex--;
                ShowEventPage();
            }

            return;
        }

        if (mode == GuideMode.Treasure)
        {
            if (treasurePageIndex > 0)
            {
                treasurePageIndex--;
                ShowTreasurePage();
            }

            return;
        }

        if (mode != GuideMode.Combat)
        {
            return;
        }

        if (showingCombatSystemPages)
        {
            if (combatSystemPageIndex > 0)
            {
                combatSystemPageIndex--;
                ShowCombatSystemPage();
            }

            return;
        }

        int visibleStepIndex = combatReviewStepIndex >= 0
            ? combatReviewStepIndex
            : combatStepIndex;
        if (visibleStepIndex > 0)
        {
            combatReviewStepIndex = visibleStepIndex - 1;
            ShowCombatReviewStep();
            return;
        }

        if (CombatSystemPages.Length > 0)
        {
            combatReviewStepIndex = -1;
            showingCombatSystemPages = true;
            combatSystemPageIndex = CombatSystemPages.Length - 1;
            ShowCombatSystemPage();
        }
    }

    private void RefreshBackButton()
    {
        if (cardBackButton == null)
        {
            return;
        }

        bool canGoBack = !completionCardOpen && (mode switch
        {
            GuideMode.NodeMap => nodeMapPageIndex > 0,
            GuideMode.Shop => shopPageIndex > 0,
            GuideMode.Event => eventPageIndex > 0,
            GuideMode.Treasure => treasurePageIndex > 0,
            GuideMode.Combat when showingCombatSystemPages =>
                combatSystemPageIndex > 0,
            GuideMode.Combat => combatStepIndex > 0
                || combatReviewStepIndex > 0
                || CombatSystemPages.Length > 0,
            _ => false
        });
        cardBackButton.gameObject.SetActive(canGoBack);
    }

    private void EvaluateMission()
    {
        if (!missionActive)
        {
            return;
        }

        bool completed = mode switch
        {
            GuideMode.Combat => combatStepIndex >= 0
                && combatStepIndex < CombatSteps.Length
                && IsCombatStepSatisfied(
                    CombatSteps[combatStepIndex].Step),
            GuideMode.Item => itemUsed,
            _ => false
        };

        if (!completed)
        {
            RefreshMissionBar();
            return;
        }

        missionActive = false;
        pendingAdvance = true;
        advanceAt = Time.unscaledTime + StepAdvanceDelay;
        SetMissionPanelText(
            "<color=#76E38A><b>MISSION COMPLETE!</b></color>");
        SetTutorialInputLocked(true);
    }

    private void RefreshMissionBar()
    {
        if (missionText == null)
        {
            return;
        }

        if (mode == GuideMode.Combat
            && combatStepIndex >= 0
            && combatStepIndex < CombatSteps.Length)
        {
            GuideStepDefinition step = CombatSteps[combatStepIndex];
            if (TryGetPriorityMission(step.Step, out PriorityMission priority))
            {
                SetMissionPanelText(
                    "<color=#FF4B4B><b>MISSION</b></color>  "
                    + $"<color=#8FE6FF><b>{priority.Text}</b></color>");
                ApplyMissionTarget(priority.TargetName, priority.TargetKind);
            }
            else
            {
                SetMissionPanelText(
                    "<color=#FFD05A><b>MISSION</b></color>  "
                    + $"<color=#8FE6FF><b>{GetMissionText(step)}</b></color>");
                ApplyMissionTarget(step.TargetName, step.TargetKind);
            }
        }
        else if (mode == GuideMode.Item)
        {
            SetMissionPanelText(
                "<color=#FFD05A><b>MISSION</b></color>  "
                + "<color=#8FE6FF><b>보유 아이템 한 번 사용</b></color>");
        }
    }

    private void SetMissionPanelText(string text)
    {
        text = RemoveBoldTags(text);
        if (missionText == null || missionText.text == text)
        {
            return;
        }

        missionText.text = text;
        if (missionBar == null || !missionBar.activeInHierarchy)
        {
            return;
        }

        if (missionScaleCoroutine != null)
        {
            StopCoroutine(missionScaleCoroutine);
        }

        missionScaleCoroutine = StartCoroutine(PulseMissionPanel());
    }

    private IEnumerator PulseMissionPanel()
    {
        RectTransform panelRect = missionBar.transform as RectTransform;
        if (panelRect == null)
        {
            missionScaleCoroutine = null;
            yield break;
        }

        const float growDuration = 0.1f;
        const float settleDuration = 0.16f;
        Vector3 baseScale = Vector3.one;
        Vector3 emphasizedScale = Vector3.one * 1.08f;
        panelRect.localScale = baseScale;

        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / growDuration);
            panelRect.localScale = Vector3.LerpUnclamped(
                baseScale,
                emphasizedScale,
                1f - Mathf.Pow(1f - progress, 3f));
        }

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / settleDuration);
            panelRect.localScale = Vector3.LerpUnclamped(
                emphasizedScale,
                baseScale,
                1f - Mathf.Pow(1f - progress, 3f));
        }

        panelRect.localScale = baseScale;
        missionScaleCoroutine = null;
    }

    private void AdvanceCurrentMission()
    {
        if (!missionActive || pendingAdvance)
        {
            return;
        }

        missionActive = false;

        if (mode == GuideMode.Combat)
        {
            combatStepIndex++;
            ShowNextCombatStep();
        }
        else if (mode == GuideMode.Item)
        {
            FinishItemGuide();
        }
    }

    private void ShowCurrentMissionGuide()
    {
        if (!missionActive || pendingAdvance)
        {
            return;
        }

        if (mode == GuideMode.Combat
            && combatStepIndex >= 0
            && combatStepIndex < CombatSteps.Length)
        {
            GuideStepDefinition step = CombatSteps[combatStepIndex];
            SetActiveTarget(step.TargetName, step.TargetKind);
            ShowCard(
                $"MISSION {combatStepIndex + 1}/{CombatSteps.Length}",
                step.Title,
                step.Description,
                GetMissionText(step),
                step.VideoPath,
                "미션 재개");
            return;
        }

        if (mode == GuideMode.Item)
        {
            SetActiveTarget("Layout | Inventory", TargetKind.Named);
            ShowCard(
                "ITEM GUIDE",
                "아이템 사용",
                "<color=#FF5757><b>1/2/3 키</b></color> 또는 <color=#FF5757><b>인벤토리 슬롯 클릭</b></color>으로 아이템을 사용합니다.\n<color=#FFD05A><b>사용 조건이 맞지 않으면 소비되지 않습니다.</b></color>\n적이 나온 뒤 다시 시도하세요.",
                "보유 아이템 한 번 사용",
                null,
                "미션 재개");
        }
    }

    private string GetMissionText(GuideStepDefinition step)
    {
        if (step.Step == CombatStep.ReloadThree)
        {
            return $"탄환 3회 장전 ({Mathf.Min(3, reloadCount)}/3)";
        }

        return step.Mission;
    }

    private bool IsCombatStepSatisfied(CombatStep step)
    {
        return step switch
        {
            CombatStep.Move => moved,
            CombatStep.Rotate => rotated,
            CombatStep.Wait => waited,
            CombatStep.InspectEnemyAction => enemyActionInspected,
            CombatStep.ReloadThree => reloadCount >= 3,
            CombatStep.EjectChamber => chamberEjected,
            CombatStep.InspectBulletInfo => bulletInfoInspected,
            CombatStep.ReorderCylinder => cylinderReordered,
            CombatStep.PreviewDamage => damagePreviewInspected,
            CombatStep.UseItem => itemUsed,
            CombatStep.Kick => kickPerformed,
            CombatStep.Fire => fired,
            _ => false
        };
    }

    private void ApplyMissionTarget(string targetName, TargetKind targetKind)
    {
        if (activeTargetName == targetName && activeTargetKind == targetKind)
        {
            return;
        }

        SetActiveTarget(targetName, targetKind);
    }

    private bool TryGetPriorityMission(
        CombatStep step,
        out PriorityMission priority)
    {
        priority = default;
        int loadedBulletCount = deckManager?.LoadedBullets.Count ?? 0;

        // Avoiding guaranteed incoming damage always outranks instructional
        // setup such as facing an enemy, entering range, ejecting, or reloading.
        if (IsPlayerInPreparedAttackDanger())
        {
            priority = new PriorityMission(
                "적의 공격으로부터 회피하세요!",
                null,
                TargetKind.MoveButtons);
            return true;
        }

        switch (step)
        {
            case CombatStep.Move:
                return TryGetMovePriority(out priority);

            case CombatStep.InspectEnemyAction:
                if (!HasInspectableEnemyAction())
                {
                    priority = new PriorityMission(
                        "적이 등장할 때까지 회전해 행동 진행",
                        "Button | Rotate");
                    return true;
                }

                break;

            case CombatStep.ReloadThree:
                if (deckManager != null
                    && reloadCount < 3
                    && (loadedBulletCount >= deckManager.MaxReloadAmount
                        || deckManager.ReloadableBulletCount <= 0))
                {
                    priority = new PriorityMission(
                        "실린더를 발사해 장전 공간 만들기",
                        "Button | Shoot");
                    return true;
                }

                break;

            case CombatStep.EjectChamber:
                if (loadedBulletCount <= 0)
                {
                    priority = new PriorityMission(
                        "제거할 탄환 1발 장전",
                        "Button | Reload");
                    return true;
                }

                break;

            case CombatStep.InspectBulletInfo:
                if (loadedBulletCount <= 0)
                {
                    priority = new PriorityMission(
                        "확인할 탄환 1발 장전",
                        "Button | Reload");
                    return true;
                }

                break;

            case CombatStep.Fire:
                if (loadedBulletCount <= 0)
                {
                    priority = new PriorityMission(
                        "발사할 탄환 1발 장전",
                        "Button | Reload");
                    return true;
                }

                return TryGetAttackTargetPriority(
                    loadedBulletCount,
                    out priority);

            case CombatStep.ReorderCylinder:
                if (loadedBulletCount < 2)
                {
                    priority = new PriorityMission(
                        "실린더에 탄환 2발 이상 장전",
                        "Button | Reload");
                    return true;
                }

                break;

            case CombatStep.PreviewDamage:
                return TryGetAttackTargetPriority(
                    loadedBulletCount,
                    out priority);

            case CombatStep.UseItem:
                if (!HasInventoryItem())
                {
                    EnsureTutorialStunItem();
                    if (!HasInventoryItem())
                    {
                        priority = new PriorityMission(
                            "사용할 아이템이 지급될 때까지 잠시 대기",
                            "Layout | Inventory");
                        return true;
                    }
                }

                break;

            case CombatStep.Kick:
                return TryGetKickPriority(out priority);
        }

        return false;
    }

    private bool TryGetMovePriority(out PriorityMission priority)
    {
        priority = default;
        if (playerMove == null || boardManager == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex))
        {
            return false;
        }

        int facingDirection = GetPlayerFacingDirection();
        bool canMove = false;
        bool canRotateToPush = false;

        for (int direction = -1; direction <= 1; direction += 2)
        {
            int targetTileIndex = playerTileIndex + direction;
            if (targetTileIndex < 0
                || targetTileIndex >= boardManager.BoardCount)
            {
                continue;
            }

            if (waveManager.TryGetEnemyAtTile(
                    targetTileIndex,
                    playerMove == null ? 0 : playerMove.CurrentLaneIndex,
                    out EnemyController _))
            {
                canMove |= direction == facingDirection && playerMove.CanPush;
                canRotateToPush |= direction != facingDirection;
                continue;
            }

            if (!waveManager.IsTileReservedForSpawn(
                    targetTileIndex,
                    playerMove == null ? 0 : playerMove.CurrentLaneIndex))
            {
                canMove = true;
            }
        }

        if (canMove)
        {
            return false;
        }

        if (canRotateToPush && playerMove.CanPush)
        {
            priority = new PriorityMission(
                "이동할 수 있도록 인접한 적 바라보기",
                "Button | Rotate");
        }
        else
        {
            priority = new PriorityMission(
                "이동 경로가 열릴 때까지 회전해 DUEL CLOCK 충전",
                "Button | Rotate");
        }

        return true;
    }

    private bool TryGetAttackTargetPriority(
        int loadedBulletCount,
        out PriorityMission priority)
    {
        priority = default;
        if (loadedBulletCount <= 0)
        {
            priority = new PriorityMission(
                "예상 피해를 확인할 탄환 장전",
                "Button | Reload");
            return true;
        }

        int maximumRange = 1;
        if (deckManager != null)
        {
            foreach (BulletInstance bullet in deckManager.LoadedBullets)
            {
                if (bullet != null)
                {
                    maximumRange = Mathf.Max(maximumRange, bullet.MaxRange);
                }
            }
        }

        int facingDirection = GetPlayerFacingDirection();
        if (HasEnemyInDirection(facingDirection, maximumRange, true))
        {
            return false;
        }

        if (HasEnemyInDirection(-facingDirection, maximumRange, true)
            || !HasEnemyInDirection(facingDirection, int.MaxValue, false)
                && HasLivingEnemy())
        {
            priority = new PriorityMission(
                "공격할 적 바라보기",
                "Button | Rotate");
            return true;
        }

        if (HasLivingEnemy())
        {
            priority = new PriorityMission(
                $"적이 탄환 사거리 {maximumRange}칸 안에 들도록 이동",
                null,
                TargetKind.MoveButtons);
        }
        else
        {
            priority = new PriorityMission(
                "적이 등장할 때까지 회전해 DUEL CLOCK 충전",
                "Button | Rotate");
        }

        return true;
    }

    private bool TryGetKickPriority(out PriorityMission priority)
    {
        priority = default;
        if (!TryGetNearestEnemy(out int direction, out int distance))
        {
            priority = new PriorityMission(
                "적이 등장할 때까지 회전해 DUEL CLOCK 충전",
                "Button | Rotate");
            return true;
        }

        if (distance > 1)
        {
            priority = new PriorityMission(
                "적 바로 앞까지 이동",
                null,
                TargetKind.MoveButtons);
            return true;
        }

        if (direction != GetPlayerFacingDirection())
        {
            priority = new PriorityMission(
                "인접한 적 바라보기",
                "Button | Rotate");
            return true;
        }

        if (playerMove != null && !playerMove.CanPush)
        {
            priority = new PriorityMission(
                $"발차기 재사용까지 회전 ({playerMove.RemainingPushCooldownTurns}회 행동)",
                "Button | Rotate");
            return true;
        }

        return false;
    }

    private bool HasInspectableEnemyAction()
    {
        if (waveManager == null)
        {
            return false;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0
                && enemy.ActionQueueView != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasLivingEnemy()
    {
        if (waveManager == null)
        {
            return false;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerInPreparedAttackDanger()
    {
        if (waveManager == null)
        {
            return false;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.WillPreparedAttackHitPlayer())
            {
                return true;
            }
        }

        return false;
    }

    private bool HasEnemyInDirection(
        int direction,
        int maximumRange,
        bool enforceRange)
    {
        if (playerMove == null || boardManager == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex))
        {
            return false;
        }

        int normalizedDirection = direction >= 0 ? 1 : -1;
        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0
                || !boardManager.TryGetTileIndex(
                    enemy.transform.position,
                    enemy.CurrentLaneIndex,
                    out int enemyTileIndex))
            {
                continue;
            }

            int offset = enemyTileIndex - playerTileIndex;
            if (offset * normalizedDirection > 0
                && (!enforceRange || Mathf.Abs(offset) <= maximumRange))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetNearestEnemy(out int direction, out int distance)
    {
        direction = 0;
        distance = int.MaxValue;
        if (playerMove == null || boardManager == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                playerMove.CurrentLaneIndex,
                out int playerTileIndex))
        {
            return false;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0
                || !boardManager.TryGetTileIndex(
                    enemy.transform.position,
                    enemy.CurrentLaneIndex,
                    out int enemyTileIndex))
            {
                continue;
            }

            int offset = enemyTileIndex - playerTileIndex;
            int candidateDistance = Mathf.Abs(offset);
            int candidateDirection = offset > 0 ? 1 : -1;
            if (offset != 0
                && (candidateDistance < distance
                    || candidateDistance == distance
                    && candidateDirection == GetPlayerFacingDirection()))
            {
                direction = candidateDirection;
                distance = candidateDistance;
            }
        }

        return direction != 0;
    }

    private int GetPlayerFacingDirection()
    {
        return playerMove == null || playerMove.transform.localScale.x >= 0f
            ? 1
            : -1;
    }

    private void FinishCombatGuide()
    {
        CommitNeverShowPreference();
        SaveCompleted(CombatGuideKey);
        SaveCompleted(ItemGuideKey);
        ShowCompletionCard(
            "전투 가이드 완료",
            "이제 <color=#FFD05A><b>DUEL CLOCK, 콤보와 적 행동</b></color>을 확인하며 전투하세요.");
    }

    private void FinishItemGuide()
    {
        CommitNeverShowPreference();
        SaveCompleted(ItemGuideKey);
        ShowCompletionCard(
            "아이템 가이드 완료",
            "아이템은 <color=#FFD05A><b>필요한 순간에 바로 사용</b></color>할 수 있습니다.");
    }

    private void ShowCompletionCard(string title, string description)
    {
        SetActiveTarget(null, TargetKind.Named);
        ShowCard("GUIDE COMPLETE", title, description, null, null, "확인");
        completionCardOpen = true;
        cardBackButton.gameObject.SetActive(false);
        continueButton.onClick.RemoveListener(HandleContinue);
        continueButton.onClick.AddListener(CloseCompletionCard);
    }

    private void CloseCompletionCard()
    {
        completionCardOpen = false;
        continueButton.onClick.RemoveListener(CloseCompletionCard);
        continueButton.onClick.AddListener(HandleContinue);
        HideGuide(true);
    }

    private void SkipCurrentGuide()
    {
        if (isMandatoryGuideSession)
        {
            return;
        }

        if (completionCardOpen)
        {
            CloseCompletionCard();
            return;
        }

        HideGuide(true);
    }

    private void CommitNeverShowPreference()
    {
        if (neverShowToggle == null || !neverShowToggle.isOn)
        {
            return;
        }

        PlayerPrefs.SetInt(GuideDisabledKey, 1);
        PlayerPrefs.SetInt(CombatGuideKey, 1);
        PlayerPrefs.SetInt(ItemGuideKey, 1);
        PlayerPrefs.SetInt(ShopGuideKey, 1);
        PlayerPrefs.SetInt(NodeMapGuideKey, 1);
        PlayerPrefs.SetInt(EventGuideKey, 1);
        PlayerPrefs.SetInt(TreasureGuideKey, 1);
        PlayerPrefs.Save();

        combatGuideStarted = true;
        shopGuideStarted = true;
        nodeMapGuideStarted = true;
        eventGuideStarted = true;
        treasureGuideStarted = true;
    }

    private void HideGuide(bool unlockInput)
    {
        CommitNeverShowPreference();
        GuideMode previousMode = mode;
        mode = GuideMode.None;
        missionActive = false;
        pendingAdvance = false;
        completionCardOpen = false;
        showingCombatSystemPages = false;
        combatReviewStepIndex = -1;
        isMandatoryGuideSession = false;
        activeTarget = null;
        activeSecondaryTarget = null;
        activeTargetName = null;
        StopVideo();
        ResetWarningDemo();

        if (missionScaleCoroutine != null)
        {
            StopCoroutine(missionScaleCoroutine);
            missionScaleCoroutine = null;
        }

        if (missionBar != null)
        {
            missionBar.transform.localScale = Vector3.one;
        }

        if (guideRoot != null)
        {
            guideRoot.gameObject.SetActive(false);
        }

        if (unlockInput
            && (previousMode == GuideMode.Combat
                || previousMode == GuideMode.Item)
            && stateManager != null
            && stateManager.CurrentState == GameFlowState.Battle)
        {
            SetTutorialInputLocked(false);
        }
    }

    private void HandleBehaviourActionStarted(PlayerBehaviourAction action)
    {
        switch (action)
        {
            case PlayerBehaviourAction.MoveLeft:
            case PlayerBehaviourAction.MoveRight:
            case PlayerBehaviourAction.MoveUp:
            case PlayerBehaviourAction.MoveDown:
                moved = true;
                break;
            case PlayerBehaviourAction.Rotate:
                rotated = true;
                break;
            case PlayerBehaviourAction.Wait:
                waited = true;
                break;
            case PlayerBehaviourAction.Reload:
                reloadCount++;
                break;
        }

        EvaluateMission();
    }

    private void HandleEnemyActionInspected(EnemyActionData _)
    {
        enemyActionInspected = true;
        EvaluateMission();
    }

    private void HandleCylinderOrderChanged()
    {
        cylinderReordered = true;
        EvaluateMission();
    }

    private void HandleBulletInspected()
    {
        if (mode != GuideMode.Combat || !missionActive
            || combatStepIndex < 0 || combatStepIndex >= CombatSteps.Length
            || CombatSteps[combatStepIndex].Step
                != CombatStep.InspectBulletInfo)
        {
            return;
        }

        bulletInfoInspected = true;
        EvaluateMission();
    }

    private void HandleLoadedBulletEjected(BulletInstance _)
    {
        chamberEjected = true;
        EvaluateMission();
    }

    private void HandleDamagePreviewShown()
    {
        damagePreviewInspected = true;
        EvaluateMission();
    }

    private void HandleKickPerformed()
    {
        kickPerformed = true;
        EvaluateMission();
    }

    private void HandleBulletFired(BulletInstance _)
    {
        fired = true;
        EvaluateMission();
    }

    private void HandleItemUsed(int _, ItemData __)
    {
        itemUsed = true;

        if (mode != GuideMode.Item && !IsCompleted(ItemGuideKey))
        {
            SaveCompleted(ItemGuideKey);
        }

        EvaluateMission();
    }

    private bool EnsureTutorialStunItem()
    {
        if (playerInventory == null)
        {
            return false;
        }

        if (tutorialStunItemGranted)
        {
            return true;
        }

        shopManager ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
        ItemData stunItem = shopManager?.ResolveSavedItem("StunAll");

        if (stunItem == null)
        {
            Debug.LogWarning(
                "[FirstRunGuide] 튜토리얼 아이템 StunAll을 찾지 못했습니다.");
            return false;
        }

        if (playerInventory.Contains(stunItem))
        {
            tutorialStunItemGranted = true;
            return true;
        }

        if (!playerInventory.TryAdd(stunItem))
        {
            Debug.LogWarning(
                "[FirstRunGuide] 인벤토리가 가득 차 전기충격을 지급하지 못했습니다.");
            return false;
        }

        tutorialStunItemGranted = true;
        return true;
    }

    private void ResolveReferences()
    {
        rootCanvas ??= GetComponent<Canvas>()?.rootCanvas;
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        playerMove ??= FindFirstObjectByType<PlayerMove>(
            FindObjectsInactive.Include);
        playerShoot ??= FindFirstObjectByType<PlayerShoot>(
            FindObjectsInactive.Include);
        cylinderUI ??= FindFirstObjectByType<PlayerCylinderUI>(
            FindObjectsInactive.Include);
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        deckManager ??= FindFirstObjectByType<DeckManager>(
            FindObjectsInactive.Include);
        boardManager ??= FindFirstObjectByType<BoardManager>(
            FindObjectsInactive.Include);
        waveManager ??= FindFirstObjectByType<WaveManager>(
            FindObjectsInactive.Include);
        shopManager ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (subscribed || playerMove == null || playerShoot == null
            || cylinderUI == null || playerInventory == null)
        {
            return;
        }

        playerMove.BehaviourActionStarted += HandleBehaviourActionStarted;
        playerMove.PushPerformed += HandleKickPerformed;
        playerShoot.BehaviourActionStarted += HandleBehaviourActionStarted;
        playerShoot.BulletFired += HandleBulletFired;
        playerShoot.LoadedBulletEjected += HandleLoadedBulletEjected;
        playerShoot.LoadedBulletDamagePreviewShown +=
            HandleDamagePreviewShown;
        cylinderUI.BulletOrderChanged += HandleCylinderOrderChanged;
        playerInventory.ItemUsed += HandleItemUsed;
        InventoryTooltipUI.BulletInspected += HandleBulletInspected;
        EnemyActionTooltipTrigger.ActionInspected +=
            HandleEnemyActionInspected;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.BehaviourActionStarted -= HandleBehaviourActionStarted;
            playerMove.PushPerformed -= HandleKickPerformed;
        }

        if (playerShoot != null)
        {
            playerShoot.BehaviourActionStarted -= HandleBehaviourActionStarted;
            playerShoot.BulletFired -= HandleBulletFired;
            playerShoot.LoadedBulletEjected -= HandleLoadedBulletEjected;
            playerShoot.LoadedBulletDamagePreviewShown -=
                HandleDamagePreviewShown;
        }

        if (cylinderUI != null)
        {
            cylinderUI.BulletOrderChanged -= HandleCylinderOrderChanged;
        }

        if (playerInventory != null)
        {
            playerInventory.ItemUsed -= HandleItemUsed;
        }

        InventoryTooltipUI.BulletInspected -= HandleBulletInspected;

        EnemyActionTooltipTrigger.ActionInspected -=
            HandleEnemyActionInspected;
        subscribed = false;
    }

    private bool HasInventoryItem()
    {
        if (playerInventory == null)
        {
            return false;
        }

        for (int index = 0; index < playerInventory.SlotCount; index++)
        {
            if (playerInventory.GetItem(index) != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPresentationSettled()
    {
        return !LoadingTransitionController.IsTransitioning
            && (playerMove == null
                || !playerMove.IsActing
                && !playerMove.IsEnemyTurnResolving
                && !playerMove.IsShooting)
            && (playerShoot == null || !playerShoot.IsFiring);
    }

    private bool IsFirstBattleNode()
    {
        return NodeMapSaveSystem.TryLoad(out NodeMapRunData mapData)
            ? IsFirstBattleNode(mapData)
            : stateManager != null && stateManager.CurrentBattleIndex == 0;
    }

    internal static bool IsFirstBattleNode(NodeMapRunData mapData)
    {
        if (mapData == null || mapData.activeNodeId < 0
            || mapData.nodes == null || mapData.completedNodeIds == null)
        {
            return false;
        }

        NodeMapNodeData activeNode = null;
        foreach (NodeMapNodeData node in mapData.nodes)
        {
            if (node != null && node.id == mapData.activeNodeId)
            {
                activeNode = node;
                break;
            }
        }

        if (activeNode == null || !IsBattleNodeType(activeNode.type))
        {
            return false;
        }

        foreach (int completedNodeId in mapData.completedNodeIds)
        {
            foreach (NodeMapNodeData node in mapData.nodes)
            {
                if (node != null && node.id == completedNodeId
                    && IsBattleNodeType(node.type))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsInitialNodeSelection()
    {
        return NodeMapSaveSystem.TryLoad(out NodeMapRunData mapData)
            && IsInitialNodeSelection(mapData);
    }

    internal static bool IsInitialNodeSelection(NodeMapRunData mapData)
    {
        if (mapData == null || !mapData.awaitingNodeSelection
            || mapData.activeNodeId >= 0 || mapData.nodes == null
            || mapData.completedNodeIds == null)
        {
            return false;
        }

        foreach (NodeMapNodeData node in mapData.nodes)
        {
            if (node == null || node.id == mapData.currentNodeId)
            {
                continue;
            }

            if (mapData.completedNodeIds.Contains(node.id))
            {
                return false;
            }
        }

        foreach (NodeMapNodeData node in mapData.nodes)
        {
            if (node != null && node.id == mapData.currentNodeId)
            {
                return node.type == NodeMapNodeType.Start;
            }
        }

        return false;
    }

    private static bool IsFirstActiveNodeOfType(NodeMapNodeType nodeType)
    {
        return NodeMapSaveSystem.TryLoad(out NodeMapRunData mapData)
            && IsFirstActiveNodeOfType(mapData, nodeType);
    }

    internal static bool IsFirstActiveNodeOfType(
        NodeMapRunData mapData,
        NodeMapNodeType nodeType)
    {
        if (mapData == null || mapData.activeNodeId < 0
            || mapData.nodes == null || mapData.completedNodeIds == null)
        {
            return false;
        }

        NodeMapNodeData activeNode = null;
        foreach (NodeMapNodeData node in mapData.nodes)
        {
            if (node != null && node.id == mapData.activeNodeId)
            {
                activeNode = node;
                break;
            }
        }

        if (activeNode == null || activeNode.type != nodeType)
        {
            return false;
        }

        foreach (int completedNodeId in mapData.completedNodeIds)
        {
            foreach (NodeMapNodeData node in mapData.nodes)
            {
                if (node != null && node.id == completedNodeId
                    && node.type == nodeType)
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static int ResolveFirstAvailableNodeId(NodeMapRunData mapData)
    {
        if (mapData == null || mapData.nodes == null)
        {
            return -1;
        }

        foreach (NodeMapNodeData node in mapData.nodes)
        {
            if (node != null && node.id == mapData.currentNodeId
                && node.nextNodeIds != null && node.nextNodeIds.Count > 0)
            {
                return node.nextNodeIds[0];
            }
        }

        return -1;
    }

    private static bool IsBattleNodeType(NodeMapNodeType nodeType)
    {
        return nodeType == NodeMapNodeType.NormalBattle
            || nodeType == NodeMapNodeType.EliteBattle
            || nodeType == NodeMapNodeType.SpecialBattle
            || nodeType == NodeMapNodeType.Boss;
    }

    private void SetTutorialInputLocked(bool locked)
    {
        playerMove?.SetInputLocked(locked);
    }

    private static bool IsCompleted(string key)
    {
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    private static bool IsFirstGuideSession(string guideKey)
    {
        return !IsCompleted(guideKey);
    }

    private static bool IsGuideDisabled()
    {
        return PlayerPrefs.GetInt(GuideDisabledKey, 0) != 0;
    }

    internal static bool RequiresGuideProgressReset(int storedVersion)
    {
        return storedVersion < CurrentGuideContentVersion;
    }

    private static void EnsureCurrentGuideContentVersion()
    {
        int storedVersion = PlayerPrefs.GetInt(GuideContentVersionKey, 0);
        if (!RequiresGuideProgressReset(storedVersion))
        {
            return;
        }

        ResetSavedProgress();
        PlayerPrefs.SetInt(
            GuideContentVersionKey,
            CurrentGuideContentVersion);
        PlayerPrefs.Save();
    }

    private static void SaveCompleted(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private void SetActiveTarget(string targetName, TargetKind targetKind)
    {
        activeTargetName = targetName;
        activeTargetKind = targetKind;
        activeSecondaryTarget = null;
        activeTutorialEnemy = targetKind == TargetKind.TutorialEnemyAction
            ? FindTutorialEnemy()
            : null;
        activeTarget = ResolveActiveTarget();
    }

    private RectTransform ResolveActiveTarget()
    {
        if (activeTargetKind == TargetKind.Cylinder)
        {
            return cylinderUI == null ? null : cylinderUI.CylinderTransform;
        }

        if (activeTargetKind == TargetKind.TutorialEnemyAction)
        {
            activeTutorialEnemy ??= FindTutorialEnemy();
            if (activeTutorialEnemy == null)
            {
                return null;
            }

            foreach (RectTransform candidate in
                     activeTutorialEnemy.GetComponentsInChildren<RectTransform>(
                         true))
            {
                if (candidate != null
                    && candidate.name == activeTargetName
                    && candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return null;
        }

        if (activeTargetKind == TargetKind.MoveButtons)
        {
            activeSecondaryTarget = ResolveNamedTarget("Button | Move R");
            return ResolveNamedTarget("Button | Move L");
        }

        if (activeTargetKind == TargetKind.AvailableNode)
        {
            return ResolveAvailableNodeTarget();
        }

        if (string.IsNullOrWhiteSpace(activeTargetName))
        {
            return null;
        }


        return ResolveNamedTarget(activeTargetName);
    }

    private RectTransform ResolveNamedTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        RectTransform best = null;
        float bestArea = -1f;

        foreach (RectTransform candidate in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.name != targetName
                || !candidate.gameObject.activeInHierarchy
                || guideRoot != null && candidate.IsChildOf(guideRoot))
            {
                continue;
            }

            float area = Mathf.Abs(candidate.rect.width * candidate.rect.height
                * candidate.lossyScale.x * candidate.lossyScale.y);
            if (area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

    private RectTransform ResolveAvailableNodeTarget()
    {
        if (!NodeMapSaveSystem.TryLoad(out NodeMapRunData mapData))
        {
            return null;
        }

        int nodeId = ResolveFirstAvailableNodeId(mapData);
        if (nodeId < 0)
        {
            return null;
        }

        string namePrefix = $"Node {nodeId} |";
        foreach (RectTransform candidate in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (candidate != null
                && candidate.name.StartsWith(
                    namePrefix,
                    StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static EnemyController FindTutorialEnemy()
    {
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                return enemy;
            }
        }

        return null;
    }

    private void UpdateHighlight()
    {
        if (highlight == null || guideRoot == null
            || !guideRoot.gameObject.activeSelf)
        {
            return;
        }

        if (activeTarget == null || !activeTarget.gameObject.activeInHierarchy)
        {
            activeTarget = ResolveActiveTarget();
        }

        if (activeTargetKind == TargetKind.MoveButtons
            && (activeSecondaryTarget == null
                || !activeSecondaryTarget.gameObject.activeInHierarchy))
        {
            activeSecondaryTarget = ResolveNamedTarget("Button | Move R");
        }

        highlightPresenter?.Present(
            activeTarget,
            activeSecondaryTarget,
            Time.unscaledTime);
    }

    private void ResolveFont()
    {
        guideFont = FindLoadedFontAsset(PreferredGuideFontName)
            ?? FindLoadedFontAsset(FallbackGuideFontName);

        if (guideFont != null)
        {
            return;
        }

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (text != null && text.font != null)
            {
                guideFont = text.font;
                return;
            }
        }
    }

    private static TMP_FontAsset FindLoadedFontAsset(string fontAssetName)
    {
        foreach (TMP_FontAsset fontAsset
                 in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (fontAsset != null && fontAsset.name == fontAssetName)
            {
                return fontAsset;
            }
        }

        return null;
    }

    private void BuildInterface()
    {
        if (rootCanvas == null)
        {
            return;
        }

        FirstRunGuideRuntimeView view = FirstRunGuideRuntimeView.Create(
            rootCanvas,
            guideFont,
            GuideSortingOrder);
        if (view == null)
        {
            return;
        }

        guideRoot = view.Root;
        inputBlocker = view.InputBlocker;
        highlight = view.Highlight;
        highlightImage = view.HighlightImage;
        card = view.Card;
        cardStepText = view.CardStepText;
        cardTitleText = view.CardTitleText;
        cardBodyText = view.CardBodyText;
        cardMissionPanel = view.CardMissionPanel;
        cardMissionText = view.CardMissionText;
        cardBackButton = view.CardBackButton;
        cardExitButton = view.CardExitButton;
        neverShowToggle = view.NeverShowToggle;
        continueButton = view.ContinueButton;
        continueButtonText = view.ContinueButtonText;
        missionGuideButton = view.MissionGuideButton;
        missionNextButton = view.MissionNextButton;
        missionBar = view.MissionBar;
        missionText = view.MissionText;
        warningDemoRoot = view.WarningDemoRoot;
        warningSoundButton = view.WarningSoundButton;
        warningDemoTileImage = view.WarningDemoTileImage;
        warningDemoAttackIcon = view.WarningDemoAttackIcon;
        warningDemoReadyGlow = view.WarningDemoReadyGlow;
        debuffLegendRoot = view.DebuffLegendRoot;
        for (int i = 0; i < debuffLegendIcons.Length; i++)
        {
            debuffLegendIcons[i] = view.DebuffLegendIcons[i];
        }

        highlightPresenter = new FirstRunGuideHighlightPresenter(
            rootCanvas,
            highlight,
            highlightImage);
        videoPresenter = new FirstRunGuideVideoPresenter(
            view.VideoFrame,
            view.VideoDisplay,
            view.VideoLoadingText,
            view.VideoAspect,
            view.VideoPlayer,
            this);

        warningSoundButton.onClick.AddListener(PlayWarningDemo);
        cardExitButton.onClick.AddListener(SkipCurrentGuide);
        cardBackButton.onClick.AddListener(HandleBack);
        continueButton.onClick.AddListener(HandleContinue);
        missionGuideButton.onClick.AddListener(ShowCurrentMissionGuide);
        missionNextButton.onClick.AddListener(AdvanceCurrentMission);

    }

    private void SetCardVideo(string relativePath)
    {
        bool hasVideo = !string.IsNullOrWhiteSpace(relativePath);
        SetAnchors(
            cardBodyText.rectTransform,
            0.08f,
            hasVideo ? 0.13f : 0.23f,
            0.92f,
            hasVideo ? 0.29f : 0.76f);
        videoPresenter?.Show(relativePath);
    }

    private void StopVideo()
    {
        videoPresenter?.Stop();
    }

    private void SetWarningDemoActive(bool active)
    {
        ResetWarningDemo();
        if (warningDemoRoot == null)
        {
            return;
        }

        warningDemoRoot.SetActive(active);
        if (!active)
        {
            return;
        }

        ResolveWarningDemoVisuals();
        ApplyWarningDemoRestState();
    }

    private void ResolveWarningDemoVisuals()
    {
        EnemyActionQueueUI queueUI = FindFirstObjectByType<EnemyActionQueueUI>(
            FindObjectsInactive.Include);
        if (queueUI != null)
        {
            warningDemoNormalSprite = queueUI.NormalQueueSprite;
            warningDemoPreparedSprite = queueUI.PreparedQueueSprite;
            warningDemoReadyMaterial = queueUI.QueueReadyMaterial;
            if (warningDemoReadyGlow != null)
            {
                warningDemoReadyGlow.material = warningDemoReadyMaterial;
            }
        }

        EnemyActionData meleeAction = null;
        if (waveManager != null)
        {
            foreach (EnemyController enemy in waveManager.ActiveEnemies)
            {
                if (enemy?.Data == null)
                {
                    continue;
                }

                foreach (EnemyActionData action in enemy.Data.Actions)
                {
                    if (action != null
                        && action.ActionType == EnemyActionType.MeleeAttack
                        && action.Icon != null)
                    {
                        meleeAction = action;
                        break;
                    }
                }

                if (meleeAction != null)
                {
                    break;
                }
            }
        }

        if (meleeAction == null)
        {
            foreach (EnemyActionData action in
                     Resources.FindObjectsOfTypeAll<EnemyActionData>())
            {
                if (action != null
                    && action.ActionType == EnemyActionType.MeleeAttack
                    && action.Icon != null)
                {
                    meleeAction = action;
                    break;
                }
            }
        }

        if (warningDemoAttackIcon != null)
        {
            warningDemoAttackIcon.sprite = meleeAction?.Icon;
            warningDemoAttackIcon.gameObject.SetActive(
                warningDemoAttackIcon.sprite != null);
        }
    }

    private void SetDebuffLegendActive(bool active)
    {
        if (debuffLegendRoot == null)
        {
            return;
        }

        debuffLegendRoot.SetActive(active);
        if (!active)
        {
            return;
        }

        EnemyController enemy = FindFirstObjectByType<EnemyController>(
            FindObjectsInactive.Include);
        StatusEffectController statusEffects = enemy != null
            ? enemy.GetComponent<StatusEffectController>()
            : FindFirstObjectByType<StatusEffectController>(
                FindObjectsInactive.Include);
        StatusEffectType[] types =
        {
            StatusEffectType.Mark,
            StatusEffectType.Poison,
            StatusEffectType.Stun,
            StatusEffectType.Weakness
        };

        for (int i = 0; i < debuffLegendIcons.Length; i++)
        {
            Image icon = debuffLegendIcons[i];
            if (icon == null)
            {
                continue;
            }

            icon.sprite = statusEffects != null
                ? statusEffects.GetStatusIconSprite(types[i])
                : null;
            DebuffIconUI tooltipIcon = icon.GetComponent<DebuffIconUI>();
            if (tooltipIcon != null)
            {
                tooltipIcon.Initialize(icon.sprite, 1, types[i], null);
            }

            icon.gameObject.SetActive(icon.sprite != null);
        }
    }

    private void PlayWarningDemo()
    {
        if (warningDemoCoroutine != null || warningDemoRoot == null
            || !warningDemoRoot.activeInHierarchy)
        {
            return;
        }

        warningDemoCoroutine = StartCoroutine(PlayWarningDemoRoutine());
    }

    private IEnumerator PlayWarningDemoRoutine()
    {
        warningSoundButton.interactable = false;
        if (warningDemoTileImage != null)
        {
            warningDemoTileImage.sprite = warningDemoPreparedSprite;
            warningDemoTileImage.color = Color.white;
        }

        if (warningDemoReadyGlow != null)
        {
            warningDemoReadyGlow.material = warningDemoReadyMaterial;
            warningDemoReadyGlow.color = Color.white;
            warningDemoReadyGlow.gameObject.SetActive(
                warningDemoReadyMaterial != null);
        }

        SoundManager.PlaySfx("SFX_EnemyReady");
        float elapsed = 0f;
        const float previewDuration = 1.5f;

        while (elapsed < previewDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        warningDemoCoroutine = null;
        ApplyWarningDemoRestState();
    }

    private void ResetWarningDemo()
    {
        if (warningDemoCoroutine != null)
        {
            StopCoroutine(warningDemoCoroutine);
            warningDemoCoroutine = null;
        }

        ApplyWarningDemoRestState();
    }

    private void ApplyWarningDemoRestState()
    {
        if (warningSoundButton != null)
        {
            warningSoundButton.interactable = true;
        }

        if (warningDemoTileImage != null)
        {
            warningDemoTileImage.sprite = warningDemoNormalSprite;
            warningDemoTileImage.color = Color.white;
            warningDemoTileImage.rectTransform.localScale = Vector3.one;
        }

        if (warningDemoReadyGlow != null)
        {
            warningDemoReadyGlow.gameObject.SetActive(false);
        }
    }

    private static string RemoveBoldTags(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : value.Replace("<b>", string.Empty)
                .Replace("</b>", string.Empty);
    }

    private static void SetAnchors(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
