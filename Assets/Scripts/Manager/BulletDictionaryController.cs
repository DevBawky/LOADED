using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BulletDictionaryController : MonoBehaviour
{
    private static readonly BulletGrade[] GradeOrder =
    {
        BulletGrade.Normal,
        BulletGrade.Rare,
        BulletGrade.Ace,
        BulletGrade.Legendary
    };

    [Header("Bullet Assets")]
    [SerializeField] private List<BulletData> bullets = new();

    [Header("List Prefabs")]
    [SerializeField] private Transform content;
    [SerializeField] private GameObject bulletLayoutPrefab;
    [SerializeField] private GameObject bulletButtonPrefab;
    [Range(1, 4)] [SerializeField] private int bulletsPerRow = 4;
    [Min(0f)] [SerializeField] private float sectionSpacing = 16f;
    [Min(1f)] [SerializeField] private float sectionHeaderHeight = 64f;
    [Min(1f)] [SerializeField] private float scrollSensitivity = 35f;
    [Min(0f)] [SerializeField] private float scrollbarReservedWidth = 24f;

    [Header("Bullet Button Visual")]
    [SerializeField] private Color hoverIndicatorColor = Color.white;
    [SerializeField] private Color selectedIndicatorColor =
        new(1f, 0.5f, 0f, 1f);

    [Header("Section Header Font Sizes")]
    [Min(1f)] [SerializeField] private float normalHeaderFontSize = 42f;
    [Min(1f)] [SerializeField] private float rareHeaderFontSize = 42f;
    [Min(1f)] [SerializeField] private float aceHeaderFontSize = 42f;
    [Min(1f)] [SerializeField] private float legendaryHeaderFontSize = 42f;

    [Header("Selected Bullet")]
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private Image bulletIcon;
    [SerializeField] private TMP_Text bulletNameText;
    [SerializeField] private TMP_Text bulletDescriptionText;
    [SerializeField] private Button downgradeButton;
    [SerializeField] private Button upgradeButton;

    private BulletData selectedBullet;
    private int selectedLevel;
    private readonly Dictionary<BulletData, BulletButtonVisualState>
        buttonVisuals = new();

    private void Awake()
    {
        ResolveReferences();
        ConfigureContent();
        ConfigureScrollView();
        BindLevelButtons();
        BuildDictionary();
    }

    private void OnValidate()
    {
        bulletsPerRow = Mathf.Clamp(bulletsPerRow, 1, 4);
        sectionHeaderHeight = Mathf.Max(1f, sectionHeaderHeight);
        normalHeaderFontSize = Mathf.Max(1f, normalHeaderFontSize);
        rareHeaderFontSize = Mathf.Max(1f, rareHeaderFontSize);
        aceHeaderFontSize = Mathf.Max(1f, aceHeaderFontSize);
        legendaryHeaderFontSize = Mathf.Max(1f, legendaryHeaderFontSize);
        scrollSensitivity = Mathf.Max(1f, scrollSensitivity);
        scrollbarReservedWidth = Mathf.Max(0f, scrollbarReservedWidth);
    }

    private void OnDestroy()
    {
        if (downgradeButton != null)
        {
            downgradeButton.onClick.RemoveListener(ShowPreviousLevel);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(ShowNextLevel);
        }
    }

    public void ShowPreviousLevel()
    {
        if (selectedBullet == null || selectedLevel <= 0)
        {
            return;
        }

        selectedLevel--;
        RefreshSelectedBullet();
    }

    public void ShowNextLevel()
    {
        if (selectedBullet == null || selectedLevel >= GetMaximumLevel(selectedBullet))
        {
            return;
        }

        selectedLevel++;
        RefreshSelectedBullet();
    }

    private void ResolveReferences()
    {
        content ??= FindDescendant("Content");
        gradeText ??= FindComponent<TMP_Text>("Text | Bullet Grade");
        bulletIcon ??= FindComponent<Image>("Image | Bullet Cylinder Sprite");
        bulletNameText ??= FindComponent<TMP_Text>("Text | Bullet Name");
        bulletDescriptionText ??= FindComponent<TMP_Text>("Text | Bullet Description");
        downgradeButton ??= FindComponent<Button>("Button | DownGrade");
        upgradeButton ??= FindComponent<Button>("Button | UpGrade");
    }

    private void ConfigureContent()
    {
        if (content == null)
        {
            Debug.LogError("Bullet dictionary Content could not be found.", this);
            return;
        }

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = sectionSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void BindLevelButtons()
    {
        if (downgradeButton != null)
        {
            downgradeButton.onClick.AddListener(ShowPreviousLevel);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(ShowNextLevel);
        }
    }

    private void ConfigureScrollView()
    {
        if (content == null)
        {
            return;
        }

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>(true);
        if (scrollRect == null)
        {
            Debug.LogError("Bullet dictionary ScrollRect could not be found.", this);
            return;
        }

        scrollRect.content = content as RectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = scrollSensitivity;

        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
        {
            Transform viewportTransform = FindDescendant("Viewport");
            viewport = viewportTransform as RectTransform;
            scrollRect.viewport = viewport;
        }

        if (viewport == null)
        {
            Debug.LogError("Bullet dictionary Viewport could not be found.", this);
            return;
        }

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-scrollbarReservedWidth, 0f);

        Mask stencilMask = viewport.GetComponent<Mask>();
        if (stencilMask != null)
        {
            stencilMask.enabled = false;
        }

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.enabled = false;
            viewportImage.raycastTarget = false;
        }

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = viewport.gameObject.AddComponent<RectMask2D>();
        }

        rectMask.enabled = true;
        rectMask.padding = Vector4.zero;
        rectMask.softness = Vector2Int.zero;
    }

    private void BuildDictionary()
    {
        if (content == null || bulletLayoutPrefab == null || bulletButtonPrefab == null)
        {
            Debug.LogError("Bullet dictionary prefabs or Content are not assigned.", this);
            SetLevelButtons(false, false);
            return;
        }

        for (int index = content.childCount - 1; index >= 0; index--)
        {
            Destroy(content.GetChild(index).gameObject);
        }

        List<BulletData> validBullets = bullets
            .Where(data => data != null)
            .Distinct()
            .OrderBy(data => data.Grade)
            .ThenBy(data => data.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        buttonVisuals.Clear();

        BulletData firstBullet = null;
        foreach (BulletGrade grade in GradeOrder)
        {
            List<BulletData> gradeBullets = validBullets
                .Where(data => data.Grade == grade)
                .ToList();

            if (gradeBullets.Count == 0)
            {
                continue;
            }

            CreateSectionHeader(grade);
            CreateGradeRows(gradeBullets);
            firstBullet ??= gradeBullets[0];
        }

        if (firstBullet != null)
        {
            SelectBullet(firstBullet);
        }
        else
        {
            ClearSelectedBullet();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>(true);
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void CreateSectionHeader(BulletGrade grade)
    {
        TMP_Text header;
        if (gradeText != null)
        {
            header = Instantiate(gradeText, content);
        }
        else
        {
            GameObject headerObject = new($"Text | {grade}", typeof(RectTransform));
            headerObject.transform.SetParent(content, false);
            header = headerObject.AddComponent<TextMeshProUGUI>();
        }

        header.gameObject.name = $"Text | Grade {grade}";
        header.text = grade.ToString();
        header.color = GetGradeColor(grade);
        header.fontSize = GetHeaderFontSize(grade);
        header.enableAutoSizing = false;
        header.alignment = TextAlignmentOptions.MidlineLeft;
        header.raycastTarget = false;

        LayoutElement layoutElement = header.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = header.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = sectionHeaderHeight;
        layoutElement.preferredHeight = sectionHeaderHeight;
        layoutElement.flexibleHeight = 0f;
    }

    private void CreateGradeRows(IReadOnlyList<BulletData> gradeBullets)
    {
        Transform row = null;
        int perRow = Mathf.Clamp(bulletsPerRow, 1, 4);

        for (int index = 0; index < gradeBullets.Count; index++)
        {
            if (index % perRow == 0)
            {
                GameObject rowObject = Instantiate(bulletLayoutPrefab, content);
                rowObject.name = $"Layout | {gradeBullets[index].Grade} {index / perRow + 1}";
                PreservePrefabRowHeight(rowObject);
                row = rowObject.transform;
            }

            CreateBulletButton(gradeBullets[index], row);
        }
    }

    private void CreateBulletButton(BulletData data, Transform row)
    {
        GameObject buttonObject = Instantiate(bulletButtonPrefab, row);
        buttonObject.name = $"Dict_Button | {data.DisplayName}";

        Image icon = FindComponentInChildren<Image>(buttonObject.transform, "Image | Bullet Sprite");
        if (icon != null)
        {
            icon.sprite = data.CylinderIcon;
            icon.preserveAspect = true;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"Dictionary button prefab has no Button component: {data.name}", buttonObject);
            return;
        }

        button.transition = Selectable.Transition.None;
        BulletButtonVisualState visual = ConfigureButtonVisual(buttonObject, icon);
        if (visual != null)
        {
            buttonVisuals[data] = visual;
        }

        button.onClick.AddListener(() => SelectBullet(data));
    }

    private static void PreservePrefabRowHeight(GameObject rowObject)
    {
        RectTransform rowTransform = rowObject.transform as RectTransform;
        float prefabHeight = rowTransform == null
            ? 200f
            : Mathf.Max(1f, Mathf.Abs(rowTransform.sizeDelta.y));

        LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = prefabHeight;
        layoutElement.preferredHeight = prefabHeight;
        layoutElement.flexibleHeight = 0f;
    }

    private BulletButtonVisualState ConfigureButtonVisual(
        GameObject buttonObject,
        Image icon)
    {
        Image indicator = buttonObject.GetComponent<Image>();
        if (indicator == null)
        {
            Debug.LogWarning("Dictionary bullet button has no indicator Image.", buttonObject);
            return null;
        }

        if (icon != null)
        {
            icon.raycastTarget = true;
        }

        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            // EventTrigger implements scroll and drag handler interfaces even
            // when it only has enter/exit entries. It therefore becomes the
            // first event handler above the pointer and prevents the parent
            // ScrollRect from receiving wheel and drag events.
            trigger.enabled = false;
        }

        BulletButtonVisualState visual =
            buttonObject.GetComponent<BulletButtonVisualState>();
        if (visual == null)
        {
            visual = buttonObject.AddComponent<BulletButtonVisualState>();
        }

        visual.Initialize(
            indicator,
            hoverIndicatorColor,
            selectedIndicatorColor);
        return visual;
    }

    private void SelectBullet(BulletData data)
    {
        selectedBullet = data;
        selectedLevel = 0;
        RefreshButtonSelection();
        RefreshSelectedBullet();
    }

    private void RefreshButtonSelection()
    {
        foreach (KeyValuePair<BulletData, BulletButtonVisualState> pair in buttonVisuals)
        {
            if (pair.Value != null)
            {
                pair.Value.SetSelected(pair.Key == selectedBullet);
            }
        }
    }

    private void RefreshSelectedBullet()
    {
        if (selectedBullet == null)
        {
            ClearSelectedBullet();
            return;
        }

        selectedLevel = Mathf.Clamp(selectedLevel, 0, GetMaximumLevel(selectedBullet));

        if (gradeText != null)
        {
            gradeText.text = selectedBullet.Grade.ToString();
            gradeText.color = selectedBullet.GradeNameColor;
        }

        if (bulletIcon != null)
        {
            bulletIcon.sprite = selectedBullet.CylinderIcon;
            bulletIcon.preserveAspect = true;
        }

        if (bulletNameText != null)
        {
            bulletNameText.text = selectedBullet.GetRichDisplayName(selectedLevel);
        }

        if (bulletDescriptionText != null)
        {
            bulletDescriptionText.text = selectedBullet.GetDetailedDescription(selectedLevel);
        }

        SetLevelButtons(
            selectedLevel > 0,
            selectedLevel < GetMaximumLevel(selectedBullet));
    }

    private void ClearSelectedBullet()
    {
        selectedBullet = null;
        selectedLevel = 0;

        if (gradeText != null) gradeText.text = string.Empty;
        if (bulletIcon != null) bulletIcon.sprite = null;
        if (bulletNameText != null) bulletNameText.text = string.Empty;
        if (bulletDescriptionText != null) bulletDescriptionText.text = string.Empty;
        SetLevelButtons(false, false);
    }

    private void SetLevelButtons(bool canDowngrade, bool canUpgrade)
    {
        if (downgradeButton != null) downgradeButton.interactable = canDowngrade;
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
    }

    private static int GetMaximumLevel(BulletData data)
    {
        return data == null
            ? 0
            : Mathf.Min(BulletData.MaximumUpgradeLevel, data.UpgradeLevels.Count);
    }

    private float GetHeaderFontSize(BulletGrade grade)
    {
        return grade switch
        {
            BulletGrade.Normal => normalHeaderFontSize,
            BulletGrade.Rare => rareHeaderFontSize,
            BulletGrade.Ace => aceHeaderFontSize,
            BulletGrade.Legendary => legendaryHeaderFontSize,
            _ => normalHeaderFontSize
        };
    }

    private static Color GetGradeColor(BulletGrade grade)
    {
        return grade switch
        {
            BulletGrade.Normal => new Color(0.86f, 0.86f, 0.86f, 1f),
            BulletGrade.Rare => new Color(0.3f, 0.65f, 1f, 1f),
            BulletGrade.Ace => new Color(0.75f, 0.4f, 1f, 1f),
            BulletGrade.Legendary => new Color(1f, 0.62f, 0.16f, 1f),
            _ => Color.white
        };
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        return descendants.FirstOrDefault(candidate => candidate.name == objectName);
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform target = FindDescendant(objectName);
        return target == null ? null : target.GetComponent<T>();
    }

    private static T FindComponentInChildren<T>(Transform root, string objectName)
        where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        return components.FirstOrDefault(component => component.name == objectName);
    }
}

[DisallowMultipleComponent]
public sealed class BulletButtonVisualState : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Image indicator;
    private Color hoverColor = Color.white;
    private Color selectedColor = new(1f, 0.5f, 0f, 1f);
    private bool isHovered;
    private bool isSelected;

    public void Initialize(
        Image indicatorImage,
        Color pointerHoverColor,
        Color latestSelectedColor)
    {
        indicator = indicatorImage;
        hoverColor = pointerHoverColor;
        selectedColor = latestSelectedColor;

        if (indicator != null)
        {
            indicator.material = null;
            indicator.raycastTarget = false;
        }

        isHovered = false;
        isSelected = false;
        RefreshIndicator();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshIndicator();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshIndicator();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshIndicator();
    }

    private void OnDisable()
    {
        isHovered = false;
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (indicator == null)
        {
            return;
        }

        indicator.enabled = isSelected || isHovered;
        indicator.color = isSelected ? selectedColor : hoverColor;
    }
}
