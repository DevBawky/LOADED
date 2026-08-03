using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionQueueUI : MonoBehaviour
{
    private const string ReadyImageName = "Image | Queue Ready";

    [Header("References")]
    [SerializeField] private Image queueImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private Image attackIconPrefab;
    [SerializeField] private Image queueReadyImage;

    [Header("Queue State Sprites")]
    [SerializeField] private Sprite normalQueueSprite;
    [SerializeField] private Sprite preparedQueueSprite;

    [Header("Ready Emphasis")]
    [SerializeField] private Material queueReadyMaterial;

    [Header("Fallback")]
    [SerializeField] private Color missingIconColor = Color.red;

    private readonly List<Image> spawnedIcons = new List<Image>();

    public int IconCount => spawnedIcons.Count;

    private void Awake()
    {
        EnsureReadyImage();
        ResetDisplay();
    }

    public void ShowQueue()
    {
        if (queueImage == null)
        {
            return;
        }

        ApplyQueueSprite(normalQueueSprite);
        queueImage.gameObject.SetActive(true);
        SetReadyImageActive(false);
        RefreshQueueWidth();
    }

    public bool AddAttackIcon(EnemyActionData actionData)
    {
        return AddAttackIcon(actionData, out _);
    }

    public bool AddAttackIcon(
        EnemyActionData actionData,
        out Image attackIcon)
    {
        attackIcon = null;

        if (queueImage == null || iconParent == null
            || attackIconPrefab == null || actionData == null)
        {
            return false;
        }

        ShowQueue();
        attackIcon = Instantiate(attackIconPrefab, iconParent);
        attackIcon.sprite = actionData.Icon;
        attackIcon.color = actionData.Icon == null
            ? missingIconColor
            : Color.white;
        attackIcon.preserveAspect = true;
        EnemyActionTooltipTrigger tooltipTrigger =
            attackIcon.GetComponent<EnemyActionTooltipTrigger>();
        if (tooltipTrigger == null)
        {
            tooltipTrigger = attackIcon.gameObject.AddComponent<
                EnemyActionTooltipTrigger>();
        }

        tooltipTrigger.Configure(actionData);
        spawnedIcons.Add(attackIcon);
        RefreshQueueWidth();
        return true;
    }

    public IEnumerator RevealQueue(float duration)
    {
        ShowQueue();

        if (queueImage != null)
        {
            yield return RevealGraphic(queueImage.gameObject, duration);
        }
    }

    public IEnumerator RevealIcon(Image icon, float duration)
    {
        if (icon != null)
        {
            yield return RevealGraphic(icon.gameObject, duration);
        }
    }

    public void SetPrepared(bool prepared)
    {
        ApplyQueueSprite(prepared
            ? preparedQueueSprite
            : normalQueueSprite);
        SetReadyImageActive(prepared && queueImage != null
            && queueImage.gameObject.activeSelf);
        RefreshQueueWidth();
    }

    public void RemoveFirstIcon()
    {
        if (spawnedIcons.Count == 0)
        {
            return;
        }

        Image icon = spawnedIcons[0];
        spawnedIcons.RemoveAt(0);

        if (icon != null)
        {
            icon.gameObject.SetActive(false);
            Destroy(icon.gameObject);
        }

        RefreshQueueWidth();
    }

    public void ResetDisplay()
    {
        foreach (Image icon in spawnedIcons)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
        }

        spawnedIcons.Clear();

        if (queueImage != null)
        {
            ApplyQueueSprite(normalQueueSprite);
            queueImage.gameObject.SetActive(false);
        }

        SetReadyImageActive(false);
        RefreshQueueWidth();
    }

    private void ApplyQueueSprite(Sprite stateSprite)
    {
        if (queueImage == null)
        {
            return;
        }

        if (stateSprite != null)
        {
            queueImage.sprite = stateSprite;
        }

        queueImage.color = Color.white;
    }

    private void EnsureReadyImage()
    {
        if (queueReadyImage == null)
        {
            Transform readyTransform = transform.Find(ReadyImageName);
            if (readyTransform != null)
            {
                queueReadyImage = readyTransform.GetComponent<Image>();
            }
        }

        if (queueReadyImage == null && queueImage != null)
        {
            GameObject readyObject = new GameObject(
                ReadyImageName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            readyObject.layer = queueImage.gameObject.layer;

            RectTransform readyRect =
                readyObject.GetComponent<RectTransform>();
            RectTransform queueRect = queueImage.rectTransform;
            readyRect.SetParent(queueRect.parent, false);
            readyRect.anchorMin = queueRect.anchorMin;
            readyRect.anchorMax = queueRect.anchorMax;
            readyRect.anchoredPosition = queueRect.anchoredPosition;
            readyRect.sizeDelta = queueRect.sizeDelta;
            readyRect.pivot = queueRect.pivot;
            readyRect.SetSiblingIndex(queueRect.GetSiblingIndex() + 1);

            queueReadyImage = readyObject.GetComponent<Image>();
        }

        if (queueReadyImage == null)
        {
            return;
        }

        queueReadyImage.sprite = null;
        queueReadyImage.color = Color.white;
        queueReadyImage.raycastTarget = false;
        queueReadyImage.material = queueReadyMaterial;
        SyncReadyImageRect();
    }

    private void SetReadyImageActive(bool active)
    {
        EnsureReadyImage();
        if (queueReadyImage != null)
        {
            queueReadyImage.gameObject.SetActive(
                active && queueReadyMaterial != null);
        }
    }

    private void RefreshQueueWidth()
    {
        if (queueImage == null || iconParent == null)
        {
            return;
        }

        HorizontalLayoutGroup layoutGroup =
            iconParent.GetComponent<HorizontalLayoutGroup>();
        float spacing = layoutGroup != null
            ? layoutGroup.spacing
            : 0f;
        float width = layoutGroup != null
            ? layoutGroup.padding.left + layoutGroup.padding.right
            : 0f;
        int activeChildCount = 0;

        for (int i = 0; i < iconParent.childCount; i++)
        {
            RectTransform child =
                iconParent.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            if (activeChildCount > 0)
            {
                width += spacing;
            }

            width += child.rect.width;
            activeChildCount++;
        }

        if (activeChildCount == 0)
        {
            width += GetEmptyQueueWidth();
        }

        queueImage.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width);
        SyncReadyImageRect();
    }

    private float GetEmptyQueueWidth()
    {
        if (attackIconPrefab == null)
        {
            return queueImage.rectTransform.rect.height;
        }

        float prefabWidth = attackIconPrefab.rectTransform.rect.width;
        return prefabWidth > 0f
            ? prefabWidth
            : queueImage.rectTransform.rect.height;
    }

    private void SyncReadyImageRect()
    {
        if (queueImage == null || queueReadyImage == null)
        {
            return;
        }

        RectTransform queueRect = queueImage.rectTransform;
        RectTransform readyRect = queueReadyImage.rectTransform;
        readyRect.anchorMin = queueRect.anchorMin;
        readyRect.anchorMax = queueRect.anchorMax;
        readyRect.anchoredPosition = queueRect.anchoredPosition;
        readyRect.sizeDelta = queueRect.sizeDelta;
        readyRect.pivot = queueRect.pivot;
    }

    private static IEnumerator RevealGraphic(
        GameObject target,
        float duration)
    {
        if (target == null)
        {
            yield break;
        }

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        Transform targetTransform = target.transform;
        Vector3 finalScale = targetTransform.localScale;

        if (duration <= 0f)
        {
            canvasGroup.alpha = 1f;
            targetTransform.localScale = finalScale;
            yield break;
        }

        canvasGroup.alpha = 0f;
        targetTransform.localScale = finalScale * 0.75f;
        float elapsedTime = 0f;

        while (elapsedTime < duration && target != null)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            canvasGroup.alpha = easedProgress;
            targetTransform.localScale = Vector3.LerpUnclamped(
                finalScale * 0.75f,
                finalScale,
                easedProgress);
        }

        if (target != null)
        {
            canvasGroup.alpha = 1f;
            targetTransform.localScale = finalScale;
        }
    }
}
