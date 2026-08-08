using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHudController : MonoBehaviour
{
    [Header("Boss HUD")]
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private Image healthValue;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Transform statusLayout;
    [SerializeField] private GameObject bossDebuffIconPrefab;

    private EnemyController boundBoss;

    public Image HealthValue => healthValue;
    public Transform StatusLayout => statusLayout;
    public GameObject BossDebuffIconPrefab => bossDebuffIconPrefab;

    private void Awake()
    {
        ResolveHealthText();
        SetPanelActive(false);
        ClearStatusIcons();
    }

    public bool Bind(
        EnemyController boss,
        EnemyHealthBarFeedback healthFeedback,
        EnemyHealthTextFeedback healthTextFeedback,
        StatusEffectController statusEffects)
    {
        ResolveHealthText();

        if (boss == null || healthFeedback == null
            || healthTextFeedback == null || statusEffects == null
            || bossPanel == null || healthValue == null
            || healthText == null
            || statusLayout == null || bossDebuffIconPrefab == null)
        {
            Debug.LogWarning(
                "Boss HUD requires Panel | Boss, HP Value, Layout | Status, and Boss Image _ Debuff references.",
                this);
            return false;
        }

        if (boundBoss != null && boundBoss != boss)
        {
            Unbind(boundBoss);
        }

        boundBoss = boss;
        boundBoss.Defeated -= HandleBossDefeated;
        boundBoss.Defeated += HandleBossDefeated;
        ClearStatusIcons();
        SetPanelActive(true);
        healthValue.fillAmount = 1f;
        healthFeedback.Rebind(healthValue);
        healthTextFeedback.Rebind(healthText);
        healthText.transform.SetAsLastSibling();
        statusEffects.ConfigureIconUI(
            statusLayout,
            bossDebuffIconPrefab);
        return true;
    }

    public void Unbind(EnemyController boss)
    {
        if (boundBoss == null || boss != null && boundBoss != boss)
        {
            return;
        }

        boundBoss.Defeated -= HandleBossDefeated;
        boundBoss = null;
        ClearStatusIcons();
        SetPanelActive(false);
    }

    private void HandleBossDefeated(EnemyController boss)
    {
        Unbind(boss);
    }

    private void SetPanelActive(bool active)
    {
        if (bossPanel != null)
        {
            bossPanel.SetActive(active);
        }
    }

    private void ResolveHealthText()
    {
        if (healthText != null || bossPanel == null)
        {
            return;
        }

        foreach (TMP_Text candidate in
                 bossPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (candidate.name == "Text | HP")
            {
                healthText = candidate;
                return;
            }
        }

        if (healthValue == null)
        {
            return;
        }

        GameObject textObject = new(
            "Text | HP",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = healthValue.gameObject.layer;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        RectTransform healthRect = healthValue.rectTransform;
        textRect.SetParent(healthRect.parent, false);
        textRect.anchorMin = healthRect.anchorMin;
        textRect.anchorMax = healthRect.anchorMax;
        textRect.anchoredPosition = healthRect.anchoredPosition;
        textRect.sizeDelta = healthRect.sizeDelta;
        textRect.pivot = healthRect.pivot;
        textRect.localScale = Vector3.one;

        TextMeshProUGUI createdText = textObject.GetComponent<TextMeshProUGUI>();
        createdText.alignment = TextAlignmentOptions.Center;
        createdText.enableAutoSizing = true;
        createdText.fontSizeMin = 12f;
        createdText.fontSizeMax = 48f;
        createdText.color = Color.white;
        createdText.raycastTarget = false;
        healthText = createdText;
    }

    private void ClearStatusIcons()
    {
        if (statusLayout == null)
        {
            return;
        }

        for (int index = statusLayout.childCount - 1; index >= 0; index--)
        {
            Transform child = statusLayout.GetChild(index);

            if (child != null)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }

    private void OnDisable()
    {
        Unbind(null);
    }
}
