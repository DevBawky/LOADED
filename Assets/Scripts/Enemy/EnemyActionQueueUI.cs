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
    }

    public bool AddAttackIcon(EnemyActionData actionData)
    {
        if (queueImage == null || iconParent == null
            || attackIconPrefab == null || actionData == null)
        {
            return false;
        }

        ShowQueue();
        Image attackIcon = Instantiate(attackIconPrefab, iconParent);
        attackIcon.sprite = actionData.Icon;
        attackIcon.color = actionData.Icon == null
            ? missingIconColor
            : Color.white;
        attackIcon.preserveAspect = true;
        spawnedIcons.Add(attackIcon);
        return true;
    }

    public void SetPrepared(bool prepared)
    {
        ApplyQueueSprite(prepared
            ? preparedQueueSprite
            : normalQueueSprite);
        SetReadyImageActive(prepared && queueImage != null
            && queueImage.gameObject.activeSelf);
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
}
