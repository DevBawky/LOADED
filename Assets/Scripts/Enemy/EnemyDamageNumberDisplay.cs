using System.Collections.Generic;
using DamageNumbersPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDamageNumberDisplay : MonoBehaviour
{
    private const float SeparationPerScaleUnit = 0.9f;

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

    [Header("Overlap Avoidance")]
    [Tooltip("모든 적에게 표시 중인 숫자 사이에 확보할 추가 월드 간격입니다. 텍스트 스케일 기반의 자동 최소 간격보다 작게 설정해도 자동 최소값이 적용됩니다.")]
    [SerializeField, Min(0f)] private float minimumSpawnSeparation = 0.2f;
    [Tooltip("기준 위치에서 위로 분산할 수 있는 최대 행 수입니다.")]
    [SerializeField, Range(1, 4)] private int maximumSpawnRows = 2;

    [Header("Readability Scale")]
    [SerializeField, Range(0.1f, 1f)] private float damageNumberScaleMultiplier = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float statusTextScaleMultiplier = 0.5f;

    [Header("Attack Styling")]
    [SerializeField] private float criticalDamageScale = 1.18f;

    private static readonly DamageNumberSpawnLayout SharedSpawnLayout =
        new DamageNumberSpawnLayout();

    public void ShowAttackDamage(int damage, bool isCritical = false)
    {
        DamageNumber preferredPrefab = ResolveAttackDamagePrefab(
            normalDamagePrefab,
            criticalDamagePrefab,
            isCritical);
        SpawnNumber(
            preferredPrefab,
            damage,
            normalDamagePrefab,
            isCritical);
    }

    internal static DamageNumber ResolveAttackDamagePrefab(
        DamageNumber normalPrefab,
        DamageNumber criticalPrefab,
        bool isCritical)
    {
        // Impact tier controls presentation intensity only. The attack's
        // authoritative critical result is the sole prefab selector.
        return isCritical ? criticalPrefab : normalPrefab;
    }

    public void ShowPoisonDamage(int damage)
    {
        SpawnNumber(
            poisonDamagePrefab,
            damage,
            normalDamagePrefab,
            false);
    }

    public void ShowMarkBonusDamage(int damage)
    {
        SpawnNumber(
            markBonusDamagePrefab,
            damage,
            normalDamagePrefab,
            false);
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
        bool isCritical)
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

        Camera battleCamera = Camera.main;
        Vector3 visualOrigin = BattleCameraEffectSpace.ResolveActorVisualOrigin(
            transform,
            transform.position);
        Vector3 position = SharedSpawnLayout.FindAvailableOffset(
            ResolveSpawnAnchor(
                visualOrigin,
                damageOffset,
                battleCamera),
            ResolveMinimumSpawnSeparation(
                minimumSpawnSeparation,
                damageNumberScaleMultiplier,
                statusTextScaleMultiplier),
            battleCamera,
            Mathf.Max(1, maximumSpawnRows));
        DamageNumber number = SpawnWithoutSpamMovement(
            prefab,
            position,
            damage);
        ConfigureSpawnedNumber(number, position);
        ApplyAttackStyle(number, isCritical);
    }

    private void ConfigureSpawnedNumber(
        DamageNumber number,
        Vector3 spawnPosition)
    {
        if (number == null)
        {
            return;
        }

        ConfigureForBattleCamera(number);

        // The project layout owns separation. Keeping Damage Numbers Pro's
        // Collision and Push active here would apply a second, much larger
        // offset on top of minimumSpawnSeparation.
        number.SetSpamGroup(string.Empty);

        if (followTarget)
        {
            number.SetFollowedTarget(transform, false);
        }

        SharedSpawnLayout.Track(spawnPosition, number);
    }

    private static DamageNumber SpawnWithoutSpamMovement(
        DamageNumber prefab,
        Vector3 position,
        float damage)
    {
        bool collisionEnabled = prefab.enableCollision;
        bool pushEnabled = prefab.enablePush;
        bool game3DEnabled = prefab.enable3DGame;
        bool faceCameraViewEnabled = prefab.faceCameraView;

        try
        {
            // Spawn initializes spam control immediately. Suppress only that
            // synchronous initialization, then restore the prefab settings.
            prefab.enableCollision = false;
            prefab.enablePush = false;
            prefab.enable3DGame = true;
            prefab.faceCameraView = true;
            return prefab.Spawn(position, damage);
        }
        finally
        {
            prefab.enableCollision = collisionEnabled;
            prefab.enablePush = pushEnabled;
            prefab.enable3DGame = game3DEnabled;
            prefab.faceCameraView = faceCameraViewEnabled;
        }
    }

    private static DamageNumber SpawnWithoutSpamMovement(
        DamageNumber prefab,
        Vector3 position,
        string statusText)
    {
        bool collisionEnabled = prefab.enableCollision;
        bool pushEnabled = prefab.enablePush;
        bool game3DEnabled = prefab.enable3DGame;
        bool faceCameraViewEnabled = prefab.faceCameraView;

        try
        {
            prefab.enableCollision = false;
            prefab.enablePush = false;
            prefab.enable3DGame = true;
            prefab.faceCameraView = true;
            return prefab.Spawn(position, statusText);
        }
        finally
        {
            prefab.enableCollision = collisionEnabled;
            prefab.enablePush = pushEnabled;
            prefab.enable3DGame = game3DEnabled;
            prefab.faceCameraView = faceCameraViewEnabled;
        }
    }

    internal static void ConfigureForBattleCamera(
        DamageNumber number,
        Camera battleCamera = null)
    {
        if (number == null)
        {
            return;
        }

        battleCamera ??= Camera.main;
        number.enable3DGame = true;
        number.faceCameraView = true;
        number.lookAtCamera = false;
        number.cameraOverride = battleCamera == null
            ? null
            : battleCamera.transform;
        BattleSpriteBillboard.FaceTransform(number.transform, battleCamera);
    }

    private void ApplyAttackStyle(DamageNumber number, bool isCritical)
    {
        if (number == null)
        {
            return;
        }

        number.SetScale(ResolveDamageNumberScale(
            isCritical,
            damageNumberScaleMultiplier,
            criticalDamageScale));
    }

    internal static float ResolveDamageNumberScale(
        bool isCritical,
        float scaleMultiplier,
        float criticalScale)
    {
        float attackScale = isCritical ? criticalScale : 1f;
        return Mathf.Max(0f, scaleMultiplier)
            * Mathf.Max(0f, attackScale);
    }

    internal static float ResolveMinimumSpawnSeparation(
        float authoredMinimum,
        float damageScaleMultiplier,
        float statusScaleMultiplier)
    {
        float largestBaseScale = Mathf.Max(
            Mathf.Max(0f, damageScaleMultiplier),
            Mathf.Max(0f, statusScaleMultiplier));
        return Mathf.Max(
            Mathf.Max(0f, authoredMinimum),
            largestBaseScale * SeparationPerScaleUnit);
    }

    internal static Vector3 ResolveSpawnAnchor(
        Vector3 targetPosition,
        Vector3 authoredOffset,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return targetPosition + authoredOffset;
        }

        // The old 2D Z value was a render-depth offset. Move toward the
        // camera so it cannot shift the popup away from the target on screen.
        return BattleCameraEffectSpace.Offset(
            targetPosition,
            new Vector3(
                authoredOffset.x,
                authoredOffset.y,
                -authoredOffset.z),
            battleCamera);
    }

    private void SpawnStatus(DamageNumber prefab, string statusText)
    {
        if (prefab == null || string.IsNullOrWhiteSpace(statusText))
        {
            return;
        }

        Camera battleCamera = Camera.main;
        Vector3 visualOrigin = BattleCameraEffectSpace.ResolveActorVisualOrigin(
            transform,
            transform.position);
        Vector3 position = SharedSpawnLayout.FindAvailableOffset(
            ResolveSpawnAnchor(
                visualOrigin,
                statusOffset,
                battleCamera),
            ResolveMinimumSpawnSeparation(
                minimumSpawnSeparation,
                damageNumberScaleMultiplier,
                statusTextScaleMultiplier),
            battleCamera,
            Mathf.Max(1, maximumSpawnRows));
        DamageNumber number = SpawnWithoutSpamMovement(
            prefab,
            position,
            statusText);
        ConfigureSpawnedNumber(number, position);

        if (number != null)
        {
            number.SetScale(statusTextScaleMultiplier);
        }
    }
}

