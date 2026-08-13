using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class RunManager : MonoBehaviour
{
    public const string NodeMapSceneName = "NodeMap";
    public const string CombatSceneName = "Stage 1";
    public const string ShopSceneName = "Shop";
    public const string EventSceneName = "Event";

    private const string MapSaveKey = "loaded.run.map.v1";
    private static RunManager instance;

    private ActMapData map;
    private RunMapProgress progress;
    private MapNodeData activeNode;

    public static RunManager Instance => EnsureInstance();
    public ActMapData Map => map;
    public RunMapState State => progress?.State;
    public MapNodeData ActiveNode => activeNode;
    public event Action ProgressChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static RunManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<RunManager>();

        if (instance == null)
        {
            GameObject owner = new GameObject("Run Manager");
            instance = owner.AddComponent<RunManager>();
        }

        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        map = CreateMilestoneMap();
    }

    public void Begin(RunStartMode startMode)
    {
        if (startMode == RunStartMode.Continue
            && TryLoadMapState(out RunMapState savedMap))
        {
            Restore(savedMap);
            return;
        }

        if (startMode == RunStartMode.Continue
            && RunSaveSystem.TryLoad(out RunSaveData saveData))
        {
            Restore(saveData.map);
            return;
        }

        Restore(new RunMapState());
        SaveMapState();
    }

    public void Restore(RunMapState state)
    {
        state ??= new RunMapState();
        progress = new RunMapProgress(map, state);
        activeNode = null;

        if (!string.IsNullOrWhiteSpace(state.activeNodeId))
        {
            map.TryGetNode(state.activeNodeId, out activeNode);
        }

        SaveMapState();
        ProgressChanged?.Invoke();
    }

    public bool CanEnter(string nodeId)
    {
        EnsureProgress();
        return progress.CanEnter(nodeId);
    }

    public bool TryEnterNode(string nodeId)
    {
        EnsureProgress();

        if (!progress.TryEnter(nodeId)
            || !map.TryGetNode(nodeId, out activeNode))
        {
            return false;
        }

        SaveMapState();
        ProgressChanged?.Invoke();

        switch (activeNode.NodeType)
        {
            case MapNodeType.NormalBattle:
            case MapNodeType.EliteBattle:
            case MapNodeType.Boss:
                RunSaveSystem.RequestStart(RunSaveSystem.HasValidSave
                    ? RunStartMode.Continue
                    : RunStartMode.New);
                LoadScene(CombatSceneName);
                break;
            case MapNodeType.Shop:
                RunSaveSystem.RequestStart(RunStartMode.Continue);
                LoadScene(ShopSceneName);
                break;
            case MapNodeType.Event:
            case MapNodeType.Treasure:
                LoadScene(EventSceneName);
                break;
            default:
                return false;
        }

        return true;
    }

    public bool CompleteActiveNode()
    {
        EnsureProgress();

        if (!progress.TryCompleteActiveNode())
        {
            return false;
        }

        activeNode = null;
        SaveMapState();
        ProgressChanged?.Invoke();
        return true;
    }

    public bool CompleteActiveNode(NodeResult result)
    {
        if (result == null || !result.succeeded)
        {
            return false;
        }

        if (!CompleteActiveNode())
        {
            return false;
        }

        if (result.goldDelta > 0)
        {
            AddPendingGold(result.goldDelta);
        }

        return true;
    }

    public void ApplyToSave(RunSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        EnsureProgress();
        saveData.map = CloneState(progress.State);
    }

    public void ReturnToMap()
    {
        LoadScene(NodeMapSceneName);
    }

    public bool ResumeActiveNode()
    {
        EnsureProgress();

        if (activeNode == null)
        {
            return false;
        }

        switch (activeNode.NodeType)
        {
            case MapNodeType.NormalBattle:
            case MapNodeType.EliteBattle:
            case MapNodeType.Boss:
                RunSaveSystem.RequestStart(RunStartMode.Continue);
                LoadScene(CombatSceneName);
                return true;
            case MapNodeType.Shop:
                RunSaveSystem.RequestStart(RunStartMode.Continue);
                LoadScene(ShopSceneName);
                return true;
            case MapNodeType.Event:
            case MapNodeType.Treasure:
                LoadScene(EventSceneName);
                return true;
            default:
                return false;
        }
    }

    public void ClearRun()
    {
        PersistentRunContext.MarkRunEnded();
        PlayerPrefs.DeleteKey(MapSaveKey);
        PlayerPrefs.Save();
        progress = null;
        activeNode = null;
    }

    private void EnsureProgress()
    {
        if (progress == null)
        {
            Begin(RunStartMode.New);
        }
    }

    private void SaveMapState()
    {
        if (progress == null)
        {
            return;
        }

        PlayerPrefs.SetString(MapSaveKey, JsonUtility.ToJson(progress.State));
        PlayerPrefs.Save();
    }

    private static bool TryLoadMapState(out RunMapState state)
    {
        state = null;
        string json = PlayerPrefs.GetString(MapSaveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        state = JsonUtility.FromJson<RunMapState>(json);
        return state != null;
    }

    private static RunMapState CloneState(RunMapState source)
    {
        source ??= new RunMapState();
        return new RunMapState
        {
            actId = source.actId,
            currentNodeId = source.currentNodeId,
            activeNodeId = source.activeNodeId,
            visitedNodeIds = new List<string>(source.visitedNodeIds),
            completedNodeIds = new List<string>(source.completedNodeIds),
            pendingGold = source.pendingGold
        };
    }

    public void AddPendingGold(int amount)
    {
        EnsureProgress();
        progress.State.pendingGold = Mathf.Max(
            0,
            progress.State.pendingGold + Mathf.Max(0, amount));
        SaveMapState();
        ProgressChanged?.Invoke();
    }

    public int ConsumePendingGold()
    {
        EnsureProgress();
        int amount = Mathf.Max(0, progress.State.pendingGold);
        progress.State.pendingGold = 0;
        SaveMapState();
        return amount;
    }

    private static ActMapData CreateMilestoneMap()
    {
        ActMapData result = ScriptableObject.CreateInstance<ActMapData>();
        result.name = "Milestone 1 Map";
        result.ConfigureRuntime("act_1", "start", new[]
        {
            new MapNodeData("start", MapNodeType.Start,
                new[] { "battle_1" }, new Vector2(0f, -360f)),
            new MapNodeData("battle_1", MapNodeType.NormalBattle,
                new[] { "shop", "treasure" }, new Vector2(0f, -210f), 0, 0),
            new MapNodeData("shop", MapNodeType.Shop,
                new[] { "battle_2" }, new Vector2(-180f, -50f)),
            new MapNodeData("treasure", MapNodeType.Treasure,
                new[] { "battle_2" }, new Vector2(180f, -50f)),
            new MapNodeData("battle_2", MapNodeType.NormalBattle,
                new[] { "boss" }, new Vector2(0f, 120f), 0, 1),
            new MapNodeData("boss", MapNodeType.Boss,
                Array.Empty<string>(), new Vector2(0f, 300f), 0, 5)
        });
        return result;
    }

    private static void LoadScene(string sceneName)
    {
        PersistentRunContext.PrepareForScene(sceneName);
        PersistentGameCanvas.PrepareForScene(sceneName);

        if (!LoadingTransitionController.LoadScene(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
