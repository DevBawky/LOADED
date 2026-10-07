using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public enum BattleTestDragKind { None, BulletCatalog, Bullet, RelicCatalog, Relic, EnemyCatalog, Enemy, ItemCatalog, Item, Player }

/// <summary>A disposable list view; callbacks always target the real combat owners.</summary>
public sealed class BattleTestListRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private UnityEngine.UI.Button[] actions;
    [SerializeField] private TMP_Text[] actionLabels;
    private BattleTestInteractions interactions;
    private System.Func<string> description;
    private int primaryAction;
    private System.Action select;
    private CanvasGroup actionGroup;
    internal BattleTestDragKind Kind { get; private set; }
    internal int Id { get; private set; }
    internal bool IsCatalog { get; private set; }
    internal string Title => title.text;
    internal string Subtitle => subtitle.text;
    internal Sprite Icon => icon.sprite;
    internal string Description => description?.Invoke() ?? subtitle.text;
    internal UnityEngine.UI.Button[] Actions => actions;
    internal string ActionLabel(int index) => actionLabels[index].text;
    internal void OnSelect(System.Action action) => select = action;

    internal void ConfigureInteraction(BattleTestInteractions owner, BattleTestDragKind kind, int id,
        bool catalog, int primary, System.Func<string> tooltip)
    {
        interactions = owner; Kind = kind; Id = id; IsCatalog = catalog;
        primaryAction = primary; description = tooltip;
        for (int i = 0; i < actions.Length; i++)
        {
            BattleTestControlTooltip help = actions[i].GetComponent<BattleTestControlTooltip>()
                ?? actions[i].gameObject.AddComponent<BattleTestControlTooltip>();
            help.Configure(owner, BattleTestHelpText.For(actionLabels[i].text));
        }
        actionGroup = actions[0].transform.parent.GetComponent<CanvasGroup>();
        SetHovered(false);
    }

    private void SetHovered(bool value)
    {
        if (actionGroup == null) return;
        actionGroup.alpha = value ? 1 : 0;
        actionGroup.blocksRaycasts = value;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        interactions?.Hover(this, eventData.position);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
        interactions?.Leave(this);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) interactions?.ShowMenu(this, eventData.position);
        else if (eventData.clickCount == 2 && actions[primaryAction].interactable)
            actions[primaryAction].onClick.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Left) select?.Invoke();
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) interactions?.BeginDrag(this, eventData.position);
    }
    public void OnDrag(PointerEventData eventData) => interactions?.MoveDrag(eventData.position);
    public void OnEndDrag(PointerEventData eventData) => interactions?.Cancel();
    public void OnDrop(PointerEventData eventData)
    {
        if (!IsCatalog) interactions?.DropOnRow(this, eventData.position);
    }
    private void OnDisable()
    {
        interactions?.Leave(this);
        SetHovered(false);
    }

    internal void Bind(string nameText, string description, Sprite sprite, BulletData bullet = null)
    {
        title.text = nameText;
        // The entire card, including icon padding and empty space, is the pointer target.
        GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        subtitle.text = description;
        icon.material = null;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        if (bullet != null) BulletIconPresenter.Apply(icon, bullet, true);
        foreach (UnityEngine.UI.Button button in actions)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
    }

    internal void Action(int index, string label, UnityAction action, bool available = true)
    {
        UnityEngine.UI.Button button = actions[index];
        button.gameObject.SetActive(true);
        button.interactable = available;
        actionLabels[index].text = label;
        button.onClick.AddListener(action);
    }
}
