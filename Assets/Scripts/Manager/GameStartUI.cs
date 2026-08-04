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

    [Header("Gameplay References")]
    [Tooltip("Canvas | Game Start와 별개인 일반 게임 HUD Canvas입니다.")]
    [SerializeField] private Canvas gameplayCanvas;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform playerTrackingTarget;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;

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

    private bool isCollectingReport;
    private int cumulativeDamage;
    private int highestCumulativeDamage;
    private int currentTurnDamage;
    private int highestSingleDamage;
    private int damageTaken;
    private int healingReceived;
    private int totalShots;
    private int startingTurnCount;
    private int lastPlayerHealth;
    private bool clickReceived;
    private Button pendingClickButton;
    private UnityAction pendingClickAction;
    private bool reportEventsSubscribed;

    public bool IsConfigured => stageNoticePanel != null
        && stageNoticeButton != null
        && stageInfoText != null
        && stageSubTitleText != null
        && stageNoticeClickText != null
        && stageReportPanel != null
        && stageReportButton != null
        && stageReportTitleText != null
        && stageReportBodyText != null
        && stageReportClickText != null
        && fightText != null
        && gameplayCanvas != null
        && cinemachineCamera != null
        && playerTrackingTarget != null
        && playerShoot != null
        && playerMove != null
        && playerHealth != null
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
    }

    public IEnumerator Play(BattleData battleData, Action onFightStarted)
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
        SetBattleText(battleData);
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

    public IEnumerator PlayBattleClear(BattleData battleData)
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
        stageReportClickText.text = "클릭하여 상점으로 이동";

        SetGameplayCanvasActive(false);
        gameObject.SetActive(true);
        stageReportPanel.SetActive(true);

        yield return WaitForPanelClick(stageReportButton, stageReportClickText);

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
            stageReportTitleText = FindComponent<TMP_Text>(report, "Text | Stage Info");
            stageReportBodyText = FindComponent<TMP_Text>(report, "Text | Stage Report")
                ?? FindComponent<TMP_Text>(report, "Text | Stage Sub Title");
            stageReportClickText = FindComponent<TMP_Text>(report, "Text | Click to Play");
        }

        fightText ??= FindComponent<TMP_Text>(transform, "Text | Fight");
    }

    private void ResolveGameplayReferences()
    {
        playerShoot ??= FindSceneObject<PlayerShoot>();
        playerMove ??= FindSceneObject<PlayerMove>();
        playerHealth ??= FindSceneObject<PlayerHealth>();
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
        startingTurnCount = playerMove == null ? 0 : playerMove.TurnCount;
        lastPlayerHealth = playerHealth == null ? 0 : playerHealth.CurrentHealth;
        isCollectingReport = true;
    }

    private void EndReportCollection()
    {
        CommitCurrentTurnDamage();
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

    private void SetBattleText(BattleData battleData)
    {
        stageInfoText.text = battleData == null
            ? string.Empty
            : battleData.NoticeTitle;
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
            .Append(Colorize(averageDamagePerShot.ToString("N1"), damageValueColor));
        stageReportBodyText.text = report.ToString();
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

        if (fightText != null)
        {
            SetTextAlpha(fightText, 0f);
            fightText.gameObject.SetActive(false);
        }

        SetTextAlpha(stageNoticeClickText, 1f);
        SetTextAlpha(stageReportClickText, 1f);
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
