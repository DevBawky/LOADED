using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField, Min(1)] private int boardCount = 1;
    [SerializeField, Min(1)] private int laneCount =
        StageData.DefaultLaneCount;
    [SerializeField, Min(0.01f)] private float boardDistance = 1f;
    [SerializeField, Min(0.01f)] private float laneDistance = 0.92f;

    [Header("References")]
    [SerializeField] private BoardTile tilePrefab;
    [FormerlySerializedAs("spawnOrigin")]
    [SerializeField] private Transform tileParent;

    private bool isGenerated;
    private Component focusedWarningOwner;
    private readonly HashSet<int> urgentWarningOwners = new HashSet<int>();
    private readonly List<BoardTile> spawnedTiles = new List<BoardTile>();
    private readonly HashSet<int> persistentWarningCells = new HashSet<int>();
    private readonly Dictionary<int, HashSet<int>> warningOwnerIdsByCell =
        new Dictionary<int, HashSet<int>>();

    public int BoardCount => boardCount;
    public int LaneCount => laneCount;
    public int TotalTileCount => boardCount * laneCount;
    public float BoardDistance => boardDistance;
    public float LaneDistance => laneDistance;
    private int LaneColumnOffset => boardCount > 1 ? 1 : 0;

    public bool ConfigureBoard(
        int configuredBoardCount,
        BoardTile configuredTilePrefab)
    {
        return ConfigureBoard(
            configuredBoardCount,
            laneCount,
            configuredTilePrefab);
    }

    public bool ConfigureBoard(
        int configuredBoardCount,
        int configuredLaneCount,
        BoardTile configuredTilePrefab)
    {
        if (configuredBoardCount <= 0 || configuredLaneCount <= 0
            || configuredTilePrefab == null)
        {
            Debug.LogError(
                "Board Count and Lane Count must be greater than zero, "
                + "and Tile Prefab must be assigned.",
                this);
            return false;
        }

        boardCount = configuredBoardCount;
        laneCount = configuredLaneCount;
        tilePrefab = configuredTilePrefab;
        RegenerateBoard();
        return isGenerated;
    }

    public bool SetTileWarningActive(int tileIndex, bool isActive)
    {
        if (tileIndex < 0 || tileIndex >= boardCount)
        {
            return false;
        }

        bool changedAny = false;

        for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            changedAny |= SetTileWarningActive(
                tileIndex,
                laneIndex,
                isActive);
        }

        return changedAny;
    }

    public bool SetTileWarningActive(
        int tileIndex,
        int laneIndex,
        bool isActive)
    {
        int spawnedTileIndex = GetSpawnedTileIndex(tileIndex, laneIndex);

        if (spawnedTileIndex < 0 || spawnedTileIndex >= spawnedTiles.Count
            || spawnedTiles[spawnedTileIndex] == null)
        {
            return false;
        }

        if (isActive)
        {
            persistentWarningCells.Add(spawnedTileIndex);
        }
        else
        {
            persistentWarningCells.Remove(spawnedTileIndex);
        }

        RefreshTileWarning(spawnedTileIndex);
        return true;
    }

    public bool SetTileWarningActive(
        int tileIndex,
        int laneIndex,
        Component owner,
        bool isActive)
    {
        if (owner == null)
        {
            return false;
        }

        int spawnedTileIndex = GetSpawnedTileIndex(tileIndex, laneIndex);

        if (spawnedTileIndex < 0 || spawnedTileIndex >= spawnedTiles.Count
            || spawnedTiles[spawnedTileIndex] == null)
        {
            return false;
        }

        if (!warningOwnerIdsByCell.TryGetValue(
                spawnedTileIndex,
                out HashSet<int> ownerIds))
        {
            ownerIds = new HashSet<int>();
            warningOwnerIdsByCell.Add(spawnedTileIndex, ownerIds);
        }

        if (isActive)
        {
            ownerIds.Add(owner.GetInstanceID());
        }
        else
        {
            ownerIds.Remove(owner.GetInstanceID());

            if (ownerIds.Count == 0)
            {
                warningOwnerIdsByCell.Remove(spawnedTileIndex);
            }
        }

        RefreshTileWarning(spawnedTileIndex);
        return true;
    }

    public void ReleaseTileWarnings(Component owner)
    {
        if (owner == null)
        {
            return;
        }

        if (ReferenceEquals(focusedWarningOwner, owner))
        {
            SetWarningFocus(null);
        }
        int ownerId = owner.GetInstanceID();
        urgentWarningOwners.Remove(ownerId);
        List<int> changedCells = new List<int>();

        foreach (KeyValuePair<int, HashSet<int>> warningOwners
                 in warningOwnerIdsByCell)
        {
            if (warningOwners.Value.Remove(ownerId))
            {
                changedCells.Add(warningOwners.Key);
            }
        }

        foreach (int cellIndex in changedCells)
        {
            if (warningOwnerIdsByCell.TryGetValue(
                    cellIndex,
                    out HashSet<int> ownerIds)
                && ownerIds.Count == 0)
            {
                warningOwnerIdsByCell.Remove(cellIndex);
            }

            RefreshTileWarning(cellIndex);
        }
    }

    internal void SetWarningFocus(Component owner)
    {
        if (ReferenceEquals(focusedWarningOwner, owner))
        {
            return;
        }
        focusedWarningOwner = owner;
        for (int index = 0; index < spawnedTiles.Count; index++)
        {
            RefreshTileWarning(index);
        }
    }

    internal void SetWarningUrgent(Component owner, bool urgent)
    {
        if (owner == null)
        {
            return;
        }
        int ownerId = owner.GetInstanceID();
        bool changed = urgent ? urgentWarningOwners.Add(ownerId) : urgentWarningOwners.Remove(ownerId);
        if (!changed)
        {
            return;
        }
        foreach (var entry in warningOwnerIdsByCell)
        {
            if (entry.Value.Contains(ownerId))
            {
                RefreshTileWarning(entry.Key);
            }
        }
    }

    // Normal completion leaves a visual tail; cancellation still releases immediately.
    internal void CompleteTileWarnings(Component owner)
    {
        if (owner == null)
        {
            return;
        }
        int ownerId = owner.GetInstanceID();
        var completedCells = new List<int>();
        foreach (var entry in warningOwnerIdsByCell)
        {
            if (entry.Value.Contains(ownerId))
            {
                completedCells.Add(entry.Key);
            }
        }
        ReleaseTileWarnings(owner);
        foreach (int index in completedCells)
        {
            if (index < spawnedTiles.Count && spawnedTiles[index] != null)
            {
                spawnedTiles[index].PlayWarningAfterglow();
            }
        }
    }

    internal bool SetTilePreviewColor(int tileIndex, int laneIndex, Color? color)
    {
        int index = GetSpawnedTileIndex(tileIndex, laneIndex);
        if (index < 0 || index >= spawnedTiles.Count || spawnedTiles[index] == null)
        {
            return false;
        }
        spawnedTiles[index].SetPreviewColor(color);
        return true;
    }

    public bool TryGetTilePosition(int tileIndex, out Vector3 worldPosition)
    {
        return TryGetTilePosition(tileIndex, 0, out worldPosition);
    }

    public bool TryGetTilePosition(
        int tileIndex,
        int laneIndex,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (tileParent == null || boardCount <= 0 || laneCount <= 0
            || boardDistance <= 0f || laneDistance <= 0f
            || tileIndex < 0 || tileIndex >= boardCount
            || laneIndex < 0 || laneIndex >= laneCount)
        {
            return false;
        }

        float positionX = GetStartOffset(laneIndex) + boardDistance * tileIndex;
        float positionY = GetLaneStartOffset() + laneDistance * laneIndex;
        worldPosition = tileParent.TransformPoint(
            new Vector3(positionX, positionY, 0f));
        return true;
    }

    public bool TryGetTileIndex(Vector3 worldPosition, out int tileIndex)
    {
        return TryGetTileIndex(worldPosition, 0, out tileIndex);
    }

    public bool TryGetTileIndex(
        Vector3 worldPosition,
        int laneIndex,
        out int tileIndex)
    {
        tileIndex = -1;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f
            || laneIndex < 0 || laneIndex >= laneCount)
        {
            return false;
        }

        Vector3 localPosition = tileParent.InverseTransformPoint(worldPosition);
        float rawIndex = (localPosition.x - GetStartOffset(laneIndex)) / boardDistance;
        int nearestIndex = Mathf.RoundToInt(rawIndex);

        if (nearestIndex < 0 || nearestIndex >= boardCount
            || Mathf.Abs(rawIndex - nearestIndex) > 0.5f)
        {
            return false;
        }

        tileIndex = nearestIndex;
        return true;
    }

    public bool TryGetTileDistance(
        Vector3 firstWorldPosition,
        Vector3 secondWorldPosition,
        out int tileDistance)
    {
        tileDistance = 0;

        if (tileParent == null || boardDistance <= 0f)
        {
            return false;
        }

        Vector3 first = tileParent.InverseTransformPoint(firstWorldPosition);
        Vector3 second = tileParent.InverseTransformPoint(secondWorldPosition);
        int firstColumn = Mathf.RoundToInt((first.x - GetStartOffset(0)) / boardDistance);
        int secondColumn = Mathf.RoundToInt((second.x - GetStartOffset(0)) / boardDistance);
        int columnCount = boardCount + (laneCount - 1) * LaneColumnOffset;
        if (firstColumn < 0 || firstColumn >= columnCount
            || secondColumn < 0 || secondColumn >= columnCount)
        {
            return false;
        }
        tileDistance = Mathf.Abs(firstColumn - secondColumn);
        return true;
    }

    public bool TryGetAdjacentTilePosition(
        Vector3 currentWorldPosition,
        int direction,
        out Vector3 targetWorldPosition)
    {
        return TryGetAdjacentTilePosition(currentWorldPosition, 0, direction,
            out targetWorldPosition);
    }

    public bool TryGetAdjacentTilePosition(
        Vector3 currentWorldPosition,
        int laneIndex,
        int direction,
        out Vector3 targetWorldPosition)
    {
        targetWorldPosition = currentWorldPosition;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f
            || direction == 0)
        {
            return false;
        }

        if (!TryGetTileIndex(currentWorldPosition, laneIndex, out int currentIndex))
        {
            return false;
        }

        float startOffset = GetStartOffset(laneIndex);
        Vector3 currentLocalPosition = tileParent.InverseTransformPoint(currentWorldPosition);

        int moveDirection = direction > 0 ? 1 : -1;
        int targetIndex = currentIndex + moveDirection;

        if (targetIndex < 0 || targetIndex >= boardCount)
        {
            return false;
        }

        currentLocalPosition.x = startOffset + boardDistance * targetIndex;
        targetWorldPosition = tileParent.TransformPoint(currentLocalPosition);
        return true;
    }

    public bool TryGetAdjacentLanePosition(
        int tileIndex,
        int currentLaneIndex,
        int direction,
        out int targetLaneIndex,
        out Vector3 targetWorldPosition)
    {
        targetLaneIndex = currentLaneIndex;
        targetWorldPosition = Vector3.zero;

        if (direction == 0 || currentLaneIndex < 0 || currentLaneIndex >= laneCount
            || tileIndex < 0 || tileIndex >= boardCount)
        {
            return false;
        }

        targetLaneIndex += direction > 0 ? 1 : -1;

        if (!TryGetTilePosition(
                tileIndex + (currentLaneIndex - targetLaneIndex) * LaneColumnOffset,
                targetLaneIndex,
                out targetWorldPosition))
        {
            targetLaneIndex = currentLaneIndex;
            return false;
        }

        return true;
    }

    public bool TryGetRangedTilePosition(
        Vector3 currentWorldPosition,
        int direction,
        int range,
        out Vector3 targetWorldPosition)
    {
        return TryGetRangedTilePosition(currentWorldPosition, 0, direction,
            range, out targetWorldPosition);
    }

    public bool TryGetRangedTilePosition(
        Vector3 currentWorldPosition,
        int laneIndex,
        int direction,
        int range,
        out Vector3 targetWorldPosition)
    {
        targetWorldPosition = currentWorldPosition;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f
            || direction == 0 || range <= 0)
        {
            return false;
        }

        if (!TryGetTileIndex(currentWorldPosition, laneIndex, out int currentIndex))
        {
            return false;
        }

        float startOffset = GetStartOffset(laneIndex);
        Vector3 currentLocalPosition = tileParent.InverseTransformPoint(currentWorldPosition);

        int moveDirection = direction > 0 ? 1 : -1;
        int targetIndex = Mathf.Clamp(
            currentIndex + moveDirection * range,
            0,
            boardCount - 1);

        if (targetIndex == currentIndex)
        {
            return false;
        }

        float targetPositionX = startOffset + boardDistance * targetIndex;
        currentLocalPosition.x = targetPositionX;
        targetWorldPosition = tileParent.TransformPoint(currentLocalPosition);
        return true;
    }

    private void Start()
    {
        GenerateBoard();
    }

    private void GenerateBoard()
    {
        if (isGenerated)
        {
            return;
        }

        if (boardCount <= 0)
        {
            Debug.LogError("Board Count는 0 이상이어야 합니다.", this);
            return;
        }

        if (boardDistance <= 0f)
        {
            Debug.LogError("Board Distance는 0보다 커야 합니다.", this);
            return;
        }

        if (laneCount <= 0)
        {
            Debug.LogError("Lane Count는 0보다 커야 합니다.", this);
            return;
        }

        if (laneDistance <= 0f)
        {
            Debug.LogError("Lane Distance는 0보다 커야 합니다.", this);
            return;
        }

        if (tilePrefab == null || tileParent == null)
        {
            Debug.LogError("Tile Prefab and Tile Parent must be assigned.", this);
            return;
        }

        isGenerated = true;
        spawnedTiles.Clear();
        float laneStartOffset = GetLaneStartOffset();

        for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            float positionY = laneStartOffset + laneDistance * laneIndex;
            float startOffset = GetStartOffset(laneIndex);

            for (int tileIndex = 0; tileIndex < boardCount; tileIndex++)
            {
                float positionX = startOffset + boardDistance * tileIndex;
                BoardTile tile = Instantiate(tilePrefab, tileParent);
                tile.SetWarningActive(false);

                tile.transform.SetLocalPositionAndRotation(
                    new Vector3(positionX, positionY, 0f),
                    Quaternion.identity);
                tile.ConfigureGrid(boardDistance, laneDistance,
                    tileIndex == 0, tileIndex == boardCount - 1,
                    laneIndex == 0 || LaneColumnOffset > 0 && tileIndex == boardCount - 1,
                    laneIndex == laneCount - 1 || LaneColumnOffset > 0 && tileIndex == 0);
                spawnedTiles.Add(tile);
            }
        }
    }

    private void RegenerateBoard()
    {
        foreach (BoardTile tile in spawnedTiles)
        {
            if (tile == null)
            {
                continue;
            }

            tile.gameObject.SetActive(false);
            Destroy(tile.gameObject);
        }

        spawnedTiles.Clear();
        persistentWarningCells.Clear();
        warningOwnerIdsByCell.Clear();
        urgentWarningOwners.Clear();
        focusedWarningOwner = null;
        isGenerated = false;
        GenerateBoard();
    }

    private void RefreshTileWarning(int spawnedTileIndex)
    {
        if (spawnedTileIndex < 0 || spawnedTileIndex >= spawnedTiles.Count
            || spawnedTiles[spawnedTileIndex] == null)
        {
            return;
        }

        bool hasOwner = warningOwnerIdsByCell.TryGetValue(
                spawnedTileIndex,
                out HashSet<int> ownerIds)
            && ownerIds.Count > 0;
        spawnedTiles[spawnedTileIndex].SetWarningActive(
            persistentWarningCells.Contains(spawnedTileIndex) || hasOwner);
        spawnedTiles[spawnedTileIndex].SetWarningEmphasized(
            hasOwner && focusedWarningOwner != null
            && ownerIds.Contains(focusedWarningOwner.GetInstanceID()));
        spawnedTiles[spawnedTileIndex].SetWarningUrgent(
            hasOwner && ownerIds.Overlaps(urgentWarningOwners));
    }

    // Local tile IDs stay stable for saves; columns describe physical alignment.
    public int GetColumnIndex(int tileIndex, int laneIndex)
    {
        return tileIndex + laneIndex * LaneColumnOffset;
    }

    private float GetStartOffset(int laneIndex)
    {
        return (laneIndex * LaneColumnOffset
            - (boardCount - 1 + (laneCount - 1) * LaneColumnOffset) * 0.5f)
            * boardDistance;
    }

    private float GetLaneStartOffset()
    {
        return -(laneCount - 1) * laneDistance * 0.5f;
    }

    private int GetSpawnedTileIndex(int tileIndex, int laneIndex)
    {
        if (tileIndex < 0 || tileIndex >= boardCount
            || laneIndex < 0 || laneIndex >= laneCount)
        {
            return -1;
        }

        return laneIndex * boardCount + tileIndex;
    }
}
