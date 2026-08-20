using DamageNumbersPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDamageNumberDisplay : MonoBehaviour
{
    [Header("Damage Number Prefabs")]
    [SerializeField] private DamageNumber normalDamagePrefab;
    [SerializeField] private DamageNumber criticalDamagePrefab;
    [SerializeField] private DamageNumber devastatingDamagePrefab;
    [SerializeField] private DamageNumber poisonDamagePrefab;
    [SerializeField] private DamageNumber markBonusDamagePrefab;

    [Header("Status Text Prefabs")]
    [Tooltip("독 상태 텍스트 전용 DamageNumbersPro 프리팹입니다.")]
    [SerializeField] private DamageNumber poisonStatusPrefab;
    [Tooltip("표식 상태 텍스트 전용 DamageNumbersPro 프리팹입니다.")]
    [SerializeField] private DamageNumber markStatusPrefab;
    [Tooltip("기절 상태 텍스트 전용 DamageNumbersPro 프리팹입니다.")]
    [SerializeField] private DamageNumber stunStatusPrefab;
    [Tooltip("흡혈 상태 텍스트 전용 DamageNumbersPro 프리팹입니다.")]
    [SerializeField] private DamageNumber lifeStealStatusPrefab;
    [Tooltip("약화 상태 텍스트 전용 DamageNumbersPro 프리팹입니다.")]
    [SerializeField] private DamageNumber weaknessStatusPrefab;

    [Header("Status Text")]
    [SerializeField] private string poisonText = "독";
    [SerializeField] private string markText = "표식";
    [SerializeField] private string stunText = "기절";
    [SerializeField] private string lifeStealText = "흡혈";
    [SerializeField] private string weaknessText = "약화";

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 damageOffset =
        new Vector3(0f, 0.75f, -1f);
    [SerializeField] private Vector3 statusOffset =
        new Vector3(0f, 1f, -1f);
    [SerializeField] private bool followTarget;

    [Header("Impact Tier Styling")]
    [SerializeField] private Color criticalDamageColor =
        new Color(1f, 0.86f, 0.28f, 1f);
    [SerializeField] private Color devastatingDamageColor =
        new Color(1f, 0.32f, 0.08f, 1f);
    [SerializeField] private Color defeatDamageColor =
        new Color(1f, 0.92f, 0.78f, 1f);
    [SerializeField] private float criticalDamageScale = 1.18f;
    [SerializeField] private float devastatingDamageScale = 1.42f;
    [SerializeField] private float defeatDamageScale = 1.58f;

    public void ShowAttackDamage(
        int damage,
        CombatImpactTier impactTier,
        bool isCritical = false)
    {
        DamageNumber preferredPrefab = impactTier switch
        {
            CombatImpactTier.Defeat => isCritical
                ? criticalDamagePrefab
                : normalDamagePrefab,
            CombatImpactTier.Devastating => devastatingDamagePrefab != null
                ? devastatingDamagePrefab
                : criticalDamagePrefab,
            CombatImpactTier.Critical => criticalDamagePrefab,
            _ => normalDamagePrefab
        };
        SpawnNumber(
            preferredPrefab,
            damage,
            normalDamagePrefab,
            impactTier);
    }

    public void ShowPoisonDamage(int damage)
    {
        SpawnNumber(
            poisonDamagePrefab,
            damage,
            normalDamagePrefab,
            CombatImpactTier.Normal);
    }

    public void ShowMarkBonusDamage(int damage)
    {
        SpawnNumber(
            markBonusDamagePrefab,
            damage,
            normalDamagePrefab,
            CombatImpactTier.Normal);
    }

    public void ShowStatus(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Poison:
                SpawnStatus(poisonStatusPrefab, poisonText);
                break;
            case StatusEffectType.Mark:
                SpawnStatus(markStatusPrefab, markText);
                break;
            case StatusEffectType.Stun:
                SpawnStatus(stunStatusPrefab, stunText);
                break;
            case StatusEffectType.Weakness:
                SpawnStatus(weaknessStatusPrefab, weaknessText);
                break;
        }
    }

    public void ShowLifeStealStatus()
    {
        SpawnStatus(lifeStealStatusPrefab, lifeStealText);
    }

    private void SpawnNumber(
        DamageNumber preferredPrefab,
        int damage,
        DamageNumber fallbackPrefab,
        CombatImpactTier impactTier)
    {
        if (damage <= 0)
        {
            return;
        }

        DamageNumber prefab = preferredPrefab != null
            ? preferredPrefab
            : fallbackPrefab;

        if (prefab == null)
        {
            return;
        }

        Vector3 position = transform.position + damageOffset;
        DamageNumber number = followTarget
            ? prefab.Spawn(position, damage, transform)
            : prefab.Spawn(position, damage);
        ApplyTierStyle(number, impactTier);
    }

    private void ApplyTierStyle(
        DamageNumber number,
        CombatImpactTier impactTier)
    {
        if (number == null || impactTier == CombatImpactTier.Normal)
        {
            return;
        }

        switch (impactTier)
        {
            case CombatImpactTier.Critical:
                number.SetColor(criticalDamageColor);
                number.SetScale(criticalDamageScale);
                break;
            case CombatImpactTier.Devastating:
                number.SetColor(devastatingDamageColor);
                number.SetScale(devastatingDamageScale);
                break;
            case CombatImpactTier.Defeat:
                number.SetColor(defeatDamageColor);
                number.SetScale(defeatDamageScale);
                break;
        }
    }

    private void SpawnStatus(DamageNumber prefab, string statusText)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(statusText))
        {
            return;
        }

        Vector3 position = transform.position + statusOffset;
        if (followTarget)
        {
            prefab.Spawn(position, statusText, transform);
            return;
        }

        prefab.Spawn(position, statusText);
    }
}
