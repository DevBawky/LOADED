using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TreasureNodeController : MonoBehaviour
{
    private bool claimed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name == RunManager.EventSceneName
            && FindFirstObjectByType<TreasureNodeController>() == null)
        {
            new GameObject("Treasure Node Controller")
                .AddComponent<TreasureNodeController>();
        }
    }

    private void Awake()
    {
        if (RunManager.Instance.ActiveNode == null
            || RunManager.Instance.ActiveNode.NodeType != MapNodeType.Treasure)
        {
            RunManager.Instance.ReturnToMap();
            return;
        }

        BuildScreen();
    }

    private void ClaimTreasure()
    {
        if (claimed)
        {
            return;
        }

        claimed = true;
        bool completed = RunManager.Instance.CompleteActiveNode(new NodeResult
        {
            succeeded = true,
            goldDelta = 100
        });

        if (completed)
        {
            RunManager.Instance.ReturnToMap();
        }
        else
        {
            claimed = false;
            Debug.LogError("The treasure node could not be completed.", this);
        }
    }

    private void BuildScreen()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        Canvas canvas = new GameObject("Canvas | Treasure", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Image background = CreateImage(canvas.transform, "Background",
            new Color(0.055f, 0.045f, 0.04f, 1f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = background.rectTransform.offsetMax =
            Vector2.zero;

        CreateText(canvas.transform, font, "TREASURE", 58,
            new Vector2(0f, 220f), new Vector2(900f, 100f));
        CreateText(canvas.transform, font,
            "황야의 상자에서 $100을 발견했습니다.", 30,
            new Vector2(0f, 80f), new Vector2(900f, 80f));

        Image buttonImage = CreateImage(canvas.transform, "Button | Claim",
            new Color(0.75f, 0.18f, 0.1f, 1f));
        buttonImage.rectTransform.anchorMin = buttonImage.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        buttonImage.rectTransform.anchoredPosition = new Vector2(0f, -100f);
        buttonImage.rectTransform.sizeDelta = new Vector2(420f, 100f);
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.onClick.AddListener(ClaimTreasure);
        CreateText(buttonImage.transform, font, "TAKE $100", 30,
            Vector2.zero, buttonImage.rectTransform.sizeDelta);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private static void CreateText(
        Transform parent,
        Font font,
        string value,
        int size,
        Vector2 position,
        Vector2 dimensions)
    {
        Text text = new GameObject("Text", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.96f, 0.9f, 0.78f, 1f);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = dimensions;
    }
}
