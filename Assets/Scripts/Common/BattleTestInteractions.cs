using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Transient pointer state only. Every mutation uses the existing validated test commands.
public sealed class BattleTestInteractions : MonoBehaviour
{
    [SerializeField] private BattleTestController controller;
    [SerializeField] private BattleTestConsole console;
    [SerializeField] private BattleTestGui gui;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform boardPlane;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private RectTransform ghost;
    [SerializeField] private UnityEngine.UI.Image ghostIcon;
    [SerializeField] private TMP_Text ghostText;
    [SerializeField] private RectTransform marker;
    [SerializeField] private TMP_Text markerText;
    [SerializeField] private RectTransform menu;
    [SerializeField] private UnityEngine.UI.Button[] menuButtons;
    [SerializeField] private TMP_Text[] menuLabels;
    private BattleTestListRow hovered;
    private BattleTestListRow dragged;
    private BattleTestControlTooltip hoveredControl;
    private string controlExplanation;
    private float hoverAt;
    private Vector2 pointerPosition;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    internal bool IsDragging => dragged != null;
    internal bool TooltipVisible => tooltip.gameObject.activeSelf;

    private void OnEnable() => console.CommandExecuted += HandleCommand;
    private void OnDisable() { console.CommandExecuted -= HandleCommand; Cancel(); }
    private void HandleCommand(string _) => Cancel();

    private void Update()
    {
        if (!console.IsOpen) { Cancel(); return; }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        if (Mouse.current != null) pointerPosition = Mouse.current.position.ReadValue();
        if (IsDragging) MoveDrag(pointerPosition);
        else if ((hoveredControl != null || hovered != null && !menu.gameObject.activeSelf) && Time.unscaledTime >= hoverAt)
        {
            tooltipText.text = hoveredControl != null ? controlExplanation : (hovered.Title + "\n" + hovered.Subtitle + "\n" + hovered.Description
                + "\n<color=#84CBB8>드래그로 옮기기 · 우클릭 조작\n더블클릭 빠른 실행</color>").Replace("DUEL CLOCK", "행동 기반 전투");
            tooltip.sizeDelta = new Vector2(440, Mathf.Min(580, tooltipText.GetPreferredValues(tooltipText.text, 412, 0).y + 32));
            tooltip.gameObject.SetActive(true);
            Position(tooltip, pointerPosition + new Vector2(20, -16));
        }
        if (menu.gameObject.activeSelf && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && !RectTransformUtility.RectangleContainsScreenPoint(menu, pointerPosition))
            menu.gameObject.SetActive(false);
    }

    internal void Hover(BattleTestListRow row, Vector2 point)
    {
        if (IsDragging) return;
        hovered = row; pointerPosition = point; hoverAt = Time.unscaledTime + .35f;
    }
    internal void HoverControl(BattleTestControlTooltip control, string explanation, Vector2 point)
    {
        if (IsDragging) return;
        hoveredControl = control; controlExplanation = explanation;
        pointerPosition = point; hoverAt = Time.unscaledTime + .35f;
        tooltip.gameObject.SetActive(false);
    }
    internal void LeaveControl(BattleTestControlTooltip control)
    {
        if (hoveredControl != control) return;
        hoveredControl = null; tooltip.gameObject.SetActive(false);
    }
    internal void Inspect(string text)
    {
        Cancel();
        tooltipText.text = text.Replace("DUEL CLOCK", "행동 기반 전투");
        tooltip.sizeDelta = new Vector2(440, Mathf.Min(580, tooltipText.GetPreferredValues(text, 412, 0).y + 32));
        tooltip.gameObject.SetActive(true);
        Position(tooltip, pointerPosition + new Vector2(20, -16));
    }
    internal void Leave(BattleTestListRow row)
    {
        if (hovered != row) return;
        hovered = null; tooltip.gameObject.SetActive(false);
    }
    internal void ShowMenu(BattleTestListRow row, Vector2 point)
    {
        Cancel();
        int count = 0;
        for (int i = 0; i < row.Actions.Length; i++)
        {
            UnityEngine.UI.Button source = row.Actions[i];
            UnityEngine.UI.Button button = menuButtons[i];
            button.onClick.RemoveAllListeners();
            bool visible = source.gameObject.activeSelf;
            button.gameObject.SetActive(visible);
            if (!visible) continue;
            count++;
            button.interactable = source.interactable;
            menuLabels[i].text = row.ActionLabel(i);
            BattleTestControlTooltip help = button.GetComponent<BattleTestControlTooltip>()
                ?? button.gameObject.AddComponent<BattleTestControlTooltip>();
            help.Configure(this, BattleTestHelpText.For(row.ActionLabel(i)));
            button.onClick.AddListener(() =>
            {
                menu.gameObject.SetActive(false);
                if (source != null && source.interactable) source.onClick.Invoke();
            });
        }
        menu.sizeDelta = new Vector2(220, count * 42 + 12);
        menu.gameObject.SetActive(true);
        Position(menu, point);
    }

