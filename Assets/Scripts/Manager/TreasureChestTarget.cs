using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TreasureChestTarget : MonoBehaviour,
    IPlayerAttackTarget,
    IPlayerAttackTargetPresentation
{
    private static readonly Color HitChestColor =
        new Color(1f, 0.76f, 0.24f, 1f);

    [SerializeField, Min(0)] private int tileIndex;
    [SerializeField, Min(0)] private int laneIndex;
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private TreasureChestHealthBar healthBar;
    [SerializeField, Min(0)] private int currentHealth;
    [SerializeField, Min(1)] private int maxHealth = 1;
    [SerializeField] private bool destroyed;

    private PlayerAttackTargetRegistry registry;
    private Coroutine hitFeedbackCoroutine;
    private Sprite generatedSprite;
    private Texture2D generatedTexture;
    private bool withdrawn;

    public event Action<TreasureChestTarget> Destroyed;
    public event Action<TreasureChestTarget> HealthChanged;

    public Transform TargetTransform => transform;
    public Vector3 ImpactPoint => chestRenderer == null
        ? transform.position
        : chestRenderer.bounds.center;
    public SpriteRenderer ImpactRenderer => chestRenderer;
    public int LaneIndex => laneIndex;
    public int CurrentDurability => Mathf.Max(0, currentHealth);
    public int MaxDurability => Mathf.Max(1, maxHealth);
    public int TotalStatusStackCount => 0;
    public int ActiveStatusTypeCount => 0;
    public bool IsTargetable => isActiveAndEnabled
        && !destroyed
        && !withdrawn
        && currentHealth > 0;
    public int TileIndex => tileIndex;
    public bool IsDestroyed => destroyed;
    public long AcceptedDamageTotal { get; private set; }

    public void Configure(
        int configuredTileIndex,
        int configuredLaneIndex,
        Sprite authoredSprite = null)
    {
        tileIndex = Mathf.Max(0, configuredTileIndex);
        laneIndex = Mathf.Max(0, configuredLaneIndex);
        EnsureRendererObject();

        if (authoredSprite != null)
        {
            chestRenderer.sprite = authoredSprite;
            chestRenderer.color = Color.white;
        }
        else
        {
            EnsureRenderer();
        }
    }

    public void RestoreState(
        int configuredMaxHealth,
        int configuredCurrentHealth,
        bool wasDestroyed)
    {
        EnsureRenderer();
        EnsureHealthBar();

        maxHealth = Mathf.Max(1, configuredMaxHealth);
        destroyed = wasDestroyed || configuredCurrentHealth <= 0;
        withdrawn = false;
        currentHealth = destroyed
            ? 0
            : Mathf.Clamp(configuredCurrentHealth, 1, maxHealth);
        AcceptedDamageTotal = 0L;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        chestRenderer.color = Color.white;
        chestRenderer.enabled = !destroyed;

        if (destroyed)
        {
            healthBar.Hide();
        }
        else
        {
            healthBar.SetHealth(currentHealth, maxHealth);
        }
    }

    public bool Withdraw()
    {
        if (destroyed || withdrawn)
        {
            return false;
        }

        withdrawn = true;
        if (hitFeedbackCoroutine != null)
        {
            StopCoroutine(hitFeedbackCoroutine);
            hitFeedbackCoroutine = null;
        }

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        if (chestRenderer != null)
        {
            chestRenderer.color = Color.white;
            chestRenderer.enabled = false;
        }
        healthBar?.ClearDamagePreview();
        healthBar?.Hide();
        return true;
    }

    public void Place(BoardManager boardManager)
    {
        if (boardManager != null && boardManager.TryGetTilePosition(
                tileIndex,
                laneIndex,
                out Vector3 tilePosition))
        {
            transform.position = tilePosition;
        }
    }

    private void Awake()
    {
        EnsureRenderer();
        EnsureHealthBar();
    }

    private void OnEnable()
    {
        registry = FindFirstObjectByType<PlayerAttackTargetRegistry>();
        registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    private void OnDestroy()
    {
        if (generatedSprite != null)
        {
            Destroy(generatedSprite);
        }
        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
        }
    }

    public int PredictAttackDamage(int attackDamage)
    {
        return IsTargetable
            ? Mathf.Max(0, attackDamage)
            : 0;
    }

    public void ShowDamagePreview(
        System.Collections.Generic.IReadOnlyList<
            EnemyHealthBarFeedback.DamagePreviewSegment> segments,
        BulletData bullet)
    {
        if (IsTargetable)
        {
            healthBar.ShowDamagePreview(segments);
        }
    }

    public void ClearDamagePreview()
    {
        healthBar?.ClearDamagePreview();
    }

    public int ApplyAttackDamage(
        int attackDamage,
        bool isCritical,
        BulletInstance sourceBullet)
    {
        int appliedDamage = Mathf.Min(
            PredictAttackDamage(attackDamage),
            currentHealth);
        if (appliedDamage <= 0)
        {
            return 0;
        }

        AcceptedDamageTotal = AcceptedDamageTotal
            > long.MaxValue - appliedDamage
                ? long.MaxValue
                : AcceptedDamageTotal + appliedDamage;
        currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
        healthBar.ClearDamagePreview();
        healthBar.SetHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(this);

        if (hitFeedbackCoroutine != null)
        {
            StopCoroutine(hitFeedbackCoroutine);
        }

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        chestRenderer.color = Color.white;

        if (currentHealth <= 0)
        {
            destroyed = true;
            healthBar.Hide();
            hitFeedbackCoroutine = StartCoroutine(
                PlayDestroyedFeedback(isCritical));
            Destroyed?.Invoke(this);
        }
        else
        {
            hitFeedbackCoroutine = StartCoroutine(
                PlayHitFeedback(isCritical));
        }

        return appliedDamage;
    }

    public bool TryApplyEffect(
        BulletEffectData effect,
        int horizontalDirection)
    {
        // Chests are fixed puzzle targets. Debuffs and relocation effects do
        // not mutate them; direct, repeated, piercing, Storm, and board-wide
        // attacks still reach this target through the shared attack pipeline.
        return false;
    }

    private IEnumerator PlayHitFeedback(bool isCritical)
    {
        if (chestRenderer == null)
        {
            yield break;
        }

        chestRenderer.color = isCritical ? Color.white : HitChestColor;
        transform.localScale = new Vector3(1.12f, 0.86f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
        yield return WaitForUnpausedSeconds(0.045f);

        transform.localScale = new Vector3(0.95f, 1.1f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, 1.25f);
        yield return WaitForUnpausedSeconds(0.075f);

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        chestRenderer.color = Color.white;
        hitFeedbackCoroutine = null;
    }

    private IEnumerator PlayDestroyedFeedback(bool isCritical)
    {
        if (chestRenderer == null)
        {
            yield break;
        }

        chestRenderer.color = isCritical ? Color.white : HitChestColor;
        transform.localScale = new Vector3(1.18f, 0.82f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        yield return WaitForUnpausedSeconds(0.055f);

        transform.localScale = new Vector3(0.9f, 1.16f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
        yield return WaitForUnpausedSeconds(0.105f);

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        chestRenderer.color = Color.white;
        chestRenderer.enabled = false;
        hitFeedbackCoroutine = null;
    }

    private static IEnumerator WaitForUnpausedSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.unscaledDeltaTime;
            }
            yield return null;
        }
    }

    private void EnsureRenderer()
    {
        EnsureRendererObject();

        if (chestRenderer.sprite == null)
        {
            generatedSprite = CreateFallbackChestSprite(out generatedTexture);
            chestRenderer.sprite = generatedSprite;
        }

        chestRenderer.color = Color.white;
    }

    private void EnsureRendererObject()
    {
        if (chestRenderer == null)
        {
            GameObject view = new GameObject("Chest View");
            view.transform.SetParent(transform, false);
            view.transform.localPosition = Vector3.zero;
            chestRenderer = view.AddComponent<SpriteRenderer>();
            chestRenderer.sortingOrder = 3;
        }
    }

    private void EnsureHealthBar()
    {
        healthBar ??= GetComponent<TreasureChestHealthBar>();
        healthBar ??= gameObject.AddComponent<TreasureChestHealthBar>();
    }

    private static Sprite CreateFallbackChestSprite(out Texture2D texture)
    {
        const int width = 32;
        const int height = 24;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Treasure Chest Fallback (Runtime)",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color outline = new Color(0.13f, 0.055f, 0.015f, 1f);
        Color wood = new Color(0.64f, 0.31f, 0.075f, 1f);
        Color brass = new Color(0.96f, 0.66f, 0.18f, 1f);

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool body = x >= 3 && x <= 28 && y >= 3 && y <= 15;
                bool lid = x >= 5 && x <= 26 && y >= 15 && y <= 21;
                bool edge = body && (x == 3 || x == 28 || y == 3)
                    || lid && (x == 5 || x == 26 || y == 21);
                bool band = (body || lid) && (x == 15 || x == 16);
                pixels[y * width + x] = edge
                    ? outline
                    : band ? brass : body || lid ? wood : transparent;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0f),
            24f);
    }
}
