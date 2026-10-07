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
    [Tooltip("Battle 씬의 3D 환경, 카메라와 조명 설정입니다.")]
    [SerializeField] private BattleEnvironmentProfile environmentProfile;
    [Tooltip("전투 시작 시 생성할 보드 칸 수의 최솟값입니다.")]
    [Min(1)]
    [SerializeField] private int minimumBoardCount = 7;
    [Tooltip("전투 시작 시 생성할 보드 칸 수의 최댓값입니다.")]
    [Min(1)]
    [SerializeField] private int maximumBoardCount = 7;
    [SerializeField] private BoardTile tilePrefab;

    [Header("Enemy Spawning")]
    [Min(1)]
    [Tooltip("이 전투에서 생성되는 적의 총 수입니다.")]
    [SerializeField] private int duelClockEnemySpawnCount = 1;
    [Tooltip("이 전투에서 등장할 수 있는 적과 가중치입니다.")]
    [SerializeField] private DuelClockEnemySpawnEntry[]
        duelClockEnemySpawnEntries =
            Array.Empty<DuelClockEnemySpawnEntry>();

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
    public BattleEnvironmentProfile EnvironmentProfile =>
        environmentProfile;
    public bool IsBoss => battleType == BattleType.Boss;
    public int MinimumBoardCount => Mathf.Max(
        1,
        Mathf.Min(minimumBoardCount, maximumBoardCount));
    public int MaximumBoardCount => Mathf.Max(
        MinimumBoardCount,
        Mathf.Max(minimumBoardCount, maximumBoardCount));
    public BoardTile TilePrefab => tilePrefab;
    public int DuelClockEnemySpawnCount =>
        Mathf.Max(1, duelClockEnemySpawnCount);
    public IReadOnlyList<DuelClockEnemySpawnEntry>
        DuelClockEnemySpawnEntries =>
            duelClockEnemySpawnEntries
            ?? (IReadOnlyList<DuelClockEnemySpawnEntry>)Array.Empty<
                DuelClockEnemySpawnEntry>();

    public int RollBoardCount()
    {
        int minimum = MinimumBoardCount;
        int maximum = MaximumBoardCount;
        return minimum == maximum
            ? minimum
            : UnityEngine.Random.Range(minimum, maximum + 1);
    }

    public int ResolveSavedBoardCount(int savedBoardCount)
    {
        return savedBoardCount >= MinimumBoardCount
            && savedBoardCount <= MaximumBoardCount
                ? savedBoardCount
                : MinimumBoardCount;
    }

    private void OnValidate()
    {
        minimumBoardCount = Mathf.Max(1, minimumBoardCount);
        maximumBoardCount = Mathf.Max(
            minimumBoardCount,
            maximumBoardCount);
        duelClockEnemySpawnCount = Mathf.Max(
            1,
            duelClockEnemySpawnCount);
    }
}
