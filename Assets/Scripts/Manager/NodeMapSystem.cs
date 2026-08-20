using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class NodeMapSettingsDefinition : ScriptableObject
{
    [SerializeField] private StageData stage;
    [Header("Normal Battles by Map Progress")]
    [Tooltip("0% 이상 33% 미만 구간의 일반 전투 목록입니다.")]
    [FormerlySerializedAs("normalBattles")]
    [SerializeField] private BattleData[] earlyNormalBattles =
        Array.Empty<BattleData>();
    [Tooltip("33% 이상 66% 미만 구간의 일반 전투 목록입니다.")]
    [SerializeField] private BattleData[] middleNormalBattles =
        Array.Empty<BattleData>();
    [Tooltip("66% 이상 100% 구간의 일반 전투 목록입니다.")]
    [SerializeField] private BattleData[] lateNormalBattles =
        Array.Empty<BattleData>();
    [Header("Special Battles")]
    [SerializeField] private BattleData[] eliteBattles = Array.Empty<BattleData>();
    [SerializeField] private BattleData bossBattle;
    [Header("Node Generation Rules")]
    [Tooltip("시작과 보스를 제외한 중간 노드 타입별 가중치 및 개수 제한입니다.")]
    [SerializeField] private NodeMapGenerationRule[] generationRules =
    {
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.NormalBattle,
            weight = 50,
            minimumCount = 4,
            maximumCount = -1
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.EliteBattle,
            weight = 10,
            minimumCount = 1,
            maximumCount = 3
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Shop,
            weight = 10,
            minimumCount = 1,
            maximumCount = 2
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Treasure,
            weight = 10,
            minimumCount = 0,
            maximumCount = 0
        },
        new NodeMapGenerationRule
        {
            nodeType = NodeMapNodeType.Event,
            weight = 20,
            minimumCount = 2,
            maximumCount = -1
        }
    };
    [Header("Node Icons")]
    [SerializeField] private Sprite startIcon;
    [SerializeField] private Sprite battleIcon;
    [SerializeField] private Sprite eliteIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite treasureIcon;
    [SerializeField] private Sprite eventIcon;
    [SerializeField] private Sprite bossIcon;

    [Header("Node Type Descriptions")]
    [Tooltip("노드 타입별 표시 이름과 설명입니다. 아이콘은 Node Type Icons 설정을 사용합니다.")]
    [SerializeField] private NodeMapNodeDescription[] nodeDescriptions =
    {
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.Start,
            displayName = "시작",
            description = "여정이 시작되는 지점입니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.NormalBattle,
            displayName = "전투",
            description = "일반 적들과 전투를 진행합니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.EliteBattle,
            displayName = "정예 전투",
            description = "강력한 정예 적들과 전투를 진행합니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.Shop,
            displayName = "상점",
            description = "아이템을 구매하고 전열을 정비합니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.Treasure,
            displayName = "보물",
            description = "보물상자를 열어 유물을 획득합니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.Event,
            displayName = "이벤트",
            description = "특별한 사건과 선택지를 만납니다."
        },
        new NodeMapNodeDescription
        {
            nodeType = NodeMapNodeType.Boss,
            displayName = "보스",
            description = "스테이지의 보스와 결전을 치릅니다."
        }
    };
    [Min(3)] [SerializeField] private int columns = 12;
    [Min(2)] [SerializeField] private int rows = 4;

    public StageData Stage => stage;
    public IReadOnlyList<BattleData> EarlyNormalBattles =>
        earlyNormalBattles ?? Array.Empty<BattleData>();
    public IReadOnlyList<BattleData> MiddleNormalBattles =>
        middleNormalBattles ?? Array.Empty<BattleData>();
    public IReadOnlyList<BattleData> LateNormalBattles =>
        lateNormalBattles ?? Array.Empty<BattleData>();
    public IReadOnlyList<BattleData> EliteBattles =>
        eliteBattles ?? Array.Empty<BattleData>();
    public BattleData BossBattle => bossBattle;
    public IReadOnlyList<NodeMapGenerationRule> GenerationRules =>
        generationRules ?? Array.Empty<NodeMapGenerationRule>();
    public IReadOnlyList<NodeMapNodeDescription> NodeDescriptions =>
        nodeDescriptions ?? Array.Empty<NodeMapNodeDescription>();
    public Sprite GetIcon(NodeMapNodeType type) => type switch
    {
        NodeMapNodeType.Start => startIcon,
        NodeMapNodeType.NormalBattle => battleIcon,
        NodeMapNodeType.EliteBattle => eliteIcon != null ? eliteIcon : battleIcon,
        NodeMapNodeType.Shop => shopIcon,
        NodeMapNodeType.Treasure => treasureIcon,
        NodeMapNodeType.Event => eventIcon,
        NodeMapNodeType.Boss => bossIcon,
        _ => null
    };
    public int Columns => Mathf.Max(3, columns);
    public int Rows => Mathf.Max(2, rows);
    public int GenerationSettingsHash
    {
        get
        {
            unchecked
            {
                const int GenerationAlgorithmRevision = 4;
                int hash = 17;
                hash = hash * 31 + GenerationAlgorithmRevision;
                hash = hash * 31 + Columns;
                hash = hash * 31 + Rows;
                hash = hash * 31 + EarlyNormalBattles.Count;
                hash = hash * 31 + MiddleNormalBattles.Count;
                hash = hash * 31 + LateNormalBattles.Count;
                hash = hash * 31 + EliteBattles.Count;

                foreach (NodeMapGenerationRule rule in GenerationRules)
                {
                    if (rule == null)
                    {
                        hash *= 31;
                        continue;
                    }

                    hash = hash * 31 + (int)rule.nodeType;
                    hash = hash * 31 + Mathf.Max(0, rule.weight);
                    hash = hash * 31 + Mathf.Max(0, rule.minimumCount);
                    hash = hash * 31 + Mathf.Max(-1, rule.maximumCount);
                }

                return hash;
            }
        }
    }

    public IReadOnlyList<BattleData> GetNormalBattles(
        NodeMapBattleProgressSection section)
    {
        IReadOnlyList<BattleData> selected = section switch
        {
            NodeMapBattleProgressSection.Middle => MiddleNormalBattles,
            NodeMapBattleProgressSection.Late => LateNormalBattles,
            _ => EarlyNormalBattles
        };

        if (selected.Count > 0)
        {
            return selected;
        }

        if (EarlyNormalBattles.Count > 0)
        {
            return EarlyNormalBattles;
        }

        if (MiddleNormalBattles.Count > 0)
        {
            return MiddleNormalBattles;
        }

        return LateNormalBattles;
    }
}

