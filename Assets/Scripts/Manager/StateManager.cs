using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameFlowState
{
    Initializing = 0,
    Battle = 1,
    BattleClear = 2,
    Shop = 3,
    RunComplete = 4,
    RunFailed = 5,
    Event = 6
}

[DefaultExecutionOrder(-100)]
public class StateManager : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private StageData[] stages = Array.Empty<StageData>();
    [Min(0)]
    [SerializeField] private int startingStageIndex;
    [SerializeField] private Vector3 playerSpawnOffset =
        new Vector3(0f, -0.7f, 0f);

    [Header("System References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private CombatFeedbackController combatFeedback;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameStartUI gameStartUI;

    [Header("Panels")]
    [SerializeField] private GameObject mainGamePanel;
    [SerializeField] private GameObject stageClearPanel;
    [SerializeField] private GameObject shopPanel;

    [Header("Navigation")]
    [SerializeField] private Button goToMaintenanceButton;
    [SerializeField] private TMP_Text goToMaintenanceText;
    [SerializeField] private Button goToBattleButton;
    [SerializeField] private TMP_Text goToBattleText;

    [Header("Runtime State")]
    [SerializeField] private int currentStageIndex = -1;
    [SerializeField] private int currentBattleIndex = -1;
    [SerializeField] private GameFlowState currentState =
        GameFlowState.Initializing;

    private Coroutine battleClearCoroutine;
    private Coroutine battleStartCoroutine;
    private Coroutine webAutosaveCoroutine;
    private RunSaveData pendingRestoredRun;
    private bool suppressExitSave;
    private RunStartMode currentRunStartMode = RunStartMode.None;
    private int turnCountBeforeCurrentBattle;

    public event Action StateChanged;

    public int CurrentStageIndex => currentStageIndex;
    public int CurrentBattleIndex => currentBattleIndex;
    public GameFlowState CurrentState => currentState;
    public RunStartMode CurrentRunStartMode => currentRunStartMode;
    public bool IsFreshRun => currentRunStartMode != RunStartMode.Continue;
    public StageData CurrentStage =>
        stages != null
        && currentStageIndex >= 0
        && currentStageIndex < stages.Length
            ? stages[currentStageIndex]
            : null;
    public BattleData CurrentBattle =>
        TryGetCurrentBattle(out BattleData battle) ? battle : null;
    public bool IsCombatSettledForExit => currentState switch
    {
        GameFlowState.Battle => playerMove == null
            || (!playerMove.IsShooting
                && !playerMove.IsActing
                && !playerMove.IsEnemyTurnResolving
                && (waveManager == null || !waveManager.IsResolvingTurn)),
        GameFlowState.Shop => shopManager == null
            || !shopManager.IsRefreshing,
        _ => true
    };

    public void LockInputForExitSave()
    {
        SetInputLocked(true);
    }

    /// <summary>
    /// Exposes read-only run progress to shared UI in a non-Battle scene.
    /// The component itself may remain disabled so its Battle lifecycle does
    /// not start in that scene.
    /// </summary>
    public void ConfigureExternalSceneState(
        int stageIndex,
        int battleIndex,
        GameFlowState flowState)
    {
        currentStageIndex = stageIndex;
        currentBattleIndex = battleIndex;
        currentState = flowState;
        StateChanged?.Invoke();
    }

    private void Awake()
    {
        SetPanels(false, false, false);

        currencyManager ??= FindFirstObjectByType<CurrencyManager>(
            FindObjectsInactive.Include);
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        rewardManager ??= FindFirstObjectByType<RewardManager>(
            FindObjectsInactive.Include);
        combatFeedback ??= FindFirstObjectByType<CombatFeedbackController>(
            FindObjectsInactive.Include);

        gameStartUI ??= FindFirstObjectByType<GameStartUI>(
            FindObjectsInactive.Include);

        if (gameStartUI != null)
        {
            gameStartUI.PrepareForUse();
            gameStartUI.ResetAndHide();
        }

        if (playerMove != null)
        {
            playerMove.SetInputLocked(true);
        }
    }

    private void OnEnable()
    {
        Application.wantsToQuit -= HandleWantsToQuit;
        Application.wantsToQuit += HandleWantsToQuit;

        if (waveManager != null)
        {
            waveManager.BattleCompleted += HandleBattleCompleted;
            waveManager.BattleFailed += HandleBattleFailed;
            waveManager.EnemyTurnCycleCompleted +=
                HandleWebEnemyTurnCycleCompleted;
        }

        if (shopManager != null)
        {
            shopManager.OffersChanged += HandleWebShopStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated += HandlePlayerDefeated;
        }

        if (deckManager != null)
        {
            deckManager.BulletsDepleted += HandleBulletsDepleted;
        }

        if (goToMaintenanceButton != null)
        {
            goToMaintenanceButton.onClick.AddListener(GoToMaintenance);
        }

        if (goToBattleButton != null)
        {
            goToBattleButton.onClick.AddListener(GoToBattle);
        }
    }

    private void Start()
    {
        RunStartMode startMode = RunSaveSystem.ConsumeRequestedStartMode();
        currentRunStartMode = startMode;

        if (!ValidateReferences())
        {
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        RunSaveData restoredSaveData = null;
        bool restored = startMode == RunStartMode.Continue
            && RunSaveSystem.TryLoad(out restoredSaveData)
            && TryRestoreRun(restoredSaveData);

        if (restored)
        {
            GameStatistics.ResumeRun(restoredSaveData);
        }
        else
        {
            if (startMode == RunStartMode.Continue)
            {
                RunSaveSystem.DeleteSave();
                currentRunStartMode = RunStartMode.New;
            }

            if (!TryFindNextStageIndex(
                    Mathf.Max(0, startingStageIndex),
                    out currentStageIndex))
            {
                ShowRunComplete("CONFIGURATION ERROR");
                return;
            }

            if (!NodeMapSaveSystem.TryGetSelectedBattle(
                    out currentStageIndex,
                    out currentBattleIndex))
            {
                currentBattleIndex = 0;
            }

            if (startMode == RunStartMode.None)
            {
                GameStatistics.BeginRun();
            }
            else
            {
                GameStatistics.BeginFreshRun();
            }
        }

        if (restored)
        {
            StartRestoredFlow();
        }
        else
        {
            StartCurrentBattle();
        }
    }

    public bool SaveCurrentRun()
    {
        bool isBattle = currentState == GameFlowState.Battle;
        bool isBattleClear = currentState == GameFlowState.BattleClear;
        bool isShop = currentState == GameFlowState.Shop;

        if ((!isBattle && !isBattleClear && !isShop)
            || !TryGetCurrentBattle(out BattleData currentBattle)
            || deckManager == null || playerHealth == null
            || currencyManager == null || playerInventory == null
            || waveManager == null || boardManager == null
            || playerMove == null
            || isBattle && (waveManager.IsBattleCompleted
                || waveManager.IsBattleCompletionPending
                || (waveManager.ActiveEnemies.Count == 0
                    && !waveManager.IsWaitingForNextWave)))
        {
            return false;
        }

        if (isBattleClear && currentBattle.IsBoss
            && !TryGetNextBattlePosition(out _, out _))
        {
            RunSaveSystem.DeleteSave();
            return false;
        }

        currencyManager.FlushPendingMoney();

        bool hasPlayerTile = boardManager.TryGetTileIndex(
            playerMove.transform.position,
            out int playerTileIndex);

        if (isBattle && !hasPlayerTile)
        {
            return false;
        }

        if (!hasPlayerTile)
        {
            playerTileIndex = Mathf.Clamp(
                boardManager.BoardCount / 2,
                0,
                Mathf.Max(0, boardManager.BoardCount - 1));
        }

        RunSaveData saveData = new RunSaveData
        {
            flowState = (int)currentState,
            stageIndex = currentStageIndex,
            battleIndex = currentBattleIndex,
            currentHealth = playerHealth.CurrentHealth,
            maxHealth = playerHealth.MaxHealth,
            money = currencyManager.CurrentMoney,
            paidBulletRemovalCount = deckManager.PaidBulletRemovalCount,
            shopRefreshCost = shopManager == null
                ? 0
                : shopManager.CurrentRefreshCost,
            playerTileIndex = playerTileIndex,
            playerFacingRight = playerMove.transform.localScale.x >= 0f,
            playerTurnCount = playerMove.TurnCount,
            cumulativeBattleTurnCount = turnCountBeforeCurrentBattle
                + playerMove.TurnCount,
            nextPushAvailableTurn = playerMove.NextPushAvailableTurn,
            playerStatusEffects = playerHealth.CaptureStatusRunState(),
            combatReport = gameStartUI == null
                ? new RunCombatReportSaveData()
                : gameStartUI.CaptureRunState(),
            randomStateJson = JsonUtility.ToJson(UnityEngine.Random.state)
        };
        deckManager.CaptureRunState(
            saveData.bullets,
            saveData.nextCycleAcquisitionOrders);
        playerInventory.CaptureRunState(
            saveData.inventoryItemAssetNames);
        if (isBattle)
        {
            waveManager.CaptureRunState(saveData);
        }

        combatFeedback?.CaptureRunState(saveData);
        GameStatistics.CaptureRunState(saveData);
        rewardManager?.CaptureRunState(saveData.droppedItems);
        shopManager?.CaptureRunState(saveData);
        bool saved = saveData.bullets.Count > 0
            && RunSaveSystem.Save(saveData);

        if (saved)
        {
            GameStatistics.SaveCheckpoint();
        }

        return saved;
    }

    private bool TryRestoreRun(RunSaveData saveData)
    {
        GameFlowState savedFlow = Enum.IsDefined(
            typeof(GameFlowState),
            saveData == null ? -1 : saveData.flowState)
                ? (GameFlowState)saveData.flowState
                : GameFlowState.Initializing;

        if (saveData == null || shopManager == null
            || savedFlow != GameFlowState.Battle
                && savedFlow != GameFlowState.BattleClear
                && savedFlow != GameFlowState.Shop
            || saveData.stageIndex < 0
            || saveData.stageIndex >= stages.Length)
        {
            return false;
        }

        StageData savedStage = stages[saveData.stageIndex];

        if (savedStage == null || saveData.battleIndex < 0
            || saveData.battleIndex >= savedStage.Battles.Count
            || savedStage.Battles[saveData.battleIndex] == null
            || !deckManager.RestoreRunState(
                saveData.bullets,
                shopManager.ResolveSavedBullet,
                saveData.paidBulletRemovalCount,
                saveData.nextCycleAcquisitionOrders))
        {
            return false;
        }

        currentStageIndex = saveData.stageIndex;
        currentBattleIndex = saveData.battleIndex;
        turnCountBeforeCurrentBattle = saveData.startSelectedBattleFresh
            ? Mathf.Max(0, saveData.cumulativeBattleTurnCount)
            : Mathf.Max(
                0,
                saveData.cumulativeBattleTurnCount
                    - saveData.playerTurnCount);
        playerHealth.RestoreRunHealth(
            saveData.currentHealth,
            saveData.maxHealth);
        currencyManager.RestoreRunMoney(saveData.money);
        playerInventory.RestoreRunState(
            saveData.inventoryItemAssetNames,
            shopManager.ResolveSavedItem);
        if (savedFlow == GameFlowState.Shop)
        {
            if (!shopManager.RestoreShopRunState(
                    saveData.shop,
                    saveData.shopRefreshCost))
            {
                return false;
            }
        }
        else
        {
            shopManager.RestoreRunState(saveData.shopRefreshCost);
        }

        pendingRestoredRun = saveData.startSelectedBattleFresh
            ? null
            : saveData;
        return true;
    }

    private void StartRestoredFlow()
    {
        if (pendingRestoredRun == null)
        {
            StartCurrentBattle();
            return;
        }

        GameFlowState savedFlow = (GameFlowState)pendingRestoredRun.flowState;

        switch (savedFlow)
        {
            case GameFlowState.BattleClear:
                StartRestoredBattleClear();
                break;
            case GameFlowState.Shop:
                StartRestoredShop();
                break;
            default:
                StartCurrentBattle();
                break;
        }
    }

    private void StartRestoredBattleClear()
    {
        RunSaveData restoredRun = pendingRestoredRun;
        pendingRestoredRun = null;

        if (!TryGetCurrentBattle(out BattleData battle)
            || battle.TilePrefab == null
            || !boardManager.ConfigureBoard(
                battle.BoardCount,
                battle.TilePrefab)
            || !RestorePlayerRuntime(restoredRun))
        {
            RunSaveSystem.DeleteSave();
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        waveManager.StopBattle();
        combatFeedback?.RestoreRunState(restoredRun);
        RestoreRandomState(restoredRun);
        currentState = GameFlowState.BattleClear;
        SetPanels(false, false, false);
        SetInputLocked(true);
        gameStartUI?.PrepareRestoredBattle(
            restoredRun.combatReport,
            battle);
        StateChanged?.Invoke();
        battleClearCoroutine = StartCoroutine(
            ResumeRestoredBattleClear(battle));
    }

    private IEnumerator ResumeRestoredBattleClear(BattleData battle)
    {
        bool isFinalBossBattle = battle.IsBoss
            && !TryGetNextBattlePosition(out _, out _);

        if (gameStartUI != null)
        {
            yield return gameStartUI.PlayBattleClear(
                battle,
                isFinalBossBattle);
        }

        if (currentState != GameFlowState.BattleClear)
        {
            battleClearCoroutine = null;
            yield break;
        }

        battleClearCoroutine = null;

        if (isFinalBossBattle)
        {
            CompleteRunAndLoadEnding();
            yield break;
        }

        NodeMapSaveSystem.CompleteActiveNode();
        if (!LoadingTransitionController.LoadScene("NodeMap"))
        {
            SceneManager.LoadScene("NodeMap");
        }
    }

    private void StartRestoredShop()
    {
        RunSaveData restoredRun = pendingRestoredRun;
        pendingRestoredRun = null;

        if (!TryGetCurrentBattle(out BattleData battle)
            || battle.TilePrefab == null
            || !boardManager.ConfigureBoard(
                battle.BoardCount,
                battle.TilePrefab)
            || !RestorePlayerRuntime(restoredRun))
        {
            RunSaveSystem.DeleteSave();
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        waveManager.StopBattle();
        combatFeedback?.RestoreRunState(restoredRun);
        RestoreRandomState(restoredRun);
        gameStartUI?.ResetAndHide();
        currentState = GameFlowState.Shop;
        SetPanels(false, false, true);
        SetInputLocked(true);

        if (goToBattleButton != null)
        {
            goToBattleButton.interactable = true;
        }

        if (goToBattleText != null)
        {
            goToBattleText.text = GetShopExitLabel();
        }

        StateChanged?.Invoke();
    }

    private bool RestorePlayerRuntime(RunSaveData saveData)
    {
        if (saveData == null || !boardManager.TryGetTilePosition(
                saveData.playerTileIndex,
                out Vector3 playerPosition))
        {
            return false;
        }

        playerPosition += playerSpawnOffset;
        playerMove.RestoreRunState(
            playerPosition,
            saveData.playerFacingRight,
            saveData.playerTurnCount,
            saveData.nextPushAvailableTurn);
        playerHealth.RestoreStatusRunState(saveData.playerStatusEffects);
        return true;
    }

    private bool RestoreBattleRuntime(
        BattleData battle,
        RunSaveData saveData)
    {
        if (battle == null || saveData == null
            || !RestorePlayerRuntime(saveData))
        {
            return false;
        }

        if (!waveManager.RestoreBattle(
                battle.Waves,
                battle.SpawnTerm,
                saveData))
        {
            return false;
        }

        combatFeedback?.RestoreRunState(saveData);

        if (rewardManager != null && !rewardManager.RestoreRunState(
                saveData.droppedItems,
                shopManager.ResolveSavedItem))
        {
            return false;
        }

        RestoreRandomState(saveData);

        return true;
    }

    private void RestoreRandomState(RunSaveData saveData)
    {
        if (saveData != null
            && !string.IsNullOrWhiteSpace(saveData.randomStateJson)
            && saveData.randomStateJson.Length > 2)
        {
            try
            {
                UnityEngine.Random.state = JsonUtility.FromJson<
                    UnityEngine.Random.State>(saveData.randomStateJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Saved random state could not be restored: {exception.Message}",
                    this);
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (!suppressExitSave)
        {
            SaveCurrentRun();
        }
    }

    private bool HandleWantsToQuit()
    {
        if (!suppressExitSave)
        {
            SaveCurrentRun();
        }

        return true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && !suppressExitSave)
        {
            SaveCurrentRun();
        }
    }

    private void OnDisable()
    {
        Application.wantsToQuit -= HandleWantsToQuit;
        StopBattleStartPresentation();
        webAutosaveCoroutine = null;

        if (battleClearCoroutine != null)
        {
            StopCoroutine(battleClearCoroutine);
            battleClearCoroutine = null;
        }

        if (waveManager != null)
        {
            waveManager.BattleCompleted -= HandleBattleCompleted;
            waveManager.BattleFailed -= HandleBattleFailed;
            waveManager.EnemyTurnCycleCompleted -=
                HandleWebEnemyTurnCycleCompleted;
        }

        if (shopManager != null)
        {
            shopManager.OffersChanged -= HandleWebShopStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated -= HandlePlayerDefeated;
        }

        if (deckManager != null)
        {
            deckManager.BulletsDepleted -= HandleBulletsDepleted;
        }

        if (goToMaintenanceButton != null)
        {
            goToMaintenanceButton.onClick.RemoveListener(GoToMaintenance);
        }

        if (goToBattleButton != null)
        {
            goToBattleButton.onClick.RemoveListener(GoToBattle);
        }
    }

    public void GoToMaintenance()
    {
        if (currentState != GameFlowState.BattleClear
            || !TryGetCurrentBattle(out _))
        {
            return;
        }

        SetInputLocked(true);
        LoadingTransitionController.RunTransition(OpenShopAfterBattleClear);
    }

    private void OpenShopAfterBattleClear()
    {
        if (currentState != GameFlowState.BattleClear)
        {
            return;
        }

        GameStatistics.SaveCheckpoint();
        currentState = GameFlowState.Shop;
        SetPanels(false, false, true);
        SetInputLocked(true);

        if (goToBattleButton != null)
        {
            goToBattleButton.interactable = true;
        }

        if (goToBattleText != null)
        {
            goToBattleText.text = GetShopExitLabel();
        }

        shopManager.OpenShop();
        StateChanged?.Invoke();
    }

    public void GoToBattle()
    {
        if (currentState != GameFlowState.Shop)
        {
            return;
        }

        GameStatistics.SaveCheckpoint();
        SetInputLocked(true);
        LoadingTransitionController.RunTransition(ContinueToBattle);
    }

    private void ContinueToBattle()
    {
        if (currentState != GameFlowState.Shop)
        {
            return;
        }

        if (TryGetNextBattlePosition(
                out int nextStageIndex,
                out int nextBattleIndex))
        {
            bool startsNewStage = nextStageIndex != currentStageIndex;

            if (startsNewStage)
            {
                deckManager.PrepareForNewStage();
            }

            currentStageIndex = nextStageIndex;
            currentBattleIndex = nextBattleIndex;
            StartCurrentBattle();
            return;
        }

        ShowRunComplete("RUN COMPLETE");
    }

    private void StartCurrentBattle()
    {
        StopBattleStartPresentation();
        waveManager.StopBattle();

        if (!TryGetCurrentBattle(out BattleData battle)
            || battle.TilePrefab == null
            || !boardManager.ConfigureBoard(
                battle.BoardCount,
                battle.TilePrefab))
        {
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        MovePlayerToBoardCenter(battle.BoardCount);
        currentState = GameFlowState.Battle;
        SetPanels(true, false, false);
        SetInputLocked(true);
        StateChanged?.Invoke();

        if (pendingRestoredRun != null)
        {
            gameStartUI?.PrepareRestoredBattle(
                pendingRestoredRun.combatReport,
                battle);
            BeginBattleGameplay(battle);
            return;
        }

        if (gameStartUI != null)
        {
            battleStartCoroutine = StartCoroutine(
                PlayBattleStart(battle));
            return;
        }

        BeginBattleGameplay(battle);
    }

    private IEnumerator PlayBattleStart(BattleData battle)
    {
        yield return gameStartUI.Play(
            CurrentStage,
            battle,
            () => BeginBattleGameplay(battle));
        battleStartCoroutine = null;
    }

    private void BeginBattleGameplay(BattleData battle)
    {
        if (currentState != GameFlowState.Battle || battle == null)
        {
            return;
        }

        bool beganBattle;

        if (pendingRestoredRun != null)
        {
            RunSaveData restoredRun = pendingRestoredRun;
            pendingRestoredRun = null;
            beganBattle = RestoreBattleRuntime(battle, restoredRun);
        }
        else
        {
            beganBattle = waveManager.BeginBattle(
                battle.Waves,
                battle.SpawnTerm);
        }

        if (!beganBattle)
        {
            RunSaveSystem.DeleteSave();
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        SetInputLocked(false);
        RequestWebAutosave();
    }

    private void HandleWebEnemyTurnCycleCompleted(int _)
    {
        RequestWebAutosave();
    }

    private void HandleWebShopStateChanged()
    {
        RequestWebAutosave();
    }

    private void RequestWebAutosave()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer
            || suppressExitSave || webAutosaveCoroutine != null)
        {
            return;
        }

        webAutosaveCoroutine = StartCoroutine(
            SaveWebCheckpointWhenSettled());
    }

    private IEnumerator SaveWebCheckpointWhenSettled()
    {
        yield return null;

        while ((currentState == GameFlowState.Battle
                && !IsCombatSettledForExit)
               || (currentState == GameFlowState.Shop
                   && shopManager != null
                   && shopManager.IsRefreshing))
        {
            yield return null;
        }

        webAutosaveCoroutine = null;

        if (!suppressExitSave)
        {
            SaveCurrentRun();
        }
    }

    private void HandleBattleCompleted()
    {
        if (currentState != GameFlowState.Battle
            || battleClearCoroutine != null)
        {
            return;
        }

        StopBattleStartPresentation();
        SetInputLocked(true);
        battleClearCoroutine = StartCoroutine(ShowBattleClearWhenSettled());
    }

    private IEnumerator ShowBattleClearWhenSettled()
    {
        while (playerMove != null
               && (playerMove.IsShooting
                   || playerMove.IsActing
                   || playerMove.IsEnemyTurnResolving))
        {
            yield return null;
        }

        if (currentState != GameFlowState.Battle
            || !TryGetCurrentBattle(out BattleData battle))
        {
            battleClearCoroutine = null;
            yield break;
        }

        deckManager.ClearLoadedBullets();

        currentState = GameFlowState.BattleClear;
        SetPanels(false, false, false);
        SetInputLocked(true);
        StateChanged?.Invoke();
        bool isFinalBossBattle = battle.IsBoss
            && !TryGetNextBattlePosition(out _, out _);

        if (gameStartUI != null)
        {
            yield return gameStartUI.PlayBattleClear(
                battle,
                isFinalBossBattle);
        }

        if (currentState != GameFlowState.BattleClear)
        {
            battleClearCoroutine = null;
            yield break;
        }

        battleClearCoroutine = null;

        if (isFinalBossBattle)
        {
            CompleteRunAndLoadEnding();
            yield break;
        }

        SaveCurrentRun();
        NodeMapSaveSystem.CompleteActiveNode();
        if (!LoadingTransitionController.LoadScene("NodeMap"))
        {
            SceneManager.LoadScene("NodeMap");
        }
    }

    private void CompleteRunAndLoadEnding()
    {
        if (currentState != GameFlowState.BattleClear)
        {
            return;
        }

        suppressExitSave = true;
        RunSaveSystem.DeleteSave();
        NodeMapSaveSystem.DeleteSave();
        GameStatistics.EndRun(true);
        currentState = GameFlowState.RunComplete;
        SetPanels(false, false, false);
        SetInputLocked(true);
        StateChanged?.Invoke();

        if (!LoadingTransitionController.LoadScene("Ending"))
        {
            SceneManager.LoadScene("Ending");
        }
    }

    private void HandleBattleFailed()
    {
        StopBattleStartPresentation();

        if (battleClearCoroutine != null)
        {
            StopCoroutine(battleClearCoroutine);
            battleClearCoroutine = null;
        }

        ShowRunComplete("BATTLE ERROR");
    }

    private void HandlePlayerDefeated()
    {
        if (currentState != GameFlowState.Battle)
        {
            return;
        }

        FailCurrentRun();
    }

    private void HandleBulletsDepleted()
    {
        if (currentState != GameFlowState.Battle
            || waveManager != null && waveManager.IsBattleCompleted)
        {
            return;
        }

        FailCurrentRun();
    }

    private void FailCurrentRun()
    {
        if (currentState != GameFlowState.Battle)
        {
            return;
        }

        StopBattleStartPresentation();

        if (battleClearCoroutine != null)
        {
            StopCoroutine(battleClearCoroutine);
            battleClearCoroutine = null;
        }

        waveManager.StopBattle();
        suppressExitSave = true;
        RunSaveSystem.DeleteSave();
        NodeMapSaveSystem.DeleteSave();
        GameStatistics.EndRun(false);
        currentState = GameFlowState.RunFailed;
        SetPanels(false, true, false);
        SetInputLocked(true);

        if (goToMaintenanceButton != null)
        {
            goToMaintenanceButton.interactable = false;
        }

        if (goToMaintenanceText != null)
        {
            goToMaintenanceText.text = "GAME OVER";
        }

        StateChanged?.Invoke();
    }

    private void ShowRunComplete(string label)
    {
        StopBattleStartPresentation();

        if (label == "RUN COMPLETE")
        {
            suppressExitSave = true;
            RunSaveSystem.DeleteSave();
            GameStatistics.EndRun(true);
        }

        currentState = GameFlowState.RunComplete;
        SetPanels(false, true, false);
        SetInputLocked(true);

        if (goToMaintenanceButton != null)
        {
            goToMaintenanceButton.interactable = false;
        }

        if (goToMaintenanceText != null)
        {
            goToMaintenanceText.text = label;
        }

        StateChanged?.Invoke();
    }

    private void StopBattleStartPresentation()
    {
        if (battleStartCoroutine != null)
        {
            StopCoroutine(battleStartCoroutine);
            battleStartCoroutine = null;
        }

        // UnityEngine.Object keeps a managed wrapper after native destruction.
        // A null-conditional call only checks the wrapper and can therefore call
        // into an already destroyed GameStartUI while the application exits.
        if (gameStartUI != null)
        {
            gameStartUI.ResetAndHide();
        }
    }

    private bool TryGetCurrentBattle(out BattleData battle)
    {
        battle = null;

        if (stages == null || currentStageIndex < 0
            || currentStageIndex >= stages.Length)
        {
            return false;
        }

        StageData stage = stages[currentStageIndex];

        if (stage == null || currentBattleIndex < 0
            || currentBattleIndex >= stage.Battles.Count)
        {
            return false;
        }

        battle = stage.Battles[currentBattleIndex];
        return battle != null;
    }

    private bool TryGetNextBattlePosition(
        out int nextStageIndex,
        out int nextBattleIndex)
    {
        nextStageIndex = -1;
        nextBattleIndex = -1;

        if (!TryGetCurrentBattle(out _))
        {
            return false;
        }

        StageData currentStage = stages[currentStageIndex];
        int followingBattleIndex = currentBattleIndex + 1;

        if (followingBattleIndex < currentStage.Battles.Count)
        {
            nextStageIndex = currentStageIndex;
            nextBattleIndex = followingBattleIndex;
            return true;
        }

        if (!TryFindNextStageIndex(
                currentStageIndex + 1,
                out nextStageIndex))
        {
            return false;
        }

        nextBattleIndex = 0;
        return true;
    }

    private string GetShopExitLabel()
    {
        if (!TryGetNextBattlePosition(
                out int nextStageIndex,
                out _))
        {
            return "RUN COMPLETE";
        }

        return nextStageIndex == currentStageIndex
            ? "TO BATTLE"
            : "NEXT STAGE";
    }

    private bool TryFindNextStageIndex(int startIndex, out int stageIndex)
    {
        stageIndex = -1;

        if (stages == null)
        {
            return false;
        }

        for (int index = Mathf.Max(0, startIndex);
             index < stages.Length;
             index++)
        {
            if (stages[index] != null && stages[index].Battles.Count > 0)
            {
                stageIndex = index;
                return true;
            }
        }

        return false;
    }

    private void MovePlayerToBoardCenter(int boardCount)
    {
        int centerTileIndex = Mathf.Clamp(boardCount / 2, 0, boardCount - 1);

        if (boardManager.TryGetTilePosition(
                centerTileIndex,
                out Vector3 centerTilePosition))
        {
            playerMove.transform.position = centerTilePosition
                + playerSpawnOffset;
        }
    }

    private void SetInputLocked(bool inputLocked)
    {
        if (playerMove != null)
        {
            playerMove.SetInputLocked(inputLocked);
        }
    }

    private void SetPanels(
        bool showMainGame,
        bool showStageClear,
        bool showShop)
    {
        if (mainGamePanel != null)
        {
            mainGamePanel.SetActive(showMainGame);
        }

        if (stageClearPanel != null)
        {
            stageClearPanel.SetActive(showStageClear);
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(showShop);
        }
    }

    private bool ValidateReferences()
    {
        if (waveManager != null && boardManager != null
            && shopManager != null && deckManager != null
            && deckManager.TotalBulletCount
                >= DeckManager.MinimumOwnedBulletCount
            && currencyManager != null && playerInventory != null
            && playerMove != null
            && playerHealth != null
            && mainGamePanel != null && stageClearPanel != null
            && shopPanel != null && goToMaintenanceButton != null
            && goToBattleButton != null
            && ValidateStageConfiguration())
        {
            return true;
        }

        Debug.LogError(
            "State Manager requires valid references, navigation buttons, "
            + "at least one starting bullet, and a valid stage configuration.",
            this);
        return false;
    }

    private bool ValidateStageConfiguration()
    {
        bool foundConfiguredStage = false;

        if (stages == null)
        {
            return false;
        }

        foreach (StageData stage in stages)
        {
            if (stage == null || stage.Battles.Count == 0)
            {
                continue;
            }

            foundConfiguredStage = true;
            int lastBattleIndex = stage.Battles.Count - 1;

            for (int battleIndex = 0;
                 battleIndex < stage.Battles.Count;
                 battleIndex++)
            {
                BattleData battle = stage.Battles[battleIndex];

                if (battle == null || battle.TilePrefab == null
                    || battle.Waves.Count == 0
                    || battle.IsBoss != (battleIndex == lastBattleIndex)
                    || !ValidateBattleWaves(stage, battle, battleIndex))
                {
                    Debug.LogError(
                        $"Stage '{stage.name}' has an invalid battle at index {battleIndex}. The final battle must be the only Boss battle.",
                        stage);
                    return false;
                }
            }
        }

        return foundConfiguredStage;
    }

    private bool ValidateBattleWaves(
        StageData stage,
        BattleData battle,
        int battleIndex)
    {
        int maximumEnemyCount = Mathf.Max(0, battle.BoardCount - 1);

        for (int waveIndex = 0; waveIndex < battle.Waves.Count; waveIndex++)
        {
            EnemyWave wave = battle.Waves[waveIndex];

            if (wave == null || wave.Enemies.Count == 0)
            {
                return false;
            }

            int enemyCount = 0;

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry == null || entry.EnemyData == null
                    || entry.Count <= 0)
                {
                    return false;
                }

                enemyCount += entry.Count;
            }

            if (enemyCount <= 0 || enemyCount > maximumEnemyCount)
            {
                Debug.LogError(
                    $"Stage '{stage.name}', battle {battleIndex}, wave {waveIndex} does not fit on its board.",
                    stage);
                return false;
            }
        }

        return true;
    }
}
