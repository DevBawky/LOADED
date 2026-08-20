using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField, Min(1)] private int boardCount = 1;
    [SerializeField, Min(0.01f)] private float boardDistance = 1f;

    [Header("References")]
    [SerializeField] private BoardTile tilePrefab;
    [FormerlySerializedAs("spawnOrigin")]
    [SerializeField] private Transform tileParent;

    private bool isGenerated;
    private readonly List<BoardTile> spawnedTiles = new List<BoardTile>();

    public int BoardCount => boardCount;
    public float BoardDistance => boardDistance;

    public bool ConfigureBoard(int configuredBoardCount, BoardTile configuredTilePrefab)
    {
        if (configuredBoardCount <= 0 || configuredTilePrefab == null)
        {
            Debug.LogError(
                "Board Count must be greater than zero and Tile Prefab must be assigned.",
                this);
            return false;
        }

        boardCount = configuredBoardCount;
        tilePrefab = configuredTilePrefab;
        RegenerateBoard();
        return isGenerated;
    }

    public bool SetTileWarningActive(int tileIndex, bool isActive)
    {
        if (tileIndex < 0 || tileIndex >= spawnedTiles.Count
            || spawnedTiles[tileIndex] == null)
        {
            return false;
        }

        spawnedTiles[tileIndex].SetWarningActive(isActive);
        return true;
    }

    public bool TryGetTilePosition(int tileIndex, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f
            || tileIndex < 0 || tileIndex >= boardCount)
        {
            return false;
        }

        float positionX = GetStartOffset() + boardDistance * tileIndex;
        worldPosition = tileParent.TransformPoint(new Vector3(positionX, 0f, 0f));
        return true;
    }

    public bool TryGetTileIndex(Vector3 worldPosition, out int tileIndex)
    {
        tileIndex = -1;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f)
        {
            return false;
        }

        Vector3 localPosition = tileParent.InverseTransformPoint(worldPosition);
        float rawIndex = (localPosition.x - GetStartOffset()) / boardDistance;
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

        if (!TryGetTileIndex(firstWorldPosition, out int firstIndex)
            || !TryGetTileIndex(secondWorldPosition, out int secondIndex))
        {
            return false;
        }

        tileDistance = Mathf.Abs(firstIndex - secondIndex);
        return true;
    }

    public bool TryGetAdjacentTilePosition(
        Vector3 currentWorldPosition,
        int direction,
        out Vector3 targetWorldPosition)
    {
        targetWorldPosition = currentWorldPosition;

        if (tileParent == null || boardCount <= 0 || boardDistance <= 0f
            || direction == 0)
        {
            return false;
        }

        float startOffset = GetStartOffset();
        Vector3 currentLocalPosition = tileParent.InverseTransformPoint(currentWorldPosition);
        int currentIndex = Mathf.RoundToInt(
            (currentLocalPosition.x - startOffset) / boardDistance);
        currentIndex = Mathf.Clamp(currentIndex, 0, boardCount - 1);

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

    public bool TryGetRangedTilePosition(
        Vector3 currentWorldPosition,
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

        float startOffset = GetStartOffset();
        Vector3 currentLocalPosition = tileParent.InverseTransformPoint(currentWorldPosition);
        int currentIndex = Mathf.RoundToInt(
            (currentLocalPosition.x - startOffset) / boardDistance);
        currentIndex = Mathf.Clamp(currentIndex, 0, boardCount - 1);

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
        targetWorldPosition = tileParent.TransformPoint(
            new Vector3(targetPositionX, 0f, 0f));
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

        if (tilePrefab == null || tileParent == null)
        {
            Debug.LogError("Tile Prefab and Tile Parent must be assigned.", this);
            return;
        }

        isGenerated = true;
        spawnedTiles.Clear();
        float startOffset = GetStartOffset();

        for (int index = 0; index < boardCount; index++)
        {
            float positionX = startOffset + boardDistance * index;
            BoardTile tile = Instantiate(tilePrefab, tileParent);

            tile.transform.SetLocalPositionAndRotation(
                new Vector3(positionX, 0f, 0f),
                Quaternion.identity);
            spawnedTiles.Add(tile);
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
        isGenerated = false;
        GenerateBoard();
    }

    private float GetStartOffset()
    {
        return -(boardCount - 1) * boardDistance * 0.5f;
    }
}
