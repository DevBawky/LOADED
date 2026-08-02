using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehaviorType
{
    Melee = 0,
    Gunner = 1,
    Thrower = 2,
    Porter = 3
}

public enum EnemyDropType
{
    Gold = 0,
    InventoryItem = 1,
    Bullet = 2
}

[Serializable]
public class EnemyDropItemData
{
    [Tooltip("보상으로 지급할 항목의 종류입니다.")]
    [SerializeField] private EnemyDropType dropType;
    [Min(1)]
    [Tooltip("이 항목이 선택됐을 때 지급할 수량입니다.")]
    [SerializeField] private int amount = 1;
    [Min(0f)]
    [Tooltip("다른 보상 항목과 비교할 때 사용하는 상대적인 선택 가중치입니다.")]
    [SerializeField] private float selectionWeight = 1f;
    [Tooltip("보상 종류가 Inventory Item일 때 지급할 아이템입니다.")]
    [SerializeField] private ItemData itemData;
    [Tooltip("보상 종류가 Bullet일 때 지급할 탄환입니다.")]
    [SerializeField] private BulletData bulletData;

    public EnemyDropType DropType => dropType;
    public int Amount => Mathf.Max(1, amount);
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public ItemData ItemData => itemData;
    public BulletData BulletData => bulletData;

    public bool IsConfigured => dropType switch
    {
        EnemyDropType.Gold => amount > 0,
        EnemyDropType.InventoryItem => itemData != null && amount > 0,
        EnemyDropType.Bullet => bulletData != null && amount > 0,
        _ => false
    };
}

