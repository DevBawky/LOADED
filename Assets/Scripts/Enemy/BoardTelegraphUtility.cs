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
        Vector3 tileAxis = Vector3.right;
        float tileLength = boardManager.BoardDistance;

        if (boardManager.BoardCount > 1
            && boardManager.TryGetTilePosition(0, out Vector3 firstPosition)
            && boardManager.TryGetTilePosition(1, out Vector3 secondPosition))
        {
            Vector3 tileStep = secondPosition - firstPosition;

            if (tileStep.sqrMagnitude > Mathf.Epsilon)
            {
                tileAxis = tileStep.normalized;
                tileLength = tileStep.magnitude;
            }
        }

        // Extend from tile centres to tile boundaries so the rendered range
        // always covers complete board tiles, including a single-tile range.
        Vector3 halfTile = tileAxis * (tileLength * 0.5f);
        startPosition -= halfTile;
        endPosition += halfTile;
        startPosition.y += verticalOffset;
        endPosition.y += verticalOffset;

        line.SetPosition(0, startPosition);
        line.SetPosition(1, endPosition);
        return line;
    }
}
