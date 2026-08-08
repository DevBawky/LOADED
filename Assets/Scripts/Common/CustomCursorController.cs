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
        instance.CreateReadableCursorTextures();
        instance.ApplyCursor(false);
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

        DestroyReadableCursor(ref standardCursor);
        DestroyReadableCursor(ref pressedCursor);
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

    private void CreateReadableCursorTextures()
    {
        standardCursor = CreateReadableCopy(theme.StandardCursor);
        pressedCursor = CreateReadableCopy(theme.PressedCursor);
    }

    private static Texture2D CreateReadableCopy(Texture2D source)
    {
        if (source == null)
        {
            return null;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);

        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;

        Texture2D copy = new Texture2D(
            source.width,
            source.height,
            TextureFormat.RGBA32,
            false);
        copy.name = source.name;
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        return copy;
    }

    private static void DestroyReadableCursor(ref Texture2D cursorTexture)
    {
        if (cursorTexture == null)
        {
            return;
        }

        Destroy(cursorTexture);
        cursorTexture = null;
    }
}
