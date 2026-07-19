using System;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameStartUI : MonoBehaviour
{
    [Header("Stage Notice")]
    [SerializeField] private GameObject stageNoticePanel;
    [SerializeField] private Image stageNoticeImage;
    [SerializeField] private TMP_Text stageInfoText;
    [SerializeField] private TMP_Text stageSubTitleText;

    [Header("Fight Notice")]
    [SerializeField] private TMP_Text fightText;

    [Header("Gameplay References")]
    [Tooltip("Canvas | Game Start와 별개인 일반 게임 HUD Canvas입니다.")]
    [SerializeField] private Canvas gameplayCanvas;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform playerTrackingTarget;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float noticeRevealDuration = 1f;
    [Min(0f)]
    [SerializeField] private float noticeHoldDuration = 2f;
    [Min(0f)]
    [SerializeField] private float fightHoldDuration = 1f;
    [Min(0f)]
    [SerializeField] private float fightFadeDuration = 1f;
    [SerializeField] private bool useUnscaledTime = true;

    public bool IsConfigured => stageNoticePanel != null
        && stageNoticeImage != null
        && stageInfoText != null
        && stageSubTitleText != null
        && fightText != null
        && gameplayCanvas != null
        && cinemachineCamera != null
        && playerTrackingTarget != null
        && IsGameplayCanvasSeparate();

    private void Reset()
    {
        FindChildReferences();
        ResetVisualState();
    }

    private void OnValidate()
    {
        noticeRevealDuration = Mathf.Max(0f, noticeRevealDuration);
        noticeHoldDuration = Mathf.Max(0f, noticeHoldDuration);
        fightHoldDuration = Mathf.Max(0f, fightHoldDuration);
        fightFadeDuration = Mathf.Max(0f, fightFadeDuration);
    }

    public IEnumerator Play(BattleData battleData, Action onFightStarted)
    {
        if (!IsConfigured)
        {
            Debug.LogError(
                "Game Start UI references must be assigned. The gameplay Canvas must be separate from Canvas | Game Start.",
                this);
            SetGameplayReady();
            onFightStarted?.Invoke();
            ResetAndHide();
            yield break;
        }

        ResetVisualState();
        SetBattleText(battleData);

        gameplayCanvas.gameObject.SetActive(false);
        cinemachineCamera.Follow = null;
        gameObject.SetActive(true);
        stageNoticePanel.SetActive(true);

        yield return AnimateNoticeReveal();
        yield return WaitForDuration(noticeHoldDuration);

        stageNoticePanel.SetActive(false);
        ResetStageNotice();

        SetTextAlpha(fightText, 1f);
        fightText.gameObject.SetActive(true);
        SetGameplayReady();
        onFightStarted?.Invoke();

        yield return WaitForDuration(fightHoldDuration);
        yield return AnimateFightFade();

        fightText.gameObject.SetActive(false);
        SetTextAlpha(fightText, 0f);
        gameObject.SetActive(false);
    }

    public IEnumerator PlayBattleClear(BattleData battleData)
    {
        if (!IsConfigured)
        {
            Debug.LogError(
                "Game Start UI references must be assigned. The gameplay Canvas must be separate from Canvas | Game Start.",
                this);
            SetGameplayCanvasActive(true);
            ResetAndHide();
            yield break;
        }

        ResetVisualState();
        SetBattleClearText(battleData);

        SetGameplayCanvasActive(false);
        gameObject.SetActive(true);
        stageNoticePanel.SetActive(true);

        yield return AnimateNoticeReveal();
        yield return WaitForDuration(noticeHoldDuration);

        stageNoticePanel.SetActive(false);
        ResetStageNotice();
        gameObject.SetActive(false);
        SetGameplayCanvasActive(true);
    }

    public void ResetAndHide()
    {
        ResetVisualState();
        gameObject.SetActive(false);
    }

    [ContextMenu("Find Child UI References")]
    private void FindChildReferences()
    {
        Transform stageNotice = FindChild("Panel | Stage Notice");
        Transform stageInfo = FindChild("Text | Stage Info");
        Transform stageSubTitle = FindChild("Text | Stage Sub Title");
        Transform fight = FindChild("Text | Fight");

        stageNoticePanel ??= stageNotice == null
            ? null
            : stageNotice.gameObject;
        stageNoticeImage ??= stageNotice == null
            ? null
            : stageNotice.GetComponent<Image>();
        stageInfoText ??= stageInfo == null
            ? null
            : stageInfo.GetComponent<TMP_Text>();
        stageSubTitleText ??= stageSubTitle == null
            ? null
            : stageSubTitle.GetComponent<TMP_Text>();
        fightText ??= fight == null
            ? null
            : fight.GetComponent<TMP_Text>();
    }

    private IEnumerator AnimateNoticeReveal()
    {
        if (noticeRevealDuration <= 0f)
        {
            SetNoticeProgress(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < noticeRevealDuration)
        {
            elapsed += GetDeltaTime();
            SetNoticeProgress(
                Mathf.Clamp01(elapsed / noticeRevealDuration));
            yield return null;
        }

        SetNoticeProgress(1f);
    }

    private IEnumerator AnimateFightFade()
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
            SetTextAlpha(
                fightText,
                1f - Mathf.Clamp01(elapsed / fightFadeDuration));
            yield return null;
        }

        SetTextAlpha(fightText, 0f);
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

    private void SetBattleText(BattleData battleData)
    {
        stageInfoText.text = battleData == null
            ? string.Empty
            : battleData.NoticeTitle;
        stageSubTitleText.text = battleData == null
            ? string.Empty
            : battleData.NoticeDescription;
    }

    private void SetBattleClearText(BattleData battleData)
    {
        stageInfoText.text = battleData == null
            ? "BATTLE CLEAR"
            : battleData.ClearNoticeTitle;
        stageSubTitleText.text = battleData == null
            ? string.Empty
            : battleData.ClearNoticeDescription;
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
        ResetStageNotice();

        if (stageNoticePanel != null)
        {
            stageNoticePanel.SetActive(false);
        }

        if (fightText != null)
        {
            SetTextAlpha(fightText, 0f);
            fightText.gameObject.SetActive(false);
        }
    }

    private void ResetStageNotice()
    {
        SetNoticeProgress(0f);
    }

    private void SetNoticeProgress(float progress)
    {
        float value = Mathf.Clamp01(progress);

        if (stageNoticeImage != null)
        {
            stageNoticeImage.fillAmount = value;
        }

        SetTextAlpha(stageInfoText, value);
        SetTextAlpha(stageSubTitleText, value);
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
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

    private Transform FindChild(string childName)
    {
        foreach (Transform child in
                 GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
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
}
