using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TreasureChestHealthBar : MonoBehaviour
{
    private static readonly Color BackgroundColor =
        new Color(0.055f, 0.035f, 0.025f, 0.94f);
    private static readonly Color FillColor =
        new Color(0.95f, 0.54f, 0.10f, 1f);
    private static readonly Color PreviewTextColor =
        new Color(1f, 0.88f, 0.36f, 1f);

    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text healthText;

    private readonly List<Image> damagePreviewImages = new List<Image>();
    private RectTransform fillAreaRect;
    private int displayedCurrentHealth;
    private int displayedMaxHealth = 1;
    private bool previewActive;

    public void SetHealth(int currentHealth, int maxHealth)
    {
        EnsureView();

        int safeMaxHealth = Mathf.Max(1, maxHealth);
        int safeCurrentHealth = Mathf.Clamp(
            currentHealth,
            0,
            safeMaxHealth);
        ClearDamagePreview();
        displayedCurrentHealth = safeCurrentHealth;
        displayedMaxHealth = safeMaxHealth;
        float ratio = safeCurrentHealth / (float)safeMaxHealth;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        healthText.text = $"{safeCurrentHealth:N0} / {safeMaxHealth:N0}";
        worldCanvas.gameObject.SetActive(safeCurrentHealth > 0);
    }

    public void ShowDamagePreview(
        IReadOnlyList<EnemyHealthBarFeedback.DamagePreviewSegment> segments)
    {
        EnsureView();
        ClearDamagePreview();

        if (segments == null || segments.Count == 0
            || displayedCurrentHealth <= 0)
        {
            return;
        }

        int remainingHealth = displayedCurrentHealth;
        int visibleSegmentCount = 0;

        foreach (EnemyHealthBarFeedback.DamagePreviewSegment segment
                 in segments)
        {
            int damage = Mathf.Min(
                remainingHealth,
                Mathf.Max(0, segment.Damage));
            if (damage <= 0)
            {
                continue;
            }

            int nextHealth = remainingHealth - damage;
            Image previewImage = EnsureDamagePreviewImage(
                visibleSegmentCount++);
            RectTransform previewRect = previewImage.rectTransform;
            previewRect.anchorMin = new Vector2(
                nextHealth / (float)displayedMaxHealth,
                0f);
            previewRect.anchorMax = new Vector2(
                remainingHealth / (float)displayedMaxHealth,
                1f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            Color previewColor = Color.Lerp(segment.Color, Color.white, 0.14f);
            previewColor.a = segment.Emphasized ? 0.98f : 0.68f;
            previewImage.color = previewColor;
            previewImage.gameObject.SetActive(true);
            remainingHealth = nextHealth;
        }

        if (visibleSegmentCount == 0)
        {
            return;
        }

        previewActive = true;
        healthText.text = $"{remainingHealth:N0} / {displayedMaxHealth:N0}";
        healthText.color = PreviewTextColor;
    }

    public void ClearDamagePreview()
    {
        for (int index = 0; index < damagePreviewImages.Count; index++)
        {
            if (damagePreviewImages[index] != null)
            {
                damagePreviewImages[index].gameObject.SetActive(false);
            }
        }

        if (!previewActive)
        {
            return;
        }

        previewActive = false;
        if (healthText != null)
        {
            healthText.text =
                $"{displayedCurrentHealth:N0} / {displayedMaxHealth:N0}";
            healthText.color = Color.white;
        }
    }

    public void Hide()
    {
        ClearDamagePreview();
        if (worldCanvas != null)
        {
            worldCanvas.gameObject.SetActive(false);
        }
    }

    private void EnsureView()
    {
        if (worldCanvas != null && fillImage != null && healthText != null)
        {
            fillAreaRect ??= fillImage.rectTransform.parent as RectTransform;
            if (fillAreaRect != null)
            {
                return;
            }
        }

        GameObject canvasObject = new GameObject(
            "Canvas | Chest Health",
            typeof(RectTransform),
            typeof(Canvas));
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 1.28f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        worldCanvas = canvasObject.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 25;
        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(180f, 34f);

        UnityEngine.UI.Image background = CreateImage(
            "Image | Health Background",
            canvasRect,
            BackgroundColor);
        background.raycastTarget = false;

        GameObject fillAreaObject = new GameObject(
            "Rect | Health Fill Area",
            typeof(RectTransform));
        fillAreaObject.layer = gameObject.layer;
        fillAreaRect =
            fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.SetParent(background.rectTransform, false);
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObject = new GameObject(
            "Image | Health Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image));
        fillObject.layer = gameObject.layer;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(fillAreaRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = FillColor;
        fillImage.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Text | Chest Health",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(canvasRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        healthText = textObject.GetComponent<TextMeshProUGUI>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 19f;
        healthText.fontStyle = FontStyles.Bold;
        healthText.color = Color.white;
        healthText.raycastTarget = false;

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (text != null && text != healthText && text.font != null)
            {
                healthText.font = text.font;
                break;
            }
        }
    }

    private Image EnsureDamagePreviewImage(int index)
    {
        while (damagePreviewImages.Count <= index)
        {
            Image image = CreateImage(
                $"Image | Damage Preview {damagePreviewImages.Count + 1}",
                fillAreaRect,
                Color.white);
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            damagePreviewImages.Add(image);
        }

        return damagePreviewImages[index];
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }
}
