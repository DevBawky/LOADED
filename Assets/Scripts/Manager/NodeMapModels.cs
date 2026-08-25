using System;
using System.Collections.Generic;
using UnityEngine;

public enum NodeMapNodeType
{
    Start,
    NormalBattle,
    EliteBattle,
    Shop,
    Treasure,
    Event,
    Boss
}

public enum NodeMapBattleProgressSection
{
    Early,
    Middle,
    Late
}

[Serializable]
public sealed class NodeMapNodeDescription
{
    public NodeMapNodeType nodeType;
    [Tooltip("Panel | Node Description의 Text | Node Type에 표시할 이름입니다.")]
    public string displayName;
    [Tooltip("Panel | Node Description의 Text | Node Description에 표시할 설명입니다.")]
    [TextArea(2, 5)] public string description;
}

[Serializable]
public sealed class NodeMapGenerationRule
{
    public NodeMapNodeType nodeType = NodeMapNodeType.NormalBattle;
    [Tooltip("일반 노드 배치의 상대 가중치입니다. Shop과 Treasure는 확정 전용 열만 사용하므로 이 값을 사용하지 않습니다.")]
    [Min(0)] public int weight = 1;
    [Tooltip("일반 배치에서 보장할 최소 개수입니다. Shop과 Treasure에는 사용하지 않습니다.")]
    [Min(0)] public int minimumCount;
    [Tooltip("일반 배치의 최대 개수이며 -1은 무제한입니다. Shop과 Treasure에는 사용하지 않습니다.")]
    [Min(-1)] public int maximumCount = -1;
}

[Serializable]
public sealed class NodeMapNodeData
{
    public int id;
    public int column;
    public int row;
    public NodeMapNodeType type;
    public int battleIndex = -1;
    public List<int> nextNodeIds = new List<int>();
}

[Serializable]
public sealed class NodeMapRunData
{
    public int version = 1;
    public int generationSettingsHash;
    public int seed;
    public int stageIndex;
    public int currentNodeId;
    public int activeNodeId = -1;
    public int selectedBattleIndex = -1;
    public bool awaitingNodeSelection = true;
    public List<int> completedNodeIds = new List<int>();
    public List<NodeMapNodeData> nodes = new List<NodeMapNodeData>();
}
