using TMPro;
using UnityEngine;

public class TurnCountText : MonoBehaviour
{
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private TextMeshProUGUI turnCountText;
    private int externalTurnCount;
    private bool useExternalTurnCount;

    public void SetExternalTurnCount(int turnCount)
    {
        externalTurnCount = Mathf.Max(0, turnCount);
        useExternalTurnCount = true;
        Refresh();
    }

    private void Awake()
    {
        if (turnCountText == null)
        {
            turnCountText = GetComponent<TextMeshProUGUI>();
        }

        Refresh();
    }

    private void OnEnable()
    {
        if (playerMove != null)
        {
            playerMove.TurnCountChanged += HandleTurnCountChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerMove != null)
        {
            playerMove.TurnCountChanged -= HandleTurnCountChanged;
        }
    }

    private void HandleTurnCountChanged(int turnCount)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (turnCountText == null)
        {
            return;
        }

        if (useExternalTurnCount)
        {
            turnCountText.text = $"Turn {externalTurnCount}";
            return;
        }

        if (playerMove != null)
        {
            turnCountText.text = $"Turn {playerMove.TurnCount}";
        }
    }
}
