using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleTestControlTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private BattleTestInteractions interactions;
    [SerializeField, TextArea] private string explanation;
    internal string Explanation => explanation;
    internal void Configure(BattleTestInteractions owner, string text) { interactions = owner; explanation = text; }
    public void OnPointerEnter(PointerEventData eventData) => interactions?.HoverControl(this, explanation, eventData.position);
    public void OnPointerExit(PointerEventData eventData) => interactions?.LeaveControl(this);
    private void OnDisable() => interactions?.LeaveControl(this);
}
