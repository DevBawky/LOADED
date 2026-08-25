using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button), typeof(Image))]
public sealed class MainButtonShaderFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private static readonly int HoverId = Shader.PropertyToID("_Hover");
    private static readonly int PressId = Shader.PropertyToID("_Press");
    private static readonly int ClickId = Shader.PropertyToID("_Click");
    private static readonly int ClickProgressId =
        Shader.PropertyToID("_ClickProgress");
    private static readonly int ClickOriginId =
        Shader.PropertyToID("_ClickOrigin");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int UnscaledTimeId =
        Shader.PropertyToID("_UnscaledTime");
    private static readonly int DisabledId = Shader.PropertyToID("_Disabled");
    private static readonly int InstanceTintId =
        Shader.PropertyToID("_InstanceTint");

    [SerializeField] private Image targetImage;
    [SerializeField] private Image legacyTintImage;
    [Min(0.01f)] [SerializeField] private float hoverResponse = 18f;
    [Min(0.01f)] [SerializeField] private float pressResponse = 28f;
    [Min(0.01f)] [SerializeField] private float clickDuration = 0.22f;

    private Button button;
    private Material sourceMaterial;
    private Material runtimeMaterial;
    private bool pointerInside;
    private bool pointerDown;
    private bool clickSubscribed;
    private bool clickPlaying;
    private float hoverAmount;
    private float pressAmount;
    private float clickElapsed;
    private Vector2 clickOrigin = new Vector2(0.5f, 0.5f);

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        CreateRuntimeMaterial();
        SubscribeClick();
        ResetInteraction();
    }

    private void Update()
    {
        if (runtimeMaterial == null)
        {
            CreateRuntimeMaterial();

            if (runtimeMaterial == null)
            {
                return;
            }
        }

        bool canInteract = button != null
            && button.IsActive()
            && button.IsInteractable();
        float deltaTime = Time.unscaledDeltaTime;
        hoverAmount = Damp(
            hoverAmount,
            canInteract && pointerInside ? 1f : 0f,
            hoverResponse,
            deltaTime);
        pressAmount = Damp(
            pressAmount,
            canInteract && pointerDown ? 1f : 0f,
            pressResponse,
            deltaTime);

        float clickProgress = 1f;
        float clickStrength = 0f;

        if (clickPlaying)
        {
            clickElapsed += deltaTime;
            clickProgress = Mathf.Clamp01(clickElapsed / clickDuration);
            clickStrength = 1f - clickProgress;
            clickPlaying = clickProgress < 1f;
        }

        Rect rect = targetImage.rectTransform.rect;
        float height = Mathf.Max(1f, Mathf.Abs(rect.height));
        float aspect = Mathf.Max(1f, Mathf.Abs(rect.width) / height);

        runtimeMaterial.SetFloat(HoverId, hoverAmount);
        runtimeMaterial.SetFloat(PressId, pressAmount);
        runtimeMaterial.SetFloat(ClickId, clickStrength);
        runtimeMaterial.SetFloat(ClickProgressId, clickProgress);
        runtimeMaterial.SetVector(
            ClickOriginId,
            new Vector4(clickOrigin.x, clickOrigin.y, 0f, 0f));
        runtimeMaterial.SetFloat(AspectId, aspect);
        runtimeMaterial.SetFloat(UnscaledTimeId, Time.unscaledTime);
        runtimeMaterial.SetFloat(DisabledId, canInteract ? 0f : 1f);
        runtimeMaterial.SetColor(
            InstanceTintId,
            legacyTintImage == null ? Color.white : legacyTintImage.color);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerDown = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            return;
        }

        pointerDown = true;
        CaptureClickOrigin(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
    }

    private void OnDisable()
    {
        UnsubscribeClick();
        ResetInteraction();
        ReleaseRuntimeMaterial();
    }

    private void OnDestroy()
    {
        UnsubscribeClick();
        ReleaseRuntimeMaterial();
    }

    internal static float Damp(
        float current,
        float target,
        float response,
        float deltaTime)
    {
        if (deltaTime <= 0f || response <= 0f)
        {
            return current;
        }

        float blend = 1f - Mathf.Exp(-response * deltaTime);
        return Mathf.Lerp(current, target, blend);
    }

    private void CacheReferences()
    {
        button ??= GetComponent<Button>();
        targetImage ??= GetComponent<Image>();
    }

    private void CreateRuntimeMaterial()
    {
        if (runtimeMaterial != null || targetImage == null)
        {
            return;
        }

        sourceMaterial = targetImage.material;

        if (sourceMaterial == null
            || sourceMaterial.shader == null
            || sourceMaterial.shader.name != "Loaded/UI/Main Button")
        {
            return;
        }

        runtimeMaterial = new Material(sourceMaterial)
        {
            name = sourceMaterial.name + " | " + name,
            hideFlags = HideFlags.HideAndDontSave
        };
        targetImage.material = runtimeMaterial;
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (targetImage != null && targetImage.material == runtimeMaterial)
        {
            targetImage.material = sourceMaterial;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }

        runtimeMaterial = null;
    }

    private void SubscribeClick()
    {
        if (clickSubscribed || button == null)
        {
            return;
        }

        button.onClick.AddListener(HandleClick);
        clickSubscribed = true;
    }

    private void UnsubscribeClick()
    {
        if (!clickSubscribed)
        {
            return;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }

        clickSubscribed = false;
    }

    private void HandleClick()
    {
        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            return;
        }

        if (!pointerInside)
        {
            clickOrigin = new Vector2(0.5f, 0.5f);
        }

        clickElapsed = 0f;
        clickPlaying = true;
    }

    private void CaptureClickOrigin(PointerEventData eventData)
    {
        if (eventData == null || targetImage == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetImage.rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            clickOrigin = new Vector2(0.5f, 0.5f);
            return;
        }

        Rect rect = targetImage.rectTransform.rect;
        clickOrigin = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
    }

    private void ResetInteraction()
    {
        pointerInside = false;
        pointerDown = false;
        clickPlaying = false;
        hoverAmount = 0f;
        pressAmount = 0f;
        clickElapsed = clickDuration;
        clickOrigin = new Vector2(0.5f, 0.5f);
    }
}
