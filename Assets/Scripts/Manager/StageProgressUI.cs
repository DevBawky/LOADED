using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StateManager stateManager;
    [SerializeField] private TMP_Text stageTitleText;
    [SerializeField] private RectTransform battleContainer;
    [SerializeField] private Image battleIconPrefab;
    [SerializeField] private Sprite normalBattleSprite;
    [SerializeField] private Sprite bossBattleSprite;
    [SerializeField] private RectTransform currentStageIndicator;

    [Header("Connector")]
    [Min(0f)]
    [SerializeField] private float connectorWidth = 48f;
    [Min(1f)]
    [SerializeField] private float connectorDotSize = 6f;
    [Min(1)]
    [SerializeField] private int connectorDotCount = 5;
    [SerializeField] private Color connectorDotColor =
        new Color(0.7f, 0.7f, 0.7f, 1f);

    private readonly List<RectTransform> battleIcons =
        new List<RectTransform>();
    private StageData displayedStage;
    private string externalStageTitle;

    public void SetExternalStageTitle(string title)
    {
        externalStageTitle = title;
        UpdateStageTitle();
    }

    private void Awake()
    {
        if (stateManager == null)
        {
            stateManager = FindFirstObjectByType<StateManager>();
        }

        ResolveStageTitleText();
        ResolveCurrentStageIndicator();
    }

    private void OnEnable()
    {
        if (stateManager != null)
        {
            stateManager.StateChanged += HandleStateChanged;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (stateManager != null)
        {
            stateManager.StateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        StageData currentStage =
            stateManager != null ? stateManager.CurrentStage : null;

        if (currentStage != displayedStage)
        {
            Rebuild(currentStage);
        }

        UpdateCurrentBattlePosition();
        UpdateStageTitle();
    }

    private void UpdateStageTitle()
    {
        ResolveStageTitleText();

        if (stageTitleText == null || stateManager == null)
        {
            if (stageTitleText != null
                && !string.IsNullOrEmpty(externalStageTitle))
            {
                stageTitleText.text = externalStageTitle;
            }
            return;
        }

        if (!string.IsNullOrEmpty(externalStageTitle))
        {
            stageTitleText.text = externalStageTitle;
            return;
        }

        if (stateManager.CurrentState == GameFlowState.Shop)
        {
            stageTitleText.text = "마을. 상점";
            return;
        }

        StageData stage = stateManager.CurrentStage;
        BattleData battle = stateManager.CurrentBattle;

        if (stateManager.CurrentState != GameFlowState.Battle
            || stage == null || battle == null)
        {
            stageTitleText.text = string.Empty;
            return;
        }

        stageTitleText.text = StageTitleFormatter.Format(stage, battle);
    }

    private void ResolveStageTitleText()
    {
        if (stageTitleText != null)
        {
            return;
        }

        foreach (TMP_Text candidate in FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.name == "Text | Stage Title")
            {
                stageTitleText = candidate;
                return;
            }
        }
    }

    private void Rebuild(StageData stage)
    {
        displayedStage = stage;
        battleIcons.Clear();
        ClearBattleContainer();

        if (stage == null || battleContainer == null
            || battleIconPrefab == null)
        {
            SetIndicatorActive(false);
            return;
        }

        for (int battleIndex = 0;
             battleIndex < stage.Battles.Count;
             battleIndex++)
        {
            if (battleIndex > 0)
            {
                CreateDottedConnector();
            }

            BattleData battle = stage.Battles[battleIndex];
            Image battleIcon = Instantiate(
                battleIconPrefab,
                battleContainer,
                false);

            battleIcon.name = $"Image _ Stages ({battleIndex + 1})";
            battleIcon.sprite = battle != null && battle.IsBoss
                ? bossBattleSprite
                : normalBattleSprite;
            battleIcon.raycastTarget = false;
            battleIcons.Add(battleIcon.rectTransform);
        }
    }

    private void ClearBattleContainer()
    {
        if (battleContainer == null)
        {
            return;
        }

        for (int childIndex = battleContainer.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            GameObject child = battleContainer
                .GetChild(childIndex)
                .gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void CreateDottedConnector()
    {
        GameObject connectorObject = new GameObject(
            "Connector | Dotted",
            typeof(RectTransform),
            typeof(LayoutElement));
        connectorObject.layer = battleContainer.gameObject.layer;

        RectTransform connectorRect =
            connectorObject.GetComponent<RectTransform>();
        connectorRect.SetParent(battleContainer, false);
        connectorRect.sizeDelta = new Vector2(connectorWidth, connectorDotSize);

        LayoutElement layoutElement =
            connectorObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = connectorWidth;
        layoutElement.preferredHeight = connectorDotSize;

        int dotCount = Mathf.Max(1, connectorDotCount);

        for (int dotIndex = 0; dotIndex < dotCount; dotIndex++)
        {
            GameObject dotObject = new GameObject(
                $"Dot {dotIndex + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dotObject.layer = connectorObject.layer;

            RectTransform dotRect =
                dotObject.GetComponent<RectTransform>();
            dotRect.SetParent(connectorRect, false);
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = Vector2.one * connectorDotSize;

            float normalizedPosition = dotCount == 1
                ? 0.5f
                : dotIndex / (float)(dotCount - 1);
            float usableWidth = Mathf.Max(
                0f,
                connectorWidth - connectorDotSize);
            dotRect.anchoredPosition = new Vector2(
                Mathf.Lerp(
                    -usableWidth * 0.5f,
                    usableWidth * 0.5f,
                    normalizedPosition),
                0f);

            Image dotImage = dotObject.GetComponent<Image>();
            dotImage.color = connectorDotColor;
            dotImage.raycastTarget = false;
        }
    }

    private void UpdateCurrentBattlePosition()
    {
        int battleIndex =
            stateManager != null ? stateManager.CurrentBattleIndex : -1;
        bool hasCurrentBattle = currentStageIndicator != null
            && battleIndex >= 0
            && battleIndex < battleIcons.Count
            && battleIcons[battleIndex] != null;

        SetIndicatorActive(hasCurrentBattle);

        if (!hasCurrentBattle)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(battleContainer);
        currentStageIndicator.position = battleIcons[battleIndex].position;
        currentStageIndicator.SetAsLastSibling();
    }

    private void ResolveCurrentStageIndicator()
    {
        RectTransform firstIndicator = null;

        foreach (Transform child in transform)
        {
            if (child.name != "Image | Current Stage")
            {
                continue;
            }

            RectTransform indicator = child as RectTransform;

            if (firstIndicator == null)
            {
                firstIndicator = indicator;
            }

            if (currentStageIndicator != null
                && indicator != currentStageIndicator)
            {
                indicator.gameObject.SetActive(false);
            }
        }

        if (currentStageIndicator == null)
        {
            currentStageIndicator = firstIndicator;
        }
    }

    private void SetIndicatorActive(bool active)
    {
        if (currentStageIndicator != null)
        {
            currentStageIndicator.gameObject.SetActive(active);
        }
    }
}
