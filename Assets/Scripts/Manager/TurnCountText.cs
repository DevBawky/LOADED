using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class TurnCountText : MonoBehaviour
{
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private StateManager stateManager;
    [FormerlySerializedAs("turnCountText")]
    [SerializeField] private TextMeshProUGUI countText;
    private int externalCount;
    private bool useExternalCount;

    public void SetExternalTurnCount(int turnCount)
    {
        SetExternalCount(turnCount);
    }

    public void SetExternalCount(int count)
    {
        externalCount = Mathf.Max(0, count);
        useExternalCount = true;
        Refresh();
    }

    private void Awake()
    {
        if (countText == null)
        {
            countText = GetComponent<TextMeshProUGUI>();
        }

        ResolveCountSources();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveCountSources();

        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted += HandleCountCompleted;
        }

        if (stateManager != null)
        {
            stateManager.StateChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (waveManager != null)
        {
            waveManager.EnemyTurnCycleCompleted -= HandleCountCompleted;
        }

        if (stateManager != null)
        {
            stateManager.StateChanged -= Refresh;
        }
    }

    private void HandleCountCompleted(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (countText == null)
        {
            return;
        }

        int count = useExternalCount
            ? externalCount
            : stateManager != null
                ? stateManager.CumulativeBattleCount
                : waveManager == null
                    ? 0
                    : waveManager.CurrentEnemyTurnCycle;
        countText.text = FormatCount(count);
    }

    private void ResolveCountSources()
    {
        waveManager ??= FindFirstObjectByType<WaveManager>(
            FindObjectsInactive.Include);
        stateManager ??= FindFirstObjectByType<StateManager>(
            FindObjectsInactive.Include);
    }

    internal static string FormatCount(int count)
    {
        return $"COUNT {Mathf.Max(0, count)}";
    }
}
