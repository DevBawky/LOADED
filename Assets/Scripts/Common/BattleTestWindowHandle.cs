using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleTestWindowHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform window;
    [SerializeField] private GameObject content;
    [SerializeField] private UnityEngine.UI.Button collapseButton;
    [SerializeField] private TMPro.TMP_Text collapseLabel;
    private Canvas canvas;
    private Vector2 homePosition;
    private void OnEnable() => collapseButton.onClick.AddListener(ToggleCollapse);
    private void OnDisable() => collapseButton.onClick.RemoveListener(ToggleCollapse);
    private void ToggleCollapse()
    {
        bool expanded = !content.activeSelf;
        content.SetActive(expanded);
        window.GetComponent<UnityEngine.UI.Image>().enabled = expanded;
        collapseLabel.text = expanded ? "접기" : "펼치기";
    }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        homePosition = window.anchoredPosition;
    }
    public void OnBeginDrag(PointerEventData eventData) => window.SetAsLastSibling();
    public void OnDrag(PointerEventData eventData)
    {
        window.anchoredPosition += eventData.delta / canvas.scaleFactor;
        var corners = new Vector3[4];
        window.GetWorldCorners(corners);
        Vector2 shift = Vector2.zero;
        if (corners[0].x < 0) shift.x = -corners[0].x;
        if (corners[2].x > Screen.width) shift.x = Screen.width - corners[2].x;
        if (corners[0].y < 0) shift.y = -corners[0].y;
        if (corners[2].y > Screen.height) shift.y = Screen.height - corners[2].y;
        window.anchoredPosition += shift / canvas.scaleFactor;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            window.anchoredPosition = homePosition;
    }
}
