using UnityEngine;
using UnityEngine.Serialization;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField, Min(0)] private int boardCount;
    [SerializeField] private float boardDistance = 1f;

    [Header("References")]
    [SerializeField] private GameObject tilePrefab;
    [FormerlySerializedAs("spawnOrigin")]
    [SerializeField] private Transform tileParent;

    private bool isGenerated;

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

        if (tilePrefab == null || tileParent == null)
        {
            Debug.LogError("Tile Prefab과 Tile Parent를 Inspector에서 할당해야 합니다.", this);
            return;
        }

        if (boardCount < 0)
        {
            Debug.LogError("Board Count는 0 이상이어야 합니다.", this);
            return;
        }

        isGenerated = true;
        float startOffset = -(boardCount - 1) * boardDistance * 0.5f;

        for (int index = 0; index < boardCount; index++)
        {
            float positionX = startOffset + boardDistance * index;
            GameObject tile = Instantiate(tilePrefab, tileParent);

            tile.transform.SetLocalPositionAndRotation(
                new Vector3(positionX, 0f, 0f),
                Quaternion.identity);
        }
    }
}