[DisallowMultipleComponent]
public class NodeMapControllerDefinition : MonoBehaviour
{
    private const string SettingsResourceName = "NodeMapSettings";

    [Header("Data")]
    [Tooltip("비워두면 Resources/NodeMapSettings를 사용합니다.")]
    [SerializeField] private NodeMapSettings mapSettings;
    [Tooltip("체크하면 Play 시작 때 저장된 맵을 무시하고 새 맵을 만듭니다. 디버그/디자인 조정용입니다.")]
    [SerializeField] private bool regenerateMapOnStart;
    [Tooltip("0이면 매번 랜덤, 0이 아니면 같은 구조를 생성합니다.")]
    [SerializeField] private int generationSeed;

    [Header("Layout")]
    [Tooltip("X는 시작/보스 양쪽에 동일한 수평 여백으로 적용되고, Y는 맵 전체의 세로 위치를 이동합니다.")]
    [SerializeField] private Vector2 generationOffset = Vector2.zero;
    [Min(0.1f)] [SerializeField] private float mapScale = 1f;
    [Header("Node Distance")]
    [InspectorName("Horizontal Node Distance")]
    [Tooltip("열과 열 사이의 가로 거리입니다.")]
    [Min(1f)] [SerializeField] private float horizontalSpacing = 230f;
    [InspectorName("Vertical Node Distance")]
    [Tooltip("같은 열에 있는 Row 사이의 세로 거리입니다.")]
    [Min(1f)] [SerializeField] private float verticalSpacing = 145f;
    [Tooltip("중간 노드에 적용되는 최대 위아래 위치 편차입니다.")]
    [Min(0f)] [SerializeField] private float nodeVerticalJitter = 28f;
    [Header("Node Sizing & Viewport")]
    [SerializeField] private Vector2 iconSize = new Vector2(58f, 58f);
    [Tooltip("일반 노드 대비 보스 아이콘 크기 배율입니다.")]
    [Min(0.1f)] [SerializeField] private float bossIconScale = 1.5f;
    [Min(100f)] [SerializeField] private float minimumContentHeight = 500f;
    [Tooltip("Viewport의 Left, Bottom, Right, Top 여백입니다.")]
    [SerializeField] private Vector4 viewportPadding = new Vector4(0f, 24f, 0f, 0f);

