using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

internal enum PlayerShootInputAction
{
    None = 0,
    Reload = 1,
    Shoot = 2
}

/// <summary>
/// Translates device state into player shooting intent. Gameplay validation
/// and action execution remain in <see cref="PlayerShoot"/>.
/// </summary>
internal static class PlayerShootInputReader
{
    private static readonly List<RaycastResult> PointerRaycastResults =
        new List<RaycastResult>();

    public static PlayerShootInputAction Read(EventSystem eventSystem)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            PlayerShootInputAction keyboardAction = ResolveKeyboardAction(
                keyboard.rKey.wasPressedThisFrame,
                keyboard.spaceKey.wasPressedThisFrame);

            if (keyboardAction != PlayerShootInputAction.None)
            {
                return keyboardAction;
            }
        }

        Mouse mouse = Mouse.current;

        return mouse != null
            && mouse.leftButton.wasPressedThisFrame
            && (eventSystem == null
                || !IsPointerOverInteractiveUi(
                    eventSystem,
                    mouse.position.ReadValue()))
                    ? PlayerShootInputAction.Shoot
                    : PlayerShootInputAction.None;
    }

    internal static bool IsPointerOverInteractiveUi(
        EventSystem eventSystem,
        Vector2 screenPosition)
    {
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };
        PointerRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, PointerRaycastResults);

        for (int index = 0; index < PointerRaycastResults.Count; index++)
        {
            if (IsInteractiveUiTarget(
                    PointerRaycastResults[index].gameObject))
            {
                PointerRaycastResults.Clear();
                return true;
            }
        }

        PointerRaycastResults.Clear();
        return false;
    }

    internal static bool IsInteractiveUiTarget(GameObject target)
    {
        return target != null
            && (target.GetComponentInParent<UnityEngine.UI.Selectable>() != null
                || ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)
                    != null
                || ExecuteEvents.GetEventHandler<IBeginDragHandler>(target)
                    != null
                || ExecuteEvents.GetEventHandler<IDragHandler>(target) != null
                || ExecuteEvents.GetEventHandler<IEndDragHandler>(target)
                    != null
                || ExecuteEvents.GetEventHandler<IScrollHandler>(target)
                    != null);
    }

    internal static PlayerShootInputAction ResolveKeyboardAction(
        bool reloadPressed,
        bool shootPressed)
    {
        if (reloadPressed)
        {
            return PlayerShootInputAction.Reload;
        }

        return shootPressed
            ? PlayerShootInputAction.Shoot
            : PlayerShootInputAction.None;
    }
}
