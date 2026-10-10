using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public sealed class TreasureCombatSceneController : MonoBehaviour
{
    private const string NodeMapSceneName = "NodeMap";
    private const int PlayerTileIndex = 0;
    private const int PlayerLaneIndex = 0;
    private const int ExitTileIndex = 9;
    private const int ChestCount = 3;
    private const int RewardChoiceCount = 3;

    [Header("Scene References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private ShopManager dataResolver;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RelicManager relicManager;
    [SerializeField] private TMP_Text exitLabel;
    [SerializeField] private Transform cameraAnchor;

    [Header("Placement")]
    [SerializeField] private Vector3 playerSpawnOffset =
        new Vector3(0f, 0.3f, 0f);
    [SerializeField] private Vector3 exitLabelOffset =
        new Vector3(0f, 1.35f, 0f);
    [SerializeField] private Color exitTileColor =
        new Color(0.08f, 0.9f, 0.27f, 0.72f);

    private readonly List<TreasureChestTarget> chests =
        new List<TreasureChestTarget>();
    private readonly List<BulletInstance> ownedBullets =
        new List<BulletInstance>();
    private readonly List<RelicData> offers = new List<RelicData>();
    private readonly UnityEngine.UI.Button[] relicButtons =
        new UnityEngine.UI.Button[RewardChoiceCount];
    private readonly UnityEngine.UI.Image[] relicIcons =
        new UnityEngine.UI.Image[RewardChoiceCount];
    private readonly TMP_Text[] relicNames =
        new TMP_Text[RewardChoiceCount];
    private readonly TMP_Text[] relicDescriptions =
        new TMP_Text[RewardChoiceCount];

    private GameObject rewardPanel;
    private GameObject choicesPanel;
    private GameObject chestHousing;
    private TMP_Text rewardTitle;
    private TMP_Text rewardKicker;
    private TMP_Text instructionText;
    private UnityEngine.UI.Button skipButton;
    private RelicTooltipUI relicTooltip;
    private RunSaveData runData;
    private Coroutine rewardPresentationCoroutine;
    private bool initialized;
    private bool leaving;
    private bool transitionRequested;

    private IEnumerator Start()
    {
        ResolveReferences();
        HideBattleOnlyPresentation();
        playerMove?.SetInputLocked(true);

        if (!TryRestoreRun(out bool firstVisit)
            || !ResolveAndRestoreChests())
        {
            Debug.LogError(
                "Treasure combat scene could not restore the current run.",
                this);
            yield break;
        }

        ResolveRewardUi();
        BindRewardUi();
        PlacePlayer(firstVisit);
        PlaceTreasureObjects();
        ConfigureCameraFraming();
        playerMove.SetWaveManager(waveManager);
        SubscribeRuntimeEvents();
        initialized = true;

        if (!SaveTreasureState())
        {
            Debug.LogWarning(
                "Treasure combat state could not be checkpointed on entry.",
                this);
        }

        // BoardManager creates tile visuals in Start. Apply the persistent
        // exit tint after that lifecycle pass has completed.
        yield return null;
        boardManager.SetTilePersistentHighlightColor(
            ExitTileIndex,
            PlayerLaneIndex,
            exitTileColor);

        if (runData.treasureRewardSelectionActive
            || runData.treasurePendingRewardCount > 0)
        {
            QueueRewardPresentation();
        }
        else
        {
            playerMove.SetInputLocked(false);
        }
    }

    private void OnDisable()
    {
        if (initialized && !leaving)
        {
            SaveTreasureState();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeRuntimeEvents();
        UnbindRewardUi();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && initialized && !leaving)
        {
            SaveTreasureState();
        }
    }

    public void SelectRelic(int index)
    {
        if (!initialized || leaving
            || !runData.treasureRewardSelectionActive
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
                    ? "유물 보관함이 가득 찼습니다. 다른 유물을 고르거나 건너뛰세요."
                    : "이 유물을 획득할 수 없습니다.";
            }
            return;
        }

        CompleteCurrentReward();
    }

    public void SkipRelic()
    {
        if (!initialized || leaving
            || !runData.treasureRewardSelectionActive)
        {
            return;
        }

        CompleteCurrentReward();
    }

    private bool TryRestoreRun(out bool firstVisit)
    {
        firstVisit = false;
        if (dataResolver == null || deckManager == null
            || currencyManager == null || playerMove == null
            || playerShoot == null || playerHealth == null
            || playerInventory == null || relicManager == null
            || boardManager == null || waveManager == null
            || !RunSaveSystem.TryLoad(out runData)
            || !deckManager.RestoreRunState(
                runData.bullets,
                dataResolver.ResolveSavedBullet,
                runData.paidBulletRemovalCount,
                runData.nextCycleAcquisitionOrders)
            || !relicManager.RestoreRunState(runData.relics))
        {
            return false;
        }

        RestoreRandomState();
        playerHealth.RestoreRunHealth(
            runData.currentHealth,
            runData.maxHealth);
        playerHealth.RestoreStatusRunState(runData.playerStatusEffects);
        currencyManager.RestoreRunMoney(runData.money);
        playerInventory.RestoreRunState(
            runData.inventoryItemAssetNames,
            dataResolver.ResolveSavedItem);
        stateManager?.ConfigureExternalSceneState(
            runData.stageIndex,
            runData.battleIndex,
            GameFlowState.Treasure,
            true);

        int cumulativeCount = Mathf.Max(
            0,
            runData.cumulativeBattleTurnCount);
        foreach (TurnCountText countText in FindObjectsByType<TurnCountText>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            countText.SetExternalCount(cumulativeCount);
        }

        firstVisit = !HasResumableCombatTreasureState(runData);
        if (firstVisit)
        {
            InitializeNewTreasureVisit();
        }
        else
        {
            NormalizeTreasureVisitState();
        }

        runData.flowState = (int)GameFlowState.Treasure;
        runData.startSelectedBattleFresh = false;
        return true;
    }

    private void InitializeNewTreasureVisit()
    {
        deckManager.GetOwnedBullets(ownedBullets);
        int maxHealth = TreasureChestHealthCalculator.CalculateRoundedAdpc(
            ownedBullets,
            deckManager.MaxReloadAmount);

        runData.treasureVisitActive = true;
        runData.treasureChestMaxHealth = maxHealth;
        runData.treasureChestCurrentHealth.Clear();
        runData.treasureChestDestroyed.Clear();
        for (int index = 0; index < ChestCount; index++)
        {
            runData.treasureChestCurrentHealth.Add(maxHealth);
            runData.treasureChestDestroyed.Add(false);
        }
        runData.treasurePendingRewardCount = 0;
        runData.treasureRewardSelectionActive = false;
        runData.treasureOfferRelicIds.Clear();
        runData.treasureChestOpened = false;
        runData.treasureChoiceResolved = false;
        runData.playerTileIndex = PlayerTileIndex;
        runData.playerLaneIndex = PlayerLaneIndex;
        runData.playerFacingRight = true;
    }

    private void NormalizeTreasureVisitState()
    {
        int maxHealth = Mathf.Max(1, runData.treasureChestMaxHealth);
        runData.treasureChestMaxHealth = maxHealth;
        NormalizeListCount(
            runData.treasureChestCurrentHealth,
            ChestCount,
            maxHealth);
        NormalizeListCount(
            runData.treasureChestDestroyed,
            ChestCount,
            false);

        int destroyedCount = 0;
        for (int index = 0; index < ChestCount; index++)
        {
            bool destroyed = runData.treasureChestDestroyed[index]
                || runData.treasureChestCurrentHealth[index] <= 0;
            runData.treasureChestDestroyed[index] = destroyed;
            runData.treasureChestCurrentHealth[index] = destroyed
                ? 0
                : Mathf.Clamp(
                    runData.treasureChestCurrentHealth[index],
                    1,
                    maxHealth);
            if (destroyed)
            {
                destroyedCount++;
            }
        }

        runData.treasurePendingRewardCount = Mathf.Clamp(
            runData.treasurePendingRewardCount,
            0,
            destroyedCount);
        if (!runData.treasureRewardSelectionActive)
        {
            runData.treasureOfferRelicIds.Clear();
        }
    }

    private bool ResolveAndRestoreChests()
    {
        chests.Clear();
        chests.AddRange(FindObjectsByType<TreasureChestTarget>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None));
        chests.Sort((left, right) => left.TileIndex.CompareTo(
            right.TileIndex));
        if (chests.Count != ChestCount)
        {
            Debug.LogError(
                $"Treasure scene requires {ChestCount} chests, found "
                + chests.Count + ".",
                this);
            return false;
        }

        for (int index = 0; index < chests.Count; index++)
        {
            TreasureChestTarget chest = chests[index];
            chest.Destroyed += HandleChestDestroyed;
            chest.HealthChanged += HandleChestHealthChanged;
            chest.RestoreState(
                runData.treasureChestMaxHealth,
                runData.treasureChestCurrentHealth[index],
                runData.treasureChestDestroyed[index]);
        }

        if (runData.treasureChestDestroyed.Contains(true))
        {
            WithdrawRemainingChests();
        }

        return true;
    }

    private void PlacePlayer(bool firstVisit)
    {
        int tileIndex = firstVisit
            ? PlayerTileIndex
            : Mathf.Clamp(
                runData.playerTileIndex,
                0,
                Mathf.Max(0, boardManager.BoardCount - 1));
        if (!boardManager.TryGetTilePosition(
                tileIndex,
                PlayerLaneIndex,
                out Vector3 playerPosition))
        {
            return;
        }

        playerMove.RestoreRunState(
            playerPosition + playerSpawnOffset,
            firstVisit || runData.playerFacingRight,
            runData.playerTurnCount,
            runData.nextPushAvailableTurn,
            PlayerLaneIndex);
    }

    private void PlaceTreasureObjects()
    {
        foreach (TreasureChestTarget chest in chests)
        {
            chest.Place(boardManager);
        }

        if (exitLabel == null || !boardManager.TryGetTilePosition(
                ExitTileIndex,
                PlayerLaneIndex,
                out Vector3 exitPosition))
        {
            return;
        }

        RectTransform rect = exitLabel.rectTransform;
        rect.pivot = new Vector2(0.5f, 0.5f);
        exitLabel.alignment = TextAlignmentOptions.Center;
        exitLabel.transform.position = exitPosition + exitLabelOffset;
        exitLabel.text = "다음 지역으로 이동";
    }

    private void ConfigureCameraFraming()
    {
        cameraAnchor ??= FindNamedTransform("Treasure Camera Anchor");
        if (cameraAnchor == null
            || !boardManager.TryGetTilePosition(0, 0, out Vector3 first)
            || !boardManager.TryGetTilePosition(
                ExitTileIndex,
                0,
                out Vector3 last))
        {
            return;
        }

        cameraAnchor.position = (first + last) * 0.5f + playerSpawnOffset;
        Camera mainCamera = Camera.main;
        CinemachineCamera cinemachineCamera = mainCamera == null
            ? null
            : mainCamera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = cameraAnchor;
        }
    }

    private void HandleChestHealthChanged(TreasureChestTarget chest)
    {
        if (!initialized || leaving)
        {
            return;
        }

        CaptureChestState();
        SaveTreasureState();
    }

    private void HandleChestDestroyed(TreasureChestTarget chest)
    {
        if (!initialized || leaving)
        {
            return;
        }

        int index = chests.IndexOf(chest);
        if (index < 0 || runData.treasureChestDestroyed[index])
        {
            return;
        }

        runData.treasureChestDestroyed[index] = true;
        runData.treasureChestCurrentHealth[index] = 0;
        runData.treasurePendingRewardCount = Mathf.Min(
            ChestCount,
            runData.treasurePendingRewardCount + 1);
        playerMove.SetInputLocked(true);
        SaveTreasureState();
        QueueRewardPresentation();
    }

    private void QueueRewardPresentation()
    {
        if (rewardPresentationCoroutine == null && isActiveAndEnabled)
        {
            rewardPresentationCoroutine = StartCoroutine(
                PresentNextRewardWhenSettled());
        }
    }

    private IEnumerator PresentNextRewardWhenSettled()
    {
        playerMove.SetInputLocked(true);
        while (playerShoot != null && playerShoot.IsFiring)
        {
            yield return null;
        }

        if (WithdrawRemainingChests())
        {
            SaveTreasureState();
        }

        while (playerMove != null && playerMove.IsActing)
        {
            yield return null;
        }

        while (runData.treasurePendingRewardCount > 0)
        {
            if (TryPrepareCurrentOffer())
            {
                ShowRewardPanel();
                rewardPresentationCoroutine = null;
                yield break;
            }

            runData.treasurePendingRewardCount--;
            runData.treasureRewardSelectionActive = false;
            runData.treasureOfferRelicIds.Clear();
            SaveTreasureState();
            yield return null;
        }

        HideRewardPanel();
        playerMove.SetInputLocked(false);
        rewardPresentationCoroutine = null;
    }

    private bool WithdrawRemainingChests()
    {
        bool hasDestroyedChest = false;
        foreach (TreasureChestTarget chest in chests)
        {
            if (chest != null && chest.IsDestroyed)
            {
                hasDestroyedChest = true;
                break;
            }
        }

        if (!hasDestroyedChest)
        {
            return false;
        }

        bool withdrewAny = false;
        foreach (TreasureChestTarget chest in chests)
        {
            if (chest != null && !chest.IsDestroyed)
            {
                withdrewAny |= chest.Withdraw();
            }
        }

        return withdrewAny;
    }

    private bool TryPrepareCurrentOffer()
    {
        offers.Clear();
        if (runData.treasureRewardSelectionActive)
        {
            foreach (string relicId in runData.treasureOfferRelicIds)
            {
                RelicData relic = relicManager.ResolveRelicData(relicId);
                if (relic != null)
                {
                    offers.Add(relic);
                }
            }

            if (offers.Count > 0)
            {
                return true;
            }

            runData.treasureRewardSelectionActive = false;
            runData.treasureOfferRelicIds.Clear();
        }

        relicManager.GetUniformRewardChoices(RewardChoiceCount, offers);
        if (offers.Count == 0)
        {
            return false;
        }

        runData.treasureRewardSelectionActive = true;
        runData.treasureOfferRelicIds.Clear();
        foreach (RelicData offer in offers)
        {
            runData.treasureOfferRelicIds.Add(offer.Id);
        }
        SaveTreasureState();
        return true;
    }

    private void ShowRewardPanel()
    {
        if (rewardPanel == null)
        {
            Debug.LogError("Treasure reward panel is missing.", this);
            return;
        }

        rewardPanel.SetActive(true);
        rewardPanel.transform.SetAsLastSibling();
        chestHousing?.SetActive(false);
        choicesPanel?.SetActive(true);
        if (rewardTitle != null)
        {
            rewardTitle.text = "RELIC RECOVERY";
        }
        if (rewardKicker != null)
        {
            rewardKicker.text =
                $"RECOVERED CACHE  //  {runData.treasurePendingRewardCount} REMAINING";
        }
        if (instructionText != null)
        {
            instructionText.text = "유물 하나를 선택하거나 보상을 포기할 수 있습니다.";
        }

        for (int index = 0; index < relicButtons.Length; index++)
        {
            bool visible = index < offers.Count;
            UnityEngine.UI.Button button = relicButtons[index];
            if (button != null)
            {
                button.gameObject.SetActive(visible);
                button.interactable = visible;
            }
            if (!visible)
            {
                continue;
            }

            RelicData relic = offers[index];
            if (relicIcons[index] != null)
            {
                relicIcons[index].sprite = relic.Icon;
                relicIcons[index].enabled = relic.Icon != null;
                relicIcons[index].preserveAspect = true;
            }
            if (relicNames[index] != null)
            {
                relicNames[index].text = relic.DisplayName;
            }
            if (relicDescriptions[index] != null)
            {
                string summary = relic.BuildEffectSummary();
                relicDescriptions[index].text = string.IsNullOrWhiteSpace(
                    summary)
                        ? relic.Description
                        : summary;
            }

            TreasureRelicChoiceUI interaction =
                button.GetComponent<TreasureRelicChoiceUI>();
            interaction ??= button.gameObject.AddComponent<
                TreasureRelicChoiceUI>();
            relicTooltip ??= RelicTooltipUI.GetOrCreate(
                button,
                relicNames[index]);
            interaction.Initialize(relicTooltip, relic);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
            TMP_Text label = skipButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "건너뛰기";
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    private void CompleteCurrentReward()
    {
        runData.treasurePendingRewardCount = Mathf.Max(
            0,
            runData.treasurePendingRewardCount - 1);
        runData.treasureRewardSelectionActive = false;
        runData.treasureOfferRelicIds.Clear();
        offers.Clear();
        HideRewardPanel();
        SaveTreasureState();

        if (runData.treasurePendingRewardCount > 0)
        {
            QueueRewardPresentation();
        }
        else
        {
            playerMove.SetInputLocked(false);
        }
    }

    private void HideRewardPanel()
    {
        relicTooltip?.Hide();
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(false);
        }
    }

    private void HandlePlayerPositionChanged()
    {
        if (!initialized || leaving)
        {
            return;
        }

        SaveTreasureState();
        if (!transitionRequested
            && boardManager.TryGetTileIndex(
                playerMove.transform.position,
                PlayerLaneIndex,
                out int tileIndex)
            && tileIndex == ExitTileIndex)
        {
            transitionRequested = true;
            playerMove.SetInputLocked(true);
            StartCoroutine(ReturnToNodeMapWhenSettled());
        }
    }

    private void HandleTurnCompleted()
    {
        if (initialized && !leaving)
        {
            SaveTreasureState();
        }
    }

    private IEnumerator ReturnToNodeMapWhenSettled()
    {
        while ((playerShoot != null && playerShoot.IsFiring)
            || (playerMove != null && playerMove.IsActing))
        {
            yield return null;
        }

        if (!SaveTreasureState())
        {
            transitionRequested = false;
            playerMove.SetInputLocked(false);
            Debug.LogError(
                "Treasure state could not be saved before returning to the node map.",
                this);
            yield break;
        }

        leaving = true;
        NodeMapSaveSystem.CompleteActiveNode();
        ClearTreasureVisitState();
        RunSaveSystem.Save(runData);

        if (!LoadingTransitionController.LoadScene(NodeMapSceneName))
        {
            SceneManager.LoadScene(NodeMapSceneName);
        }
    }

    private void ClearTreasureVisitState()
    {
        runData.treasureVisitActive = false;
        runData.treasureChestOpened = false;
        runData.treasureChoiceResolved = false;
        runData.treasureChestMaxHealth = 0;
        runData.treasureChestCurrentHealth.Clear();
        runData.treasureChestDestroyed.Clear();
        runData.treasurePendingRewardCount = 0;
        runData.treasureRewardSelectionActive = false;
        runData.treasureOfferRelicIds.Clear();
    }

    private bool SaveTreasureState()
    {
        if (runData == null || boardManager == null
            || deckManager == null || currencyManager == null
            || playerMove == null || playerHealth == null
            || playerInventory == null || relicManager == null)
        {
            return false;
        }

        CaptureChestState();
        currencyManager.FlushPendingMoney();
        runData.flowState = (int)GameFlowState.Treasure;
        runData.startSelectedBattleFresh = false;
        runData.currentHealth = playerHealth.CurrentHealth;
        runData.maxHealth = playerHealth.MaxHealth;
        runData.playerStatusEffects = playerHealth.CaptureStatusRunState();
        runData.money = currencyManager.CurrentMoney;
        runData.paidBulletRemovalCount = deckManager.PaidBulletRemovalCount;
        runData.playerFacingRight = playerMove.transform.localScale.x >= 0f;
        runData.playerTurnCount = playerMove.TurnCount;
        runData.playerLaneIndex = PlayerLaneIndex;
        runData.nextPushAvailableTurn = playerMove.NextPushAvailableTurn;
        if (boardManager.TryGetTileIndex(
                playerMove.transform.position,
                PlayerLaneIndex,
                out int playerTileIndex))
        {
            runData.playerTileIndex = playerTileIndex;
        }
        runData.randomStateJson = JsonUtility.ToJson(UnityEngine.Random.state);

        deckManager.CaptureRunState(
            runData.bullets,
            runData.nextCycleAcquisitionOrders);
        playerInventory.CaptureRunState(runData.inventoryItemAssetNames);
        relicManager.CaptureRunState(runData.relics);
        return RunSaveSystem.Save(runData);
    }

    private void CaptureChestState()
    {
        if (runData == null || chests.Count != ChestCount)
        {
            return;
        }

        NormalizeListCount(
            runData.treasureChestCurrentHealth,
            ChestCount,
            runData.treasureChestMaxHealth);
        NormalizeListCount(
            runData.treasureChestDestroyed,
            ChestCount,
            false);
        for (int index = 0; index < ChestCount; index++)
        {
            runData.treasureChestCurrentHealth[index] =
                chests[index].CurrentDurability;
            runData.treasureChestDestroyed[index] =
                chests[index].IsDestroyed;
        }
    }

    private void RestoreRandomState()
    {
        if (string.IsNullOrWhiteSpace(runData.randomStateJson)
            || runData.randomStateJson.Length <= 2)
        {
            return;
        }

        try
        {
            UnityEngine.Random.state = JsonUtility.FromJson<
                UnityEngine.Random.State>(runData.randomStateJson);
        }
        catch (ArgumentException)
        {
            Debug.LogWarning(
                "Treasure scene ignored an invalid saved random state.",
                this);
        }
    }

    private void ResolveRewardUi()
    {
        rewardPanel = FindNamedGameObject("Panel | Treasure");
        choicesPanel = FindNamedGameObject("Panel | Relic Choices");
        chestHousing = FindNamedGameObject("Panel | Chest Housing");
        rewardTitle = FindNamed<TMP_Text>("Text | Treasure Title");
        rewardKicker = FindNamed<TMP_Text>("Text | Treasure Kicker");
        instructionText = FindNamed<TMP_Text>(
            "Text | Treasure Instruction");
        skipButton = FindNamed<UnityEngine.UI.Button>(
            "Button | Treasure Continue");

        for (int index = 0; index < RewardChoiceCount; index++)
        {
            int number = index + 1;
            relicButtons[index] = FindNamed<UnityEngine.UI.Button>(
                $"Button | Relic Choice {number}");
            relicIcons[index] = FindNamed<UnityEngine.UI.Image>(
                $"Image | Relic Icon {number}");
            relicNames[index] = FindNamed<TMP_Text>(
                $"Text | Relic Name {number}");
            relicDescriptions[index] = FindNamed<TMP_Text>(
                $"Text | Relic Description {number}");
        }
    }

    private void BindRewardUi()
    {
        for (int index = 0; index < relicButtons.Length; index++)
        {
            if (relicButtons[index] == null)
            {
                continue;
            }

            int capturedIndex = index;
            relicButtons[index].onClick.AddListener(
                () => SelectRelic(capturedIndex));
        }
        skipButton?.onClick.AddListener(SkipRelic);
    }

    private void UnbindRewardUi()
    {
        foreach (UnityEngine.UI.Button button in relicButtons)
        {
            button?.onClick.RemoveAllListeners();
        }
        skipButton?.onClick.RemoveListener(SkipRelic);
    }

    private void SubscribeRuntimeEvents()
    {
        playerMove.PositionChanged += HandlePlayerPositionChanged;
        playerMove.TurnCompleted += HandleTurnCompleted;
    }

    private void UnsubscribeRuntimeEvents()
    {
        if (playerMove != null)
        {
            playerMove.PositionChanged -= HandlePlayerPositionChanged;
            playerMove.TurnCompleted -= HandleTurnCompleted;
        }
        foreach (TreasureChestTarget chest in chests)
        {
            if (chest == null)
            {
                continue;
            }
            chest.Destroyed -= HandleChestDestroyed;
            chest.HealthChanged -= HandleChestHealthChanged;
        }
    }

    private void HideBattleOnlyPresentation()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (canvas.name == "Canvas | Game Start")
            {
                canvas.gameObject.SetActive(false);
            }
        }

        FindNamedGameObject("Panel | Treasure")?.SetActive(false);
        FindNamedGameObject("Panel | Cylinder Tempo")?.SetActive(false);
        foreach (BattleCameraEdgeHoverController edgeHover in
                 FindObjectsByType<BattleCameraEdgeHoverController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            edgeHover.enabled = false;
        }
    }

    private void ResolveReferences()
    {
        boardManager ??= FindFirstObjectByType<BoardManager>(
            FindObjectsInactive.Include);
        waveManager ??= FindFirstObjectByType<WaveManager>(
            FindObjectsInactive.Include);
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
        dataResolver ??= FindFirstObjectByType<ShopManager>(
            FindObjectsInactive.Include);
        deckManager ??= FindFirstObjectByType<DeckManager>(
            FindObjectsInactive.Include);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>(
            FindObjectsInactive.Include);
        playerMove ??= FindFirstObjectByType<PlayerMove>(
            FindObjectsInactive.Include);
        playerShoot ??= FindFirstObjectByType<PlayerShoot>(
            FindObjectsInactive.Include);
        playerHealth ??= FindFirstObjectByType<PlayerHealth>(
            FindObjectsInactive.Include);
        playerInventory ??= FindFirstObjectByType<PlayerInventory>(
            FindObjectsInactive.Include);
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        exitLabel ??= FindNamed<TMP_Text>("Text | Next Area");
        cameraAnchor ??= FindNamedTransform("Treasure Camera Anchor");
    }

    private static bool HasResumableCombatTreasureState(RunSaveData data)
    {
        return data != null
            && data.treasureVisitActive
            && data.flowState == (int)GameFlowState.Treasure
            && data.treasureChestMaxHealth > 0
            && data.treasureChestCurrentHealth?.Count == ChestCount
            && data.treasureChestDestroyed?.Count == ChestCount;
    }

    private static void NormalizeListCount<T>(
        List<T> list,
        int count,
        T defaultValue)
    {
        if (list == null)
        {
            return;
        }
        while (list.Count < count)
        {
            list.Add(defaultValue);
        }
        if (list.Count > count)
        {
            list.RemoveRange(count, list.Count - count);
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

    private static Transform FindNamedTransform(string objectName)
    {
        return FindNamed<Transform>(objectName);
    }

    private static GameObject FindNamedGameObject(string objectName)
    {
        Transform transform = FindNamedTransform(objectName);
        return transform == null ? null : transform.gameObject;
    }
}
