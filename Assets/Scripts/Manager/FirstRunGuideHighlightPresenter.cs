using UnityEngine;
using UnityEngine.UI;

internal sealed class FirstRunGuideHighlightPresenter
{
    private const float HighlightPadding = 12f;

    private readonly Canvas rootCanvas;
    private readonly RectTransform highlight;
    private readonly Image highlightImage;
    private readonly Vector3[] targetWorldCorners = new Vector3[4];

    public FirstRunGuideHighlightPresenter(
        Canvas rootCanvas,
        RectTransform highlight,
        Image highlightImage)
    {
        this.rootCanvas = rootCanvas;
        this.highlight = highlight;
        this.highlightImage = highlightImage;
    }

    public void Present(
        RectTransform primaryTarget,
        RectTransform secondaryTarget,
        float unscaledTime)
    {
        if (highlight == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas == null
            ? null
            : rootCanvas.transform as RectTransform;
        if (canvasRect == null
            || !TryGetScreenBounds(
                primaryTarget,
                secondaryTarget,
                out Vector2 screenMin,
                out Vector2 screenMax))
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        Camera rootCamera = rootCanvas.renderMode
            == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenMin,
                rootCamera,
                out Vector2 localMin)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenMax,
                rootCamera,
                out Vector2 localMax))
        {
            highlight.gameObject.SetActive(false);
            return;
        }

        CalculateHighlightRect(
            localMin,
            localMax,
            HighlightPadding,
            out Vector2 position,
            out Vector2 size);
        highlight.gameObject.SetActive(true);
        highlight.anchoredPosition = position;
        highlight.sizeDelta = size;

        if (highlightImage != null)
        {
            Color color = highlightImage.color;
            color.a = CalculatePulseAlpha(unscaledTime);
            highlightImage.color = color;
        }
    }

    internal static void CalculateHighlightRect(
        Vector2 minimum,
        Vector2 maximum,
        float padding,
        out Vector2 position,
        out Vector2 size)
    {
        position = (minimum + maximum) * 0.5f;
        size = new Vector2(
            Mathf.Abs(maximum.x - minimum.x) + padding * 2f,
            Mathf.Abs(maximum.y - minimum.y) + padding * 2f);
    }

    internal static float CalculatePulseAlpha(float unscaledTime)
    {
        return 0.05f + 0.14f
            * (0.5f + 0.5f * Mathf.Sin(unscaledTime * 5f));
    }

    private bool TryGetScreenBounds(
        RectTransform primaryTarget,
        RectTransform secondaryTarget,
        out Vector2 screenMin,
        out Vector2 screenMax)
    {
        screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;

        IncludeTarget(
            primaryTarget,
            ref hasBounds,
            ref screenMin,
            ref screenMax);
        IncludeTarget(
            secondaryTarget,
            ref hasBounds,
            ref screenMin,
            ref screenMax);
        return hasBounds;
    }

    private void IncludeTarget(
        RectTransform target,
        ref bool hasBounds,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return;
        }

        target.GetWorldCorners(targetWorldCorners);
        Camera targetCamera = GetCanvasCamera(target);
        IncludeScreenPoint(
            RectTransformUtility.WorldToScreenPoint(
                targetCamera,
                targetWorldCorners[0]),
            ref minimum,
            ref maximum);
        IncludeScreenPoint(
            RectTransformUtility.WorldToScreenPoint(
                targetCamera,
                targetWorldCorners[2]),
            ref minimum,
            ref maximum);
        hasBounds = true;
    }

    private static void IncludeScreenPoint(
        Vector2 point,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        minimum = Vector2.Min(minimum, point);
        maximum = Vector2.Max(maximum, point);
    }

    private static Camera GetCanvasCamera(RectTransform target)
    {
        Canvas canvas = target == null
            ? null
            : target.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
    }
}
