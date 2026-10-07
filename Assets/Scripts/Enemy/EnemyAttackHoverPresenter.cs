using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Connects pointer inspection to existing attack intent, without changing it.</summary>
internal sealed class EnemyAttackHoverPresenter
{
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private Camera battleCamera;
    private EventSystem eventSystem;
    private PointerEventData pointerData;
    private BoardManager board;
    private EnemyController hoveredEnemy;

    public void Update(BoardManager currentBoard, IReadOnlyList<EnemyController> enemies,
        bool battleCompleted)
    {
        if (battleCompleted || GamePauseController.IsPaused || !Application.isFocused
            || Mouse.current == null || currentBoard == null || enemies.Count == 0)
        {
            Clear();
            return;
        }

        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }
        if (battleCamera == null || !battleCamera.isActiveAndEnabled)
        {
            Clear();
            return;
        }

        Vector2 pointer = Mouse.current.position.ReadValue();
        if (pointer.x < 0f || pointer.y < 0f || pointer.x >= Screen.width || pointer.y >= Screen.height)
        {
            Clear();
            return;
        }

        Vector2 viewport = new Vector2(pointer.x / Screen.width, pointer.y / Screen.height);
        if (TryResolveUI(enemies, pointer, ref viewport, out EnemyController uiEnemy))
        {
            SetHovered(currentBoard, uiEnemy);
            return;
        }

        Ray ray = battleCamera.targetTexture == null
            ? battleCamera.ScreenPointToRay(pointer)
            : battleCamera.ViewportPointToRay(viewport);
        SetHovered(currentBoard, FindWorldTarget(enemies, ray));
    }

    private bool TryResolveUI(IReadOnlyList<EnemyController> enemies, Vector2 pointer,
        ref Vector2 viewport, out EnemyController enemy)
    {
        enemy = null;
        if (EventSystem.current == null)
        {
            return false;
        }
        if (eventSystem != EventSystem.current)
        {
            eventSystem = EventSystem.current;
            pointerData = new PointerEventData(eventSystem);
        }
        pointerData.Reset();
        pointerData.position = pointer;
        uiHits.Clear();
        eventSystem.RaycastAll(pointerData, uiHits);
        if (uiHits.Count == 0)
        {
            return false;
        }

        RaycastResult hit = uiHits[0];
        enemy = FindUIOwner(enemies, hit.gameObject.transform);
        if (enemy != null)
        {
            return true;
        }

        // The battle may be displayed through a RenderTexture RawImage.
        // Its pixels belong to the battlefield; menus and other UI still block hover.
        if (hit.gameObject.TryGetComponent(out RawImage image)
            && battleCamera.targetTexture != null && image.texture == battleCamera.targetTexture
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,
                pointer, hit.module.eventCamera, out Vector2 local))
        {
            Rect rect = image.rectTransform.rect;
            viewport = new Vector2(
                image.uvRect.x + (local.x - rect.xMin) / rect.width * image.uvRect.width,
                image.uvRect.y + (local.y - rect.yMin) / rect.height * image.uvRect.height);
            return false;
        }
        return true;
    }

    internal static EnemyController FindUIOwner(
        IReadOnlyList<EnemyController> enemies, Transform hit)
    {
        if (hit == null)
        {
            return null;
        }
        foreach (EnemyController enemy in enemies)
        {
            if (IsInspectable(enemy) && hit.IsChildOf(enemy.transform))
            {
                return enemy;
            }
        }
        return null;
    }

    internal static EnemyController FindWorldTarget(IReadOnlyList<EnemyController> enemies, Ray ray)
    {
        EnemyController best = null;
        int bestLayer = int.MinValue;
        int bestOrder = int.MinValue;
        float bestDistance = float.MaxValue;
        foreach (EnemyController enemy in enemies)
        {
            if (!IsInspectable(enemy))
            {
                continue;
            }
            SpriteRenderer renderer = enemy.HoverRenderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || renderer.sprite == null || !renderer.bounds.IntersectRay(ray, out float distance))
            {
                continue;
            }
            int layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
            int order = enemy.HoverSortingOrder;
            if (best == null || layer > bestLayer || layer == bestLayer &&
                (order > bestOrder || order == bestOrder && distance < bestDistance))
            {
                best = enemy;
                bestLayer = layer;
                bestOrder = order;
                bestDistance = distance;
            }
        }
        return best;
    }

    internal void SetHovered(BoardManager currentBoard, EnemyController enemy)
    {
        if (board != currentBoard)
        {
            Clear();
            board = currentBoard;
        }
        if (!IsInspectable(enemy))
        {
            enemy = null;
        }
        if (!ReferenceEquals(hoveredEnemy, enemy))
        {
            if (hoveredEnemy != null)
            {
                hoveredEnemy.ActionQueueView?.SetHovered(false);
            }
            hoveredEnemy = enemy;
        }
        if (hoveredEnemy != null)
        {
            hoveredEnemy.ActionQueueView?.SetHovered(true);
        }
        if (board != null)
        {
            board.SetWarningFocus(hoveredEnemy);
        }
    }

    public void Clear()
    {
        SetHovered(board, null);
    }

    private static bool IsInspectable(EnemyController enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0;
    }
}
