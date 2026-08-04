using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHudController : MonoBehaviour
{
    [Header("Boss HUD")]
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private Image healthValue;
    [SerializeField] private Transform statusLayout;
    [SerializeField] private GameObject bossDebuffIconPrefab;

    private EnemyController boundBoss;

    public Image HealthValue => healthValue;
    public Transform StatusLayout => statusLayout;
    public GameObject BossDebuffIconPrefab => bossDebuffIconPrefab;

    private void Awake()
    {
        SetPanelActive(false);
        ClearStatusIcons();
    }

    public bool Bind(
        EnemyController boss,
        EnemyHealthBarFeedback healthFeedback,
        StatusEffectController statusEffects)
    {
        if (boss == null || healthFeedback == null || statusEffects == null
            || bossPanel == null || healthValue == null
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
