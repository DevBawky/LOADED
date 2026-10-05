using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TMP_InputField))]
public sealed class BattleTestNumberField : MonoBehaviour, IScrollHandler
{
    private TMP_InputField input;
    private void Awake() => input = GetComponent<TMP_InputField>();
    public void OnScroll(PointerEventData eventData)
    {
        if (eventData.scrollDelta.y == 0) return;
        int.TryParse(input.text, out int value);
        Keyboard keyboard = Keyboard.current;
        int step = keyboard != null && keyboard.ctrlKey.isPressed ? 100
            : keyboard != null && keyboard.shiftKey.isPressed ? 10 : 1;
        long next = (long)value + (eventData.scrollDelta.y > 0 ? step : -step);
        input.text = System.Math.Clamp(next, 0L, int.MaxValue).ToString();
        eventData.Use();
    }
}
