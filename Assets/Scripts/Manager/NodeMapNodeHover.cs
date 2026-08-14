using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class NodeMapNodeHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Action<bool> hoverChanged;
    private RectTransform target;
    private float hoverScale = 1.15f;
    private float hoverDuration = 0.12f;
    private bool hovered;

    public void Configure(
        RectTransform targetRect,
        bool active,
        float scale,
        float duration,
        Action<bool> callback)
    {
        target = targetRect;
        hoverScale = Mathf.Max(1f, scale);
        hoverDuration = Mathf.Max(0.01f, duration);
        hoverChanged = callback;
        enabled = active;
        if (target != null)
        {
            target.localScale = Vector3.one;
        }
    }

    public void OnPointerEnter(PointerEventData _)
    {
        hovered = true;
        hoverChanged?.Invoke(true);
    }

    public void OnPointerExit(PointerEventData _)
    {
        hovered = false;
        hoverChanged?.Invoke(false);
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredScale = Vector3.one
            * (hovered ? hoverScale : 1f);
        float blend = 1f - Mathf.Exp(
            -Time.unscaledDeltaTime * 4f / hoverDuration);
        target.localScale = Vector3.Lerp(
            target.localScale, desiredScale, blend);
    }

    private void OnDisable()
    {
        hovered = false;
        hoverChanged?.Invoke(false);
        if (target != null)
        {
            target.localScale = Vector3.one;
        }
    }
}
