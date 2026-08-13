using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapNodeType
{
    Start = 0,
    NormalBattle = 1,
    EliteBattle = 2,
    Shop = 3,
    Event = 4,
    Treasure = 5,
    Boss = 6
}

[Serializable]
public sealed class MapNodeData
{
    [SerializeField] private string nodeId;
    [SerializeField] private MapNodeType nodeType;
    [SerializeField] private List<string> nextNodeIds = new List<string>();
    [SerializeField] private Vector2 mapPosition;
    [SerializeField] private int stageIndex;
    [SerializeField] private int battleIndex;

    public string NodeId => nodeId ?? string.Empty;
    public MapNodeType NodeType => nodeType;
    public IReadOnlyList<string> NextNodeIds => nextNodeIds;
    public Vector2 MapPosition => mapPosition;
    public int StageIndex => Mathf.Max(0, stageIndex);
    public int BattleIndex => Mathf.Max(0, battleIndex);

    public MapNodeData(
        string nodeId,
        MapNodeType nodeType,
        IEnumerable<string> nextNodeIds,
        Vector2 mapPosition,
        int stageIndex = 0,
        int battleIndex = 0)
    {
        this.nodeId = nodeId;
        this.nodeType = nodeType;
        this.nextNodeIds = nextNodeIds == null
            ? new List<string>()
            : new List<string>(nextNodeIds);
        this.mapPosition = mapPosition;
        this.stageIndex = Mathf.Max(0, stageIndex);
        this.battleIndex = Mathf.Max(0, battleIndex);
    }
}

[CreateAssetMenu(fileName = "New Act Map", menuName = "Loaded/Act Map")]
public sealed class ActMapData : ScriptableObject
{
    [SerializeField] private string actId = "act_1";
    [SerializeField] private string startNodeId = "start";
    [SerializeField] private List<MapNodeData> nodes = new List<MapNodeData>();

    public string ActId => actId ?? string.Empty;
    public string StartNodeId => startNodeId ?? string.Empty;
    public IReadOnlyList<MapNodeData> Nodes => nodes;

    public bool TryGetNode(string nodeId, out MapNodeData node)
    {
        node = null;

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        foreach (MapNodeData candidate in nodes)
        {
            if (candidate != null && string.Equals(
                    candidate.NodeId,
                    nodeId,
                    StringComparison.Ordinal))
            {
                node = candidate;
                return true;
            }
        }

        return false;
    }

    public bool Validate(out string error)
    {
        error = string.Empty;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (MapNodeData node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                error = "Every map node requires a non-empty ID.";
                return false;
            }

            if (!ids.Add(node.NodeId))
            {
                error = $"Duplicate map node ID: {node.NodeId}";
                return false;
            }
        }

        if (!ids.Contains(StartNodeId))
        {
            error = $"Start node '{StartNodeId}' does not exist.";
            return false;
        }

        foreach (MapNodeData node in nodes)
        {
            foreach (string nextNodeId in node.NextNodeIds)
            {
                if (!ids.Contains(nextNodeId))
                {
                    error = $"Node '{node.NodeId}' links to missing node '{nextNodeId}'.";
                    return false;
                }
            }
        }

        return true;
    }

    public void ConfigureRuntime(
        string actId,
        string startNodeId,
        IEnumerable<MapNodeData> nodes)
    {
        this.actId = actId ?? string.Empty;
        this.startNodeId = startNodeId ?? string.Empty;
        this.nodes = nodes == null
            ? new List<MapNodeData>()
            : new List<MapNodeData>(nodes);
    }
}

[Serializable]
public sealed class RunMapState
{
    public string actId;
    public string currentNodeId;
    public string activeNodeId;
    public List<string> visitedNodeIds = new List<string>();
    public List<string> completedNodeIds = new List<string>();
    public int pendingGold;

    public void Normalize()
    {
        actId ??= string.Empty;
        currentNodeId ??= string.Empty;
        activeNodeId ??= string.Empty;
        visitedNodeIds ??= new List<string>();
        completedNodeIds ??= new List<string>();
        RemoveDuplicates(visitedNodeIds);
        RemoveDuplicates(completedNodeIds);
    }

    private static void RemoveDuplicates(List<string> values)
    {
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);

        for (int index = values.Count - 1; index >= 0; index--)
        {
            if (string.IsNullOrWhiteSpace(values[index])
                || !unique.Add(values[index]))
            {
                values.RemoveAt(index);
            }
        }
    }
}

public sealed class RunMapProgress
{
    private readonly ActMapData map;
    private readonly RunMapState state;

    public RunMapState State => state;

    public RunMapProgress(ActMapData map, RunMapState state)
    {
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        state.Normalize();

        if (string.IsNullOrWhiteSpace(state.actId))
        {
            state.actId = map.ActId;
        }

        if (string.IsNullOrWhiteSpace(state.currentNodeId))
        {
            state.currentNodeId = map.StartNodeId;
        }

        AddUnique(state.visitedNodeIds, state.currentNodeId);
    }

    public bool CanEnter(string nodeId)
    {
        if (!map.TryGetNode(nodeId, out _)
            || Contains(state.completedNodeIds, nodeId)
            || !string.IsNullOrWhiteSpace(state.activeNodeId)
            || !map.TryGetNode(state.currentNodeId, out MapNodeData current))
        {
            return false;
        }

        foreach (string nextNodeId in current.NextNodeIds)
        {
            if (string.Equals(nextNodeId, nodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryEnter(string nodeId)
    {
        if (!CanEnter(nodeId))
        {
            return false;
        }

        state.activeNodeId = nodeId;
        AddUnique(state.visitedNodeIds, nodeId);
        return true;
    }

    public bool TryCompleteActiveNode()
    {
        if (string.IsNullOrWhiteSpace(state.activeNodeId)
            || !map.TryGetNode(state.activeNodeId, out _))
        {
            return false;
        }

        AddUnique(state.completedNodeIds, state.activeNodeId);
        state.currentNodeId = state.activeNodeId;
        state.activeNodeId = string.Empty;
        return true;
    }

    public bool IsVisited(string nodeId) => Contains(
        state.visitedNodeIds,
        nodeId);

    public bool IsCompleted(string nodeId) => Contains(
        state.completedNodeIds,
        nodeId);

    private static bool Contains(List<string> values, string value)
    {
        foreach (string candidate in values)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !Contains(values, value))
        {
            values.Add(value);
        }
    }
}