internal sealed class DamageNumberSpawnLayout
{
    private readonly List<Reservation> reservations =
        new List<Reservation>();

    public Vector3 FindAvailableOffset(
        Vector3 requestedOffset,
        float minimumSeparation,
        Camera battleCamera = null,
        int maximumRows = 2)
    {
        RemoveInactiveReservations();

        float separation = Mathf.Max(0f, minimumSeparation);

        if (separation <= 0f)
        {
            return requestedOffset;
        }

        int candidateCount = Mathf.Max(0, maximumRows) * 3 + 1;
        Vector3 bestCandidate = requestedOffset;
        float bestClearance = -1f;

        for (int index = 0; index < candidateCount; index++)
        {
            Vector3 candidate = requestedOffset
                + CalculateCandidateOffset(
                    index,
                    separation,
                    battleCamera);
            float clearance = FindNearestReservationDistance(
                candidate,
                battleCamera);

            if (clearance >= separation)
            {
                return candidate;
            }

            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    public void Track(Vector3 worldPosition, DamageNumber number)
    {
        if (number != null)
        {
            reservations.Add(new Reservation(worldPosition, number));
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

    private float FindNearestReservationDistance(
        Vector3 candidate,
        Camera battleCamera)
    {
        Vector2 candidatePosition = ResolveLayoutPosition(
            candidate,
            battleCamera);
        float nearestDistance = float.PositiveInfinity;

        foreach (Reservation reservation in reservations)
        {
            Vector3 reservedWorldPosition = reservation.Number == null
                ? reservation.WorldPosition
                : reservation.Number.transform.position;
            Vector2 reservedPosition = ResolveLayoutPosition(
                reservedWorldPosition,
                battleCamera);
            nearestDistance = Mathf.Min(
                nearestDistance,
                Vector2.Distance(candidatePosition, reservedPosition));
        }

        return nearestDistance;
    }

    private static Vector3 CalculateCandidateOffset(
        int index,
        float separation,
        Camera battleCamera)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }

        int gridIndex = index - 1;
        int row = gridIndex / 3 + 1;
        int columnIndex = gridIndex % 3;
        int column = columnIndex switch
        {
            0 => -1,
            1 => 1,
            _ => 0
        };
        Vector2 layoutOffset = new Vector2(
            column * separation,
            row * separation);
        return battleCamera == null
            ? new Vector3(layoutOffset.x, layoutOffset.y, 0f)
            : battleCamera.transform.right * layoutOffset.x
                + battleCamera.transform.up * layoutOffset.y;
    }

    private static Vector2 ResolveLayoutPosition(
        Vector3 worldPosition,
        Camera battleCamera)
    {
        if (battleCamera == null)
        {
            return worldPosition;
        }

        return new Vector2(
            Vector3.Dot(worldPosition, battleCamera.transform.right),
            Vector3.Dot(worldPosition, battleCamera.transform.up));
    }

    private readonly struct Reservation
    {
        public Reservation(Vector3 worldPosition, DamageNumber number)
        {
            WorldPosition = worldPosition;
            Number = number;
        }

        public Vector3 WorldPosition { get; }
        public DamageNumber Number { get; }
    }
}
