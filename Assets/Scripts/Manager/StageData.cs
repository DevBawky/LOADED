using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Stage", menuName = "Loaded/Stage")]
public class StageData : ScriptableObject
{
    public const int DefaultLaneCount = 2;

    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [Header("Board Settings")]
    [Min(1)]
    [SerializeField] private int laneCount = DefaultLaneCount;

    [Header("Battles")]
    [SerializeField] private BattleData[] battles =
        System.Array.Empty<BattleData>();

    public string StageId => stageId;
    public string DisplayName => displayName;
    public int LaneCount => Mathf.Max(1, laneCount);
    public IReadOnlyList<BattleData> Battles =>
        battles ?? (IReadOnlyList<BattleData>)System.Array.Empty<BattleData>();
}

public static class StageTitleFormatter
{
    public static string Format(StageData stage, BattleData battle)
    {
        if (stage == null || battle == null)
        {
            return string.Empty;
        }

        string battleName = string.IsNullOrWhiteSpace(battle.DisplayName)
            ? battle.name
            : battle.DisplayName;
        string stageName = string.IsNullOrWhiteSpace(stage.DisplayName)
            ? stage.name
            : stage.DisplayName;
        return $"{battleName}. {stageName}";
    }
}
