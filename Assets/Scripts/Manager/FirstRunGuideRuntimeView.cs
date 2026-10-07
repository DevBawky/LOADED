using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

internal sealed class FirstRunGuideRuntimeView
{
    private readonly TMP_FontAsset guideFont;

    private FirstRunGuideRuntimeView(TMP_FontAsset guideFont)
    {
        this.guideFont = guideFont;
        DebuffLegendIcons = new Image[4];
    }

    internal RectTransform Root { get; private set; }
    internal Image InputBlocker { get; private set; }
    internal RectTransform Highlight { get; private set; }
    internal Image HighlightImage { get; private set; }
    internal GameObject Card { get; private set; }
    internal TMP_Text CardStepText { get; private set; }
    internal TMP_Text CardTitleText { get; private set; }
    internal TMP_Text CardBodyText { get; private set; }
    internal GameObject CardMissionPanel { get; private set; }
    internal TMP_Text CardMissionText { get; private set; }
    internal Button CardBackButton { get; private set; }
    internal Button CardExitButton { get; private set; }
    internal Toggle NeverShowToggle { get; private set; }
    internal Button ContinueButton { get; private set; }
    internal TMP_Text ContinueButtonText { get; private set; }
    internal Button MissionGuideButton { get; private set; }
    internal Button MissionNextButton { get; private set; }
    internal GameObject VideoFrame { get; private set; }
    internal RawImage VideoDisplay { get; private set; }
    internal TMP_Text VideoLoadingText { get; private set; }
    internal AspectRatioFitter VideoAspect { get; private set; }
    internal VideoPlayer VideoPlayer { get; private set; }
    internal GameObject MissionBar { get; private set; }
    internal TMP_Text MissionText { get; private set; }
    internal GameObject WarningDemoRoot { get; private set; }
    internal Button WarningSoundButton { get; private set; }
    internal Image WarningDemoTileImage { get; private set; }
    internal Image WarningDemoAttackIcon { get; private set; }
    internal Image WarningDemoReadyGlow { get; private set; }
    internal GameObject DebuffLegendRoot { get; private set; }
    internal Image[] DebuffLegendIcons { get; }

    internal static FirstRunGuideRuntimeView Create(
        Canvas rootCanvas,
        TMP_FontAsset guideFont,
        int sortingOrder)
    {
        if (rootCanvas == null)
        {
            return null;
        }

        FirstRunGuideRuntimeView view =
            new FirstRunGuideRuntimeView(guideFont);
        view.Build(rootCanvas, sortingOrder);
        return view;
    }

