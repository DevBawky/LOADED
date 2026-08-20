using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatControlPanelController : MonoBehaviour
{
    [SerializeField] private GameObject controlPanel;

    private void Awake()
    {
        ResolveControlPanel();
        EnsureControlPanelActive();
    }

    private void OnEnable()
    {
        EnsureControlPanelActive();
    }

    private void LateUpdate()
    {
        EnsureControlPanelActive();
    }

    public bool TryClose()
    {
        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        gameObject.SetActive(false);
        return true;
    }

    private void ResolveControlPanel()
    {
        if (controlPanel != null)
        {
            return;
        }

        foreach (Transform child in transform)
        {
            if (child.name == "Panel | Control")
            {
                controlPanel = child.gameObject;
                return;
            }
        }
    }

    private void EnsureControlPanelActive()
    {
        ResolveControlPanel();

        if (controlPanel != null && !controlPanel.activeSelf)
        {
            controlPanel.SetActive(true);
        }
    }
}
