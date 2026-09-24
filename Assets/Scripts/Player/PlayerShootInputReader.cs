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
                || !eventSystem.IsPointerOverGameObject())
                    ? PlayerShootInputAction.Shoot
                    : PlayerShootInputAction.None;
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
