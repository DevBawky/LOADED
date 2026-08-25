using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RelicTooltipUI : MonoBehaviour
{
    private static readonly string[] NameTextHints =
    {
        "Text | Relic Name",
        "Text | Bullet Name",
        "Text | Name"
    };

    private static readonly string[] DescriptionTextHints =
    {
        "Text | Relic Description",
        "Text | Bullet Description",
        "Text | Description"
    };

    private static readonly string[] GuideTextHints =
    {
        "Text | Relic Guide",
        "Text | Remove",
        "Text | Guide"
    };

    private static readonly string[] RemovalProgressImageHints =
    {
        "Image | Gauge Value",
        "Image | Removal Hold Progress"
    };

    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text guideText;
    [SerializeField] private Image removalProgressImage;
    [SerializeField] private Vector2 pointerOffset = new Vector2(18f, -18f);
    [SerializeField] private float screenPadding = 10f;

    private RelicData displayedRelic;
    private Canvas positioningCanvas;

    public static RelicTooltipUI GetOrCreate(
        Component context,
        TMP_Text fontSource = null)
    {
        if (context == null)
        {
            return null;
        }

        Canvas canvas = context.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        Canvas rootCanvas = canvas.rootCanvas;
        RelicTooltipUI presenter = rootCanvas.GetComponentInChildren<
            RelicTooltipUI>(true);
        if (presenter != null)
        {
            presenter.positioningCanvas = rootCanvas;
            presenter.Initialize(fontSource);
            return presenter;
        }

        RectTransform tooltipPanel = null;
        foreach (RectTransform candidate in rootCanvas.GetComponentsInChildren<
                     RectTransform>(true))
        {
            if (candidate.name == "Panel | Relic Tooltip")
            {
                tooltipPanel = candidate;
                break;
            }
        }

        if (tooltipPanel == null)
        {
            tooltipPanel = CreatePanel(rootCanvas.transform);
        }

        presenter = tooltipPanel.GetComponent<RelicTooltipUI>();
        if (presenter == null)
        {
            presenter = tooltipPanel.gameObject.AddComponent<RelicTooltipUI>();
        }
        presenter.panel = tooltipPanel;
        presenter.positioningCanvas = rootCanvas;
        presenter.Initialize(fontSource);
        presenter.Hide();
        return presenter;
    }

    public void Show(
        RelicData relic,
        Vector2 pointerScreenPosition,
        string guide = null)
    {
        if (relic == null)
        {
            Hide();
            return;
        }

        Initialize(null);
        if (panel == null || nameText == null || descriptionText == null)
        {
            return;
        }

        displayedRelic = relic;
        nameText.text = relic.DisplayName;
        string effect = relic.BuildEffectSummary();
        descriptionText.text = TooltipTextFormatter.Format(
            string.IsNullOrWhiteSpace(effect) ? relic.Description : effect);
        if (guideText != null)
        {
            bool showGuide = !string.IsNullOrWhiteSpace(guide);
            guideText.text = showGuide ? guide : string.Empty;
            guideText.gameObject.SetActive(showGuide);
            SetRemovalProgressVisible(showGuide);
        }
        SetRemovalProgress(relic, 0f);
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Position(pointerScreenPosition);
    }

    public void Move(RelicData relic, Vector2 pointerScreenPosition)
    {
        if (ReferenceEquals(displayedRelic, relic)
            && panel != null && panel.gameObject.activeSelf)
        {
            Position(pointerScreenPosition);
        }
    }

    public void Hide(RelicData relic = null)
    {
        if (relic != null && !ReferenceEquals(displayedRelic, relic))
        {
            return;
        }

        SetRemovalProgress(displayedRelic, 0f);
        SetRemovalProgressVisible(false);
        displayedRelic = null;
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    internal void SetRemovalProgress(RelicData relic, float progress)
    {
        if (relic != null && !ReferenceEquals(displayedRelic, relic))
        {
            return;
        }

        Initialize(null);
        if (removalProgressImage != null)
        {
            removalProgressImage.fillAmount = Mathf.Clamp01(progress);
        }
    }

    private void Initialize(TMP_Text fontSource)
    {
        if (panel == null)
        {
            panel = transform as RectTransform;
        }
        if (panel == null)
        {
            return;
        }

        if (nameText == null)
        {
            nameText = FindText(panel, NameTextHints, "Name");
        }
        if (descriptionText == null)
        {
            descriptionText = FindText(
                panel,
                DescriptionTextHints,
                "Description");
        }
        if (guideText == null)
        {
            guideText = FindText(panel, GuideTextHints, "Guide");
        }
        if (removalProgressImage == null)
        {
            removalProgressImage = FindImage(
                panel,
                RemovalProgressImageHints);
        }

        if (nameText == null)
        {
            nameText = CreateText(
                "Text | Relic Name",
                panel,
                fontSource,
                new Vector2(0.06f, 0.68f),
                new Vector2(0.94f, 0.94f),
                22f,
                FontStyles.Bold);
        }
        if (descriptionText == null)
        {
            descriptionText = CreateText(
                "Text | Relic Description",
                panel,
                fontSource,
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.64f),
                16f,
                FontStyles.Normal);
        }
        if (guideText == null)
        {
            guideText = CreateText(
                "Text | Relic Guide",
                panel,
                fontSource,
                new Vector2(0.55f, 0.8f),
                new Vector2(0.94f, 0.95f),
                14f,
                FontStyles.Bold);
            guideText.alignment = TextAlignmentOptions.MidlineRight;
            guideText.color = new Color32(247, 191, 62, 255);
            guideText.gameObject.SetActive(false);
        }

        if (removalProgressImage == null)
        {
            removalProgressImage = CreateRemovalProgressImage(panel);
        }

        removalProgressImage.type = Image.Type.Filled;
        removalProgressImage.fillMethod = Image.FillMethod.Horizontal;
        removalProgressImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        removalProgressImage.raycastTarget = false;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.045f, 0.035f, 0.96f);
        }
        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private void SetRemovalProgressVisible(bool visible)
    {
        if (removalProgressImage == null)
        {
            return;
        }

        Transform progressRoot = removalProgressImage.transform.parent;
        if (progressRoot != null
            && progressRoot.name == "Image | Remove Gauge")
        {
            progressRoot.gameObject.SetActive(visible);
        }
        else
        {
            removalProgressImage.gameObject.SetActive(visible);
        }
    }

    private void Position(Vector2 pointerScreenPosition)
    {
        if (panel == null)
        {
            return;
        }

        Canvas canvas = positioningCanvas != null
            ? positioningCanvas
            : panel.GetComponentInParent<Canvas>()?.rootCanvas;
        RectTransform containerRect = panel.parent as RectTransform;
        if (containerRect == null)
        {
            containerRect = canvas == null
                ? null
                : canvas.transform as RectTransform;
        }
        if (canvas == null || containerRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            pointerScreenPosition,
            camera,
            out Vector2 pointerLocalPosition);

        Rect bounds = containerRect.rect;
        float width = Mathf.Max(panel.rect.width, 320f);
        float height = Mathf.Max(panel.rect.height, 140f);
        bool placeLeft = pointerLocalPosition.x + pointerOffset.x + width
            > bounds.xMax - screenPadding;
        bool placeAbove = pointerLocalPosition.y + pointerOffset.y - height
            < bounds.yMin + screenPadding;

        // ScreenPointToLocalPointInRectangle returns coordinates relative to
        // the parent's pivot. Match the tooltip anchors to that same origin
        // so anchoredPosition stays immediately beside the cursor.
        panel.anchorMin = containerRect.pivot;
        panel.anchorMax = containerRect.pivot;
        panel.pivot = new Vector2(placeLeft ? 1f : 0f, placeAbove ? 0f : 1f);
        panel.sizeDelta = new Vector2(width, height);

        float x = pointerLocalPosition.x
            + (placeLeft ? -pointerOffset.x : pointerOffset.x);
        float y = pointerLocalPosition.y
            + (placeAbove ? -pointerOffset.y : pointerOffset.y);
        x = Mathf.Clamp(x, bounds.xMin + screenPadding, bounds.xMax - screenPadding);
        y = Mathf.Clamp(y, bounds.yMin + screenPadding, bounds.yMax - screenPadding);
        panel.anchoredPosition = new Vector2(x, y);
    }

    private static TMP_Text FindText(
        Transform root,
        string[] exactNames,
        string fallbackNamePart)
    {
        TMP_Text fallback = null;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            foreach (string exactName in exactNames)
            {
                if (text.name == exactName)
                {
                    return text;
                }
            }

            if (fallback == null && text.name.IndexOf(
                    fallbackNamePart,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fallback = text;
            }
        }
        return fallback;
    }

    private static Image FindImage(Transform root, string[] exactNames)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            foreach (string exactName in exactNames)
            {
                if (image.name == exactName)
                {
                    return image;
                }
            }
        }

        return null;
    }

    private static Image CreateRemovalProgressImage(Transform parent)
    {
        GameObject progressObject = new GameObject(
            "Image | Removal Hold Progress",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        progressObject.layer = parent.gameObject.layer;
        RectTransform rect = progressObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.4f, 0.1f);
        rect.anchorMax = new Vector2(0.9f, 0.2f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = progressObject.GetComponent<Image>();
        image.color = new Color32(239, 75, 57, 230);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 0f;
        image.raycastTarget = false;
        progressObject.SetActive(false);
        return image;
    }

    private static RectTransform CreatePanel(Transform parent)
    {
        GameObject tooltipObject = new GameObject(
            "Panel | Relic Tooltip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        tooltipObject.layer = parent.gameObject.layer;
        RectTransform rect = tooltipObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(360f, 180f);
        Image image = tooltipObject.GetComponent<Image>();
        image.color = new Color(0.055f, 0.045f, 0.035f, 0.96f);
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        TMP_Text fontSource,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontSource != null)
        {
            text.font = fontSource.font;
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }
}
