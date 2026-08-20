using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Stage", menuName = "Loaded/Stage")]
public class StageData : ScriptableObject
{
    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [SerializeField] private BattleData[] battles =
        System.Array.Empty<BattleData>();

    public string StageId => stageId;
    public string DisplayName => displayName;
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
