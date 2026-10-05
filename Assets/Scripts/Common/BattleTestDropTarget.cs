using UnityEngine;
using UnityEngine.EventSystems;

public enum BattleTestDropKind { Collection, Trash, World, ItemSlot }

public sealed class BattleTestDropTarget : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private BattleTestInteractions interactions;
    [SerializeField] private BattleTestDropKind kind;
    [SerializeField] private int slot;

    internal BattleTestDropKind Kind => kind;
    internal int Slot => slot;
    public void OnDrop(PointerEventData eventData) => interactions.Drop(this, eventData.position);
    public void OnPointerClick(PointerEventData eventData)
    {
        if (kind == BattleTestDropKind.World) interactions.SelectWorld(eventData.position);
    }
}
