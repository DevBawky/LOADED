using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class FirstRunGuideController : MonoBehaviour
{
    private const string CombatGuideKey = "loaded.guide.combat.v1";
    private const string ItemGuideKey = "loaded.guide.item.v1";
    private const string ShopGuideKey = "loaded.guide.shop.v1";
    private const string GuideDisabledKey = "loaded.guide.disabled.v1";
    private const float StepAdvanceDelay = 0.45f;
    private const string PreferredGuideFontName = "Bold_Ko SDF";
    private const string FallbackGuideFontName = "Galmuri9 SDF";

    private static FirstRunGuideController activeInstance;

    private enum GuideMode
    {
        None,
        Combat,
        Item,
        Shop
    }

    private enum CombatStep
    {
        Move,
        Rotate,
        Wait,
        InspectEnemyAction,
        ReloadThree,
        InspectBulletInfo,
        ReorderCylinder,
        PreviewDamage,
        UseItem,
        Kick,
        Fire
    }

    private enum TargetKind
    {
        Named,
        Cylinder,
        TutorialEnemyAction,
        MoveButtons
    }

    private readonly struct GuideStepDefinition
    {
        public GuideStepDefinition(
            CombatStep step,
            string title,
            string description,
            string mission,
            string videoPath,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Step = step;
            Title = title;
            Description = description;
            Mission = mission;
            VideoPath = videoPath;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public CombatStep Step { get; }
        public string Title { get; }
        public string Description { get; }
        public string Mission { get; }
        public string VideoPath { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    private readonly struct GuidePage
    {
        public GuidePage(
            string title,
            string description,
            string videoPath,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Title = title;
            Description = description;
            VideoPath = videoPath;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public string Title { get; }
        public string Description { get; }
        public string VideoPath { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    private readonly struct PriorityMission
    {
        public PriorityMission(
            string text,
            string targetName,
            TargetKind targetKind = TargetKind.Named)
        {
            Text = text;
            TargetName = targetName;
            TargetKind = targetKind;
        }

        public string Text { get; }
        public string TargetName { get; }
        public TargetKind TargetKind { get; }
    }

    private static readonly GuidePage[] CombatSystemPages =
    {
        new GuidePage(
            "턴의 기본",
            "<color=#FFD05A><b>행동 1회마다 1턴</b></color>이 흐릅니다.\n이동, 회전, 대기, 장전, 발사를 하면 <color=#FFD05A><b>적도 바로 행동</b></color>합니다.\n행동 전에 <color=#FFD05A><b>적 아래의 행동 아이콘</b></color>을 확인하세요.",
            "Videos/Wait.mp4",
            "Panel | Behaviour Tile"),
        new GuidePage(
            "적의 공격 예고",
            "공격 준비 시 <color=#FF5757><b>경고음</b></color>이 울립니다.\n<color=#FF5757><b>적 아래의 행동 패널</b></color>도 붉어집니다.\n원거리 공격은 경로와 범위도 표시됩니다.\n다음 턴 전에 피하거나 대비하세요.",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuidePage(
            "핵심 전략: 탄환 순서",
            "<color=#FFD05A><b>탄환 순서에 따라 피해량이 달라집니다.</b></color>\n실린더는 <color=#FFD05A><b>나중에 장전한 탄환부터</b></color> 발사하며, 탄환 효과도 앞뒤 순서와 연계됩니다.\n발사 전에 <color=#FF5757><b>마우스 드래그</b></color>로 순서를 바꾸고 <color=#FFD05A><b>예상 피해</b></color>를 비교하세요.",
            "Videos/Switch_Bullet_Queue.mp4",
            null,
            TargetKind.Cylinder),
        new GuidePage(
            "사거리와 공격 방향",
            "탄환은 <color=#FFD05A><b>바라보는 방향</b></color>으로 발사됩니다.\n탄환마다 <color=#FFD05A><b>사거리</b></color>가 다릅니다.\n탄환 정보에서 <color=#FFD05A><b>유효 범위 N칸</b></color>을 확인하세요.\n예상 피해가 없다면 <color=#FFD05A><b>방향, 거리, 앞을 막는 적</b></color>을 확인하세요.",
            "Videos/Show_Expectation.mp4",
            null,
            TargetKind.Cylinder),
        new GuidePage(
            "디버프 종류",
            "<color=#FF7D7D><b>표식: 받는 피해 50% 증가</b></color>\n<color=#78D987><b>독: 턴 종료 시 스택만큼 피해, 이후 1 감소</b></color>\n<color=#75C7FF><b>기절: 행동 불가, 행동할 때마다 1 감소</b></color>\n<color=#C69CFF><b>약화: 공격력 30% 감소</b></color>\n적에게 디버프가 있다면 아래와 같은 상태 아이콘이 표시됩니다.\n아이콘에 <color=#FF5757><b>마우스 커서를 올리면</b></color> 남은 스택을 확인할 수 있습니다.",
            null,
            null)
    };

    private static readonly GuideStepDefinition[] CombatSteps =
    {
        new GuideStepDefinition(
            CombatStep.Move,
            "이동",
            "<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>으로 한 칸 이동합니다.\n이동하면 <color=#FFD05A><b>적도 바로 한 턴 행동</b></color>합니다.",
            "한 칸 이동",
            "Videos/Movement.mp4",
            null,
            TargetKind.MoveButtons),
        new GuideStepDefinition(
            CombatStep.Rotate,
            "회전",
            "<color=#FF5757><b>W 키</b></color>, <color=#FF5757><b>마우스 휠 클릭</b></color> 또는 <color=#FF5757><b>회전 버튼 클릭</b></color>으로 방향을 바꿉니다.\n탄환은 <color=#FFD05A><b>바라보는 방향</b></color>으로 발사됩니다.",
            "한 번 회전",
            "Videos/Rotate.mp4",
            "Button | Rotate"),
        new GuideStepDefinition(
            CombatStep.Wait,
            "대기",
            "<color=#FF5757><b>S 키</b></color> 또는 <color=#FF5757><b>대기 버튼 클릭</b></color>으로 제자리에서 한 턴을 보냅니다.\n<color=#FFD05A><b>적의 거리와 공격 시점</b></color>을 조절할 때 사용하세요.",
            "한 번 대기",
            "Videos/Wait.mp4",
            "Button | Wait"),
        new GuideStepDefinition(
            CombatStep.InspectEnemyAction,
            "적 행동 확인",
            "<color=#FFD05A><b>적 아래의 행동 아이콘</b></color>에서 다음 행동을 확인하세요.\n아이콘이 없다면 한 턴 진행한 뒤 <color=#FF5757><b>마우스 커서를 올리거나 클릭</b></color>하세요.",
            "적 행동 아이콘 확인",
            null,
            "Image | Queue",
            TargetKind.TutorialEnemyAction),
        new GuideStepDefinition(
            CombatStep.ReloadThree,
            "장전",
            "<color=#FF5757><b>R 키</b></color> 또는 <color=#FF5757><b>장전 버튼 클릭</b></color>으로 다음 탄환을 장전합니다.\n장전은 <color=#FFD05A><b>한 턴</b></color>을 사용합니다.\n적 행동을 먼저 확인하세요.",
            "탄환 3회 장전",
            "Videos/Reload.mp4",
            "Button | Reload"),
        new GuideStepDefinition(
            CombatStep.InspectBulletInfo,
            "탄환 정보 읽기",
            "실린더 탄환이나 다음 탄환에 <color=#FF5757><b>마우스 커서를 올리세요.</b></color>\n<color=#FFD05A><b>피해, 유효 범위, 치명타 확률, 특수 효과</b></color>를 확인할 수 있습니다.",
            "탄환 정보에서 피해와 사거리 확인",
            null,
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.ReorderCylinder,
            "실린더 순서 조작",
            "<color=#FFD05A><b>탄환 순서에 따라 피해량이 달라집니다.</b></color>\n실린더 탄환을 다른 탄환 위로 <color=#FF5757><b>마우스 드래그</b></color>하세요.\n<color=#FFD05A><b>나중에 장전한 탄환부터</b></color> 발사됩니다.",
            "탄환 순서 한 번 변경",
            "Videos/Switch_Bullet_Queue.mp4",
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.PreviewDamage,
            "적 피해 예상치",
            "실린더 탄환에 <color=#FF5757><b>마우스 커서를 올리세요.</b></color>\n해당 탄환까지 발사할 <color=#FFD05A><b>예상 피해</b></color>가 적 체력에 표시됩니다.",
            "실린더 탄환의 예상 피해 확인",
            "Videos/Show_Expectation.mp4",
            null,
            TargetKind.Cylinder),
        new GuideStepDefinition(
            CombatStep.UseItem,
            "아이템 사용",
            "모든 적을 기절시키는 <color=#FFD05A><b>전기충격</b></color>을 지급합니다.\n<color=#FF5757><b>1/2/3 키</b></color> 또는 <color=#FF5757><b>인벤토리 슬롯 클릭</b></color>으로 사용하세요.",
            "전기충격 한 번 사용",
            null,
            "Layout | Inventory"),
        new GuideStepDefinition(
            CombatStep.Kick,
            "발차기",
            "바로 앞의 적 방향으로 이동하면 발차기합니다.\n<color=#FF5757><b>A/D 키</b></color> 또는 <color=#FF5757><b>이동 버튼 클릭</b></color>을 사용하세요.\n재사용 대기시간은 <color=#FFD05A><b>3턴</b></color>입니다.\n적끼리 부딪히면 <color=#FFD05A><b>둘 다 피해</b></color>를 받습니다.",
            "적을 한 번 발차기",
            "Videos/Kick.mp4",
            "Panel | Behaviour Tile"),
        new GuideStepDefinition(
            CombatStep.Fire,
            "발사",
            "<color=#FF5757><b>화면에 마우스 왼쪽 클릭</b></color>, <color=#FF5757><b>스페이스 바</b></color> 또는 <color=#FF5757><b>사격 버튼 클릭</b></color>으로 탄환을 <color=#FFD05A><b>순서대로 모두 발사</b></color>합니다.\n발사 전에 <color=#FFD05A><b>방향, 사거리, 탄환 순서</b></color>를 확인하세요.",
            "실린더 발사",
            "Videos/Shoot.mp4",
            "Button | Shoot")
    };

    private static readonly GuidePage[] ShopPages =
    {
        new GuidePage(
            "상품 구매",
            "상단에서 <color=#FFD05A><b>탄환과 아이템</b></color>을 구매할 수 있습니다.\n상품에 마우스 커서를 올려 <color=#FFD05A><b>효과와 가격</b></color>을 확인하세요.",
            "Videos/Shop_Purchase.mp4",
            "Layout | Shop Items"),
        new GuidePage(
            "탄환 관리",
            "탄환 관리에서 보유 탄환을 <color=#FFD05A><b>강화하거나 제거</b></color>할 수 있습니다.\n비용은 선택한 탄환 아래에 표시됩니다.",
            "Videos/Shop_Idle.mp4",
            "Button | Manage Bullet"),
        new GuidePage(
            "인벤토리",
            "왼쪽 인벤토리에서 <color=#FFD05A><b>보유 아이템</b></color>을 확인하세요.\n상점에서는 아이템을 <color=#FF5757><b>마우스 우클릭</b></color>해 판매할 수 있습니다.",
            null,
            "Layout | Inventory"),
        new GuidePage(
            "새로고침",
            "원하는 상품이 없다면 <color=#FFD05A><b>새로고침</b></color>하세요.\n새로고침할 때마다 다음 비용이 증가합니다.\n<color=#67E480><b>(현재는 데모 버전이므로 새로고침 비용이 무료입니다!)</b></color>",
            null,
            "Button | Refresh"),
        new GuidePage(
            "다음 전투",
            "<color=#FFD05A><b>구매와 탄환 관리</b></color>를 마친 뒤 이 버튼을 누르세요.\n<color=#FFD05A><b>다음 전투</b></color>가 시작됩니다.",
            null,
            "Button | Go To Battle")
    };

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
    private GameObject videoFrame;
    private RawImage videoDisplay;
    private TMP_Text videoLoadingText;
    private AspectRatioFitter videoAspect;
    private VideoPlayer videoPlayer;
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
    private bool combatGuideStarted;
    private bool shopGuideStarted;
    private bool showingCombatSystemPages;
    private bool missionActive;
    private bool pendingAdvance;
    private float advanceAt;
    private bool videoShouldPlay;
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
    private bool bulletInfoInspected;
    private bool cylinderReordered;
    private bool damagePreviewInspected;
    private bool kickPerformed;
    private bool fired;
    private bool itemUsed;
    private bool tutorialStunItemGranted;
    private bool subscribed;
    private readonly Vector3[] targetWorldCorners = new Vector3[4];

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
        StateManager manager = FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        if (manager == null
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
                if (descendant.name == "Panel | MainGame")
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

    public static bool TrySkipActiveGuide()
    {
        if (activeInstance == null || activeInstance.mode == GuideMode.None)
        {
            return false;
        }

        if (activeInstance.isMandatoryGuideSession)
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
        PlayerPrefs.DeleteKey(GuideDisabledKey);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        activeInstance = this;
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

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandleVideoPrepared;
            videoPlayer.frameReady -= HandleVideoFrameReady;
            videoPlayer.errorReceived -= HandleVideoError;
        }
    }

    private void Update()
    {
        ResolveReferences();
        Subscribe();

        if (mode == GuideMode.None)
        {
            TryStartCombatGuide();
            TryStartItemGuide();
            TryStartShopGuide();
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
            || stateManager.CurrentBattleIndex != 0
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
        bulletInfoInspected = false;
        cylinderReordered = false;
        damagePreviewInspected = false;
        kickPerformed = false;
        fired = false;
        itemUsed = false;
        tutorialStunItemGranted = false;
        mode = GuideMode.Combat;
        isMandatoryGuideSession = true;
        combatSystemPageIndex = 0;
        combatStepIndex = 0;
        combatReviewStepIndex = -1;
        showingCombatSystemPages = true;
        ShowCombatSystemPage();
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
        isMandatoryGuideSession = true;
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
            || LoadingTransitionController.IsTransitioning)
        {
            return;
        }

        shopGuideStarted = true;
        mode = GuideMode.Shop;
        isMandatoryGuideSession = true;
        shopPageIndex = 0;
        ShowShopPage();
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
        cardExitButton?.gameObject.SetActive(!isMandatoryGuideSession);
        neverShowToggle?.gameObject.SetActive(!isMandatoryGuideSession);
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

    private void HandleContinue()
    {
        if (mode == GuideMode.Shop)
        {
            shopPageIndex++;
            ShowShopPage();
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
        videoShouldPlay = false;
        videoPlayer?.Pause();
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

        if (mode == GuideMode.Shop)
        {
            if (shopPageIndex > 0)
            {
                shopPageIndex--;
                ShowShopPage();
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
            GuideMode.Shop => shopPageIndex > 0,
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
        // setup such as facing an enemy, entering range, waiting, or reloading.
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
                        "적 행동 아이콘이 나타날 때까지 한 턴 진행",
                        "Button | Wait");
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
                    out EnemyController _))
            {
                canMove |= direction == facingDirection && playerMove.CanPush;
                canRotateToPush |= direction != facingDirection;
                continue;
            }

            if (!waveManager.IsTileReservedForSpawn(targetTileIndex))
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
                "이동 경로가 열릴 때까지 한 턴 대기",
                "Button | Wait");
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
                "적이 등장할 때까지 한 턴 진행",
                "Button | Wait");
        }

        return true;
    }

    private bool TryGetKickPriority(out PriorityMission priority)
    {
        priority = default;
        if (!TryGetNearestEnemy(out int direction, out int distance))
        {
            priority = new PriorityMission(
                "적이 등장할 때까지 한 턴 진행",
                "Button | Wait");
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
                $"발차기 재사용까지 대기 ({playerMove.RemainingPushCooldownTurns}턴)",
                "Button | Wait");
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
                && enemy.QueuedAttackActions.Count > 0)
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
                out int playerTileIndex))
        {
            return false;
        }

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy == null || enemy.CurrentHealth <= 0
                || !boardManager.TryGetTileIndex(
                    enemy.transform.position,
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
            "이제 <color=#FFD05A><b>탄환 순서와 적 행동</b></color>을 확인하며 전투하세요.");
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
        PlayerPrefs.Save();

        combatGuideStarted = true;
        shopGuideStarted = true;
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

    private void SetTutorialInputLocked(bool locked)
    {
        playerMove?.SetInputLocked(locked);
    }

    private static bool IsCompleted(string key)
    {
        return PlayerPrefs.GetInt(key, 0) != 0;
    }

    private static bool IsGuideDisabled()
    {
        return PlayerPrefs.GetInt(GuideDisabledKey, 0) != 0;
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

        if (rootCanvas == null)
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        Camera rootCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
        if (!TryGetHighlightScreenBounds(
                out Vector2 screenMin,
                out Vector2 screenMax))
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenMin,
                rootCamera,
                out Vector2 localMin)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenMax,
                rootCamera,
                out Vector2 localMax))
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        const float padding = 12f;
        highlight.gameObject.SetActive(true);
        highlight.anchoredPosition = (localMin + localMax) * 0.5f;
        highlight.sizeDelta = new Vector2(
            Mathf.Abs(localMax.x - localMin.x) + padding * 2f,
            Mathf.Abs(localMax.y - localMin.y) + padding * 2f);
        float pulse = 0.05f + 0.14f
            * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
        Color color = highlightImage.color;
        color.a = pulse;
        highlightImage.color = color;
    }

    private bool TryGetHighlightScreenBounds(
        out Vector2 screenMin,
        out Vector2 screenMax)
    {
        screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;

        if (activeTarget != null && activeTarget.gameObject.activeInHierarchy)
        {
            activeTarget.GetWorldCorners(targetWorldCorners);
            Camera targetCamera = GetCanvasCamera(activeTarget);
            IncludeScreenPoint(
                RectTransformUtility.WorldToScreenPoint(
                    targetCamera,
                    targetWorldCorners[0]),
                ref screenMin,
                ref screenMax);
            IncludeScreenPoint(
                RectTransformUtility.WorldToScreenPoint(
                    targetCamera,
                    targetWorldCorners[2]),
                ref screenMin,
                ref screenMax);
            hasBounds = true;
        }

        if (activeSecondaryTarget != null
            && activeSecondaryTarget.gameObject.activeInHierarchy)
        {
            activeSecondaryTarget.GetWorldCorners(targetWorldCorners);
            Camera targetCamera = GetCanvasCamera(activeSecondaryTarget);
            IncludeScreenPoint(
                RectTransformUtility.WorldToScreenPoint(
                    targetCamera,
                    targetWorldCorners[0]),
                ref screenMin,
                ref screenMax);
            IncludeScreenPoint(
                RectTransformUtility.WorldToScreenPoint(
                    targetCamera,
                    targetWorldCorners[2]),
                ref screenMin,
                ref screenMax);
            hasBounds = true;
        }

        return hasBounds;
    }

    private static void IncludeScreenPoint(
        Vector2 point,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        minimum = Vector2.Min(minimum, point);
        maximum = Vector2.Max(maximum, point);
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

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
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

        guideRoot = CreateRect("Guide | First Run", rootCanvas.transform);
        Stretch(guideRoot);

        inputBlocker = CreateImage(
            "Image | Guide Blocker",
            guideRoot,
            new Color(0.025f, 0.02f, 0.018f, 0.72f));
        Stretch(inputBlocker.rectTransform);
        inputBlocker.raycastTarget = true;

        highlightImage = CreateImage(
            "Image | Guide Highlight",
            guideRoot,
            new Color(0.02f, 0.48f, 1f, 0.11f));
        highlight = highlightImage.rectTransform;
        highlight.anchorMin = new Vector2(0.5f, 0.5f);
        highlight.anchorMax = new Vector2(0.5f, 0.5f);
        highlight.pivot = new Vector2(0.5f, 0.5f);
        highlightImage.raycastTarget = false;
        Outline highlightOutline = highlight.gameObject.AddComponent<Outline>();
        highlightOutline.effectColor = new Color(0.05f, 0.82f, 1f, 1f);
        highlightOutline.effectDistance = new Vector2(4f, -4f);

        card = CreateImage(
            "Panel | Guide Card",
            guideRoot,
            new Color(0.09f, 0.075f, 0.065f, 0.98f)).gameObject;
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(780f, 720f);
        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.95f, 0.5f, 0.12f, 0.9f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        cardStepText = CreateText("Text | Guide Step", cardRect);
        SetAnchors(cardStepText.rectTransform, 0.06f, 0.89f, 0.94f, 0.96f);
        cardStepText.alignment = TextAlignmentOptions.Center;
        cardStepText.color = new Color(1f, 0.7f, 0.28f, 1f);
        cardStepText.fontSizeMax = 25f;
        cardStepText.textWrappingMode = TextWrappingModes.NoWrap;

        cardTitleText = CreateText("Text | Guide Title", cardRect);
        SetAnchors(cardTitleText.rectTransform, 0.06f, 0.80f, 0.94f, 0.90f);
        cardTitleText.alignment = TextAlignmentOptions.Center;
        cardTitleText.fontStyle = FontStyles.Normal;
        cardTitleText.fontSizeMax = 42f;
        cardTitleText.textWrappingMode = TextWrappingModes.NoWrap;

        Image frameImage = CreateImage(
            "Image | Guide Video Frame",
            cardRect,
            new Color(0.02f, 0.018f, 0.016f, 1f));
        videoFrame = frameImage.gameObject;
        SetAnchors(frameImage.rectTransform, 0.08f, 0.31f, 0.92f, 0.79f);
        frameImage.raycastTarget = false;

        RectTransform displayRect = CreateRect(
            "RawImage | Guide Video",
            frameImage.rectTransform);
        Stretch(displayRect);
        videoDisplay = displayRect.gameObject.AddComponent<RawImage>();
        videoDisplay.color = Color.white;
        videoDisplay.raycastTarget = false;
        videoAspect = displayRect.gameObject.AddComponent<AspectRatioFitter>();
        videoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        videoAspect.aspectRatio = 16f / 9f;

        videoLoadingText = CreateText(
            "Text | Guide Video Loading",
            frameImage.rectTransform);
        Stretch(videoLoadingText.rectTransform);
        videoLoadingText.alignment = TextAlignmentOptions.Center;
        videoLoadingText.text = "영상 불러오는 중...";
        videoLoadingText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        videoLoadingText.fontSizeMax = 24f;
        videoLoadingText.textWrappingMode = TextWrappingModes.NoWrap;

        cardBodyText = CreateText("Text | Guide Body", cardRect);
        SetAnchors(cardBodyText.rectTransform, 0.08f, 0.13f, 0.92f, 0.29f);
        cardBodyText.alignment = TextAlignmentOptions.Center;
        cardBodyText.fontSizeMin = 12f;
        cardBodyText.fontSizeMax = 27f;
        cardBodyText.textWrappingMode = TextWrappingModes.NoWrap;
        cardBodyText.overflowMode = TextOverflowModes.Ellipsis;

        Image cardMissionImage = CreateImage(
            "Panel | Guide Card Mission",
            cardRect,
            new Color(0.035f, 0.09f, 0.11f, 0.98f));
        cardMissionPanel = cardMissionImage.gameObject;
        SetAnchors(
            cardMissionImage.rectTransform,
            0.12f,
            0.12f,
            0.88f,
            0.17f);
        cardMissionImage.raycastTarget = false;
        Outline cardMissionOutline = cardMissionPanel.AddComponent<Outline>();
        cardMissionOutline.effectColor = new Color(0.35f, 0.8f, 1f, 0.8f);
        cardMissionOutline.effectDistance = new Vector2(2f, -2f);

        cardMissionText = CreateText(
            "Text | Guide Card Mission",
            cardMissionImage.rectTransform);
        SetAnchors(cardMissionText.rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
        cardMissionText.alignment = TextAlignmentOptions.Center;
        cardMissionText.fontSizeMin = 11f;
        cardMissionText.fontSizeMax = 24f;
        cardMissionText.textWrappingMode = TextWrappingModes.NoWrap;
        cardMissionText.overflowMode = TextOverflowModes.Ellipsis;
        cardMissionPanel.SetActive(false);

        RectTransform warningRootRect = CreateRect(
            "Panel | Warning Sound Demo",
            cardRect);
        warningDemoRoot = warningRootRect.gameObject;
        SetAnchors(warningRootRect, 0.27f, 0.125f, 0.73f, 0.27f);

        warningSoundButton = CreateButton(
            "Button | Play Enemy Warning",
            warningRootRect,
            "경고음 듣기",
            new Color(0.42f, 0.11f, 0.08f, 1f),
            out _);
        SetAnchors(
            (RectTransform)warningSoundButton.transform,
            0f,
            0.18f,
            0.60f,
            0.82f);
        warningSoundButton.onClick.AddListener(PlayWarningDemo);

        warningDemoTileImage = CreateImage(
            "Image | Enemy Warning Tile",
            warningRootRect,
            new Color(0.18f, 0.15f, 0.13f, 1f));
        SetAnchors(
            warningDemoTileImage.rectTransform,
            0.73f,
            0.10f,
            0.96f,
            0.90f);
        warningDemoTileImage.type = Image.Type.Simple;
        warningDemoTileImage.preserveAspect = false;
        warningDemoTileImage.raycastTarget = false;

        warningDemoReadyGlow = CreateImage(
            "Image | Enemy Warning Ready Glow",
            warningDemoTileImage.rectTransform,
            Color.white);
        Stretch(warningDemoReadyGlow.rectTransform);
        warningDemoReadyGlow.sprite = null;
        warningDemoReadyGlow.type = Image.Type.Simple;
        warningDemoReadyGlow.raycastTarget = false;
        warningDemoReadyGlow.gameObject.SetActive(false);

        warningDemoAttackIcon = CreateImage(
            "Image | Melee Attack Icon",
            warningDemoTileImage.rectTransform,
            Color.white);
        SetAnchors(
            warningDemoAttackIcon.rectTransform,
            0.20f,
            0.20f,
            0.80f,
            0.80f);
        warningDemoAttackIcon.preserveAspect = true;
        warningDemoAttackIcon.raycastTarget = false;
        warningDemoRoot.SetActive(false);

        RectTransform debuffRootRect = CreateRect(
            "Panel | Debuff Legend",
            cardRect);
        debuffLegendRoot = debuffRootRect.gameObject;
        SetAnchors(debuffRootRect, 0.18f, 0.19f, 0.82f, 0.36f);
        string[] debuffNames = { "표식", "독", "기절", "약화" };
        Color[] debuffColors =
        {
            new Color(1f, 0.49f, 0.49f, 1f),
            new Color(0.47f, 0.85f, 0.53f, 1f),
            new Color(0.46f, 0.78f, 1f, 1f),
            new Color(0.78f, 0.61f, 1f, 1f)
        };
        for (int i = 0; i < debuffLegendIcons.Length; i++)
        {
            RectTransform itemRect = CreateRect(
                $"Item | Debuff {debuffNames[i]}",
                debuffRootRect);
            float minX = i / (float)debuffLegendIcons.Length;
            float maxX = (i + 1f) / debuffLegendIcons.Length;
            SetAnchors(itemRect, minX, 0f, maxX, 1f);

            Image icon = CreateImage(
                $"Image | Debuff {debuffNames[i]}",
                itemRect,
                Color.white);
            SetAnchors(icon.rectTransform, 0.25f, 0.30f, 0.75f, 0.94f);
            icon.preserveAspect = true;
            icon.raycastTarget = true;
            debuffLegendIcons[i] = icon;

            TMP_Text stackText = CreateText(
                "Text | Stack",
                icon.rectTransform);
            SetAnchors(stackText.rectTransform, 0.52f, 0f, 1f, 0.48f);
            stackText.text = "1";
            stackText.color = new Color(1f, 0.18f, 0.22f, 1f);
            stackText.fontStyle = FontStyles.Normal;
            stackText.fontSizeMin = 10f;
            stackText.fontSizeMax = 18f;
            stackText.alignment = TextAlignmentOptions.BottomRight;
            stackText.textWrappingMode = TextWrappingModes.NoWrap;
            icon.gameObject.AddComponent<DebuffIconUI>();

            TMP_Text label = CreateText(
                $"Text | Debuff {debuffNames[i]}",
                itemRect);
            SetAnchors(label.rectTransform, 0f, 0f, 1f, 0.30f);
            label.text = debuffNames[i];
            label.color = debuffColors[i];
            label.fontStyle = FontStyles.Normal;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 21f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        debuffLegendRoot.SetActive(false);

        neverShowToggle = CreateNeverShowToggle(cardRect);
        SetAnchors(
            (RectTransform)neverShowToggle.transform,
            0.035f,
            0.895f,
            0.28f,
            0.965f);

        cardExitButton = CreateGuideExitButton(cardRect);
        SetAnchors(
            (RectTransform)cardExitButton.transform,
            0.91f,
            0.895f,
            0.975f,
            0.965f);
        cardExitButton.onClick.AddListener(SkipCurrentGuide);

        cardBackButton = CreateButton(
            "Button | Previous Guide",
            cardRect,
            "이전",
            new Color(0.2f, 0.18f, 0.17f, 1f),
            out _);
        SetAnchors(
            (RectTransform)cardBackButton.transform,
            0.08f,
            0.035f,
            0.32f,
            0.11f);
        cardBackButton.onClick.AddListener(HandleBack);

        continueButton = CreateButton(
            "Button | Continue Guide",
            cardRect,
            "미션 시작",
            new Color(0.82f, 0.34f, 0.08f, 1f),
            out continueButtonText);
        SetAnchors(
            (RectTransform)continueButton.transform,
            0.68f,
            0.035f,
            0.92f,
            0.11f);
        continueButton.onClick.AddListener(HandleContinue);

        Image missionImage = CreateImage(
            "Panel | Guide Mission",
            guideRoot,
            new Color(0.065f, 0.052f, 0.045f, 0.96f));
        missionBar = missionImage.gameObject;
        SetAnchors(missionImage.rectTransform, 0.18f, 0.88f, 0.82f, 0.97f);
        missionImage.raycastTarget = false;
        Outline missionOutline = missionBar.AddComponent<Outline>();
        missionOutline.effectColor = new Color(0.95f, 0.5f, 0.12f, 0.85f);
        missionOutline.effectDistance = new Vector2(2f, -2f);

        missionText = CreateText("Text | Guide Mission", missionImage.rectTransform);
        SetAnchors(missionText.rectTransform, 0.04f, 0.12f, 0.62f, 0.88f);
        missionText.alignment = TextAlignmentOptions.MidlineLeft;
        missionText.fontSizeMin = 12f;
        missionText.fontSizeMax = 28f;
        missionText.textWrappingMode = TextWrappingModes.NoWrap;
        missionText.overflowMode = TextOverflowModes.Ellipsis;

        missionGuideButton = CreateButton(
            "Button | Show Current Guide",
            missionImage.rectTransform,
            "가이드 보기",
            new Color(0.34f, 0.20f, 0.10f, 0.95f),
            out _);
        SetAnchors(
            (RectTransform)missionGuideButton.transform,
            0.64f,
            0.18f,
            0.80f,
            0.82f);
        missionGuideButton.onClick.AddListener(ShowCurrentMissionGuide);

        missionNextButton = CreateButton(
            "Button | Next Mission Guide",
            missionImage.rectTransform,
            "다음 단계",
            new Color(0.12f, 0.32f, 0.4f, 0.95f),
            out _);
        SetAnchors(
            (RectTransform)missionNextButton.transform,
            0.82f,
            0.18f,
            0.98f,
            0.82f);
        missionNextButton.onClick.AddListener(AdvanceCurrentMission);

        GameObject playerObject = new GameObject("VideoPlayer | First Run Guide");
        playerObject.transform.SetParent(guideRoot, false);
        videoPlayer = playerObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.isLooping = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.prepareCompleted += HandleVideoPrepared;
        videoPlayer.frameReady += HandleVideoFrameReady;
        videoPlayer.errorReceived += HandleVideoError;

        card.SetActive(false);
        missionBar.SetActive(false);
        guideRoot.gameObject.SetActive(false);
    }

    private void SetCardVideo(string relativePath)
    {
        bool hasVideo = !string.IsNullOrWhiteSpace(relativePath);
        videoFrame.SetActive(hasVideo);
        SetAnchors(
            cardBodyText.rectTransform,
            0.08f,
            hasVideo ? 0.13f : 0.23f,
            0.92f,
            hasVideo ? 0.29f : 0.76f);

        StopVideo();
        if (!hasVideo || videoPlayer == null)
        {
            return;
        }

        videoShouldPlay = true;
        videoDisplay.texture = null;
        videoLoadingText.gameObject.SetActive(true);
        videoLoadingText.text = "영상 불러오는 중...";
        videoPlayer.url = StreamingVideoPlayer.GetStreamingAssetsUrl(relativePath);
        videoPlayer.Prepare();
    }

    private void StopVideo()
    {
        videoShouldPlay = false;
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (videoDisplay != null)
        {
            videoDisplay.texture = null;
        }
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

    private void HandleVideoPrepared(VideoPlayer preparedPlayer)
    {
        if (preparedPlayer == null || videoDisplay == null)
        {
            return;
        }

        AssignVideoTexture(preparedPlayer);
        int width = preparedPlayer.width == 0
            ? 16
            : (int)Math.Min(preparedPlayer.width, 8192UL);
        int height = preparedPlayer.height == 0
            ? 9
            : (int)Math.Min(preparedPlayer.height, 8192UL);
        videoAspect.aspectRatio = (float)width / Mathf.Max(1, height);
        videoLoadingText.gameObject.SetActive(false);

        if (videoShouldPlay)
        {
            preparedPlayer.time = 0d;
            preparedPlayer.Play();
        }
    }

    private void HandleVideoFrameReady(VideoPlayer preparedPlayer, long _)
    {
        AssignVideoTexture(preparedPlayer);
    }

    private void AssignVideoTexture(VideoPlayer preparedPlayer)
    {
        if (preparedPlayer != null && preparedPlayer.texture != null
            && videoDisplay != null)
        {
            videoDisplay.texture = preparedPlayer.texture;
        }
    }

    private void HandleVideoError(VideoPlayer failedPlayer, string message)
    {
        if (videoLoadingText != null)
        {
            videoLoadingText.gameObject.SetActive(true);
            videoLoadingText.text =
                "영상을 불러오지 못했습니다.\n미션은 그대로 진행할 수 있습니다.";
        }

        Debug.LogWarning(
            $"First-run guide video failed: '{failedPlayer.url}'. {message}",
            this);
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)target.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private Image CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text CreateText(string objectName, Transform parent)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (guideFont != null)
        {
            text.font = guideFont;
        }

        text.color = Color.white;
        text.fontStyle = FontStyles.Normal;
        text.richText = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Toggle CreateNeverShowToggle(Transform parent)
    {
        RectTransform root = CreateRect("Toggle | Never Show Guide", parent);
        Toggle toggle = root.gameObject.AddComponent<Toggle>();

        Image background = CreateImage(
            "Image | Checkbox",
            root,
            new Color(0.08f, 0.07f, 0.055f, 0.98f));
        SetAnchors(background.rectTransform, 0f, 0.16f, 0.18f, 0.84f);
        Outline checkboxOutline = background.gameObject.AddComponent<Outline>();
        checkboxOutline.effectColor = new Color(1f, 0.75f, 0.12f, 1f);
        checkboxOutline.effectDistance = new Vector2(2f, -2f);

        Image checkmark = CreateImage(
            "Image | Checkmark",
            background.rectTransform,
            new Color(1f, 0.62f, 0.05f, 1f));
        SetAnchors(checkmark.rectTransform, 0.2f, 0.2f, 0.8f, 0.8f);
        checkmark.raycastTarget = false;

        TMP_Text label = CreateText("Text | Never Show Guide", root);
        SetAnchors(label.rectTransform, 0.23f, 0f, 1f, 1f);
        label.text = "다시 보지 않기";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontStyle = FontStyles.Normal;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 20f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.SetIsOnWithoutNotify(false);
        checkmark.canvasRenderer.SetAlpha(0f);
        return toggle;
    }

    private Button CreateGuideExitButton(Transform parent)
    {
        Button template = null;
        foreach (Button candidate in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate == null
                || guideRoot != null
                && candidate.transform.IsChildOf(guideRoot))
            {
                continue;
            }

            if (candidate.name == "Button _ Exit"
                || candidate.name == "Button | Exit")
            {
                TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
                if (label == null
                    || !string.Equals(
                        label.text?.Trim(),
                        "X",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                template = candidate;
                break;
            }
        }

        if (template == null)
        {
            return CreateButton(
                "Button | Close Guide",
                parent,
                "X",
                new Color(0.36f, 0.06f, 0.05f, 1f),
                out _);
        }

        Button exitButton = Instantiate(template, parent, false);
        exitButton.name = "Button | Close Guide";
        exitButton.onClick = new Button.ButtonClickedEvent();
        exitButton.transform.localScale = Vector3.one;
        exitButton.gameObject.SetActive(true);
        return exitButton;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        out TMP_Text labelText)
    {
        Image image = CreateImage(objectName, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        labelText = CreateText("Text | Label", image.rectTransform);
        Stretch(labelText.rectTransform);
        labelText.text = label;
        labelText.fontStyle = FontStyles.Normal;
        labelText.fontSizeMin = 9f;
        labelText.fontSizeMax = 26f;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.margin = new Vector4(6f, 2f, 6f, 2f);
        return button;
    }

    private static string RemoveBoldTags(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : value.Replace("<b>", string.Empty)
                .Replace("</b>", string.Empty);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
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
