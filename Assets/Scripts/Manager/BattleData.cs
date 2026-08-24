using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleType
{
    Normal = 0,
    Boss = 1,
    Elite = 2
}

public enum CombatPacingMode
{
    Legacy = 0,
    DuelClock = 1
}

[Serializable]
public sealed class DuelClockEnemySpawnEntry
{
    [SerializeField] private EnemyData enemyData;
    [Min(0.01f)]
    [SerializeField] private float weight = 1f;
    [Min(0)]
    [SerializeField] private int minimumSpawnCount;
    [Min(0f)]
    [Tooltip("Fraction of base weight added for each spawn where this enemy was not selected. 0.25 adds 25% per miss.")]
    [SerializeField] private float missedSpawnWeightIncrease = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] private float previousSpawnWeightMultiplier = 0.35f;

    public DuelClockEnemySpawnEntry()
    {
    }

    internal DuelClockEnemySpawnEntry(
        EnemyData configuredEnemy,
        float configuredWeight,
        int configuredMinimumSpawnCount = 0,
        float configuredPreviousSpawnWeightMultiplier = 0.35f,
        float configuredMissedSpawnWeightIncrease = 0.25f)
    {
        enemyData = configuredEnemy;
        weight = configuredWeight;
        minimumSpawnCount = configuredMinimumSpawnCount;
        missedSpawnWeightIncrease = configuredMissedSpawnWeightIncrease;
        previousSpawnWeightMultiplier =
            configuredPreviousSpawnWeightMultiplier;
    }

    public EnemyData EnemyData => enemyData;
    public float Weight => IsFinite(weight) ? Mathf.Max(0f, weight) : 0f;
    public int MinimumSpawnCount => Mathf.Max(0, minimumSpawnCount);
    public float MissedSpawnWeightIncrease =>
        IsFinite(missedSpawnWeightIncrease)
            ? Mathf.Max(0f, missedSpawnWeightIncrease)
            : 0f;
    public float PreviousSpawnWeightMultiplier =>
        IsFinite(previousSpawnWeightMultiplier)
            ? Mathf.Clamp01(previousSpawnWeightMultiplier)
            : 0f;

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[CreateAssetMenu(fileName = "New Battle", menuName = "Loaded/Battle")]
public class BattleData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string battleId;

    [Header("Battle Start Notice")]
    [Tooltip("Text | Stage Info에 표시할 제목입니다.")]
    [SerializeField] private string displayName;
    [Tooltip("Text | Stage Sub Title에 표시할 설명입니다.")]
    [TextArea(1, 3)]
    [SerializeField] private string noticeDescription;

    [Header("Battle Clear Notice")]
    [Tooltip("전투 클리어 시 Text | Stage Info에 표시할 제목입니다.")]
    [SerializeField] private string clearNoticeTitle = "BATTLE CLEAR";
    [Tooltip("전투 클리어 시 Text | Stage Sub Title에 표시할 설명입니다.")]
    [TextArea(1, 3)]
    [SerializeField] private string clearNoticeDescription;

    [Header("Battle Settings")]
    [SerializeField] private BattleType battleType;
    [Min(1)]
    [SerializeField] private int boardCount = 7;
    [SerializeField] private BoardTile tilePrefab;
    [Min(0)]
    [SerializeField] private int spawnTerm = 2;
    [SerializeField] private EnemyWave[] waves = Array.Empty<EnemyWave>();

    [Header("Combat Pacing")]
    [SerializeField] private CombatPacingMode combatPacingMode =
        CombatPacingMode.Legacy;
    [Min(0f)]
    [Tooltip("Natural Duel Clock charge per unscaled second. A value of 4 fills one 100-point cycle in 25 seconds.")]
    [SerializeField] private float duelClockNaturalProgressPerSecond = 4f;
    [Min(0f)]
    [Tooltip("Duel Clock charge committed when one paid player action completes.")]
    [SerializeField] private float duelClockPaidActionProgress = 45f;
    [Min(1)]
    [Tooltip("Completed Duel Clock beats between single-enemy reinforcements.")]
    [SerializeField] private int duelClockEnemyWaveCount = 5;
    [Min(1)]
    [Tooltip("Total enemies spawned during this Duel Clock battle.")]
    [SerializeField] private int duelClockEnemySpawnCount = 1;
    [Tooltip("Weighted enemy types available to this Duel Clock battle.")]
    [SerializeField] private DuelClockEnemySpawnEntry[]
        duelClockEnemySpawnEntries =
            Array.Empty<DuelClockEnemySpawnEntry>();
    [HideInInspector]
    [SerializeField] private EnemyData[] duelClockEnemyPool =
        Array.Empty<EnemyData>();

    public string BattleId => battleId;
    public string DisplayName => displayName;
    public string NoticeTitle => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string NoticeDescription => noticeDescription ?? string.Empty;
    public string ClearNoticeTitle => string.IsNullOrWhiteSpace(clearNoticeTitle)
        ? "BATTLE CLEAR"
        : clearNoticeTitle;
    public string ClearNoticeDescription =>
        clearNoticeDescription ?? string.Empty;
    public BattleType BattleType => battleType;
    public bool IsBoss => battleType == BattleType.Boss;
    public int BoardCount => Mathf.Max(1, boardCount);
    public BoardTile TilePrefab => tilePrefab;
    public int SpawnTerm => Mathf.Max(0, spawnTerm);
    public IReadOnlyList<EnemyWave> Waves =>
        waves ?? (IReadOnlyList<EnemyWave>)Array.Empty<EnemyWave>();
    public CombatPacingMode PacingMode => combatPacingMode;
    public float DuelClockNaturalProgressPerSecond =>
        Mathf.Max(0f, duelClockNaturalProgressPerSecond);
    public float DuelClockPaidActionProgress =>
        Mathf.Max(0f, duelClockPaidActionProgress);
    public int DuelClockEnemyWaveCount =>
        Mathf.Max(1, duelClockEnemyWaveCount);
    public int DuelClockEnemySpawnCount =>
        Mathf.Max(1, duelClockEnemySpawnCount);
    public IReadOnlyList<DuelClockEnemySpawnEntry>
        DuelClockEnemySpawnEntries =>
            duelClockEnemySpawnEntries
            ?? (IReadOnlyList<DuelClockEnemySpawnEntry>)Array.Empty<
                DuelClockEnemySpawnEntry>();
    public IReadOnlyList<EnemyData> DuelClockEnemyPool =>
        duelClockEnemyPool ?? (IReadOnlyList<EnemyData>)Array.Empty<EnemyData>();
}
