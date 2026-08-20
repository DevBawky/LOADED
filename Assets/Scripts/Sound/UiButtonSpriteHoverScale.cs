using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class UiButtonSpriteHoverScale : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float HoverScale = 1.1f;
    private const float ScaleSpeed = 18f;

    private Button button;
    private Vector3 baseScale;
    private bool initialized;
    private bool pointerInside;

    public void Initialize(Button targetButton)
    {
        if (initialized && button == targetButton)
        {
            return;
        }

        button = targetButton;
        baseScale = transform.localScale;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        bool canEnlarge = pointerInside
            && button != null
            && button.IsActive()
            && button.IsInteractable();
        Vector3 targetScale = canEnlarge
            ? baseScale * HoverScale
            : baseScale;
        float blend = 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            blend);

        if ((transform.localScale - targetScale).sqrMagnitude < 0.000001f)
        {
            transform.localScale = targetScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void OnDisable()
    {
        pointerInside = false;

        if (initialized)
        {
            transform.localScale = baseScale;
        }
    }
}
