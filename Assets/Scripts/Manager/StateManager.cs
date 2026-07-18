using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GameFlowState
{
    Initializing = 0,
    Battle = 1,
    BattleClear = 2,
    Shop = 3,
    RunComplete = 4,
    RunFailed = 5
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
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;

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

    public event Action StateChanged;

    public int CurrentStageIndex => currentStageIndex;
    public int CurrentBattleIndex => currentBattleIndex;
    public GameFlowState CurrentState => currentState;

    private void Awake()
    {
        SetPanels(false, false, false);

        if (playerMove != null)
        {
            playerMove.SetInputLocked(true);
        }
    }

    private void OnEnable()
    {
        if (waveManager != null)
        {
            waveManager.BattleCompleted += HandleBattleCompleted;
            waveManager.BattleFailed += HandleBattleFailed;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated += HandlePlayerDefeated;
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
        if (!ValidateReferences()
            || !TryFindNextStageIndex(
                Mathf.Max(0, startingStageIndex),
                out currentStageIndex))
        {
            ShowRunComplete("CONFIGURATION ERROR");
            return;
        }

        currentBattleIndex = 0;
        StartCurrentBattle();
    }

    private void OnDisable()
    {
        if (battleClearCoroutine != null)
        {
            StopCoroutine(battleClearCoroutine);
            battleClearCoroutine = null;
        }

        if (waveManager != null)
        {
            waveManager.BattleCompleted -= HandleBattleCompleted;
            waveManager.BattleFailed -= HandleBattleFailed;
        }

        if (playerHealth != null)
        {
            playerHealth.Defeated -= HandlePlayerDefeated;
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

        if (TryGetNextBattlePosition(
                out int nextStageIndex,
                out int nextBattleIndex))
        {
            currentStageIndex = nextStageIndex;
            currentBattleIndex = nextBattleIndex;
            StartCurrentBattle();
            return;
        }

        ShowRunComplete("RUN COMPLETE");
    }

    private void StartCurrentBattle()
    {
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
        SetInputLocked(false);
        StateChanged?.Invoke();

        if (!waveManager.BeginBattle(battle.Waves, battle.SpawnTerm))
        {
            ShowRunComplete("CONFIGURATION ERROR");
        }
    }

    private void HandleBattleCompleted()
    {
        if (currentState != GameFlowState.Battle
            || battleClearCoroutine != null)
        {
            return;
        }

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

        battleClearCoroutine = null;

        if (currentState != GameFlowState.Battle
            || !TryGetCurrentBattle(out _))
        {
            yield break;
        }

        currentState = GameFlowState.BattleClear;
        SetPanels(false, true, false);
        SetInputLocked(true);

        if (goToMaintenanceButton != null)
        {
            goToMaintenanceButton.interactable = true;
        }

        if (goToMaintenanceText != null)
        {
            goToMaintenanceText.text = "TO MAINTENANCE";
        }

        StateChanged?.Invoke();
    }

    private void HandleBattleFailed()
    {
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

        if (battleClearCoroutine != null)
        {
            StopCoroutine(battleClearCoroutine);
            battleClearCoroutine = null;
        }

        waveManager.StopBattle();
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
            && shopManager != null && playerMove != null
            && playerHealth != null
            && mainGamePanel != null && stageClearPanel != null
            && shopPanel != null && goToMaintenanceButton != null
            && goToBattleButton != null
            && ValidateStageConfiguration())
        {
            return true;
        }

        Debug.LogError(
            "State Manager references and navigation buttons must be assigned in the Inspector.",
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
                if (entry == null || entry.EnemyPrefab == null
                    || entry.EnemyPrefab.Data == null || entry.Count <= 0)
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
