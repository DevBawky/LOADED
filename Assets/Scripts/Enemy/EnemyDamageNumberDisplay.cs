using DamageNumbersPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDamageNumberDisplay : MonoBehaviour
{
    [Header("Damage Number Prefabs")]
    [SerializeField] private DamageNumber normalDamagePrefab;
    [SerializeField] private DamageNumber criticalDamagePrefab;
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

    public void ShowAttackDamage(int damage, bool isCritical)
    {
        SpawnNumber(
            isCritical ? criticalDamagePrefab : normalDamagePrefab,
            damage,
            normalDamagePrefab);
    }

    public void ShowPoisonDamage(int damage)
    {
        SpawnNumber(poisonDamagePrefab, damage, normalDamagePrefab);
    }

    public void ShowMarkBonusDamage(int damage)
    {
        SpawnNumber(markBonusDamagePrefab, damage, normalDamagePrefab);
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
        DamageNumber fallbackPrefab)
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
        if (followTarget)
        {
            prefab.Spawn(position, damage, transform);
            return;
        }

        prefab.Spawn(position, damage);
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