[CreateAssetMenu(fileName = "New Enemy", menuName = "Loaded/Enemy/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Basic Information")]
    [Tooltip("저장 및 참조에 사용하는 고유 ID입니다. 다른 적과 중복되지 않게 설정하세요.")]
    [SerializeField] private string enemyId;
    [Tooltip("게임 UI에 표시할 적 이름입니다.")]
    [SerializeField] private string displayName;
    [TextArea]
    [Tooltip("적의 특징과 전투 방식을 설명하는 문장입니다.")]
    [SerializeField] private string description;
    [Tooltip("월드에 표시할 적 스프라이트입니다.")]
    [SerializeField] private Sprite sprite;

    [Header("Stats")]
    [Min(1)]
    [Tooltip("적의 최대 체력입니다.")]
    [SerializeField] private int maxHealth = 1;

    [Header("Defeat Drops")]
    [Range(0f, 100f)]
    [Tooltip("적 처치 시 보상 자체가 등장할 확률입니다.")]
    [SerializeField] private float dropChance = 100f;
    [Tooltip("보상이 등장했을 때 가중치에 따라 선택될 후보 목록입니다.")]
    [SerializeField] private List<EnemyDropItemData> dropItems =
        new List<EnemyDropItemData>();

    [Header("Behavior")]
    [Tooltip("Melee는 근접 추격형, Gunner는 직선 사격형, Thrower는 지정 타일 포격형입니다.")]
    [SerializeField] private EnemyBehaviorType behaviorType;
    [Min(0)]
    [Tooltip("근접 적이 공격 후 유지하려는 플레이어와의 거리입니다.")]
    [SerializeField] private int preferredDistance;
    [Min(1)]
    [Tooltip("적의 공격 예약 UI에 동시에 저장할 수 있는 최대 공격 개수입니다.")]
    [SerializeField] private int maxQueuedAttacks = 3;
    [Min(0f)]
    [Tooltip("예약된 공격을 연속 실행할 때 각 공격 사이의 시간입니다.")]
    [SerializeField] private float queuedActionInterval = 0.2f;
    [Min(0f)]
    [Tooltip("Image | Queue와 행동 타일 하나가 서서히 나타나는 시간입니다.")]
    [SerializeField] private float queueElementRevealDuration = 0.25f;
    [Range(0f, 1f)]
    [Tooltip("근접 적이 선호 거리 안에서 추가 공격을 예약할 확률입니다.")]
    [SerializeField] private float meleeAdditionalAttackChance = 0.5f;
    [Min(1)]
    [Tooltip("원거리 적이 플레이어를 공격 준비할 수 있는 최대 타일 거리입니다.")]
    [SerializeField] private int firingRange = 5;
    [Min(0)]
    [Tooltip("A ranged enemy spends this many turns recovering after an attack.")]
    [SerializeField] private int recoveryTurns = 1;

    [Header("Porter Support")]
    [Min(0)]
    [Tooltip("Number of heal or shield actions the porter can perform.")]
    [SerializeField] private int maxSupportCharges = 2;
    [Min(1)]
    [Tooltip("Health restored by one porter support action.")]
    [SerializeField] private int supportHealAmount = 10;
    [Min(1)]
    [Tooltip("Shield granted by one porter support action.")]
    [SerializeField] private int supportShieldAmount = 10;
    [Range(0f, 1f)]
    [Tooltip("Allies at or below this health ratio are healed before shielding.")]
    [SerializeField] private float supportHealThreshold = 0.5f;

    [Header("Thrower Projectile")]
    [Tooltip("투척병이 포물선으로 던질 투사체 프리팹입니다. 비어 있으면 스프라이트 투척물을 자동 생성합니다.")]
    [SerializeField] private GameObject thrownProjectilePrefab;
    [Tooltip("투척물 프리팹이 없을 때 표시할 스프라이트입니다. 비어 있으면 기본 원형 이미지를 사용합니다.")]
    [SerializeField] private Sprite thrownProjectileSprite;
    [Tooltip("자동 생성되는 투척 스프라이트의 색상입니다.")]
    [SerializeField] private Color thrownProjectileColor = Color.white;
    [Min(0.01f)]
    [Tooltip("자동 생성되는 투척 스프라이트의 월드 크기입니다.")]
    [SerializeField] private float thrownProjectileSize = 0.35f;
    [Min(0f)]
    [Tooltip("투척체가 목표 타일까지 날아가는 시간입니다.")]
    [SerializeField] private float thrownProjectileDuration = 0.5f;
    [Min(0f)]
    [Tooltip("투척 궤적 정점의 추가 높이입니다.")]
    [SerializeField] private float thrownProjectileArcHeight = 2f;

    [Header("Ranged Attack Telegraph")]
    [Tooltip("총잡이의 직선 사격 범위를 표시할 LineRenderer 머티리얼입니다.")]
    [SerializeField] private Material gunnerTelegraphMaterial;
    [Tooltip("투척병의 포물선 궤적을 표시할 LineRenderer 머티리얼입니다.")]
    [SerializeField] private Material throwerTelegraphMaterial;
    [Tooltip("Line material used while a porter is preparing support.")]
    [SerializeField] private Material supportTelegraphMaterial;
    [Tooltip("Telegraph color for a prepared heal.")]
    [SerializeField] private Color supportHealColor =
        new Color(0.25f, 1f, 0.35f, 1f);
    [Tooltip("Telegraph color for a prepared shield.")]
    [SerializeField] private Color supportShieldColor =
        new Color(0.2f, 0.8f, 1f, 1f);
    [Min(0.001f)]
    [Tooltip("직선 및 포물선 공격 예고선의 굵기입니다.")]
    [SerializeField] private float telegraphLineWidth = 0.08f;
    [Tooltip("공격 예고선을 캐릭터와 목표 타일 중심에서 위로 올릴 높이입니다.")]
    [SerializeField] private float telegraphVerticalOffset = 0.15f;
    [Range(4, 64)]
    [Tooltip("투척병의 포물선 예고선을 구성할 점 개수입니다. 값이 높을수록 곡선이 부드럽습니다.")]
    [SerializeField] private int throwerTelegraphSegments = 20;
    [Tooltip("적 스프라이트와 같은 Sorting Layer 안에서 LineRenderer가 사용할 렌더링 순서입니다.")]
    [SerializeField] private int telegraphSortingOrder = 20;

    [Header("Actions")]
    [Tooltip("이 적이 사용할 수 있는 이동 및 공격 행동 목록입니다.")]
    [SerializeField] private List<EnemyActionData> actions = new List<EnemyActionData>();
    [Tooltip("활성화하면 시작 공격 행동의 선택 순서를 무작위로 섞을 수 있습니다. 현재 큐 AI에서는 목록 순서를 기본으로 사용합니다.")]
    [SerializeField] private bool randomizeStartingActionIndex;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Sprite => sprite;
    public int MaxHealth => maxHealth;
    public float DropChance => Mathf.Clamp(dropChance, 0f, 100f);
    public IReadOnlyList<EnemyDropItemData> DropItems =>
        dropItems ?? (IReadOnlyList<EnemyDropItemData>)Array.Empty<EnemyDropItemData>();
    public EnemyBehaviorType BehaviorType => behaviorType;
    public int PreferredDistance => preferredDistance;
    public int MaxQueuedAttacks => Mathf.Max(1, maxQueuedAttacks);
    public float QueuedActionInterval => Mathf.Max(0f, queuedActionInterval);
    public float QueueElementRevealDuration =>
        Mathf.Max(0f, queueElementRevealDuration);
    public float MeleeAdditionalAttackChance =>
        Mathf.Clamp01(meleeAdditionalAttackChance);
    // Kept for older callers and serialized assets. Ranged enemies always use
    // the full board, so this value is intentionally infinite.
    public int FiringRange => int.MaxValue;
    public int RecoveryTurns => Mathf.Max(0, recoveryTurns);
    public int MaxSupportCharges => Mathf.Max(0, maxSupportCharges);
    public int SupportHealAmount => Mathf.Max(1, supportHealAmount);
    public int SupportShieldAmount => Mathf.Max(1, supportShieldAmount);
    public float SupportHealThreshold => Mathf.Clamp01(supportHealThreshold);
    public GameObject ThrownProjectilePrefab => thrownProjectilePrefab;
    public Sprite ThrownProjectileSprite => thrownProjectileSprite;
    public Color ThrownProjectileColor => thrownProjectileColor;
    public float ThrownProjectileSize => Mathf.Max(0.01f, thrownProjectileSize);
    public float ThrownProjectileDuration =>
        Mathf.Max(0f, thrownProjectileDuration);
    public float ThrownProjectileArcHeight =>
        Mathf.Max(0f, thrownProjectileArcHeight);
    public Material GunnerTelegraphMaterial => gunnerTelegraphMaterial;
    public Material ThrowerTelegraphMaterial => throwerTelegraphMaterial;
    public Material SupportTelegraphMaterial => supportTelegraphMaterial;
    public Color SupportHealColor => supportHealColor;
    public Color SupportShieldColor => supportShieldColor;
    public float TelegraphLineWidth => Mathf.Max(0.001f, telegraphLineWidth);
    public float TelegraphVerticalOffset => telegraphVerticalOffset;
    public int ThrowerTelegraphSegments => Mathf.Clamp(
        throwerTelegraphSegments,
        4,
        64);
    public int TelegraphSortingOrder => telegraphSortingOrder;
    public IReadOnlyList<EnemyActionData> Actions =>
        actions ?? (IReadOnlyList<EnemyActionData>)Array.Empty<EnemyActionData>();
    public bool RandomizeStartingActionIndex => randomizeStartingActionIndex;

    public bool TryRollDrop(out EnemyDropItemData selectedDrop)
    {
        selectedDrop = null;

        float chance = DropChance;

        if (chance <= 0f
            || chance < 100f && UnityEngine.Random.Range(0f, 100f) >= chance)
        {
            return false;
        }

        float totalWeight = 0f;

        foreach (EnemyDropItemData dropItem in DropItems)
        {
            if (dropItem != null && dropItem.IsConfigured)
            {
                totalWeight += dropItem.SelectionWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);

        foreach (EnemyDropItemData dropItem in DropItems)
        {
            if (dropItem == null || !dropItem.IsConfigured
                || dropItem.SelectionWeight <= 0f)
            {
                continue;
            }

            roll -= dropItem.SelectionWeight;

            if (roll <= 0f)
            {
                selectedDrop = dropItem;
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        dropChance = Mathf.Clamp(dropChance, 0f, 100f);
        preferredDistance = Mathf.Max(0, preferredDistance);
        maxQueuedAttacks = Mathf.Max(1, maxQueuedAttacks);
        queuedActionInterval = Mathf.Max(0f, queuedActionInterval);
        queueElementRevealDuration = Mathf.Max(
            0f,
            queueElementRevealDuration);
        meleeAdditionalAttackChance = Mathf.Clamp01(
            meleeAdditionalAttackChance);
        firingRange = Mathf.Max(1, firingRange);
        recoveryTurns = Mathf.Max(0, recoveryTurns);
        maxSupportCharges = Mathf.Max(0, maxSupportCharges);
        supportHealAmount = Mathf.Max(1, supportHealAmount);
        supportShieldAmount = Mathf.Max(1, supportShieldAmount);
        supportHealThreshold = Mathf.Clamp01(supportHealThreshold);
        thrownProjectileDuration = Mathf.Max(0f, thrownProjectileDuration);
        thrownProjectileSize = Mathf.Max(0.01f, thrownProjectileSize);
        thrownProjectileArcHeight = Mathf.Max(
            0f,
            thrownProjectileArcHeight);
        telegraphLineWidth = Mathf.Max(0.001f, telegraphLineWidth);
        throwerTelegraphSegments = Mathf.Clamp(
            throwerTelegraphSegments,
            4,
            64);
    }
}
