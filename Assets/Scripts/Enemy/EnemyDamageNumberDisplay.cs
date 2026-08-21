using System.Collections.Generic;
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

    [Header("Overlap Avoidance")]
    [Tooltip("같은 적에게 표시 중인 숫자 사이에 확보할 최소 월드 간격입니다. 0이면 강제 간격을 사용하지 않습니다.")]
    [SerializeField, Min(0f)] private float minimumSpawnSeparation = 0.2f;

    [Header("Impact Tier Styling")]
    [SerializeField] private Color criticalDamageColor =
        new Color(1f, 0.86f, 0.28f, 1f);
    [SerializeField] private Color devastatingDamageColor =
        new Color(1f, 0.32f, 0.08f, 1f);
    [SerializeField] private Color defeatDamageColor =
        new Color(1f, 0.92f, 0.78f, 1f);
    [SerializeField] private float criticalDamageScale = 1.18f;
    [SerializeField] private float devastatingDamageScale = 1.42f;

    private readonly DamageNumberSpawnLayout spawnLayout =
        new DamageNumberSpawnLayout();

    private void OnDisable()
    {
        spawnLayout.Clear();
    }

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

        Vector3 localOffset = spawnLayout.FindAvailableOffset(
            damageOffset,
            minimumSpawnSeparation);
        Vector3 position = transform.position + localOffset;
        DamageNumber number = SpawnWithoutSpamMovement(
            prefab,
            position,
            damage);
        ConfigureSpawnedNumber(number, localOffset);
        ApplyTierStyle(number, impactTier);
    }

    private void ConfigureSpawnedNumber(
        DamageNumber number,
        Vector3 localOffset)
    {
        if (number == null)
        {
            return;
        }

        // The project layout owns separation. Keeping Damage Numbers Pro's
        // Collision and Push active here would apply a second, much larger
        // offset on top of minimumSpawnSeparation.
        number.SetSpamGroup(string.Empty);

        if (followTarget)
        {
            number.SetFollowedTarget(transform, false);
        }

        spawnLayout.Track(localOffset, number);
    }

    private static DamageNumber SpawnWithoutSpamMovement(
        DamageNumber prefab,
        Vector3 position,
        float damage)
    {
        bool collisionEnabled = prefab.enableCollision;
        bool pushEnabled = prefab.enablePush;

        try
        {
            // Spawn initializes spam control immediately. Suppress only that
            // synchronous initialization, then restore the prefab settings.
            prefab.enableCollision = false;
            prefab.enablePush = false;
            return prefab.Spawn(position, damage);
        }
        finally
        {
            prefab.enableCollision = collisionEnabled;
            prefab.enablePush = pushEnabled;
        }
    }

    private static DamageNumber SpawnWithoutSpamMovement(
        DamageNumber prefab,
        Vector3 position,
        string statusText)
    {
        bool collisionEnabled = prefab.enableCollision;
        bool pushEnabled = prefab.enablePush;

        try
        {
            prefab.enableCollision = false;
            prefab.enablePush = false;
            return prefab.Spawn(position, statusText);
        }
        finally
        {
            prefab.enableCollision = collisionEnabled;
            prefab.enablePush = pushEnabled;
        }
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
                break;
        }
    }

    private void SpawnStatus(DamageNumber prefab, string statusText)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(statusText))
        {
            return;
        }

        Vector3 localOffset = spawnLayout.FindAvailableOffset(
            statusOffset,
            minimumSpawnSeparation);
        Vector3 position = transform.position + localOffset;
        DamageNumber number = SpawnWithoutSpamMovement(
            prefab,
            position,
            statusText);
        ConfigureSpawnedNumber(number, localOffset);
    }
}

internal sealed class DamageNumberSpawnLayout
{
    private readonly List<Reservation> reservations =
        new List<Reservation>();

    public Vector3 FindAvailableOffset(
        Vector3 requestedOffset,
        float minimumSeparation)
    {
        RemoveInactiveReservations();

        float separation = Mathf.Max(0f, minimumSeparation);

        if (separation <= 0f)
        {
            return requestedOffset;
        }

        int candidateCount = reservations.Count * 3 + 16;

        for (int index = 0; index < candidateCount; index++)
        {
            Vector3 candidate = requestedOffset
                + CalculateCandidateOffset(index, separation);

            if (!OverlapsReservation(candidate, separation))
            {
                return candidate;
            }
        }

        return requestedOffset
            + Vector3.up * separation * (reservations.Count + 1);
    }

    public void Track(Vector3 localOffset, DamageNumber number)
    {
        if (number != null)
        {
            reservations.Add(new Reservation(localOffset, number));
        }
    }

    public void Clear()
    {
        reservations.Clear();
    }

    private void RemoveInactiveReservations()
    {
        for (int index = reservations.Count - 1; index >= 0; index--)
        {
            DamageNumber number = reservations[index].Number;

            if (number == null || !number.isActiveAndEnabled)
            {
                reservations.RemoveAt(index);
            }
        }
    }

    private bool OverlapsReservation(
        Vector3 candidate,
        float minimumSeparation)
    {
        Vector2 candidatePosition = candidate;

        foreach (Reservation reservation in reservations)
        {
            Vector2 reservedPosition = reservation.LocalOffset;

            if (Vector2.Distance(candidatePosition, reservedPosition)
                < minimumSeparation)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 CalculateCandidateOffset(
        int index,
        float separation)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }

        int gridIndex = index - 1;
        int row = gridIndex / 3 + 1;
        int column = gridIndex % 3 - 1;
        return new Vector3(
            column * separation,
            row * separation,
            0f);
    }

    private readonly struct Reservation
    {
        public Reservation(Vector3 localOffset, DamageNumber number)
        {
            LocalOffset = localOffset;
            Number = number;
        }

        public Vector3 LocalOffset { get; }
        public DamageNumber Number { get; }
    }
}
