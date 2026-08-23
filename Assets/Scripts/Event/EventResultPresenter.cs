using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class EventResultPresenter
{
    private const int ReelCount = 3;

    private readonly TMP_Text dialogueText;

    public EventResultPresenter(
        TMP_Text dialogueText,
        TMP_Text resultText,
        Image[] reelResultImages)
    {
        this.dialogueText = dialogueText;
        ResultText = resultText;
        ReelResultImages = reelResultImages ?? Array.Empty<Image>();
    }

    public TMP_Text ResultText { get; private set; }
    public Image[] ReelResultImages { get; private set; }

    public void Present(
        string value,
        IReadOnlyList<string> symbols,
        Func<string, Sprite> resolveSprite)
    {
        EnsureResultText();
        SetReelResultImages(symbols, resolveSprite);
        bool reelsVisible = ReelResultImages.Length == ReelCount
            && symbols?.Count == ReelCount;
        bool textVisible = !reelsVisible && ResultText != null
            && !string.IsNullOrWhiteSpace(value);
        if (ResultText != null)
        {
            ResultText.gameObject.SetActive(textVisible);
            ResultText.text = textVisible ? value : string.Empty;
        }

        RectTransform dialogueRect = dialogueText == null
            ? null
            : dialogueText.transform as RectTransform;
        if (dialogueRect != null)
        {
            Vector2 anchorMin = dialogueRect.anchorMin;
            anchorMin.y = textVisible || reelsVisible ? 0.50f : 0.39f;
            dialogueRect.anchorMin = anchorMin;
        }
    }

    private void EnsureResultText()
    {
        if (ResultText != null || dialogueText == null)
        {
            return;
        }

        GameObject resultObject = UnityEngine.Object.Instantiate(
            dialogueText.gameObject,
            dialogueText.transform.parent);
        resultObject.name = "Text | Event Result";
        ResultText = resultObject.GetComponent<TMP_Text>();
        RectTransform resultRect = resultObject.transform as RectTransform;
        if (resultRect != null)
        {
            resultRect.anchorMin = new Vector2(0.055f, 0.39f);
            resultRect.anchorMax = new Vector2(0.945f, 0.49f);
            resultRect.offsetMin = Vector2.zero;
            resultRect.offsetMax = Vector2.zero;
        }

        ResultText.alignment = TextAlignmentOptions.Center;
        ResultText.fontSize = Mathf.Max(16f, dialogueText.fontSize - 2f);
        ResultText.raycastTarget = false;
    }

    private void SetReelResultImages(
        IReadOnlyList<string> symbols,
        Func<string, Sprite> resolveSprite)
    {
        EnsureReelResultImages();
        bool visible = symbols?.Count == ReelCount;
        for (int index = 0; index < ReelResultImages.Length; index++)
        {
            Image image = ReelResultImages[index];
            if (image == null)
            {
                continue;
            }

            image.gameObject.SetActive(visible);
            image.sprite = visible
                ? resolveSprite?.Invoke(symbols[index])
                : null;
            image.enabled = visible && image.sprite != null;
        }
    }

    private void EnsureReelResultImages()
    {
        if (ReelResultImages.Length == ReelCount || dialogueText == null)
        {
            return;
        }

        GameObject layoutObject = new GameObject(
            "Layout | Event Reel Result",
            typeof(RectTransform));
        layoutObject.layer = dialogueText.gameObject.layer;
        RectTransform layout = layoutObject.GetComponent<RectTransform>();
        layout.SetParent(dialogueText.transform.parent, false);
        layout.anchorMin = new Vector2(0.20f, 0.39f);
        layout.anchorMax = new Vector2(0.80f, 0.49f);
        layout.offsetMin = Vector2.zero;
        layout.offsetMax = Vector2.zero;

        ReelResultImages = new Image[ReelCount];
        for (int index = 0; index < ReelResultImages.Length; index++)
        {
            GameObject imageObject = new GameObject(
                $"Image | Reel {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.layer = layoutObject.layer;
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(layout, false);
            float minimum = index / (float)ReelCount;
            float maximum = (index + 1) / (float)ReelCount;
            imageRect.anchorMin = new Vector2(minimum, 0f);
            imageRect.anchorMax = new Vector2(maximum, 1f);
            imageRect.offsetMin = new Vector2(8f, 0f);
            imageRect.offsetMax = new Vector2(-8f, 0f);
            Image image = imageObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.gameObject.SetActive(false);
            ReelResultImages[index] = image;
        }
    }
}
