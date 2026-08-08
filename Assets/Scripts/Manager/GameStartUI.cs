using System;
using System.Collections;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameStartUI : MonoBehaviour
{
    [Header("Stage Notice")]
    [SerializeField] private GameObject stageNoticePanel;
    [SerializeField] private Button stageNoticeButton;
    [SerializeField] private TMP_Text stageInfoText;
    [SerializeField] private TMP_Text stageSubTitleText;
    [SerializeField] private TMP_Text stageNoticeClickText;

    [Header("Stage Report")]
    [SerializeField] private GameObject stageReportPanel;
    [SerializeField] private Button stageReportButton;
    [SerializeField] private TMP_Text stageReportTitleText;
    [SerializeField] private TMP_Text stageReportBodyText;
    [SerializeField] private TMP_Text stageReportClickText;

    [Header("Stage Result")]
    [SerializeField] private GameObject stageReportContent;
    [SerializeField] private GameObject stageResultContent;
    [SerializeField] private TMP_Text comboKillResultText;
    [SerializeField] private TMP_Text cylinderKillResultText;
    [SerializeField] private TMP_Text executorResultText;
    [SerializeField] private TMP_Text comboBronzeCriteriaText;
    [SerializeField] private TMP_Text comboSilverCriteriaText;
    [SerializeField] private TMP_Text comboGoldCriteriaText;
    [SerializeField] private Image comboMedalImage;
    [SerializeField] private Image cylinderMedalImage;
    [SerializeField] private Image executorMedalImage;
    [SerializeField] private TMP_Text bonusResultText;
    [SerializeField] private Button gainGoldButton;
    [SerializeField] private TMP_Text gainGoldAmountText;

    [Header("Medal Images")]
    [SerializeField] private Sprite bronzeMedalSprite;
    [SerializeField] private Sprite silverMedalSprite;
    [SerializeField] private Sprite goldMedalSprite;

    [Header("Report Presentation")]
    [Min(0.01f)]
    [SerializeField] private float reportPopupDuration = 0.3f;
    [Min(0.01f)]
    [SerializeField] private float resultPopupDuration = 0.35f;
    [Range(0.05f, 1f)]
    [SerializeField] private float popupStartScale = 0.72f;
    [Range(0f, 2f)]
    [SerializeField] private float popupOvershoot = 1.15f;
    [Min(0.01f)]
    [SerializeField] private float resultCountDuration = 1.2f;
    [Min(0.01f)]
    [SerializeField] private float medalPopupDuration = 0.38f;
    [Range(0.01f, 1f)]
    [SerializeField] private float medalStartScale = 0.18f;
    [Min(0f)]
    [SerializeField] private float medalRevealInterval = 0.14f;

    [Header("Gameplay References")]
    [Tooltip("Canvas | Game Start와 별개인 일반 게임 HUD Canvas입니다.")]
    [SerializeField] private Canvas gameplayCanvas;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform playerTrackingTarget;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private CombatFeedbackController combatFeedback;

    [Header("Prompt")]
    [Min(0.05f)]
    [SerializeField] private float clickTextBlinkInterval = 0.3f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Fight Notice")]
    [SerializeField] private TMP_Text fightText;
    [Min(0f)]
    [SerializeField] private float fightHoldDuration = 0.7f;
    [Min(0f)]
    [SerializeField] private float fightFadeDuration = 0.3f;

    [Header("Stage Report Colors")]
    [SerializeField] private Color damageValueColor = new Color(1f, 0.62f, 0.22f, 1f);
    [SerializeField] private Color damageTakenValueColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color summaryValueColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color goldValueColor = new Color(1f, 0.88f, 0.1f, 1f);

    private bool isCollectingReport;
    private int cumulativeDamage;
    private int highestCumulativeDamage;
    private int currentTurnDamage;
    private int highestSingleDamage;
    private int damageTaken;
    private int healingReceived;
    private int totalShots;
    private int startingTurnCount;
    private int startingGold;
    private int stageEarnedGold;
    private int stageMaxCombo;
    private int stageMaxCylinderKills;
    private float stageMaxOverkillPercent;
    private int comboBronzeThreshold = int.MaxValue;
    private int comboSilverThreshold = int.MaxValue;
    private int comboGoldThreshold = int.MaxValue;
    private int comboMedalScore;
    private int cylinderMedalScore;
    private int executorMedalScore;
    private int totalMedalScore;
    private int bonusGold;
    private float bonusGoldRate;
    private int lastPlayerHealth;
    private bool clickReceived;
    private Button pendingClickButton;
    private UnityAction pendingClickAction;
    private bool reportEventsSubscribed;
    private bool bonusClaimed;

    public bool IsConfigured => stageNoticePanel != null
        && stageNoticeButton != null
        && stageInfoText != null
        && stageSubTitleText != null
        && stageNoticeClickText != null
        && stageReportPanel != null
        && stageReportButton != null
        && stageReportTitleText != null
        && stageReportBodyText != null
        && stageReportContent != null
        && stageResultContent != null
        && comboKillResultText != null
        && cylinderKillResultText != null
        && executorResultText != null
        && comboBronzeCriteriaText != null
        && comboSilverCriteriaText != null
        && comboGoldCriteriaText != null
        && comboMedalImage != null
        && cylinderMedalImage != null
        && executorMedalImage != null
        && bonusResultText != null
        && gainGoldButton != null
        && gainGoldAmountText != null
        && fightText != null
        && gameplayCanvas != null
        && cinemachineCamera != null
        && playerTrackingTarget != null
        && playerShoot != null
        && playerMove != null
        && playerHealth != null
        && currencyManager != null
        && combatFeedback != null
        && IsGameplayCanvasSeparate();

    private void Awake()
    {
        PrepareForUse();
        ResetVisualState();
    }

    public void PrepareForUse()
    {
        FindChildReferences();
        ResolveGameplayReferences();
        SubscribeToReportEvents();
    }

    public void PrepareRestoredBattle(
        RunCombatReportSaveData state,
        BattleData battleData)
    {
        FindChildReferences();
        ResolveGameplayReferences();
        ConfigureComboMedalCriteria(battleData);
        ResetVisualState();
        SetGameplayReady();
        RestoreRunState(state);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        CancelPendingClick();
        UnsubscribeFromReportEvents();
    }

    private void Reset()
    {
        FindChildReferences();
        ResolveGameplayReferences();
        ResetVisualState();
    }

    private void OnValidate()
    {
        clickTextBlinkInterval = Mathf.Max(0.05f, clickTextBlinkInterval);
        fightHoldDuration = Mathf.Max(0f, fightHoldDuration);
        fightFadeDuration = Mathf.Max(0f, fightFadeDuration);
        reportPopupDuration = Mathf.Max(0.01f, reportPopupDuration);
        resultPopupDuration = Mathf.Max(0.01f, resultPopupDuration);
        resultCountDuration = Mathf.Max(0.01f, resultCountDuration);
        medalPopupDuration = Mathf.Max(0.01f, medalPopupDuration);
        medalRevealInterval = Mathf.Max(0f, medalRevealInterval);
    }

    public IEnumerator Play(
        StageData stageData,
        BattleData battleData,
        Action onFightStarted)
    {
        FindChildReferences();
        ResolveGameplayReferences();

        if (!IsConfigured)
        {
            Debug.LogError(
                "Game Start UI notice, report, gameplay, and player references must be assigned.",
                this);
            BeginReportCollection();
            SetGameplayReady();
            onFightStarted?.Invoke();
            ResetAndHide();
            yield break;
        }

        ResetVisualState();
        SetBattleText(stageData, battleData);
        ConfigureComboMedalCriteria(battleData);
        stageNoticeClickText.text = "클릭하여 전투 시작";

        SetGameplayCanvasActive(false);
        cinemachineCamera.Follow = null;
        gameObject.SetActive(true);
        stageNoticePanel.SetActive(true);

        yield return WaitForPanelClick(stageNoticeButton, stageNoticeClickText);

        stageNoticePanel.SetActive(false);
        SetTextAlpha(fightText, 1f);
        fightText.gameObject.SetActive(true);
        BeginReportCollection();
        SetGameplayReady();
        onFightStarted?.Invoke();

        yield return WaitForDuration(fightHoldDuration);
        yield return FadeOutFightText();

        fightText.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public IEnumerator PlayBattleClear(
        BattleData battleData,
        bool isFinalBossBattle = false)
    {
        FindChildReferences();
        ResolveGameplayReferences();
        EndReportCollection();

        if (!IsConfigured)
        {
            Debug.LogError(
                "Game Start UI report references must be assigned.",
                this);
            SetGameplayCanvasActive(true);
            ResetAndHide();
            yield break;
        }

        ResetVisualState();
        SetBattleReport(battleData);
        ConfigureComboMedalCriteria(battleData);
        PrepareStageResult();
        if (stageReportClickText != null)
        {
            stageReportClickText.text = "클릭하여 정산 결과 확인";
            stageReportClickText.gameObject.SetActive(true);
        }

        SetGameplayCanvasActive(false);
        gameObject.SetActive(true);
        stageReportPanel.SetActive(true);
        stageReportContent.SetActive(true);
        stageResultContent.SetActive(false);

        yield return AnimatePopup(
            stageReportContent.transform as RectTransform,
            reportPopupDuration);

        yield return WaitForPanelClick(stageReportButton, stageReportClickText);

        if (stageReportClickText != null)
        {
            stageReportClickText.gameObject.SetActive(false);
        }

        stageResultContent.SetActive(true);
        yield return AnimatePopup(
            stageResultContent.transform as RectTransform,
            resultPopupDuration);
        yield return RevealStageResult();
        yield return WaitForGoldClaim();

        stageResultContent.SetActive(false);
        stageReportPanel.SetActive(false);
        gameObject.SetActive(false);
        SetGameplayCanvasActive(true);
    }

    public void ResetAndHide()
    {
        CancelPendingClick();
        ResetVisualState();
        gameObject.SetActive(false);
    }

    [ContextMenu("Find Child UI References")]
    private void FindChildReferences()
    {
        Transform notice = FindChild(transform, "Panel | Stage Notice");
        Transform report = FindChild(transform, "Panel | Stage Report");

        if (report == null && Application.isPlaying && notice != null)
        {
            GameObject reportObject = Instantiate(
                notice.gameObject,
                notice.parent,
                false);
            reportObject.name = "Panel | Stage Report";
            report = reportObject.transform;
        }

        if (notice != null)
        {
            stageNoticePanel = notice.gameObject;
            stageNoticeButton = notice.GetComponent<Button>();
            stageInfoText = FindComponent<TMP_Text>(notice, "Text | Stage Info");
            stageSubTitleText = FindComponent<TMP_Text>(notice, "Text | Stage Sub Title");
            stageNoticeClickText = FindComponent<TMP_Text>(notice, "Text | Click to Play");
        }

        if (report != null)
        {
            stageReportPanel = report.gameObject;
            stageReportButton = report.GetComponent<Button>();
            Transform reportContent = FindChild(report, "Image | Stage Report");
            Transform resultContent = FindChild(report, "Image | Stage Result");
            stageReportContent = reportContent == null
                ? null
                : reportContent.gameObject;
            stageResultContent = resultContent == null
                ? null
                : resultContent.gameObject;
            stageReportTitleText = FindComponent<TMP_Text>(
                reportContent ?? report,
                "Text | Stage Info")
                ?? FindComponent<TMP_Text>(reportContent, "Text | Title");
            stageReportBodyText = FindComponent<TMP_Text>(
                reportContent ?? report,
                "Text | Stage Report")
                ?? FindComponent<TMP_Text>(report, "Text | Stage Sub Title");
            stageReportClickText = FindComponent<TMP_Text>(
                reportContent ?? report,
                "Text | Click to Play")
                ?? FindComponent<TMP_Text>(report, "Text | Click to Play");

            if (resultContent != null)
            {
                Transform medalLayout = FindDirectChild(
                    resultContent,
                    "Layout | Medal") ?? FindChild(resultContent, "Layout | Medal");
                BindResultRow(
                    FindDirectChild(medalLayout, "Layout | Combo Kill"),
                    out comboKillResultText,
                    out comboMedalImage);
                BindResultRow(
                    FindDirectChild(medalLayout, "Layout | Cylinder Kill"),
                    out cylinderKillResultText,
                    out cylinderMedalImage);
                BindResultRow(
                    FindDirectChild(medalLayout, "Layout | Executor"),
                    out executorResultText,
                    out executorMedalImage);
                bonusResultText = FindComponent<TMP_Text>(
                    resultContent,
                    "Text | Bonus Result");
                Transform gainButton = FindChild(
                    resultContent,
                    "Button | Gain Gold");
                gainGoldButton = gainButton == null
                    ? null
                    : gainButton.GetComponent<Button>();
                gainGoldAmountText = FindComponent<TMP_Text>(
                    gainButton,
                    "Text | Amount")
                    ?? gainButton?.GetComponentInChildren<TMP_Text>(true);
            }
        }

        Transform resultPost = FindChild(transform, "Panel | Result Post");
        Transform comboCriteriaRow = FindChild(
            resultPost,
            "Layout | Combo Kill");
        comboBronzeCriteriaText = FindComponent<TMP_Text>(
            comboCriteriaRow,
            "Text | Bronze");
        comboSilverCriteriaText = FindComponent<TMP_Text>(
            comboCriteriaRow,
            "Text | Silver");
        comboGoldCriteriaText = FindComponent<TMP_Text>(
            comboCriteriaRow,
            "Text | Gold");

        fightText ??= FindComponent<TMP_Text>(transform, "Text | Fight");
    }

    private void ResolveGameplayReferences()
    {
        playerShoot ??= FindSceneObject<PlayerShoot>();
        playerMove ??= FindSceneObject<PlayerMove>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
        currencyManager ??= FindSceneObject<CurrencyManager>();
        combatFeedback ??= playerShoot == null
            ? FindSceneObject<CombatFeedbackController>()
            : playerShoot.GetComponent<CombatFeedbackController>();
    }

    private IEnumerator WaitForPanelClick(Button button, TMP_Text clickText)
    {
        CancelPendingClick();
        clickReceived = false;
        pendingClickButton = button;
        pendingClickAction = () => clickReceived = true;
        pendingClickButton.onClick.AddListener(pendingClickAction);

        float elapsed = 0f;
        bool visible = true;
        SetTextAlpha(clickText, 1f);

        while (!clickReceived)
        {
            elapsed += GetDeltaTime();

            if (elapsed >= clickTextBlinkInterval)
            {
                elapsed -= clickTextBlinkInterval;
                visible = !visible;
                SetTextAlpha(clickText, visible ? 1f : 0f);
            }

            yield return null;
        }

        SetTextAlpha(clickText, 1f);
        CancelPendingClick();
    }

    private void CancelPendingClick()
    {
        if (pendingClickButton != null && pendingClickAction != null)
        {
            pendingClickButton.onClick.RemoveListener(pendingClickAction);
        }

        pendingClickButton = null;
        pendingClickAction = null;
        clickReceived = false;
    }

    private void BeginReportCollection()
    {
        cumulativeDamage = 0;
        highestCumulativeDamage = 0;
        currentTurnDamage = 0;
        highestSingleDamage = 0;
        damageTaken = 0;
        healingReceived = 0;
        totalShots = 0;
        stageEarnedGold = 0;
        stageMaxCombo = 0;
        stageMaxCylinderKills = 0;
        stageMaxOverkillPercent = 0f;
        startingTurnCount = playerMove == null ? 0 : playerMove.TurnCount;
        startingGold = currencyManager == null ? 0 : currencyManager.CurrentMoney;
        lastPlayerHealth = playerHealth == null ? 0 : playerHealth.CurrentHealth;
        isCollectingReport = true;
    }

    public RunCombatReportSaveData CaptureRunState()
    {
        return new RunCombatReportSaveData
        {
            cumulativeDamage = cumulativeDamage,
            highestCumulativeDamage = highestCumulativeDamage,
            currentTurnDamage = currentTurnDamage,
            highestSingleDamage = highestSingleDamage,
            damageTaken = damageTaken,
            healingReceived = healingReceived,
            totalShots = totalShots,
            startingTurnCount = startingTurnCount,
            startingGold = startingGold,
            stageMaxCombo = stageMaxCombo,
            stageMaxCylinderKills = stageMaxCylinderKills,
            stageMaxOverkillPercent = stageMaxOverkillPercent,
            lastPlayerHealth = lastPlayerHealth
        };
    }

    public void RestoreRunState(RunCombatReportSaveData state)
    {
        if (state == null)
        {
            return;
        }

        cumulativeDamage = Mathf.Max(0, state.cumulativeDamage);
        highestCumulativeDamage = Mathf.Max(
            0,
            state.highestCumulativeDamage);
        currentTurnDamage = Mathf.Max(0, state.currentTurnDamage);
        highestSingleDamage = Mathf.Max(0, state.highestSingleDamage);
        damageTaken = Mathf.Max(0, state.damageTaken);
        healingReceived = Mathf.Max(0, state.healingReceived);
        totalShots = Mathf.Max(0, state.totalShots);
        startingTurnCount = Mathf.Max(0, state.startingTurnCount);
        startingGold = Mathf.Max(0, state.startingGold);
        stageMaxCombo = Mathf.Max(0, state.stageMaxCombo);
        stageMaxCylinderKills = Mathf.Max(
            0,
            state.stageMaxCylinderKills);
        stageMaxOverkillPercent = Mathf.Max(
            0f,
            state.stageMaxOverkillPercent);
        lastPlayerHealth = Mathf.Max(0, state.lastPlayerHealth);
        isCollectingReport = true;
    }

    private void EndReportCollection()
    {
        CommitCurrentTurnDamage();
        currencyManager?.FlushPendingMoney();
        stageEarnedGold = currencyManager == null
            ? 0
            : Mathf.Max(0, currencyManager.CurrentMoney - startingGold);
        isCollectingReport = false;
    }

    private void SubscribeToReportEvents()
    {
        if (reportEventsSubscribed)
        {
            return;
        }

        if (playerShoot != null)
        {
            playerShoot.BulletFired += HandleBulletFired;
            playerShoot.DamageDealt += HandleDamageDealt;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged += HandlePlayerHealthChanged;
        }

        if (playerMove != null)
        {
            playerMove.TurnCompleted += HandleTurnCompleted;
        }

        if (combatFeedback != null)
        {
            combatFeedback.DefeatPerformanceRecorded +=
                HandleDefeatPerformanceRecorded;
        }

        reportEventsSubscribed = true;
    }

    private void UnsubscribeFromReportEvents()
    {
        if (!reportEventsSubscribed)
        {
            return;
        }

        if (playerShoot != null)
        {
            playerShoot.BulletFired -= HandleBulletFired;
            playerShoot.DamageDealt -= HandleDamageDealt;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= HandlePlayerHealthChanged;
        }

        if (playerMove != null)
        {
            playerMove.TurnCompleted -= HandleTurnCompleted;
        }

        if (combatFeedback != null)
        {
            combatFeedback.DefeatPerformanceRecorded -=
                HandleDefeatPerformanceRecorded;
        }

        reportEventsSubscribed = false;
    }

    private void HandleBulletFired(BulletInstance bullet)
    {
        if (!isCollectingReport || bullet == null)
        {
            return;
        }

        totalShots++;
    }

    private void HandleDamageDealt(int damage)
    {
        if (!isCollectingReport || damage <= 0)
        {
            return;
        }

        cumulativeDamage += damage;
        currentTurnDamage += damage;
        highestSingleDamage = Mathf.Max(highestSingleDamage, damage);
    }

    private void HandleTurnCompleted()
    {
        if (!isCollectingReport)
        {
            return;
        }

        CommitCurrentTurnDamage();
    }

    private void HandleDefeatPerformanceRecorded(
        int comboKills,
        int cylinderKills,
        float overkillPercent)
    {
        if (!isCollectingReport)
        {
            return;
        }

        stageMaxCombo = Mathf.Max(stageMaxCombo, comboKills);
        stageMaxCylinderKills = Mathf.Max(
            stageMaxCylinderKills,
            cylinderKills);
        stageMaxOverkillPercent = Mathf.Max(
            stageMaxOverkillPercent,
            overkillPercent);
    }

    private void CommitCurrentTurnDamage()
    {
        highestCumulativeDamage = Mathf.Max(
            highestCumulativeDamage,
            currentTurnDamage);
        currentTurnDamage = 0;
    }

    private void HandlePlayerHealthChanged(int currentHealth, int maximumHealth)
    {
        if (!isCollectingReport)
        {
            lastPlayerHealth = currentHealth;
            return;
        }

        if (currentHealth < lastPlayerHealth)
        {
            damageTaken += lastPlayerHealth - currentHealth;
        }
        else if (currentHealth > lastPlayerHealth)
        {
            healingReceived += currentHealth - lastPlayerHealth;
        }

        lastPlayerHealth = currentHealth;
    }

    private void SetBattleText(
        StageData stageData,
        BattleData battleData)
    {
        stageInfoText.text = StageTitleFormatter.Format(
            stageData,
            battleData);
        stageSubTitleText.text = battleData == null
            ? string.Empty
            : battleData.NoticeDescription;
    }

    private void SetBattleReport(BattleData battleData)
    {
        stageReportTitleText.text = battleData == null
            ? "STAGE REPORT"
            : battleData.ClearNoticeTitle;

        int battleTurns = playerMove == null
            ? 0
            : Mathf.Max(0, playerMove.TurnCount - startingTurnCount);
        float averageDamagePerTurn = battleTurns <= 0
            ? 0f
            : (float)cumulativeDamage / battleTurns;
        float averageDamagePerShot = totalShots <= 0
            ? 0f
            : (float)cumulativeDamage / totalShots;

        stageReportBodyText.richText = true;

        StringBuilder report = new StringBuilder();
        report.Append("총 대미지: ")
            .AppendLine(Colorize(cumulativeDamage.ToString("N0"), damageValueColor));
        report.Append("최고 누적 대미지: ")
            .AppendLine(Colorize(highestCumulativeDamage.ToString("N0"), damageValueColor));
        report.Append("최고 한 방 대미지: ")
            .AppendLine(Colorize(highestSingleDamage.ToString("N0"), damageValueColor));
        report.Append("입은 피해: ")
            .AppendLine(Colorize(damageTaken.ToString("N0"), damageTakenValueColor));
        report.Append("회복량: ")
            .AppendLine(Colorize(healingReceived.ToString("N0"), summaryValueColor));
        report.Append("소모 턴: ")
            .AppendLine(Colorize(battleTurns.ToString("N0"), summaryValueColor));
        report.Append("총 발사 수: ")
            .AppendLine(Colorize(totalShots.ToString("N0"), summaryValueColor));
        report.Append("턴 당 평균 대미지: ")
            .AppendLine(Colorize(averageDamagePerTurn.ToString("N1"), damageValueColor));
        report.Append("평균 발 당 대미지: ")
            .AppendLine(Colorize(averageDamagePerShot.ToString("N1"), damageValueColor));
        report.Append("획득한 골드: ")
            .Append(Colorize($"$ {stageEarnedGold:N0}", goldValueColor));
        stageReportBodyText.text = report.ToString();
    }

    private void ConfigureComboMedalCriteria(BattleData battleData)
    {
        int totalEnemyCount = GetTotalEnemyCount(battleData);

        if (totalEnemyCount <= 0)
        {
            comboBronzeThreshold = int.MaxValue;
            comboSilverThreshold = int.MaxValue;
            comboGoldThreshold = int.MaxValue;
        }
        else
        {
            comboBronzeThreshold = GetPercentageThreshold(
                totalEnemyCount,
                25);
            comboSilverThreshold = GetPercentageThreshold(
                totalEnemyCount,
                50);
            comboGoldThreshold = GetPercentageThreshold(
                totalEnemyCount,
                70);
        }

        SetComboCriteriaText(
            comboBronzeCriteriaText,
            comboBronzeThreshold);
        SetComboCriteriaText(
            comboSilverCriteriaText,
            comboSilverThreshold);
        SetComboCriteriaText(
            comboGoldCriteriaText,
            comboGoldThreshold);
    }

    private static int GetTotalEnemyCount(BattleData battleData)
    {
        if (battleData == null || battleData.Waves == null)
        {
            return 0;
        }

        long totalEnemyCount = 0;

        foreach (EnemyWave wave in battleData.Waves)
        {
            if (wave == null || wave.Enemies == null)
            {
                continue;
            }

            foreach (EnemyWaveEntry entry in wave.Enemies)
            {
                if (entry == null || entry.EnemyData == null
                    || entry.Count <= 0)
                {
                    continue;
                }

                totalEnemyCount += entry.Count;

                if (totalEnemyCount >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }
        }

        return (int)totalEnemyCount;
    }

    private static int GetPercentageThreshold(
        int totalEnemyCount,
        int percentage)
    {
        long scaledCount = (long)Mathf.Max(0, totalEnemyCount)
            * Mathf.Clamp(percentage, 0, 100);
        return Mathf.Max(1, (int)Math.Min(
            int.MaxValue,
            (scaledCount + 99L) / 100L));
    }

    private static void SetComboCriteriaText(
        TMP_Text target,
        int threshold)
    {
        if (target == null)
        {
            return;
        }

        target.text = threshold == int.MaxValue
            ? "-"
            : $"{threshold:N0}";
    }

    private void PrepareStageResult()
    {
        comboMedalScore = GetComboMedalScore(stageMaxCombo);
        cylinderMedalScore = GetCylinderMedalScore(stageMaxCylinderKills);
        executorMedalScore = GetExecutorMedalScore(stageMaxOverkillPercent);
        totalMedalScore = comboMedalScore
            + cylinderMedalScore
            + executorMedalScore;
        bonusGoldRate = GetBonusGoldRate(totalMedalScore);
        bonusGold = Mathf.Max(
            0,
            Mathf.FloorToInt(stageEarnedGold * bonusGoldRate));
        bonusClaimed = false;

        comboKillResultText.text = "0";
        cylinderKillResultText.text = "0";
        executorResultText.text = "0%";
        PrepareMedalImage(comboMedalImage);
        PrepareMedalImage(cylinderMedalImage);
        PrepareMedalImage(executorMedalImage);
        bonusResultText.text = string.Empty;
        bonusResultText.gameObject.SetActive(false);
        gainGoldAmountText.text = $"정산: $ {bonusGold:N0}";
        gainGoldButton.interactable = false;
        gainGoldButton.gameObject.SetActive(false);
    }

    private IEnumerator RevealStageResult()
    {
        yield return AnimateResultValues();

        yield return RevealMedal(comboMedalImage, comboMedalScore);
        yield return WaitForDuration(medalRevealInterval);
        yield return RevealMedal(cylinderMedalImage, cylinderMedalScore);
        yield return WaitForDuration(medalRevealInterval);
        yield return RevealMedal(executorMedalImage, executorMedalScore);

        bonusResultText.gameObject.SetActive(true);
        bonusResultText.richText = true;
        int bonusPercent = Mathf.RoundToInt(bonusGoldRate * 100f);
        string bonusColor = bonusPercent > 0 ? "#55FF66" : "#A8A8A8";
        bonusResultText.text =
            $"정산 보너스: <color={bonusColor}>추가 골드 +{bonusPercent}%</color> "
            + $"(메달 총점 <color=orange>{totalMedalScore}</color>/9)\n"
            + $"<color=#FFE21A>($ {stageEarnedGold:N0} × {bonusPercent}% = "
            + $"$ {bonusGold:N0})</color>";

        gainGoldAmountText.text = $"정산: $ {bonusGold:N0}";
        gainGoldButton.gameObject.SetActive(true);
        gainGoldButton.interactable = true;
        yield return AnimatePopup(
            gainGoldButton.transform as RectTransform,
            medalPopupDuration);
    }

    private IEnumerator AnimateResultValues()
    {
        float duration = Mathf.Max(0.01f, resultCountDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            comboKillResultText.text = Mathf.RoundToInt(
                stageMaxCombo * eased).ToString("N0");
            cylinderKillResultText.text = Mathf.RoundToInt(
                stageMaxCylinderKills * eased).ToString("N0");
            executorResultText.text =
                $"{stageMaxOverkillPercent * eased:0.#}%";
        }

        comboKillResultText.text = stageMaxCombo.ToString("N0");
        cylinderKillResultText.text = stageMaxCylinderKills.ToString("N0");
        executorResultText.text = $"{stageMaxOverkillPercent:0.#}%";
    }

    private IEnumerator RevealMedal(Image medalImage, int medalScore)
    {
        Sprite medalSprite = GetMedalSprite(medalScore);

        if (medalImage == null || medalSprite == null || medalScore <= 0)
        {
            yield break;
        }

        medalImage.sprite = medalSprite;
        medalImage.preserveAspect = true;
        medalImage.gameObject.SetActive(true);
        RectTransform rect = medalImage.rectTransform;
        Vector3 targetScale = rect.localScale;
        Quaternion targetRotation = rect.localRotation;
        Color targetColor = medalImage.color;
        targetColor.a = 1f;
        float duration = Mathf.Max(0.01f, medalPopupDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / duration);
            float scaleProgress = EaseOutBack(progress, 1.7f);
            float flash = Mathf.Sin(progress * Mathf.PI * 2f)
                * (1f - progress);
            rect.localScale = targetScale * Mathf.LerpUnclamped(
                medalStartScale,
                1f,
                scaleProgress);
            rect.localRotation = targetRotation
                * Quaternion.Euler(0f, 0f, flash * 8f);
            Color color = targetColor;
            float sparkle = progress < 0.72f
                ? 0.68f + Mathf.Abs(
                    Mathf.Sin(progress * Mathf.PI * 4f)) * 0.32f
                : 1f;
            color.a = Mathf.Clamp01(progress * 5f) * sparkle;
            medalImage.color = color;
        }

        rect.localScale = targetScale;
        rect.localRotation = targetRotation;
        medalImage.color = targetColor;
    }

    private IEnumerator AnimatePopup(RectTransform popup, float duration)
    {
        if (popup == null)
        {
            yield break;
        }

        CanvasGroup group = GetOrAddCanvasGroup(popup.gameObject);
        Vector3 targetScale = popup.localScale;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        group.alpha = 0f;
        popup.localScale = targetScale * popupStartScale;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / duration);
            float scaleProgress = EaseOutBack(progress, popupOvershoot);
            popup.localScale = targetScale * Mathf.LerpUnclamped(
                popupStartScale,
                1f,
                scaleProgress);
            group.alpha = Mathf.SmoothStep(0f, 1f, progress);
        }

        popup.localScale = targetScale;
        group.alpha = 1f;
    }

    private IEnumerator WaitForGoldClaim()
    {
        bonusClaimed = false;
        UnityAction claimAction = ClaimBonusGold;
        gainGoldButton.onClick.AddListener(claimAction);

        while (!bonusClaimed)
        {
            yield return null;
        }

        gainGoldButton.onClick.RemoveListener(claimAction);
    }

    private void ClaimBonusGold()
    {
        if (bonusClaimed || gainGoldButton == null
            || !gainGoldButton.interactable)
        {
            return;
        }

        bonusClaimed = true;
        gainGoldButton.interactable = false;

        if (bonusGold > 0)
        {
            currencyManager?.AddMoney(bonusGold);
        }
    }

    private int GetComboMedalScore(int comboKills)
    {
        if (comboGoldThreshold == int.MaxValue)
        {
            return 0;
        }

        return comboKills >= comboGoldThreshold
            ? 3
            : comboKills >= comboSilverThreshold
                ? 2
                : comboKills >= comboBronzeThreshold ? 1 : 0;
    }

    private static int GetCylinderMedalScore(int cylinderKills)
    {
        return cylinderKills >= 4 ? 3 : cylinderKills >= 3 ? 2 : cylinderKills >= 2 ? 1 : 0;
    }

    private static int GetExecutorMedalScore(float overkillPercent)
    {
        return overkillPercent >= 150f ? 3 : overkillPercent >= 75f ? 2 : overkillPercent >= 25f ? 1 : 0;
    }

    private static float GetBonusGoldRate(int score)
    {
        if (score >= 9)
        {
            return 0.3f;
        }

        if (score >= 6)
        {
            return 0.2f;
        }

        if (score >= 3)
        {
            return 0.1f;
        }

        return score >= 1 ? 0.05f : 0f;
    }

    private Sprite GetMedalSprite(int medalScore)
    {
        return medalScore switch
        {
            1 => bronzeMedalSprite,
            2 => silverMedalSprite,
            3 => goldMedalSprite,
            _ => null
        };
    }

    private static void PrepareMedalImage(Image medalImage)
    {
        if (medalImage == null)
        {
            return;
        }

        medalImage.sprite = null;
        medalImage.gameObject.SetActive(false);
    }

    private static float EaseOutBack(float progress, float overshoot)
    {
        float shifted = Mathf.Clamp01(progress) - 1f;
        float strength = Mathf.Max(0f, overshoot);
        return 1f + (strength + 1f) * shifted * shifted * shifted
            + strength * shifted * shifted;
    }

    private void SetGameplayReady()
    {
        SetGameplayCanvasActive(true);

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = playerTrackingTarget;
        }
    }

    private void SetGameplayCanvasActive(bool active)
    {
        if (gameplayCanvas != null)
        {
            gameplayCanvas.gameObject.SetActive(active);
        }
    }

    private void ResetVisualState()
    {
        CancelPendingClick();

        SetAllNamedPanelsActive("Panel | Stage Notice", false);
        SetAllNamedPanelsActive("Panel | Stage Report", false);

        stageReportContent?.SetActive(false);
        stageResultContent?.SetActive(false);
        PrepareMedalImage(comboMedalImage);
        PrepareMedalImage(cylinderMedalImage);
        PrepareMedalImage(executorMedalImage);

        if (bonusResultText != null)
        {
            bonusResultText.gameObject.SetActive(false);
        }

        if (gainGoldButton != null)
        {
            gainGoldButton.interactable = false;
            gainGoldButton.gameObject.SetActive(false);
        }

        if (fightText != null)
        {
            SetTextAlpha(fightText, 0f);
            fightText.gameObject.SetActive(false);
        }

        SetTextAlpha(stageNoticeClickText, 1f);
        SetTextAlpha(stageReportClickText, 1f);

        if (stageReportClickText != null)
        {
            stageReportClickText.gameObject.SetActive(true);
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator WaitForDuration(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }
    }

    private IEnumerator FadeOutFightText()
    {
        if (fightFadeDuration <= 0f)
        {
            SetTextAlpha(fightText, 0f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fightFadeDuration)
        {
            elapsed += GetDeltaTime();
            SetTextAlpha(fightText, 1f - Mathf.Clamp01(elapsed / fightFadeDuration));
            yield return null;
        }

        SetTextAlpha(fightText, 0f);
    }

    private void SetAllNamedPanelsActive(string panelName, bool active)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == panelName)
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    private bool IsGameplayCanvasSeparate()
    {
        if (gameplayCanvas == null)
        {
            return false;
        }

        Transform gameplayTransform = gameplayCanvas.transform;
        return gameplayTransform != transform
            && !gameplayTransform.IsChildOf(transform)
            && !transform.IsChildOf(gameplayTransform);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T FindComponent<T>(Transform root, string childName)
        where T : Component
    {
        Transform child = FindChild(root, childName);
        return child == null ? null : child.GetComponent<T>();
    }

    private static Transform FindDirectChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void BindResultRow(
        Transform row,
        out TMP_Text resultText,
        out Image medalImage)
    {
        resultText = FindComponent<TMP_Text>(row, "Text | My Result");
        medalImage = FindComponent<Image>(row, "Image | Medal");
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return objects.Length == 0 ? null : objects[0];
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private static string Colorize(string value, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";
    }
}
