using UnityEngine;
using UnityEngine.UI;

public class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject behaviourTilePanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;

    private void Awake()
    {
        RefreshButtonSprite();
    }

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
        RefreshButtonSprite();
    }

    private void RefreshButtonSprite()
    {
        if (buttonImage == null || inventoryPanel == null)
        {
            return;
        }

        buttonImage.sprite = inventoryPanel.activeSelf ? openedSprite : closedSprite;
    }
}