    internal void BeginDrag(BattleTestListRow row, Vector2 point)
    {
        Cancel();
        if (!console.IsOpen || !controller.IsSettled || row.Kind == BattleTestDragKind.None) return;
        dragged = row;
        ghostIcon.sprite = row.Icon;
        ghostIcon.enabled = row.Icon != null;
        ghostText.text = row.Title;
        ghost.gameObject.SetActive(true);
        MoveDrag(point);
    }

    internal void MoveDrag(Vector2 point)
    {
        if (!IsDragging) return;
        pointerPosition = point;
        Position(ghost, point + new Vector2(18, -18));
        marker.gameObject.SetActive(false);
        var pointer = new PointerEventData(EventSystem.current) { position = point };
        hits.Clear();
        EventSystem.current.RaycastAll(pointer, hits);
        BattleTestDropTarget target = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<BattleTestDropTarget>() : null;
        BattleTestListRow row = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<BattleTestListRow>() : null;
        bool valid = target != null && Accepts(dragged.Kind, target.Kind);
        if (row != null && !row.IsCatalog) valid = Accepts(dragged.Kind,
            row.Kind == BattleTestDragKind.Item ? BattleTestDropKind.ItemSlot : BattleTestDropKind.Collection);
        string hint = valid ? "놓아서 적용" : "알맞은 대상에 놓으세요";
        if (target != null && target.Kind == BattleTestDropKind.World)
        {
            valid = valid && TryCell(point, out _, out _);
            if (TryCell(point, out int tile, out int lane))
            {
                valid &= IsVacant(tile, lane);
                controller.TestBoard.TryGetTilePosition(tile, lane, out Vector3 world);
                marker.gameObject.SetActive(true);
                Position(marker, worldCamera.WorldToScreenPoint(world));
                markerText.text = $"{tile}칸 · {lane}레인\n{(valid ? "배치 가능" : "배치 불가")}";
                hint = valid ? "놓아서 배치" : "빈칸에 놓으세요";
            }
        }
        ghost.GetComponent<UnityEngine.UI.Image>().color = valid
            ? new Color(.08f, .34f, .28f, .98f) : new Color(.34f, .16f, .16f, .98f);
        ghostText.text = dragged.Title + "\n" + hint;
    }

    internal static bool Accepts(BattleTestDragKind source, BattleTestDropKind target) => target switch
    {
        BattleTestDropKind.Collection => source == BattleTestDragKind.BulletCatalog || source == BattleTestDragKind.RelicCatalog,
        BattleTestDropKind.Trash => source == BattleTestDragKind.Bullet || source == BattleTestDragKind.Relic
            || source == BattleTestDragKind.Enemy || source == BattleTestDragKind.Item,
        BattleTestDropKind.World => source == BattleTestDragKind.EnemyCatalog || source == BattleTestDragKind.Player,
        BattleTestDropKind.ItemSlot => source == BattleTestDragKind.ItemCatalog,
        _ => false
    };

