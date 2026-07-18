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
    [SerializeField] private string displayName;

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
    public BattleType BattleType => battleType;
    public bool IsBoss => battleType == BattleType.Boss;
    public int BoardCount => Mathf.Max(1, boardCount);
    public BoardTile TilePrefab => tilePrefab;
    public int SpawnTerm => Mathf.Max(0, spawnTerm);
    public IReadOnlyList<EnemyWave> Waves =>
        waves ?? (IReadOnlyList<EnemyWave>)Array.Empty<EnemyWave>();
}
