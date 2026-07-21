using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionQueueUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image queueImage;
    [SerializeField] private RectTransform iconParent;
    [SerializeField] private Image attackIconPrefab;

    [Header("Colors")]
    [SerializeField] private Color normalQueueColor = Color.white;
    [SerializeField] private Color preparedQueueColor =
        new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color missingIconColor = Color.red;

    private readonly List<Image> spawnedIcons = new List<Image>();

    public int IconCount => spawnedIcons.Count;

    private void Awake()
    {
        ResetDisplay();
    }

    public void ShowQueue()
    {
        if (queueImage == null)
        {
            return;
        }

        queueImage.color = normalQueueColor;
        queueImage.gameObject.SetActive(true);
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
        if (queueImage == null)
        {
            return;
        }

        queueImage.color = prepared
            ? preparedQueueColor
            : normalQueueColor;
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
            queueImage.color = normalQueueColor;
            queueImage.gameObject.SetActive(false);
        }
    }
}
