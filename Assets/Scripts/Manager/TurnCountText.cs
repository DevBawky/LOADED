using TMPro;
using UnityEngine;

public class TurnCountText : MonoBehaviour
{
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private TextMeshProUGUI turnCountText;

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
            playerMove.TurnCompleted += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerMove != null)
        {
            playerMove.TurnCompleted -= Refresh;
        }
    }

    private void Refresh()
    {
        if (playerMove == null || turnCountText == null)
        {
            return;
        }

        turnCountText.text = $"Turn {playerMove.TurnCount}";
    }
}