    private void Build(Canvas rootCanvas, int sortingOrder)
    {
        Root = CreateRect("Guide | First Run", rootCanvas.transform);
        Stretch(Root);

        Canvas guideCanvas = Root.gameObject.AddComponent<Canvas>();
        guideCanvas.overrideSorting = true;
        guideCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        guideCanvas.sortingOrder = sortingOrder;
        Root.gameObject.AddComponent<GraphicRaycaster>();

        InputBlocker = CreateImage(
            "Image | Guide Blocker",
            Root,
            new Color(0.025f, 0.02f, 0.018f, 0.72f));
        Stretch(InputBlocker.rectTransform);
        InputBlocker.raycastTarget = true;

        HighlightImage = CreateImage(
            "Image | Guide Highlight",
            Root,
            new Color(0.02f, 0.48f, 1f, 0.11f));
        Highlight = HighlightImage.rectTransform;
        Highlight.anchorMin = new Vector2(0.5f, 0.5f);
        Highlight.anchorMax = new Vector2(0.5f, 0.5f);
        Highlight.pivot = new Vector2(0.5f, 0.5f);
        HighlightImage.raycastTarget = false;
        Outline highlightOutline = Highlight.gameObject.AddComponent<Outline>();
        highlightOutline.effectColor = new Color(0.05f, 0.82f, 1f, 1f);
        highlightOutline.effectDistance = new Vector2(4f, -4f);

        Card = CreateImage(
            "Panel | Guide Card",
            Root,
            new Color(0.09f, 0.075f, 0.065f, 0.98f)).gameObject;
        RectTransform cardRect = (RectTransform)Card.transform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(780f, 720f);
        Outline cardOutline = Card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.95f, 0.5f, 0.12f, 0.9f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        CardStepText = CreateText("Text | Guide Step", cardRect);
        SetAnchors(CardStepText.rectTransform, 0.06f, 0.89f, 0.94f, 0.96f);
        CardStepText.alignment = TextAlignmentOptions.Center;
        CardStepText.color = new Color(1f, 0.7f, 0.28f, 1f);
        CardStepText.fontSizeMax = 25f;
        CardStepText.textWrappingMode = TextWrappingModes.NoWrap;

        CardTitleText = CreateText("Text | Guide Title", cardRect);
        SetAnchors(CardTitleText.rectTransform, 0.06f, 0.80f, 0.94f, 0.90f);
        CardTitleText.alignment = TextAlignmentOptions.Center;
        CardTitleText.fontStyle = FontStyles.Normal;
        CardTitleText.fontSizeMax = 42f;
        CardTitleText.textWrappingMode = TextWrappingModes.NoWrap;

        Image frameImage = CreateImage(
            "Image | Guide Video Frame",
            cardRect,
            new Color(0.02f, 0.018f, 0.016f, 1f));
        VideoFrame = frameImage.gameObject;
        SetAnchors(frameImage.rectTransform, 0.08f, 0.31f, 0.92f, 0.79f);
        frameImage.raycastTarget = false;

        RectTransform displayRect = CreateRect(
            "RawImage | Guide Video",
            frameImage.rectTransform);
        Stretch(displayRect);
        VideoDisplay = displayRect.gameObject.AddComponent<RawImage>();
        VideoDisplay.color = Color.white;
        VideoDisplay.raycastTarget = false;
        VideoAspect = displayRect.gameObject.AddComponent<AspectRatioFitter>();
        VideoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        VideoAspect.aspectRatio = 16f / 9f;

        VideoLoadingText = CreateText(
            "Text | Guide Video Loading",
            frameImage.rectTransform);
        Stretch(VideoLoadingText.rectTransform);
        VideoLoadingText.alignment = TextAlignmentOptions.Center;
        VideoLoadingText.text = "영상 불러오는 중...";
        VideoLoadingText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        VideoLoadingText.fontSizeMax = 24f;
        VideoLoadingText.textWrappingMode = TextWrappingModes.NoWrap;

        CardBodyText = CreateText("Text | Guide Body", cardRect);
        SetAnchors(CardBodyText.rectTransform, 0.08f, 0.13f, 0.92f, 0.29f);
        CardBodyText.alignment = TextAlignmentOptions.Center;
        CardBodyText.fontSizeMin = 12f;
        CardBodyText.fontSizeMax = 27f;
        CardBodyText.textWrappingMode = TextWrappingModes.NoWrap;
        CardBodyText.overflowMode = TextOverflowModes.Ellipsis;

        Image cardMissionImage = CreateImage(
            "Panel | Guide Card Mission",
            cardRect,
            new Color(0.035f, 0.09f, 0.11f, 0.98f));
        CardMissionPanel = cardMissionImage.gameObject;
        SetAnchors(
            cardMissionImage.rectTransform,
            0.12f,
            0.12f,
            0.88f,
            0.17f);
        cardMissionImage.raycastTarget = false;
        Outline cardMissionOutline = CardMissionPanel.AddComponent<Outline>();
        cardMissionOutline.effectColor = new Color(0.35f, 0.8f, 1f, 0.8f);
        cardMissionOutline.effectDistance = new Vector2(2f, -2f);

        CardMissionText = CreateText(
            "Text | Guide Card Mission",
            cardMissionImage.rectTransform);
        SetAnchors(CardMissionText.rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
        CardMissionText.alignment = TextAlignmentOptions.Center;
        CardMissionText.fontSizeMin = 11f;
        CardMissionText.fontSizeMax = 24f;
        CardMissionText.textWrappingMode = TextWrappingModes.NoWrap;
        CardMissionText.overflowMode = TextOverflowModes.Ellipsis;
        CardMissionPanel.SetActive(false);

        BuildWarningDemo(cardRect);
        BuildDebuffLegend(cardRect);

        NeverShowToggle = CreateNeverShowToggle(cardRect);
        SetAnchors(
            (RectTransform)NeverShowToggle.transform,
            0.035f,
            0.895f,
            0.28f,
            0.965f);

        CardExitButton = CreateGuideExitButton(cardRect);
        SetAnchors(
            (RectTransform)CardExitButton.transform,
            0.91f,
            0.895f,
            0.975f,
            0.965f);

        CardBackButton = CreateButton(
            "Button | Previous Guide",
            cardRect,
            "이전",
            new Color(0.2f, 0.18f, 0.17f, 1f),
            out _);
        SetAnchors(
            (RectTransform)CardBackButton.transform,
            0.08f,
            0.035f,
            0.32f,
            0.11f);

        ContinueButton = CreateButton(
            "Button | Continue Guide",
            cardRect,
            "미션 시작",
            new Color(0.82f, 0.34f, 0.08f, 1f),
            out TMP_Text continueButtonText);
        ContinueButtonText = continueButtonText;
        SetAnchors(
            (RectTransform)ContinueButton.transform,
            0.68f,
            0.035f,
            0.92f,
            0.11f);

        BuildMissionBar();
        BuildVideoPlayer();

        Card.SetActive(false);
        MissionBar.SetActive(false);
        Root.gameObject.SetActive(false);
    }

    private void BuildWarningDemo(RectTransform cardRect)
    {
        RectTransform warningRootRect = CreateRect(
            "Panel | Warning Sound Demo",
            cardRect);
        WarningDemoRoot = warningRootRect.gameObject;
        SetAnchors(warningRootRect, 0.27f, 0.125f, 0.73f, 0.27f);

        WarningSoundButton = CreateButton(
            "Button | Play Enemy Warning",
            warningRootRect,
            "경고음 듣기",
            new Color(0.42f, 0.11f, 0.08f, 1f),
            out _);
        SetAnchors(
            (RectTransform)WarningSoundButton.transform,
            0f,
            0.18f,
            0.60f,
            0.82f);

        WarningDemoTileImage = CreateImage(
            "Image | Enemy Warning Tile",
            warningRootRect,
            new Color(0.18f, 0.15f, 0.13f, 1f));
        SetAnchors(
            WarningDemoTileImage.rectTransform,
            0.73f,
            0.10f,
            0.96f,
            0.90f);
        WarningDemoTileImage.type = Image.Type.Simple;
        WarningDemoTileImage.preserveAspect = false;
        WarningDemoTileImage.raycastTarget = false;

        WarningDemoReadyGlow = CreateImage(
            "Image | Enemy Warning Ready Glow",
            WarningDemoTileImage.rectTransform,
            Color.white);
        Stretch(WarningDemoReadyGlow.rectTransform);
        WarningDemoReadyGlow.sprite = null;
        WarningDemoReadyGlow.type = Image.Type.Simple;
        WarningDemoReadyGlow.raycastTarget = false;
        WarningDemoReadyGlow.gameObject.SetActive(false);

        WarningDemoAttackIcon = CreateImage(
            "Image | Melee Attack Icon",
            WarningDemoTileImage.rectTransform,
            Color.white);
        SetAnchors(
            WarningDemoAttackIcon.rectTransform,
            0.20f,
            0.20f,
            0.80f,
            0.80f);
        WarningDemoAttackIcon.preserveAspect = true;
        WarningDemoAttackIcon.raycastTarget = false;
        WarningDemoRoot.SetActive(false);
    }

    private void BuildDebuffLegend(RectTransform cardRect)
    {
        RectTransform debuffRootRect = CreateRect(
            "Panel | Debuff Legend",
            cardRect);
        DebuffLegendRoot = debuffRootRect.gameObject;
        SetAnchors(debuffRootRect, 0.18f, 0.19f, 0.82f, 0.36f);
        string[] debuffNames = { "표식", "독", "기절", "약화" };
        Color[] debuffColors =
        {
            new Color(1f, 0.49f, 0.49f, 1f),
            new Color(0.47f, 0.85f, 0.53f, 1f),
            new Color(0.46f, 0.78f, 1f, 1f),
            new Color(0.78f, 0.61f, 1f, 1f)
        };
        for (int i = 0; i < DebuffLegendIcons.Length; i++)
        {
            RectTransform itemRect = CreateRect(
                $"Item | Debuff {debuffNames[i]}",
                debuffRootRect);
            float minX = i / (float)DebuffLegendIcons.Length;
            float maxX = (i + 1f) / DebuffLegendIcons.Length;
            SetAnchors(itemRect, minX, 0f, maxX, 1f);

            Image icon = CreateImage(
                $"Image | Debuff {debuffNames[i]}",
                itemRect,
                Color.white);
            SetAnchors(icon.rectTransform, 0.25f, 0.30f, 0.75f, 0.94f);
            icon.preserveAspect = true;
            icon.raycastTarget = true;
            DebuffLegendIcons[i] = icon;

            TMP_Text stackText = CreateText("Text | Stack", icon.rectTransform);
            SetAnchors(stackText.rectTransform, 0.52f, 0f, 1f, 0.48f);
            stackText.text = "1";
            stackText.color = new Color(1f, 0.18f, 0.22f, 1f);
            stackText.fontStyle = FontStyles.Normal;
            stackText.fontSizeMin = 10f;
            stackText.fontSizeMax = 18f;
            stackText.alignment = TextAlignmentOptions.BottomRight;
            stackText.textWrappingMode = TextWrappingModes.NoWrap;
            icon.gameObject.AddComponent<DebuffIconUI>();

            TMP_Text label = CreateText(
                $"Text | Debuff {debuffNames[i]}",
                itemRect);
            SetAnchors(label.rectTransform, 0f, 0f, 1f, 0.30f);
            label.text = debuffNames[i];
            label.color = debuffColors[i];
            label.fontStyle = FontStyles.Normal;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 21f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        DebuffLegendRoot.SetActive(false);
    }

    private void BuildMissionBar()
    {
        Image missionImage = CreateImage(
            "Panel | Guide Mission",
            Root,
            new Color(0.065f, 0.052f, 0.045f, 0.96f));
        MissionBar = missionImage.gameObject;
        SetAnchors(missionImage.rectTransform, 0.18f, 0.88f, 0.82f, 0.97f);
        missionImage.raycastTarget = false;
        Outline missionOutline = MissionBar.AddComponent<Outline>();
        missionOutline.effectColor = new Color(0.95f, 0.5f, 0.12f, 0.85f);
        missionOutline.effectDistance = new Vector2(2f, -2f);

        MissionText = CreateText(
            "Text | Guide Mission",
            missionImage.rectTransform);
        SetAnchors(MissionText.rectTransform, 0.04f, 0.12f, 0.62f, 0.88f);
        MissionText.alignment = TextAlignmentOptions.MidlineLeft;
        MissionText.fontSizeMin = 12f;
        MissionText.fontSizeMax = 28f;
        MissionText.textWrappingMode = TextWrappingModes.NoWrap;
        MissionText.overflowMode = TextOverflowModes.Ellipsis;

        MissionGuideButton = CreateButton(
            "Button | Show Current Guide",
            missionImage.rectTransform,
            "가이드 보기",
            new Color(0.34f, 0.20f, 0.10f, 0.95f),
            out _);
        SetAnchors(
            (RectTransform)MissionGuideButton.transform,
            0.64f,
            0.18f,
            0.80f,
            0.82f);

        MissionNextButton = CreateButton(
            "Button | Next Mission Guide",
            missionImage.rectTransform,
            "다음 단계",
            new Color(0.12f, 0.32f, 0.4f, 0.95f),
            out _);
        SetAnchors(
            (RectTransform)MissionNextButton.transform,
            0.82f,
            0.18f,
            0.98f,
            0.82f);
    }

    private void BuildVideoPlayer()
    {
        GameObject playerObject = new GameObject(
            "VideoPlayer | First Run Guide");
        playerObject.transform.SetParent(Root, false);
        VideoPlayer = playerObject.AddComponent<VideoPlayer>();
        VideoPlayer.playOnAwake = false;
        VideoPlayer.source = VideoSource.Url;
        VideoPlayer.renderMode = VideoRenderMode.APIOnly;
        VideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        VideoPlayer.isLooping = true;
        VideoPlayer.skipOnDrop = true;
        VideoPlayer.waitForFirstFrame = true;
        VideoPlayer.sendFrameReadyEvents = true;
        VideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)target.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private Image CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text CreateText(string objectName, Transform parent)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (guideFont != null)
        {
            text.font = guideFont;
        }

        text.color = Color.white;
        text.fontStyle = FontStyles.Normal;
        text.richText = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Toggle CreateNeverShowToggle(Transform parent)
    {
        RectTransform root = CreateRect("Toggle | Never Show Guide", parent);
        Toggle toggle = root.gameObject.AddComponent<Toggle>();

        Image background = CreateImage(
            "Image | Checkbox",
            root,
            new Color(0.08f, 0.07f, 0.055f, 0.98f));
        SetAnchors(background.rectTransform, 0f, 0.16f, 0.18f, 0.84f);
        Outline checkboxOutline = background.gameObject.AddComponent<Outline>();
        checkboxOutline.effectColor = new Color(1f, 0.75f, 0.12f, 1f);
        checkboxOutline.effectDistance = new Vector2(2f, -2f);

        Image checkmark = CreateImage(
            "Image | Checkmark",
            background.rectTransform,
            new Color(1f, 0.62f, 0.05f, 1f));
        SetAnchors(checkmark.rectTransform, 0.2f, 0.2f, 0.8f, 0.8f);
        checkmark.raycastTarget = false;

        TMP_Text label = CreateText("Text | Never Show Guide", root);
        SetAnchors(label.rectTransform, 0.23f, 0f, 1f, 1f);
        label.text = "다시 보지 않기";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontStyle = FontStyles.Normal;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 20f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.SetIsOnWithoutNotify(false);
        checkmark.canvasRenderer.SetAlpha(0f);
        return toggle;
    }

    private Button CreateGuideExitButton(Transform parent)
    {
        Button template = null;
        foreach (Button candidate in UnityEngine.Object.FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate == null
                || Root != null && candidate.transform.IsChildOf(Root))
            {
                continue;
            }

            if (candidate.name == "Button _ Exit"
                || candidate.name == "Button | Exit")
            {
                TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
                if (label == null
                    || !string.Equals(
                        label.text?.Trim(),
                        "X",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                template = candidate;
                break;
            }
        }

        if (template == null)
        {
            return CreateButton(
                "Button | Close Guide",
                parent,
                "X",
                new Color(0.36f, 0.06f, 0.05f, 1f),
                out _);
        }

        Button exitButton = UnityEngine.Object.Instantiate(
            template,
            parent,
            false);
        exitButton.name = "Button | Close Guide";
        exitButton.onClick = new Button.ButtonClickedEvent();
        exitButton.transform.localScale = Vector3.one;
        exitButton.gameObject.SetActive(true);
        return exitButton;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        out TMP_Text labelText)
    {
        Image image = CreateImage(objectName, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        labelText = CreateText("Text | Label", image.rectTransform);
        Stretch(labelText.rectTransform);
        labelText.text = label;
        labelText.fontStyle = FontStyles.Normal;
        labelText.fontSizeMin = 9f;
        labelText.fontSizeMax = 26f;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.margin = new Vector4(6f, 2f, 6f, 2f);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetAnchors(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
