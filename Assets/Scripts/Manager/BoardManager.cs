using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField, Min(0)] private int boardCount;
    [SerializeField] private float boardDistance = 1f;

    [Header("References")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform spawnOrigin;

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

        if (tilePrefab == null || spawnOrigin == null)
        {
            Debug.LogError("Tile Prefab과 Spawn Origin을 Inspector에서 할당해야 합니다.", this);
            return;
        }

        if (boardCount < 0)
        {
            Debug.LogError("Board Count는 0 이상이어야 합니다.", this);
            return;
        }

        isGenerated = true;

        for (int index = 0; index < boardCount; index++)
        {
            Vector3 spawnPosition = spawnOrigin.position
                + spawnOrigin.right * (boardDistance * index);

            Instantiate(tilePrefab, spawnPosition, spawnOrigin.rotation, transform);
        }
    }
}
