using UnityEngine;

public static class BoardTelegraphUtility
{
    public static LineRenderer CreateTileRange(
        Transform parent,
        string objectName,
        BoardManager boardManager,
        int firstTileIndex,
        int lastTileIndex,
        Material material,
        Color color,
        float verticalOffset,
        int sortingOrder)
    {
        if (parent == null || boardManager == null)
        {
            return null;
        }

        int minimumIndex = Mathf.Clamp(
            Mathf.Min(firstTileIndex, lastTileIndex),
            0,
            boardManager.BoardCount - 1);
        int maximumIndex = Mathf.Clamp(
            Mathf.Max(firstTileIndex, lastTileIndex),
            0,
            boardManager.BoardCount - 1);

        if (!boardManager.TryGetTilePosition(
                minimumIndex,
                out Vector3 startPosition)
            || !boardManager.TryGetTilePosition(
                maximumIndex,
                out Vector3 endPosition))
        {
            return null;
        }

        GameObject telegraphObject = new GameObject(objectName);
        telegraphObject.transform.SetParent(parent, true);
        LineRenderer line = telegraphObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 4;
        line.positionCount = 2;
        line.widthMultiplier = boardManager.BoardDistance * 0.72f;
        line.sortingOrder = sortingOrder;
        line.sharedMaterial = material;
        line.startColor = color;
        line.endColor = color;
        startPosition.y += verticalOffset;
        endPosition.y += verticalOffset;

        if (minimumIndex == maximumIndex)
        {
            Vector3 halfStep = Vector3.right
                * (boardManager.BoardDistance * 0.02f);
            startPosition -= halfStep;
            endPosition += halfStep;
        }

        line.SetPosition(0, startPosition);
        line.SetPosition(1, endPosition);
        return line;
    }
}
