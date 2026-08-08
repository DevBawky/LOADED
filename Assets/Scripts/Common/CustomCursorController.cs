using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CustomCursorController : MonoBehaviour
{
    private const string ThemeResourcePath = "Cursor/DefaultCustomCursorTheme";

    private static CustomCursorController instance;

    private CustomCursorTheme theme;
    private Texture2D standardCursor;
    private Texture2D pressedCursor;
    private bool showingPressedCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Web browsers own the system cursor and dispatch focus changes
        // outside Unity's player loop. Avoid the native WebGL cursor path
        // entirely: runtime texture readback followed by Cursor.SetCursor
        // can terminate the WASM player when browser focus changes.
        return;
#else
        if (instance != null)
        {
            return;
        }

        CustomCursorTheme loadedTheme =
            Resources.Load<CustomCursorTheme>(ThemeResourcePath);
        if (loadedTheme == null)
        {
            return;
        }

        GameObject cursorObject = new GameObject(nameof(CustomCursorController));
        DontDestroyOnLoad(cursorObject);

        instance = cursorObject.AddComponent<CustomCursorController>();
        instance.theme = loadedTheme;
        instance.AssignCursorTextures();
        instance.ApplyCursor(false);
#endif
    }

    private void Update()
    {
        bool pressed = IsAnyMouseButtonPressed();

        if (pressed != showingPressedCursor)
        {
            ApplyCursor(pressed);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyCursor(IsAnyMouseButtonPressed());
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        standardCursor = null;
        pressedCursor = null;
    }

    private void ApplyCursor(bool pressed)
    {
        if (theme == null)
        {
            return;
        }

        Texture2D cursorTexture = pressed ? pressedCursor : standardCursor;

        if (cursorTexture == null)
        {
            return;
        }

        Cursor.SetCursor(
            cursorTexture,
            theme.Hotspot,
            theme.CursorMode);
        showingPressedCursor = pressed;
    }

    private static bool IsAnyMouseButtonPressed()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        return mouse.leftButton.isPressed
            || mouse.rightButton.isPressed
            || mouse.middleButton.isPressed;
    }

    private void AssignCursorTextures()
    {
        standardCursor = theme.StandardCursor;
        pressedCursor = theme.PressedCursor;
    }
}
