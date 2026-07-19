using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleType
{
    Normal = 0,
    Boss = 1
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
}
