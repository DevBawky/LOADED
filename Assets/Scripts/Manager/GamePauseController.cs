using UnityEngine;
using UnityEngine.InputSystem;

public class GamePauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausedPanel;

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        SetPaused(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    private void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;

        if (pausedPanel != null)
        {
            pausedPanel.SetActive(isPaused);
        }
    }
}
