using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

internal enum PlayerShootInputAction
{
    None = 0,
    Reload = 1,
    Shoot = 2,
    EjectNextBullet = 3
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
            if (keyboard.rKey.wasPressedThisFrame)
            {
                return PlayerShootInputAction.Reload;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                return PlayerShootInputAction.Shoot;
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                return PlayerShootInputAction.EjectNextBullet;
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
}
