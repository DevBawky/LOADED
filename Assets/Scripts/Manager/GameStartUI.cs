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
    [SerializeField] private TMP_Text executorBronzeCriteriaText;
    [SerializeField] private TMP_Text executorSilverCriteriaText;
    [SerializeField] private TMP_Text executorGoldCriteriaText;
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
    // Retained to preserve existing scene and prefab serialization. Combat
    // report collection no longer reads these references.
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WaveManager waveManager;
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

    private CombatReportSnapshot reportSnapshot;
    private BattleClearSettlement battleClearSettlement;
    private bool hasConfiguredSettlement;
    private bool clickReceived;
    private Button pendingClickButton;
    private UnityAction pendingClickAction;
    private bool settlementConfirmed;

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
        && executorBronzeCriteriaText != null
        && executorSilverCriteriaText != null
        && executorGoldCriteriaText != null
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
        && IsGameplayCanvasSeparate();

    private void Awake()
    {
        PrepareForUse();
        ResetVisualState();
    }

    public void PrepareForUse()
    {
        FindChildReferences();
    }

    public void PrepareRestoredBattle(
        RunCombatReportSaveData state,
        BattleData battleData)
    {
        FindChildReferences();
        reportSnapshot = CombatReportSnapshot.Create(state, 0, 0);
        hasConfiguredSettlement = false;
        ConfigureMedalCriteria(
            BattleClearRewardCalculator.Calculate(
                reportSnapshot,
                battleData));
        ResetVisualState();
        SetGameplayReady();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        CancelPendingClick();
    }

    private void Reset()
    {
        FindChildReferences();
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

        if (!IsConfigured)
        {
            Debug.LogError(
                "Game Start UI notice, report, gameplay, and player references must be assigned.",
                this);
            SetGameplayReady();
            onFightStarted?.Invoke();
            ResetAndHide();
            yield break;
        }

        ResetVisualState();
        SetBattleText(stageData, battleData);
        ConfigureMedalCriteria(
            BattleClearRewardCalculator.Calculate(
                default,
                battleData));
        stageNoticeClickText.text = "클릭하여 전투 시작";

        SetGameplayCanvasActive(false);
        cinemachineCamera.Follow = null;
        gameObject.SetActive(true);
        stageNoticePanel.SetActive(true);

        yield return WaitForPanelClick(stageNoticeButton, stageNoticeClickText);

        stageNoticePanel.SetActive(false);
        SetTextAlpha(fightText, 1f);
        fightText.gameObject.SetActive(true);
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

        if (!hasConfiguredSettlement)
        {
            ConfigureBattleClearSettlement(
                BattleClearRewardCalculator.Calculate(
                    reportSnapshot,
                    battleData));
        }

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
        ConfigureMedalCriteria(battleClearSettlement);
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
        yield return WaitForGoldClaim();

        stageResultContent.SetActive(false);
        stageReportPanel.SetActive(false);
        gameObject.SetActive(false);
        SetGameplayCanvasActive(true);
        hasConfiguredSettlement = false;
    }

    internal void ConfigureBattleClearSettlement(
        BattleClearSettlement settlement)
    {
        battleClearSettlement = settlement
            ?? BattleClearRewardCalculator.Calculate(default, 0);
        reportSnapshot = battleClearSettlement.Report;
        hasConfiguredSettlement = true;
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
        Transform executorCriteriaRow = FindChild(
            resultPost,
            "Layout | Executor");
        executorBronzeCriteriaText = FindComponent<TMP_Text>(
            executorCriteriaRow,
            "Text | Bronze");
        executorSilverCriteriaText = FindComponent<TMP_Text>(
            executorCriteriaRow,
            "Text | Silver");
        executorGoldCriteriaText = FindComponent<TMP_Text>(
            executorCriteriaRow,
            "Text | Gold");

        fightText ??= FindComponent<TMP_Text>(transform, "Text | Fight");
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
        CombatReportSnapshot report = battleClearSettlement.Report;

        stageReportBodyText.richText = true;

        StringBuilder builder = new StringBuilder();
        builder.Append("총 대미지: ")
            .AppendLine(Colorize(
                report.CumulativeDamage.ToString("N0"),
                damageValueColor));
        builder.Append("최고 누적 대미지: ")
            .AppendLine(Colorize(
                report.HighestCumulativeDamage.ToString("N0"),
                damageValueColor));
        builder.Append("최고 한 방 대미지: ")
            .AppendLine(Colorize(
                report.HighestSingleDamage.ToString("N0"),
                damageValueColor));
        builder.Append("입은 피해: ")
            .AppendLine(Colorize(
                report.DamageTaken.ToString("N0"),
                damageTakenValueColor));
        builder.Append("회복량: ")
            .AppendLine(Colorize(
                report.HealingReceived.ToString("N0"),
                summaryValueColor));
        builder.Append("완료 COUNT: ")
            .AppendLine(Colorize(
                report.CompletedCount.ToString("N0"),
                summaryValueColor));
        builder.Append("총 발사 수: ")
            .AppendLine(Colorize(
                report.TotalShots.ToString("N0"),
                summaryValueColor));
        builder.Append("COUNT 당 평균 대미지: ")
            .AppendLine(Colorize(
                report.AverageDamagePerCount.ToString("N1"),
                damageValueColor));
        builder.Append("평균 발 당 대미지: ")
            .AppendLine(Colorize(
                report.AverageDamagePerShot.ToString("N1"),
                damageValueColor));
        builder.Append("획득한 골드: ")
            .Append(Colorize(
                $"$ {report.StageEarnedGold:N0}",
                goldValueColor));
        stageReportBodyText.text = builder.ToString();
    }

    private void ConfigureMedalCriteria(BattleClearSettlement settlement)
    {
        settlement ??= BattleClearRewardCalculator.Calculate(default, 0);

        SetComboCriteriaText(
            comboBronzeCriteriaText,
            settlement.ComboBronzeThreshold);
        SetComboCriteriaText(
            comboSilverCriteriaText,
            settlement.ComboSilverThreshold);
        SetComboCriteriaText(
            comboGoldCriteriaText,
            settlement.ComboGoldThreshold);
        SetExecutorCriteriaText(executorBronzeCriteriaText, 25f);
        SetExecutorCriteriaText(executorSilverCriteriaText, 75f);
        SetExecutorCriteriaText(executorGoldCriteriaText, 150f);
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

    private static void SetExecutorCriteriaText(
        TMP_Text target,
        float threshold)
    {
        if (target != null)
        {
            target.text = $"{threshold:0.#}%";
        }
    }

    private void PrepareStageResult()
    {
        BattleClearSettlement settlement = battleClearSettlement;
        settlementConfirmed = false;

        comboKillResultText.text = "0";
        cylinderKillResultText.text = "0";
        executorResultText.text = "0%";
        PrepareMedalImage(comboMedalImage);
        PrepareMedalImage(cylinderMedalImage);
        PrepareMedalImage(executorMedalImage);
        bonusResultText.text = string.Empty;
        bonusResultText.gameObject.SetActive(false);
        gainGoldAmountText.text = $"정산: $ {settlement.BonusGold:N0}";
        gainGoldButton.interactable = false;
        gainGoldButton.gameObject.SetActive(false);
    }

    private IEnumerator RevealStageResult()
    {
        yield return AnimateResultValues();

        BattleClearSettlement settlement = battleClearSettlement;
        CombatReportSnapshot report = settlement.Report;

        yield return RevealMedal(
            comboMedalImage,
            settlement.ComboMedalScore);
        yield return WaitForDuration(medalRevealInterval);
        yield return RevealMedal(
            cylinderMedalImage,
            settlement.CylinderMedalScore);
        yield return WaitForDuration(medalRevealInterval);
        yield return RevealMedal(
            executorMedalImage,
            settlement.ExecutorMedalScore);

        bonusResultText.gameObject.SetActive(true);
        bonusResultText.richText = true;
        int bonusPercent = Mathf.RoundToInt(
            settlement.BonusGoldRate * 100f);
        string bonusColor = bonusPercent > 0 ? "#55FF66" : "#A8A8A8";
        bonusResultText.text =
            $"정산 보너스: <color={bonusColor}>추가 골드 +{bonusPercent}%</color> "
            + $"(메달 총점 <color=orange>{settlement.TotalMedalScore}</color>/9)\n"
            + $"<color=#FFE21A>($ {report.StageEarnedGold:N0} × {bonusPercent}% = "
            + $"$ {settlement.BonusGold:N0})</color>";

        gainGoldAmountText.text = $"정산: $ {settlement.BonusGold:N0}";
        gainGoldButton.gameObject.SetActive(true);
        gainGoldButton.interactable = true;
        yield return AnimatePopup(
            gainGoldButton.transform as RectTransform,
            medalPopupDuration);
    }

    private IEnumerator AnimateResultValues()
    {
        CombatReportSnapshot report = battleClearSettlement.Report;
        float duration = Mathf.Max(0.01f, resultCountDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += GetDeltaTime();
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            comboKillResultText.text = Mathf.RoundToInt(
                report.StageMaxCombo * eased).ToString("N0");
            cylinderKillResultText.text = Mathf.RoundToInt(
                report.StageMaxCylinderKills * eased).ToString("N0");
            int displayedOverkillPercent = GetDisplayedOverkillPercent(
                report.StageMaxOverkillPercent * eased);
            executorResultText.text = $"{displayedOverkillPercent}%";
        }

        comboKillResultText.text = report.StageMaxCombo.ToString("N0");
        cylinderKillResultText.text =
            report.StageMaxCylinderKills.ToString("N0");
        executorResultText.text =
            $"{GetDisplayedOverkillPercent(report.StageMaxOverkillPercent)}%";
    }

    private static int GetDisplayedOverkillPercent(float overkillPercent)
    {
        const float calculationTolerance = 0.0001f;
        float nonNegativePercent = Mathf.Max(0f, overkillPercent);
        return Mathf.FloorToInt(
            nonNegativePercent + calculationTolerance);
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
        settlementConfirmed = false;
        UnityAction claimAction = ConfirmSettlement;
        gainGoldButton.onClick.AddListener(claimAction);

        // Register the claim before the button becomes visible and
        // interactable so a click during its popup animation is not lost.
        yield return RevealStageResult();

        while (!settlementConfirmed)
        {
            yield return null;
        }

        gainGoldButton.onClick.RemoveListener(claimAction);
    }

    private void ConfirmSettlement()
    {
        if (settlementConfirmed || gainGoldButton == null
            || !gainGoldButton.interactable)
        {
            return;
        }

        settlementConfirmed = true;
        gainGoldButton.interactable = false;
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