    [Header("Node Visuals")]
    [Tooltip("현재 위치에서 갈 수 없는 노드의 색상입니다.")]
    [SerializeField] private Color unavailableColor = new Color(0.45f, 0.45f, 0.45f, 0.72f);
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.3f, 0.65f, 0.42f, 1f);
    [Header("Active Node Hover")]
    [Min(1f)] [SerializeField] private float activeNodeHoverScale = 1.15f;
    [Min(0.01f)] [SerializeField] private float activeNodeHoverDuration = 0.12f;

    [Header("Node Type Icons (비워두면 Settings 아이콘 사용)")]
    [SerializeField] private Sprite startIcon;
    [SerializeField] private Sprite normalBattleIcon;
    [SerializeField] private Sprite eliteBattleIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite treasureIcon;
    [SerializeField] private Sprite eventIcon;
    [SerializeField] private Sprite bossIcon;

    [Header("Node Description Panel")]
    [Tooltip("비워두면 씬의 Panel | Node Description을 자동으로 찾습니다.")]
    [SerializeField] private GameObject nodeDescriptionPanel;
    [SerializeField] private Image nodeDescriptionIcon;
    [SerializeField] private TMP_Text nodeDescriptionTypeText;
    [SerializeField] private TMP_Text nodeDescriptionText;

    [Header("Dotted Path (LineRenderer)")]
    [Tooltip("비워두면 Sprites/Default 셰이더로 런타임 머티리얼을 만듭니다.")]
    [SerializeField] private Material pathMaterial;
    [SerializeField] private Color pathColor = new Color(0.72f, 0.61f, 0.45f, 0.9f);
    [SerializeField] private Color completedPathColor = new Color(0.25f, 0.8f, 0.38f, 1f);
    [Tooltip("현재 위치에서 바로 선택 가능한 경로의 색상입니다.")]
    [SerializeField] private Color availablePathColor = new Color(0.35f, 0.78f, 1f, 1f);
    [SerializeField] private Color hoveredPathColor = new Color(0.68f, 1f, 0.48f, 1f);
    [Min(0.1f)] [SerializeField] private float pathWidth = 4f;
    [Min(2f)] [SerializeField] private float pathPointSpacing = 34f;
    [Min(0f)] [SerializeField] private float pathJitter = 7f;
    [Min(0.1f)] [SerializeField] private float dashLength = 10f;
    [Min(0.1f)] [SerializeField] private float dashGap = 8f;
    [SerializeField] private int pathSortingOrder = 5;

    private NodeMapSettings settings;
    private RectTransform content;
    private ScrollRect scrollRect;
    private NodeMapRunData map;
    private Material runtimePathMaterial;
    private Texture2D runtimeDashTexture;
    private Coroutine scrollAlignmentCoroutine;
    private int hoveredNodeId = -1;
    private int describedNodeId = -1;
    private readonly List<NodeMapPathView> pathViews =
        new List<NodeMapPathView>();
    private readonly HashSet<int> availableNodeIds = new HashSet<int>();

    private sealed class NodeMapPathView
    {
        public int sourceId;
        public int targetId;
        public LineRenderer line;
        public Vector3[] points;
    }

    private readonly Dictionary<int, Vector2> positions = new Dictionary<int, Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "NodeMap"
            || FindFirstObjectByType<NodeMapController>() != null)
        {
            return;
        }

        GameObject host = GameObject.Find("Canvas | NodeMap")
            ?? new GameObject("Node Map Controller");
        host.AddComponent<NodeMapController>();
    }

    protected void Initialize()
    {
        settings = mapSettings != null
            ? mapSettings
            : Resources.Load<NodeMapSettings>(SettingsResourceName);
        scrollRect = FindFirstObjectByType<ScrollRect>();
        content = scrollRect == null ? null : scrollRect.content;
        ResolveNodeDescriptionPanel();
        HideNodeDescription();

        if (settings == null || settings.Stage == null || content == null)
        {
            Debug.LogError("NodeMap requires Resources/NodeMapSettings and Scroll View | Map/Viewport/Content.", this);
            return;
        }

        EnsureEventSystem();
        ConfigureViewportAndCanvas();
        bool loadedSavedMap = NodeMapSaveSystem.TryLoad(out map);
        int settingsHash = settings.GenerationSettingsHash;
        bool settingsChangedBeforeFirstMove = loadedSavedMap
            && map.generationSettingsHash != settingsHash
            && CanRegenerateUnstartedMap(map);

        if (regenerateMapOnStart || !loadedSavedMap
            || settingsChangedBeforeFirstMove)
        {
            int earlyBattleCount = Mathf.Max(
                1,
                settings.EarlyNormalBattles.Count);
            int seed = generationSeed != 0
                ? generationSeed
                : unchecked((int)DateTime.UtcNow.Ticks);
            map = NodeMapGenerator.Generate(
                seed,
                0,
                settings.Columns,
                settings.Rows,
                earlyBattleCount,
                settings.GenerationRules,
                Mathf.Max(1, settings.MiddleNormalBattles.Count),
                Mathf.Max(1, settings.LateNormalBattles.Count),
                Mathf.Max(1, settings.EliteBattles.Count));
            map.generationSettingsHash = settingsHash;
            NodeMapSaveSystem.Save(map);
        }

        BuildMap();
    }

    private static bool CanRegenerateUnstartedMap(NodeMapRunData runData)
    {
        return runData != null
            && runData.activeNodeId < 0
            && runData.awaitingNodeSelection
            && runData.nodes != null
            && runData.completedNodeIds != null
            && runData.completedNodeIds.Count == 1
            && runData.nodes.Any(node => node != null
                && node.id == runData.currentNodeId
                && node.type == NodeMapNodeType.Start);
    }

    private void BuildMap()
    {
        hoveredNodeId = -1;
        HideNodeDescription();
        pathViews.Clear();
        availableNodeIds.Clear();
        foreach (Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        positions.Clear();
        float scale = Mathf.Max(0.1f, mapScale);
        float horizontalPadding = Mathf.Max(0f, generationOffset.x);
        int maxColumn = map.nodes.Max(node => node.column);
        int layoutRows = Mathf.Max(
            settings.Rows,
            map.nodes.Max(node => node.row) + 1);
        float startHalfWidth = iconSize.x * scale * 0.5f;
        float bossHalfWidth = startHalfWidth
            * Mathf.Max(0.1f, bossIconScale);
        content.anchorMin = new Vector2(0f, 0.5f);
        content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;
        content.sizeDelta = new Vector2(
            Mathf.Max(
                scrollRect.viewport.rect.width,
                horizontalPadding * 2f
                    + startHalfWidth + bossHalfWidth
                    + maxColumn * horizontalSpacing * scale),
            Mathf.Max(
                minimumContentHeight,
                iconSize.y * scale
                    + (layoutRows - 1) * verticalSpacing * scale
                    + Mathf.Abs(generationOffset.y) * 2f));

        foreach (NodeMapNodeData node in map.nodes)
        {
            float verticalJitter = 0f;
            if (node.type != NodeMapNodeType.Start
                && node.type != NodeMapNodeType.Boss
                && nodeVerticalJitter > 0f)
            {
                System.Random nodeRandom = new System.Random(
                    map.seed ^ node.id * 83492791);
                float maximumJitter = Mathf.Min(
                    nodeVerticalJitter,
                    verticalSpacing * 0.35f) * scale;
                verticalJitter = (float)(nodeRandom.NextDouble() * 2d - 1d)
                    * maximumJitter;
            }
            float y = generationOffset.y
                + (node.row - (layoutRows - 1) * 0.5f)
                    * verticalSpacing * scale
                + verticalJitter;
            Vector2 position = new Vector2(
                horizontalPadding + startHalfWidth
                    + node.column * horizontalSpacing * scale,
                y);
            positions[node.id] = position;
        }

        foreach (NodeMapNodeData node in map.nodes)
        {
            foreach (int targetId in node.nextNodeIds)
            {
                if (positions.TryGetValue(targetId, out Vector2 target))
                {
                    CreateConnection(node.id, targetId, positions[node.id], target);
                }
            }
        }

        HashSet<int> available = GetAvailableNodeIds();
        availableNodeIds.UnionWith(available);
        foreach (NodeMapNodeData node in map.nodes)
        {
            CreateNodeView(node, available.Contains(node.id));
        }

        RefreshPathColors();

        if (scrollAlignmentCoroutine != null)
        {
            StopCoroutine(scrollAlignmentCoroutine);
        }
        scrollAlignmentCoroutine = StartCoroutine(AlignScrollAfterLayout());
    }

    private HashSet<int> GetAvailableNodeIds()
    {
        NodeMapNodeData current = map.nodes.FirstOrDefault(
            node => node.id == map.currentNodeId);
        return map.awaitingNodeSelection && current != null
            ? new HashSet<int>(current.nextNodeIds)
            : new HashSet<int>();
    }

    private IEnumerator AlignScrollAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            scrollRect.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;

        NodeMapNodeData current = map.nodes.FirstOrDefault(
            node => node.id == map.currentNodeId);
        if (current == null || current.column == 0)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            content.anchoredPosition = new Vector2(
                0f, content.anchoredPosition.y);
        }
        else
        {
            float currentX = positions.TryGetValue(
                current.id, out Vector2 position) ? position.x : 0f;
            float scrollableWidth = Mathf.Max(
                1f, content.rect.width - scrollRect.viewport.rect.width);
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                (currentX - scrollRect.viewport.rect.width * 0.35f)
                / scrollableWidth);
        }

        yield return null;
        if (current == null || current.column == 0)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            content.anchoredPosition = new Vector2(
                0f, content.anchoredPosition.y);
        }
        scrollAlignmentCoroutine = null;
    }

    private void ConfigureViewportAndCanvas()
    {
        RectTransform viewport = scrollRect.viewport;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = new Vector2(
            viewportPadding.x, viewportPadding.y);
        viewport.offsetMax = new Vector2(
            -viewportPadding.z, -viewportPadding.w);
        viewport.localScale = Vector3.one;
        viewport.localRotation = Quaternion.identity;

        // The legacy stencil Mask can reject dynamically created graphics
        // after the Canvas switches to Screen Space Camera. RectMask2D clips
        // the icons in UI space without making them transparent.
        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
        {
            legacyMask.enabled = false;
        }
        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        Canvas rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        // A LineRenderer cannot be composited with Screen Space Overlay UI.
        // Screen Space Camera lets the paths sit between the map background
        // and the nested canvases used by the node buttons.
        if (rootCanvas != null)
        {
            rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            rootCanvas.worldCamera = Camera.main;
            rootCanvas.planeDistance = 10f;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 0;
        }

        Canvas.ForceUpdateCanvases();
    }

    private Material GetPathMaterial()
    {
        if (runtimePathMaterial != null)
        {
            return runtimePathMaterial;
        }

        Shader shader = pathMaterial != null
            ? pathMaterial.shader
            : Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Unlit/Transparent");
        runtimePathMaterial = pathMaterial != null
            ? new Material(pathMaterial)
            : new Material(shader);
        runtimePathMaterial.name = "Node Map Dotted Path (Runtime)";

        const int textureWidth = 64;
        const int textureHeight = 4;
        runtimeDashTexture = new Texture2D(
            textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Node Map Dash Pattern (Runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        float cycle = Mathf.Max(0.2f, dashLength + dashGap);
        int dashPixels = Mathf.Clamp(
            Mathf.RoundToInt(textureWidth * dashLength / cycle),
            1,
            textureWidth - 1);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                pixels[y * textureWidth + x] = x < dashPixels
                    ? Color.white
                    : Color.clear;
            }
        }
        runtimeDashTexture.SetPixels(pixels);
        runtimeDashTexture.Apply(false, true);
        runtimePathMaterial.mainTexture = runtimeDashTexture;
        runtimePathMaterial.mainTextureScale = Vector2.one;
        return runtimePathMaterial;
    }

    private Sprite GetNodeIcon(NodeMapNodeType type)
    {
        Sprite assigned = type switch
        {
            NodeMapNodeType.Start => startIcon,
            NodeMapNodeType.NormalBattle => normalBattleIcon,
            NodeMapNodeType.EliteBattle => eliteBattleIcon,
            NodeMapNodeType.Shop => shopIcon,
            NodeMapNodeType.Treasure => treasureIcon,
            NodeMapNodeType.Event => eventIcon,
            NodeMapNodeType.Boss => bossIcon,
            _ => null
        };
        return assigned != null ? assigned : settings.GetIcon(type);
    }

    private void CreateConnection(int sourceId, int targetId, Vector2 from, Vector2 to)
    {
        GameObject lineObject = new GameObject(
            $"Path {sourceId} -> {targetId}", typeof(LineRenderer));
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(content, false);
        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Tile;
        float scale = Mathf.Max(0.1f, mapScale);
        line.widthMultiplier = pathWidth * scale * Mathf.Max(
            0.0001f, Mathf.Abs(content.lossyScale.x));
        line.startColor = pathColor;
        line.endColor = pathColor;
        line.numCapVertices = 1;
        line.numCornerVertices = 2;
        line.sortingOrder = pathSortingOrder;
        line.sharedMaterial = GetPathMaterial();

        Vector2 fullDelta = to - from;
        if (fullDelta.sqrMagnitude > 0.001f)
        {
            Vector2 direction = fullDelta.normalized;
            float baseInset = Mathf.Min(iconSize.x, iconSize.y)
                * scale * 0.42f;
            from += direction * baseInset * GetNodeVisualScale(sourceId);
            to -= direction * baseInset * GetNodeVisualScale(targetId);
        }
        Vector2 delta = to - from;
        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(
            delta.magnitude / Mathf.Max(2f, pathPointSpacing * scale)));
        line.positionCount = segmentCount + 1;
        Vector3[] points = new Vector3[segmentCount + 1];
        Vector2 perpendicular = delta.sqrMagnitude > 0.001f
            ? new Vector2(-delta.y, delta.x).normalized
            : Vector2.up;
        System.Random random = new System.Random(
            map.seed ^ sourceId * 73856093 ^ targetId * 19349663);

        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            Vector2 point = Vector2.Lerp(from, to, t);
            if (index > 0 && index < segmentCount)
            {
                float envelope = Mathf.Sin(t * Mathf.PI);
                point += perpendicular * (float)(random.NextDouble() * 2d - 1d)
                    * pathJitter * scale * envelope;
            }
            points[index] = new Vector3(point.x, point.y, 0f);
            line.SetPosition(index, points[index]);
        }
        line.textureScale = new Vector2(
            Mathf.Max(1f, delta.magnitude / Mathf.Max(
                0.2f, (dashLength + dashGap) * scale)),
            1f);
        pathViews.Add(new NodeMapPathView
        {
            sourceId = sourceId,
            targetId = targetId,
            line = line,
            points = points
        });
    }

    private void SetHoveredNode(int nodeId, bool hovered)
    {
        int nextHoveredNodeId = hovered
            && availableNodeIds.Contains(nodeId)
                ? nodeId
                : -1;
        if (hoveredNodeId == nextHoveredNodeId)
        {
            return;
        }

        hoveredNodeId = nextHoveredNodeId;
        RefreshPathColors();
    }

    private void RefreshPathColors()
    {
        if (map == null)
        {
            return;
        }

        HashSet<int> completed = new HashSet<int>(map.completedNodeIds);
        HashSet<int> hoveredFutureSources = GetHoveredFutureSources();
        foreach (NodeMapPathView path in pathViews)
        {
            if (path.line == null)
            {
                continue;
            }

            bool hoveredRoute = hoveredFutureSources.Contains(path.sourceId);
            bool completedRoute = completed.Contains(path.sourceId)
                && completed.Contains(path.targetId);
            bool availableRoute = path.sourceId == map.currentNodeId
                && availableNodeIds.Contains(path.targetId);
            Color color = hoveredRoute
                ? hoveredPathColor
                : completedRoute
                    ? completedPathColor
                    : availableRoute ? availablePathColor : pathColor;
            path.line.startColor = color;
            path.line.endColor = color;
        }
    }

    private HashSet<int> GetHoveredFutureSources()
    {
        HashSet<int> sources = new HashSet<int>();
        if (hoveredNodeId < 0 || !availableNodeIds.Contains(hoveredNodeId))
        {
            return sources;
        }

        Dictionary<int, NodeMapNodeData> nodesById = map.nodes.ToDictionary(
            node => node.id);
        Stack<int> pending = new Stack<int>();
        pending.Push(hoveredNodeId);
        while (pending.Count > 0)
        {
            int nodeId = pending.Pop();
            if (!sources.Add(nodeId)
                || !nodesById.TryGetValue(nodeId, out NodeMapNodeData node))
            {
                continue;
            }

            foreach (int nextNodeId in node.nextNodeIds)
            {
                pending.Push(nextNodeId);
            }
        }
        return sources;
    }

    private float GetNodeVisualScale(int nodeId)
    {
        NodeMapNodeData node = map.nodes.FirstOrDefault(
            candidate => candidate.id == nodeId);
        return node != null && node.type == NodeMapNodeType.Boss
            ? Mathf.Max(0.1f, bossIconScale)
            : 1f;
    }

    protected void ClipPathsToViewport()
    {
        if (content == null || scrollRect == null
            || scrollRect.viewport == null || pathViews.Count == 0)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        scrollRect.viewport.GetWorldCorners(corners);
        Vector3 bottomLeft = content.InverseTransformPoint(corners[0]);
        Vector3 topRight = content.InverseTransformPoint(corners[2]);
        Rect clipRect = Rect.MinMaxRect(
            Mathf.Min(bottomLeft.x, topRight.x),
            Mathf.Min(bottomLeft.y, topRight.y),
            Mathf.Max(bottomLeft.x, topRight.x),
            Mathf.Max(bottomLeft.y, topRight.y));
        float strokeInset = pathWidth * Mathf.Max(0.1f, mapScale) * 0.5f;
        clipRect.xMin += strokeInset;
        clipRect.xMax -= strokeInset;
        clipRect.yMin += strokeInset;
        clipRect.yMax -= strokeInset;

        foreach (NodeMapPathView path in pathViews)
        {
            if (path.line == null || path.points == null
                || path.points.Length < 2)
            {
                continue;
            }

            List<Vector3> visiblePoints = new List<Vector3>();
            for (int index = 0; index < path.points.Length - 1; index++)
            {
                Vector3 start = path.points[index];
                Vector3 end = path.points[index + 1];
                if (!ClipSegmentToRect(ref start, ref end, clipRect))
                {
                    continue;
                }

                if (visiblePoints.Count == 0
                    || (visiblePoints[visiblePoints.Count - 1] - start)
                        .sqrMagnitude > 0.001f)
                {
                    visiblePoints.Add(start);
                }
                visiblePoints.Add(end);
            }

            path.line.enabled = visiblePoints.Count >= 2;
            if (visiblePoints.Count >= 2)
            {
                path.line.positionCount = visiblePoints.Count;
                path.line.SetPositions(visiblePoints.ToArray());
            }
        }
    }

    private static bool ClipSegmentToRect(
        ref Vector3 start,
        ref Vector3 end,
        Rect rect)
    {
        float dx = end.x - start.x;
        float dy = end.y - start.y;
        float enter = 0f;
        float exit = 1f;

        if (!ClipBoundary(-dx, start.x - rect.xMin, ref enter, ref exit)
            || !ClipBoundary(dx, rect.xMax - start.x, ref enter, ref exit)
            || !ClipBoundary(-dy, start.y - rect.yMin, ref enter, ref exit)
            || !ClipBoundary(dy, rect.yMax - start.y, ref enter, ref exit))
        {
            return false;
        }

        Vector3 originalStart = start;
        Vector3 delta = end - originalStart;
        start = originalStart + delta * enter;
        end = originalStart + delta * exit;
        return true;
    }

    private static bool ClipBoundary(
        float direction,
        float distance,
        ref float enter,
        ref float exit)
    {
        if (Mathf.Abs(direction) < 0.00001f)
        {
            return distance >= 0f;
        }

        float ratio = distance / direction;
        if (direction < 0f)
        {
            if (ratio > exit)
            {
                return false;
            }
            enter = Mathf.Max(enter, ratio);
        }
        else
        {
            if (ratio < enter)
            {
                return false;
            }
            exit = Mathf.Min(exit, ratio);
        }
        return true;
    }

    private void CreateNodeView(NodeMapNodeData node, bool available)
    {
        GameObject nodeObject = new GameObject(
            $"Node {node.id} | {node.type}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(NodeMapNodeHover));
        nodeObject.layer = 5;
        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        rect.SetParent(content, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        float visualScale = node.type == NodeMapNodeType.Boss
            ? Mathf.Max(0.1f, bossIconScale)
            : 1f;
        rect.sizeDelta = iconSize * Mathf.Max(0.1f, mapScale)
            * visualScale;
        rect.anchoredPosition = positions[node.id];
        rect.localScale = Vector3.one;

        bool completed = map.completedNodeIds.Contains(node.id);
        Image image = nodeObject.GetComponent<Image>();
        image.sprite = GetNodeIcon(node.type);
        image.preserveAspect = true;
        image.color = completed
            ? completedColor
            : available ? availableColor : unavailableColor;
        Button button = nodeObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = true;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectNode(node.id));
        nodeObject.GetComponent<NodeMapNodeHover>().Configure(
            rect,
            available,
            activeNodeHoverScale,
            activeNodeHoverDuration,
            hovered => HandleNodeHover(node, hovered));
    }

    private void HandleNodeHover(NodeMapNodeData node, bool hovered)
    {
        if (node == null)
        {
            return;
        }

        SetHoveredNode(node.id, hovered);
        if (hovered)
        {
            ShowNodeDescription(node);
        }
        else if (describedNodeId == node.id)
        {
            HideNodeDescription();
        }
    }

    private void ShowNodeDescription(NodeMapNodeData node)
    {
        ResolveNodeDescriptionPanel();
        if (nodeDescriptionPanel == null)
        {
            return;
        }

        NodeMapNodeDescription description = settings.NodeDescriptions
            .FirstOrDefault(entry => entry != null
                && entry.nodeType == node.type);
        if (nodeDescriptionIcon != null)
        {
            nodeDescriptionIcon.sprite = GetNodeIcon(node.type);
            nodeDescriptionIcon.preserveAspect = true;
            nodeDescriptionIcon.enabled = nodeDescriptionIcon.sprite != null;
        }
        if (nodeDescriptionTypeText != null)
        {
            nodeDescriptionTypeText.text = description == null
                || string.IsNullOrWhiteSpace(description.displayName)
                    ? GetNodeLabel(node.type)
                    : description.displayName;
        }
        if (nodeDescriptionText != null)
        {
            nodeDescriptionText.text = description?.description
                ?? string.Empty;
        }

        describedNodeId = node.id;
        nodeDescriptionPanel.SetActive(true);
    }

    private void HideNodeDescription()
    {
        describedNodeId = -1;
        if (nodeDescriptionPanel != null)
        {
            nodeDescriptionPanel.SetActive(false);
        }
    }

    private void ResolveNodeDescriptionPanel()
    {
        if (nodeDescriptionPanel == null)
        {
            RectTransform panel = FindSceneComponent<RectTransform>(
                "Panel | Node Description");
            nodeDescriptionPanel = panel == null
                ? null
                : panel.gameObject;
        }

        if (nodeDescriptionPanel == null)
        {
            return;
        }

        nodeDescriptionIcon ??= FindChildComponent<Image>(
            nodeDescriptionPanel.transform,
            "Image | Node Icon");
        nodeDescriptionTypeText ??= FindChildComponent<TMP_Text>(
            nodeDescriptionPanel.transform,
            "Text | Node Type");
        nodeDescriptionText ??= FindChildComponent<TMP_Text>(
            nodeDescriptionPanel.transform,
            "Text | Node Description");
    }

    private static T FindSceneComponent<T>(string objectName)
        where T : Component
    {
        foreach (T candidate in FindObjectsByType<T>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static T FindChildComponent<T>(Transform root, string objectName)
        where T : Component
    {
        foreach (T candidate in root.GetComponentsInChildren<T>(true))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SelectNode(int nodeId)
    {
        if (!map.awaitingNodeSelection)
        {
            return;
        }

        NodeMapNodeData current = map.nodes.FirstOrDefault(node => node.id == map.currentNodeId);
        NodeMapNodeData selected = map.nodes.FirstOrDefault(node => node.id == nodeId);
        if (current == null || selected == null || !current.nextNodeIds.Contains(nodeId))
        {
            return;
        }

        map.activeNodeId = selected.id;
        map.awaitingNodeSelection = false;
        NodeMapSaveSystem.Save(map);
        string sceneName = GetSceneName(selected.type);

        if (selected.type == NodeMapNodeType.NormalBattle
            || selected.type == NodeMapNodeType.EliteBattle
            || selected.type == NodeMapNodeType.Boss)
        {
            int battleIndex = ResolveStageBattleIndex(selected);
            map.selectedBattleIndex = battleIndex;
            NodeMapSaveSystem.Save(map);
            if (RunSaveSystem.PrepareForSelectedBattle(
                    map.stageIndex, battleIndex))
            {
                RunSaveSystem.RequestStart(RunStartMode.Continue);
            }
        }

        if (!LoadingTransitionController.LoadScene(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private int ResolveStageBattleIndex(NodeMapNodeData node)
    {
        int maximumColumn = map.nodes.Max(candidate => candidate.column);
        IReadOnlyList<BattleData> normalBattles = settings.GetNormalBattles(
            NodeMapGenerator.GetNormalBattleProgressSection(
                node.column,
                maximumColumn));
        BattleData selected = node.type switch
        {
            NodeMapNodeType.Boss => settings.BossBattle,
            NodeMapNodeType.EliteBattle when settings.EliteBattles.Count > 0 =>
                settings.EliteBattles[Mathf.Abs(node.battleIndex) % settings.EliteBattles.Count],
            NodeMapNodeType.NormalBattle when normalBattles.Count > 0 =>
                normalBattles[Mathf.Abs(node.battleIndex) % normalBattles.Count],
            _ => null
        };

        for (int index = 0; index < settings.Stage.Battles.Count; index++)
        {
            if (settings.Stage.Battles[index] == selected)
            {
                return index;
            }
        }

        Debug.LogWarning($"Battle '{selected?.name}' is not in stage '{settings.Stage.name}'. Using the first battle.", this);
        return 0;
    }

    private static string GetSceneName(NodeMapNodeType type)
    {
        return type switch
        {
            NodeMapNodeType.Shop => "Shop",
            NodeMapNodeType.Treasure => "Treasure",
            NodeMapNodeType.Event => "Event",
            _ => "Battle"
        };
    }

    private static string GetNodeLabel(NodeMapNodeType type)
    {
        return type switch
        {
            NodeMapNodeType.Start => "START",
            NodeMapNodeType.NormalBattle => "BATTLE",
            NodeMapNodeType.EliteBattle => "ELITE",
            NodeMapNodeType.Shop => "SHOP",
            NodeMapNodeType.Treasure => "TREASURE",
            NodeMapNodeType.Event => "EVENT",
            NodeMapNodeType.Boss => "BOSS",
            _ => "?"
        };
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject(
            "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    protected void ReleaseRuntimeResources()
    {
        if (runtimePathMaterial != null)
        {
            Destroy(runtimePathMaterial);
            runtimePathMaterial = null;
        }
        if (runtimeDashTexture != null)
        {
            Destroy(runtimeDashTexture);
            runtimeDashTexture = null;
        }
    }
}
