using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryTooltipUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private RectTransform[] itemSlots;
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private float slotGap = 10f;

    private readonly Vector3[] slotCorners = new Vector3[4];

    private void OnEnable()
    {
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused
            || playerInventory == null
            || inventoryPanel == null
            || itemSlots == null
            || !inventoryPanel.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            Hide();
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        for (int index = 0; index < itemSlots.Length; index++)
        {
            RectTransform itemSlot = itemSlots[index];
            ItemData item = playerInventory.GetItem(index);

            if (itemSlot == null || item == null)
            {
                continue;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(itemSlot, mousePosition))
            {
                continue;
            }

            Show(item, itemSlot);
            return;
        }

        Hide();
    }

    private void Show(ItemData item, RectTransform itemSlot)
    {
        if (tooltip == null || itemNameText == null || itemDescriptionText == null || canvasRect == null)
        {
            return;
        }

        itemNameText.text = string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName;
        itemDescriptionText.text = item.Description;

        itemSlot.GetWorldCorners(slotCorners);
        Vector3 slotTopCenter = (slotCorners[1] + slotCorners[2]) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(null, slotTopCenter),
            null,
            out Vector2 localSlotTopCenter);

        Vector2 anchoredPosition = localSlotTopCenter;
        anchoredPosition.y += slotGap + tooltip.rect.height * tooltip.pivot.y;
        tooltip.anchoredPosition = anchoredPosition;
        tooltip.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (tooltip != null && tooltip.gameObject.activeSelf)
        {
            tooltip.gameObject.SetActive(false);
        }
    }
}
