using TMPro;
using UnityEngine;

public class BossBomb : MonoBehaviour, IPlayerBulletBlocker
{
    private const float DangerBlinkSpeed = 8f;

    [Header("Optional Visual References")]
    [SerializeField] private SpriteRenderer bombRenderer;
    [SerializeField] private TMP_Text fuseText;

    [Header("Runtime State")]
    [SerializeField] private int tileIndex = -1;
    [SerializeField] private int remainingFuse;
    [SerializeField] private bool isExploding;

    private BossBombManager manager;
    private EnemyData sourceData;
    private int createdTurnCycle;
    private LineRenderer explosionRangeLine;
    private Color baseBombColor = Color.white;
    private Color baseRangeColor = new Color(1f, 0.45f, 0f, 0.65f);

    public int TileIndex => tileIndex;
    public Vector3 WorldPosition => transform.position;
    public bool IsBulletBlocking => !isExploding && gameObject.activeInHierarchy;
    public int RemainingFuse => remainingFuse;
    public int CreatedTurnCycle => createdTurnCycle;
    public bool IsExploding => isExploding;
    public EnemyData SourceData => sourceData;

    public bool Initialize(
        BossBombManager assignedManager,
        EnemyData assignedSourceData,
        int assignedTileIndex,
        int fuseTurns,
        int turnCycle)
    {
        if (assignedManager == null || assignedSourceData == null
            || assignedTileIndex < 0)
        {
            return false;
        }

        manager = assignedManager;
        sourceData = assignedSourceData;
        tileIndex = assignedTileIndex;
        remainingFuse = Mathf.Clamp(fuseTurns, 1, 3);
        createdTurnCycle = turnCycle;
        isExploding = false;
        EnsureFallbackVisuals();
        RefreshFuseText();

        if (remainingFuse == 1)
        {
            CreateExplosionRangeTelegraph();
        }
        return true;
    }

    public void HandlePlayerBulletImpact()
    {
        // Bombs intentionally absorb every player projectile without taking
        // damage or detonating. PlayerShoot consumes the fired cylinder.
    }

    public void ProcessEnemyTurnCycleEnd(int completedTurnCycle)
    {
        if (isExploding || completedTurnCycle <= createdTurnCycle)
        {
            return;
        }

        remainingFuse = Mathf.Max(0, remainingFuse - 1);
        RefreshFuseText();

        if (remainingFuse == 1 && explosionRangeLine == null)
        {
            CreateExplosionRangeTelegraph();
        }

        if (remainingFuse == 0)
        {
            manager?.RequestDetonation(this);
        }
    }

    public bool TryBeginExplosion()
    {
        if (isExploding)
        {
            return false;
        }

        isExploding = true;
        return true;
    }

    public void DisposeVisuals()
    {
        if (explosionRangeLine != null)
        {
            explosionRangeLine.gameObject.SetActive(false);
            Destroy(explosionRangeLine.gameObject);
            explosionRangeLine = null;
        }
    }

    private void Update()
    {
        if (remainingFuse != 1 || isExploding)
        {
            ApplyBlinkAlpha(1f);
            return;
        }

        float alpha = Mathf.Lerp(
            0.2f,
            1f,
            (Mathf.Sin(Time.unscaledTime * DangerBlinkSpeed) + 1f) * 0.5f);
        ApplyBlinkAlpha(alpha);
    }

    private void OnDestroy()
    {
        DisposeVisuals();
        manager?.NotifyBombDestroyed(this);
    }

    private void EnsureFallbackVisuals()
    {
        if (bombRenderer == null)
        {
            bombRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (bombRenderer == null)
        {
            bombRenderer = gameObject.AddComponent<SpriteRenderer>();
            bombRenderer.sprite = RuntimeBossBombVisuals.BombSprite;
            bombRenderer.color = new Color(0.16f, 0.12f, 0.08f, 1f);
            transform.localScale = Vector3.one * 0.55f;
        }

        baseBombColor = bombRenderer.color;

        if (fuseText == null)
        {
            fuseText = GetComponentInChildren<TMP_Text>(true);
        }

        if (fuseText == null)
        {
            GameObject textObject = new GameObject("Text | Bomb Fuse");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            textObject.transform.localScale = Vector3.one * 0.16f;
            TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 6f;
            textMesh.color = Color.white;
            textMesh.sortingOrder = bombRenderer.sortingOrder + 2;
            fuseText = textMesh;
        }
    }

    private void CreateExplosionRangeTelegraph()
    {
        if (explosionRangeLine != null || remainingFuse != 1)
        {
            return;
        }

        BigBarrelSettings settings = sourceData.BigBarrel;
        explosionRangeLine = BoardTelegraphUtility.CreateTileRange(
            transform,
            "Line | Bomb Explosion Range",
            manager.BoardManager,
            tileIndex - settings.BombExplosionRadius,
            tileIndex + settings.BombExplosionRadius,
            settings.BombTelegraphMaterial,
            baseRangeColor,
            sourceData.TelegraphVerticalOffset * 0.5f,
            sourceData.TelegraphSortingOrder - 2);
    }

    private void RefreshFuseText()
    {
        if (fuseText != null)
        {
            fuseText.text = remainingFuse.ToString();
        }
    }

    private void ApplyBlinkAlpha(float alpha)
    {
        if (bombRenderer != null)
        {
            Color color = baseBombColor;
            color.a *= alpha;
            bombRenderer.color = color;
        }

        if (explosionRangeLine != null)
        {
            Color color = baseRangeColor;
            color.a *= alpha;
            explosionRangeLine.startColor = color;
            explosionRangeLine.endColor = color;
        }
    }
}

internal static class RuntimeBossBombVisuals
{
    private static Sprite bombSprite;

    public static Sprite BombSprite
    {
        get
        {
            if (bombSprite == null)
            {
                const int resolution = 32;
                Texture2D texture = new Texture2D(
                    resolution,
                    resolution,
                    TextureFormat.RGBA32,
                    false);
                Color[] pixels = new Color[resolution * resolution];
                Vector2 center = Vector2.one * ((resolution - 1) * 0.5f);
                float radius = resolution * 0.46f;

                for (int index = 0; index < pixels.Length; index++)
                {
                    int x = index % resolution;
                    int y = index / resolution;
                    float alpha = Vector2.Distance(
                        new Vector2(x, y),
                        center) <= radius ? 1f : 0f;
                    pixels[index] = new Color(1f, 1f, 1f, alpha);
                }

                texture.SetPixels(pixels);
                texture.Apply(false, true);
                texture.hideFlags = HideFlags.HideAndDontSave;
                bombSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, resolution, resolution),
                    new Vector2(0.5f, 0.5f),
                    resolution);
                bombSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return bombSprite;
        }
    }
}