    internal void Drop(BattleTestDropTarget target, Vector2 point) => CommitDrop(target.Kind, target.Slot, point);
    internal void DropOnRow(BattleTestListRow target, Vector2 point) => CommitDrop(
        target.Kind == BattleTestDragKind.Item ? BattleTestDropKind.ItemSlot : BattleTestDropKind.Collection, target.Id, point);

    internal void CommitDrop(BattleTestDropKind target, int slot, Vector2 point)
    {
        if (!IsDragging) return;
        BattleTestDragKind kind = dragged.Kind;
        int id = dragged.Id;
        if (!Accepts(kind, target)) { Cancel(); return; }
        string command = null;
        switch (target)
        {
            case BattleTestDropKind.Collection:
                command = kind == BattleTestDragKind.BulletCatalog ? $"bullet add {id} {gui.AddedBulletLevel}" : $"relic add {id}";
                break;
            case BattleTestDropKind.ItemSlot: command = $"item set {slot} {id}"; break;
            case BattleTestDropKind.Trash:
                command = kind switch
                {
                    BattleTestDragKind.Bullet => $"bullet remove {id}",
                    BattleTestDragKind.Relic => $"relic remove {id}",
                    BattleTestDragKind.Item => $"item remove {id}",
                    _ => gui.EnemyRemovalCommand(id)
                };
                break;
            case BattleTestDropKind.World:
                if (TryCell(point, out int tile, out int lane))
                    command = kind == BattleTestDragKind.Player ? $"player {tile} {lane}" : $"spawn {id} {tile} {lane}";
                break;
        }
        Cancel();
        if (command != null) console.RunCommand(command);
    }

    internal bool TryCell(Vector2 point, out int tile, out int lane)
    {
        tile = lane = -1;
        if (!controller.IsReady || worldCamera == null || boardPlane == null) return false;
        Ray ray = worldCamera.ScreenPointToRay(point);
        var plane = new Plane(boardPlane.forward, boardPlane.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 local = boardPlane.InverseTransformPoint(ray.GetPoint(distance));
        BoardManager board = controller.TestBoard;
        for (int y = 0; y < board.LaneCount; y++)
        for (int x = 0; x < board.BoardCount; x++)
        {
            board.TryGetTilePosition(x, y, out Vector3 center);
            Vector3 delta = local - boardPlane.InverseTransformPoint(center);
            if (Mathf.Abs(delta.x) <= board.BoardDistance * .5f && Mathf.Abs(delta.y) <= board.LaneDistance * .5f)
            { tile = x; lane = y; return true; }
        }
        return false;
    }

    private bool IsVacant(int tile, int lane)
    {
        if (controller.TestWaves.IsTileOccupied(tile, lane)) return false;
        PlayerMove player = controller.TestPlayer;
        controller.TestBoard.TryGetTileIndex(player.transform.position, player.CurrentLaneIndex, out int playerTile);
        return playerTile != tile || player.CurrentLaneIndex != lane;
    }

    internal void SelectWorld(Vector2 point)
    {
        Cancel();
        if (!controller.IsSettled || !TryCell(point, out int tile, out int lane)) return;
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
        {
            console.RunCommand($"player {tile} {lane}");
            return;
        }
        gui.SelectWorldCell(tile, lane);
    }

    internal void Cancel()
    {
        dragged = null; hovered = null; hoveredControl = null;
        tooltip.gameObject.SetActive(false); ghost.gameObject.SetActive(false);
        marker.gameObject.SetActive(false); menu.gameObject.SetActive(false);
    }

    private void Position(RectTransform popup, Vector2 screen)
    {
        popup.SetAsLastSibling();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        Rect bounds = canvasRect.rect;
        popup.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, bounds.xMin + 8, bounds.xMax - popup.rect.width - 8),
            Mathf.Clamp(local.y, bounds.yMin + popup.rect.height + 8, bounds.yMax - 8));
    }
}
