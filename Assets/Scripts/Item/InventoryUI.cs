using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private Image[] itemImages;

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.Changed += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.Changed -= Refresh;
        }
    }

    private void Refresh()
    {
        if (playerInventory == null || itemImages == null)
        {
            return;
        }

        for (int index = 0; index < itemImages.Length; index++)
        {
            Image itemImage = itemImages[index];

            if (itemImage == null)
            {
                continue;
            }

            ItemData item = playerInventory.GetItem(index);
            itemImage.preserveAspect = true;
            itemImage.sprite = item != null ? item.Icon : null;
            itemImage.gameObject.SetActive(item != null && item.Icon != null);
        }
    }
}
