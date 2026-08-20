using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NodeMapSaveSystem
{
    private const string SaveFileName = "loaded_node_map.json";
    private const string WebSaveKey = "loaded.node.map.v1";
    private const int CurrentVersion = 1;

    public static string SavePath => Path.Combine(
        Application.persistentDataPath, SaveFileName);

    public static bool HasValidSave => TryLoad(out _);

    public static bool TryLoad(out NodeMapRunData data)
    {
        data = null;
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string json = PlayerPrefs.GetString(WebSaveKey, string.Empty);
#else
            string json = File.Exists(SavePath)
                ? File.ReadAllText(SavePath)
                : string.Empty;
#endif
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            data = JsonUtility.FromJson<NodeMapRunData>(json);
            if (data == null || data.version != CurrentVersion
                || data.nodes == null || data.nodes.Count < 2)
            {
                data = null;
                return false;
            }
            data.completedNodeIds ??= new List<int>();
            foreach (NodeMapNodeData node in data.nodes)
            {
                node.nextNodeIds ??= new List<int>();
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Node map save could not be loaded: {exception.Message}");
            return false;
        }
    }

    public static bool Save(NodeMapRunData data)
    {
        if (data == null)
        {
            return false;
        }

        try
        {
            data.version = CurrentVersion;
            string json = JsonUtility.ToJson(data, true);
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(WebSaveKey, json);
            PlayerPrefs.Save();
#else
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(SavePath, json);
#endif
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Node map save could not be written: {exception.Message}");
            return false;
        }
    }

    public static bool IsAwaitingSelection
    {
        get
        {
            return TryLoad(out NodeMapRunData data)
                && data.awaitingNodeSelection;
        }
    }

    public static bool CompleteActiveNode()
    {
        if (!TryLoad(out NodeMapRunData data) || data.activeNodeId < 0)
        {
            return false;
        }

        data.currentNodeId = data.activeNodeId;
        if (!data.completedNodeIds.Contains(data.activeNodeId))
        {
            data.completedNodeIds.Add(data.activeNodeId);
        }
        data.activeNodeId = -1;
        data.awaitingNodeSelection = true;
        return Save(data);
    }

    public static int GetCompletedNodeCount(NodeMapNodeType type)
    {
        if (!TryLoad(out NodeMapRunData data)
            || data.completedNodeIds == null || data.nodes == null)
        {
            return 0;
        }

        HashSet<int> completedIds = new HashSet<int>(
            data.completedNodeIds);
        return data.nodes.Count(node => node != null
            && node.type == type && completedIds.Contains(node.id));
    }

    public static bool TryGetSelectedBattle(
        out int stageIndex,
        out int battleIndex)
    {
        stageIndex = -1;
        battleIndex = -1;
        if (!TryLoad(out NodeMapRunData data)
            || data.activeNodeId < 0 || data.selectedBattleIndex < 0)
        {
            return false;
        }

        stageIndex = data.stageIndex;
        battleIndex = data.selectedBattleIndex;
        return true;
    }

    public static bool TryGetActiveNodeScene(out string sceneName)
    {
        sceneName = string.Empty;

        if (!TryLoad(out NodeMapRunData data) || data.activeNodeId < 0)
        {
            return false;
        }

        NodeMapNodeData activeNode = data.nodes.FirstOrDefault(
            node => node != null && node.id == data.activeNodeId);

        if (activeNode == null)
        {
            return false;
        }

        sceneName = activeNode.type switch
        {
            NodeMapNodeType.Shop => "Shop",
            NodeMapNodeType.Treasure => "Treasure",
            NodeMapNodeType.Event => "Event",
            NodeMapNodeType.NormalBattle => "Battle",
            NodeMapNodeType.EliteBattle => "Battle",
            NodeMapNodeType.Boss => "Battle",
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(sceneName);
    }

    public static void DeleteSave()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.DeleteKey(WebSaveKey);
            PlayerPrefs.Save();
#endif
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Node map save could not be deleted: {exception.Message}");
        }
    }
}
