using UnityEngine;

public sealed class CustomCursorTheme : ScriptableObject
{
    [SerializeField] private Texture2D standardCursor;
    [SerializeField] private Texture2D pressedCursor;
    [SerializeField] private Vector2 hotspot;
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    public Texture2D StandardCursor => standardCursor;
    public Texture2D PressedCursor => pressedCursor;
    public Vector2 Hotspot => hotspot;
    public CursorMode CursorMode => cursorMode;
}
