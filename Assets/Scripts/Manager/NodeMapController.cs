using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class NodeMapController : MonoBehaviour
{
    private readonly Dictionary<string, Button> nodeButtons =
        new Dictionary<string, Button>();
    private RunManager runManager;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            == RunManager.NodeMapSceneName
            && FindFirstObjectByType<NodeMapController>() == null)
        {
            new GameObject("Node Map Controller")
                .AddComponent<NodeMapController>();
        }
    }

    private void Awake()
    {
        runManager = RunManager.Instance;

        if (runManager.ActiveNode != null && runManager.ResumeActiveNode())
        {
            enabled = false;
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildMap();
    }

    private void OnEnable()
    {
        if (runManager != null)
        {
            runManager.ProgressChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (runManager != null)
        {
            runManager.ProgressChanged -= Refresh;
        }
    }

    private void BuildMap()
    {
        Canvas canvas = new GameObject(
            "Canvas | Node Map",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image background = CreateImage(canvas.transform, "Background",
            new Color(0.055f, 0.045f, 0.04f, 1f));
        Stretch(background.rectTransform);
        CreateLabel(canvas.transform, "DUST'N DAWN — ROUTE", 42,
            new Vector2(0f, 465f), new Vector2(900f, 70f));

        RectTransform mapRoot = new GameObject(
            "Map",
            typeof(RectTransform)).GetComponent<RectTransform>();
        mapRoot.SetParent(canvas.transform, false);
        mapRoot.anchorMin = mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        mapRoot.sizeDelta = new Vector2(900f, 850f);

        foreach (MapNodeData node in runManager.Map.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            foreach (string nextNodeId in node.NextNodeIds)
            {
                if (runManager.Map.TryGetNode(nextNodeId, out MapNodeData next))
                {
                    CreateConnector(mapRoot, node.MapPosition, next.MapPosition);
                }
            }
        }

        foreach (MapNodeData node in runManager.Map.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            Button button = CreateNodeButton(mapRoot, node);
            nodeButtons[node.NodeId] = button;
        }

        Refresh();
    }

    private Button CreateNodeButton(Transform parent, MapNodeData node)
    {
        Image image = CreateImage(parent, $"Node | {node.NodeId}", Color.white);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        image.rectTransform.sizeDelta = new Vector2(190f, 88f);
        image.rectTransform.anchoredPosition = node.MapPosition;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        string nodeId = node.NodeId;
        button.onClick.AddListener(() => runManager.TryEnterNode(nodeId));
        CreateLabel(image.transform, GetNodeLabel(node.NodeType), 25,
            Vector2.zero, image.rectTransform.sizeDelta);
        return button;
    }

    private void Refresh()
    {
        if (runManager == null || runManager.State == null)
        {
            return;
        }

        foreach (MapNodeData node in runManager.Map.Nodes)
        {
            if (node == null || !nodeButtons.TryGetValue(
                    node.NodeId,
                    out Button button))
            {
                continue;
            }

            bool completed = runManager.State.completedNodeIds.Contains(node.NodeId);
            bool visited = runManager.State.visitedNodeIds.Contains(node.NodeId);
            bool current = runManager.State.currentNodeId == node.NodeId;
            button.interactable = runManager.CanEnter(node.NodeId);
            button.image.color = current
                ? new Color(0.95f, 0.66f, 0.16f, 1f)
                : completed
                ? new Color(0.34f, 0.26f, 0.18f, 1f)
                : visited
                    ? new Color(0.86f, 0.55f, 0.2f, 1f)
                    : button.interactable
                        ? new Color(0.82f, 0.18f, 0.11f, 1f)
                        : new Color(0.24f, 0.22f, 0.2f, 1f);
        }
    }

    private Text CreateLabel(
        Transform parent,
        string value,
        int size,
        Vector2 position,
        Vector2 dimensions)
    {
        Text label = new GameObject("Text", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.96f, 0.9f, 0.78f, 1f);
        label.text = value;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = dimensions;
        return label;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private static void CreateConnector(
        Transform parent,
        Vector2 from,
        Vector2 to)
    {
        Image connector = CreateImage(parent, "Connector",
            new Color(0.42f, 0.33f, 0.24f, 1f));
        Vector2 delta = to - from;
        connector.rectTransform.anchorMin = connector.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        connector.rectTransform.anchoredPosition = (from + to) * 0.5f;
        connector.rectTransform.sizeDelta = new Vector2(delta.magnitude, 5f);
        connector.rectTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        connector.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static string GetNodeLabel(MapNodeType type) => type switch
    {
        MapNodeType.NormalBattle => "BATTLE",
        MapNodeType.EliteBattle => "ELITE",
        MapNodeType.Shop => "SHOP",
        MapNodeType.Treasure => "TREASURE",
        MapNodeType.Event => "EVENT",
        MapNodeType.Boss => "BOSS",
        MapNodeType.Start => "START",
        _ => type.ToString().ToUpperInvariant()
    };

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
        }
    }
}
