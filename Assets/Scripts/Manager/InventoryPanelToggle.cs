using UnityEngine;

public class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject behaviourTilePanel;
    [SerializeField] private GameObject inventoryPanel;

    public void Toggle()
    {
        if (behaviourTilePanel == null || inventoryPanel == null)
        {
            Debug.LogError("InventoryPanelToggle panel references are missing.", this);
            return;
        }

        bool showInventory = !inventoryPanel.activeSelf;

        behaviourTilePanel.SetActive(!showInventory);
        inventoryPanel.SetActive(showInventory);
    }
}
