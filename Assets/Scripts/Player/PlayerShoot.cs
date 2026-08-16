using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerShoot : MonoBehaviour
{
    public event Action<BulletInstance> BulletFired;
    public event Action<int> DamageDealt;
    public event Action<PlayerBehaviourAction> BehaviourActionStarted;
    public event Action LoadedBulletDamagePreviewShown;

    private const float BulletFeedbackStartAlpha = 0.2f;
    private const float RangePreviewLineWidth = 0.08f;
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int GridColorId =
        Shader.PropertyToID("_GridColor");
    private static readonly int BeamColorId =
        Shader.PropertyToID("_BeamColor");
    private static readonly int DashCountId =
        Shader.PropertyToID("_DashCount");

    private readonly struct DamageReservation
    {
        public DamageReservation(EnemyController enemy, int damage)
        {
            Enemy = enemy;
            Damage = Mathf.Max(0, damage);
        }

        public EnemyController Enemy { get; }
        public int Damage { get; }
    }

    private readonly struct BulletHitTarget
    {
        public BulletHitTarget(EnemyController enemy)
        {
            Enemy = enemy;
            InstanceId = enemy.GetInstanceID();
            InitialPosition = enemy.transform.position;
        }

        public EnemyController Enemy { get; }
        public int InstanceId { get; }
        public Vector3 InitialPosition { get; }
    }

    private readonly struct ManagedEffectDefeatResult
    {
        public ManagedEffectDefeatResult(
            int damage,
            int healthBeforeDamage,
            int targetMaxHealth,
            Vector3 worldPosition)
        {
            Damage = Mathf.Max(0, damage);
            HealthBeforeDamage = Mathf.Max(0, healthBeforeDamage);
            TargetMaxHealth = Mathf.Max(0, targetMaxHealth);
            WorldPosition = worldPosition;
            WasDefeated = true;
        }

        public bool WasDefeated { get; }
        public int Damage { get; }
        public int HealthBeforeDamage { get; }
        public int TargetMaxHealth { get; }
        public Vector3 WorldPosition { get; }
    }

    private sealed class DamagePreviewEnemyState
    {
        public DamagePreviewEnemyState(EnemyController enemy)
        {
            Enemy = enemy;
            RemainingHealth = enemy == null ? 0 : enemy.CurrentHealth;
            StatusStacks = new int[4];
            Segments = new List<
                EnemyHealthBarFeedback.DamagePreviewSegment>();

            if (enemy == null)
            {
                return;
            }

            for (int index = 0; index < StatusStacks.Length; index++)
            {
                StatusStacks[index] = enemy.GetStatusStacks(
                    (StatusEffectType)index);
            }
        }

        public EnemyController Enemy { get; }
        public int RemainingHealth { get; set; }
        public int TileIndex { get; set; } = -1;
        public int[] StatusStacks { get; }
        public List<EnemyHealthBarFeedback.DamagePreviewSegment> Segments
        {
            get;
        }

        public int ActiveStatusTypeCount
        {
            get
            {
                int count = 0;

                foreach (int stacks in StatusStacks)
                {
                    if (stacks > 0)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int TotalStatusStackCount
        {
            get
            {
                long total = 0;

                foreach (int stacks in StatusStacks)
                {
                    total += stacks;
                }

                return total >= int.MaxValue ? int.MaxValue : (int)total;
            }
        }
    }

    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private RelicManager relicManager;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Transform firePoint;
    [FormerlySerializedAs("projectilePrefab")]
    [SerializeField] private BulletLine bulletLinePrefab;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private PlayerCylinderUI cylinderUI;
    [SerializeField] private Image bulletFeedbackImage;
    [SerializeField] private CombatPresentation combatPresentation;
    [SerializeField] private CombatFeedbackController combatFeedback;
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.2f;

    [Header("Shot Presentation")]
    [Min(0f)]
    [SerializeField] private float maxRandomShotAngle = 5f;

    private int lastActionFrame = -1;
    private bool isFiring;
    private Coroutine bulletFeedbackCoroutine;
    private readonly List<EnemyController> targetBuffer =
        new List<EnemyController>();
    private readonly List<EnemyController> hitBuffer =
        new List<EnemyController>();
    private readonly List<BulletInstance> ownedBulletBuffer =
        new List<BulletInstance>();
    private readonly HashSet<BulletData> ownedBulletTypeBuffer =
        new HashSet<BulletData>();
    private readonly Dictionary<EnemyController, int> reservedDamageByEnemy =
        new Dictionary<EnemyController, int>();
    private readonly Dictionary<EnemyController, ManagedEffectDefeatResult>
        pendingEffectDefeats =
            new Dictionary<EnemyController, ManagedEffectDefeatResult>();
    private readonly int[] ownedGradeCountBuffer = new int[4];
    private readonly Dictionary<EnemyController, DamagePreviewEnemyState>
        damagePreviewStates =
            new Dictionary<EnemyController, DamagePreviewEnemyState>();
    private readonly HashSet<EnemyController> previewedEnemies =
        new HashSet<EnemyController>();
    private readonly Dictionary<BulletInstance, float> previewDamageBonuses =
        new Dictionary<BulletInstance, float>();
    private readonly Dictionary<BulletInstance, float> previewCriticalBonuses =
        new Dictionary<BulletInstance, float>();
    private readonly Dictionary<BulletInstance, float> previewStoredBonuses =
        new Dictionary<BulletInstance, float>();
    private readonly Dictionary<BulletInstance, int> previewAbilityStacks =
        new Dictionary<BulletInstance, int>();
    private readonly Dictionary<BulletInstance, int> previewPermanentStacks =
        new Dictionary<BulletInstance, int>();
    private readonly Dictionary<BulletInstance, int> previewShotsObserved =
        new Dictionary<BulletInstance, int>();
    private int previewPlayerTileIndex = -1;
    private BulletInstance currentConsumedBullet;
    private int initialLoadedBulletCount;
    private int bulletsFiredThisCylinder;
    private int criticalShotsThisCylinder;
    private int activeShotIndex;
    private bool bulletDestroyedThisCylinder;
    private int pendingSaverGold;
    private LineRenderer rangePreviewLine;
    private LineRenderer secondaryRangePreviewLine;
    private Material rangePreviewMaterial;
    private Material rangePreviewDashedMaterial;

    public bool IsFiring => isFiring;
    public int InitialLoadedBulletCount => isFiring
        ? Mathf.Max(0, initialLoadedBulletCount)
        : deckManager == null ? 0 : deckManager.LoadedBullets.Count;
    public int BulletsFiredThisCylinder => isFiring
        ? Mathf.Max(0, bulletsFiredThisCylinder)
        : 0;
    public int CriticalShotsThisCylinder => isFiring
        ? Mathf.Max(0, criticalShotsThisCylinder)
        : 0;

    private void Awake()
    {
        CombatAccessibilitySettings.Ensure(gameObject);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>();
        combatPresentation ??= GetComponent<CombatPresentation>();
        combatFeedback ??= GetComponent<CombatFeedbackController>();
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);

        if (combatPresentation == null)
        {
            combatPresentation = gameObject.AddComponent<CombatPresentation>();
        }

        if (combatFeedback == null)
        {
            combatFeedback = gameObject.AddComponent<CombatFeedbackController>();
        }

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        ResetBulletFeedback();
        reservedDamageByEnemy.Clear();
        currentConsumedBullet = null;
    }

    private void OnEnable()
    {
        EnemyController.PlayerIndirectDamageDealt +=
            HandlePlayerIndirectDamageDealt;
        EnemyController.PlayerStatusDefeated +=
            HandlePlayerEffectDefeated;

        if (waveManager != null)
        {
            waveManager.BattleCompleted += HandleBattleCompleted;
        }
    }

    private void Start()
    {
        if (cylinderUI != null)
        {
            cylinderUI.Initialize(deckManager);
        }
    }

    private void OnDisable()
    {
        EnemyController.PlayerIndirectDamageDealt -=
            HandlePlayerIndirectDamageDealt;
        EnemyController.PlayerStatusDefeated -=
            HandlePlayerEffectDefeated;

        if (waveManager != null)
        {
            waveManager.BattleCompleted -= HandleBattleCompleted;
        }
        ClearLoadedBulletDamagePreview();
        isFiring = false;

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        if (bulletFeedbackCoroutine != null)
        {
            StopCoroutine(bulletFeedbackCoroutine);
            bulletFeedbackCoroutine = null;
        }

        ResetBulletFeedback();
        reservedDamageByEnemy.Clear();
        pendingEffectDefeats.Clear();
    }

    private void OnDestroy()
    {
        if (rangePreviewMaterial != null)
        {
            Destroy(rangePreviewMaterial);
            rangePreviewMaterial = null;
        }

        if (rangePreviewDashedMaterial != null)
        {
            Destroy(rangePreviewDashedMaterial);
            rangePreviewDashedMaterial = null;
        }
    }

    private void HandlePlayerIndirectDamageDealt(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        DamageDealt?.Invoke(damage);
        combatFeedback?.RecordDamage(damage);
    }

    private void HandlePlayerEffectDefeated(
        EnemyController enemy,
        int damage,
        int healthBeforeDamage)
    {
        if (enemy == null)
        {
            return;
        }

        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        relicManager?.NotifyEnemyDefeated(
            enemy,
            isFiring ? currentConsumedBullet : null,
            waveManager == null ? null : waveManager.ActiveEnemies,
            boardManager);

        if (!isFiring)
        {
            return;
        }

        pendingEffectDefeats[enemy] = new ManagedEffectDefeatResult(
            damage,
            healthBeforeDamage,
            enemy.MaxHealth,
            enemy.transform.position);

        int horizontalDirection = playerMove == null
            ? 0
            : enemy.transform.position.x >= playerMove.transform.position.x
                ? 1
                : -1;
        combatFeedback?.RecordDefeat(
            enemy.transform.position,
            horizontalDirection,
            damage,
            enemy.MaxHealth,
            false,
            waveManager != null && waveManager.ActiveEnemies.Count <= 1,
            GetCurrentCylinderBuild(),
            healthBeforeDamage);
    }

    private void HandleBattleCompleted()
    {
        combatFeedback?.ResetCombo();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || isFiring)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                Reload();
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Shoot();
                return;
            }
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame
            && (eventSystem == null || !eventSystem.IsPointerOverGameObject()))
        {
            Shoot();
        }
    }

    public void Reload()
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || isFiring
            || cylinderUI != null && cylinderUI.IsDragging
            || !TryBeginAction())
        {
            return;
        }

        if (deckManager == null || playerMove == null)
        {
            Debug.LogError("Deck Manager and Player Move must be assigned in the Inspector.", this);
            return;
        }

        if (!playerMove.CanStartAction)
        {
            return;
        }

        bool wasCylinderEmpty = deckManager.LoadedBullets.Count == 0;

        if (deckManager.TryReload(out BulletInstance loadedBullet))
        {
            BehaviourActionStarted?.Invoke(PlayerBehaviourAction.Reload);
            SoundManager.PlaySfx("SFX_Player_Reload");
            combatPresentation?.PlayReload(loadedBullet, cylinderUI);

            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            bool consumesTurn = relicManager == null
                ? loadedBullet == null || !loadedBullet.DoesNotConsumeTurn
                : relicManager.ShouldReloadConsumeTurn(
                    loadedBullet,
                    wasCylinderEmpty);

            if (consumesTurn)
            {
                playerMove.CompleteTurn();
            }
        }
    }

    public void Shoot()
    {
        if (GamePauseController.IsPaused || isFiring
            || cylinderUI != null && cylinderUI.IsDragging
            || !TryBeginAction())
        {
            return;
        }

        if (deckManager == null || playerMove == null || playerHealth == null
            || boardManager == null || waveManager == null || firePoint == null
            || bulletLinePrefab == null)
        {
            Debug.LogError(
                "Deck Manager, Player Move, Player Health, Board Manager, Wave Manager, Fire Point, and Bullet Line Prefab must be assigned in the Inspector.",
                this);
            return;
        }

        if (!playerMove.CanStartAction)
        {
            return;
        }

        if (deckManager.LoadedBullets.Count == 0)
        {
            return;
        }

        int horizontalDirection = transform.localScale.x >= 0f ? 1 : -1;
        int firstBulletIndex = deckManager.LoadedBullets.Count - 1;
        BulletInstance firstBullet = deckManager.LoadedBullets[firstBulletIndex];

        if (firstBullet == null
            || !boardManager.TryGetTileIndex(transform.position, out _))
        {
            return;
        }

        ClearLoadedBulletDamagePreview();
        BehaviourActionStarted?.Invoke(PlayerBehaviourAction.Shoot);
        StartCoroutine(ShootLoadedBullets(horizontalDirection));
    }

    public bool ShowLoadedBulletDamagePreview(int loadedBulletIndex)
    {
        ClearLoadedBulletDamagePreview();

        if (isFiring || deckManager == null || waveManager == null
            || playerHealth == null || boardManager == null
            || loadedBulletIndex < 0
            || loadedBulletIndex >= deckManager.LoadedBullets.Count)
        {
            return false;
        }

        ShowLoadedBulletRangePreview(loadedBulletIndex);
        InitializeDamagePreviewState();
        SimulateLoadedBulletDamage(loadedBulletIndex);
        bool displayedAnyDamage = false;

        foreach (DamagePreviewEnemyState state in damagePreviewStates.Values)
        {
            if (state.Enemy == null || state.Segments.Count == 0)
            {
                continue;
            }

            state.Enemy.ShowDamagePreview(state.Segments);
            previewedEnemies.Add(state.Enemy);
            displayedAnyDamage = true;
        }

        if (displayedAnyDamage)
        {
            LoadedBulletDamagePreviewShown?.Invoke();
        }

        return displayedAnyDamage;
    }

    public void ClearLoadedBulletDamagePreview()
    {
        HideLoadedBulletRangePreview();

        foreach (EnemyController enemy in previewedEnemies)
        {
            if (enemy != null)
            {
                enemy.ClearDamagePreview();
            }
        }

        previewedEnemies.Clear();
    }

    public bool ShowLoadedBulletRangePreview(int loadedBulletIndex)
    {
        if (isFiring || deckManager == null || boardManager == null
            || loadedBulletIndex < 0
            || loadedBulletIndex >= deckManager.LoadedBullets.Count)
        {
            HideLoadedBulletRangePreview();
            return false;
        }

        BulletInstance bullet = ResolveLoadedBulletForRangePreview(
            loadedBulletIndex);

        if (bullet == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int playerTileIndex))
        {
            HideLoadedBulletRangePreview();
            return false;
        }

        LineRenderer line = GetOrCreateRangePreviewLine();

        if (line == null)
        {
            return false;
        }

        Vector3 startPosition;

        if (firePoint != null)
        {
            startPosition = firePoint.position;
        }
        else if (boardManager.TryGetTilePosition(
                     playerTileIndex,
                     out startPosition))
        {
            startPosition.y += 0.15f;
        }
        else
        {
            HideLoadedBulletRangePreview();
            return false;
        }

        ApplyRangePreviewColors(bullet);

        if (IsBoardWideShot(bullet))
        {
            LineRenderer secondaryLine =
                GetOrCreateSecondaryRangePreviewLine();

            if (secondaryLine == null
                || !boardManager.TryGetTilePosition(
                    0,
                    out Vector3 leftEndPosition)
                || !boardManager.TryGetTilePosition(
                    boardManager.BoardCount - 1,
                    out Vector3 rightEndPosition))
            {
                HideLoadedBulletRangePreview();
                return false;
            }

            leftEndPosition.y = startPosition.y;
            leftEndPosition.z = startPosition.z;
            rightEndPosition.y = startPosition.y;
            rightEndPosition.z = startPosition.z;
            SetRangePreviewLine(
                line,
                startPosition,
                leftEndPosition,
                rangePreviewMaterial,
                RangePreviewLineWidth,
                1f);
            SetRangePreviewLine(
                secondaryLine,
                startPosition,
                rightEndPosition,
                rangePreviewMaterial,
                RangePreviewLineWidth,
                1f);
            return true;
        }

        int direction = transform.localScale.x >= 0f ? 1 : -1;
        int endTileIndex = Mathf.Clamp(
            playerTileIndex + direction * bullet.MaxRange,
            0,
            boardManager.BoardCount - 1);

        if (endTileIndex == playerTileIndex
            || !boardManager.TryGetTilePosition(
                endTileIndex,
                out Vector3 endPosition))
        {
            HideLoadedBulletRangePreview();
            return false;
        }

        endPosition.y = startPosition.y;
        endPosition.z = startPosition.z;
        targetBuffer.Clear();

        if (waveManager != null)
        {
            waveManager.GetEnemiesInDirection(
                transform.position,
                direction,
                bullet.MaxRange,
                targetBuffer);
        }

        Vector3 solidEndPosition = endPosition;
        Vector3 dashedStartPosition = Vector3.zero;
        Vector3 dashedEndPosition = Vector3.zero;
        bool hasDashedRange = false;
        float dashedAlpha = 1f;

        for (int targetIndex = 0;
             targetIndex < targetBuffer.Count;
             targetIndex++)
        {
            EnemyController target = targetBuffer[targetIndex];
            Vector3 targetNearPosition = GetRangePreviewEnemyEdge(
                target,
                startPosition,
                direction,
                false);
            solidEndPosition = targetNearPosition;
            float penetrationChance = GetPenetrationPreviewChance(
                bullet,
                targetIndex);

            if (penetrationChance >= 100f)
            {
                if (targetIndex == targetBuffer.Count - 1)
                {
                    solidEndPosition = endPosition;
                }

                continue;
            }

            if (penetrationChance <= 0f)
            {
                break;
            }

            hasDashedRange = true;
            dashedAlpha = penetrationChance / 100f;
            dashedStartPosition = GetRangePreviewEnemyEdge(
                target,
                startPosition,
                direction,
                true);
            dashedEndPosition = endPosition;

            for (int uncertainTargetIndex = targetIndex + 1;
                 uncertainTargetIndex < targetBuffer.Count;
                 uncertainTargetIndex++)
            {
                EnemyController uncertainTarget =
                    targetBuffer[uncertainTargetIndex];
                dashedEndPosition = GetRangePreviewEnemyEdge(
                    uncertainTarget,
                    startPosition,
                    direction,
                    false);
                float nextPenetrationChance =
                    GetPenetrationPreviewChance(
                        bullet,
                        uncertainTargetIndex);

                if (nextPenetrationChance <= 0f)
                {
                    break;
                }

                if (uncertainTargetIndex == targetBuffer.Count - 1)
                {
                    dashedEndPosition = endPosition;
                }
            }

            break;
        }

        SetRangePreviewLine(
            line,
            startPosition,
            solidEndPosition,
            rangePreviewMaterial,
            RangePreviewLineWidth,
            1f);

        if (hasDashedRange)
        {
            LineRenderer dashedLine =
                GetOrCreateSecondaryRangePreviewLine();

            if (dashedLine == null)
            {
                HideLoadedBulletRangePreview();
                return false;
            }

            float dashCount = Mathf.Max(
                2f,
                Vector3.Distance(
                    dashedStartPosition,
                    dashedEndPosition)
                / Mathf.Max(0.01f, boardManager.BoardDistance)
                * 4f);
            rangePreviewDashedMaterial.SetFloat(
                DashCountId,
                dashCount);
            SetRangePreviewLine(
                dashedLine,
                dashedStartPosition,
                dashedEndPosition,
                rangePreviewDashedMaterial,
                RangePreviewLineWidth * 0.5f,
                dashedAlpha);
        }
        else if (secondaryRangePreviewLine != null)
        {
            secondaryRangePreviewLine.enabled = false;
        }

        return true;
    }

    private Vector3 GetRangePreviewEnemyEdge(
        EnemyController enemy,
        Vector3 linePosition,
        int direction,
        bool farSide)
    {
        Vector3 position = enemy.transform.position;
        float edgeOffset = boardManager.BoardDistance * 0.2f;
        position.x += (farSide ? direction : -direction) * edgeOffset;
        position.y = linePosition.y;
        position.z = linePosition.z;
        return position;
    }

    private static float GetPenetrationPreviewChance(
        BulletInstance bullet,
        int hitIndex)
    {
        if (bullet == null || hitIndex < 0
            || hitIndex >= bullet.PenetrationChances.Count)
        {
            return 0f;
        }

        PenetrationChanceData chanceData =
            bullet.PenetrationChances[hitIndex];
        return chanceData == null
            ? 0f
            : Mathf.Clamp(chanceData.Chance, 0f, 100f);
    }

    private static void SetRangePreviewLine(
        LineRenderer line,
        Vector3 startPosition,
        Vector3 endPosition,
        Material material,
        float widthMultiplier,
        float alpha)
    {
        line.sharedMaterial = material;
        line.widthMultiplier = widthMultiplier;
        Color lineColor = Color.white;
        lineColor.a = Mathf.Clamp01(alpha);
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.positionCount = 2;
        line.SetPosition(0, startPosition);
        line.SetPosition(1, endPosition);
        line.enabled = true;
    }

    private BulletInstance ResolveLoadedBulletForRangePreview(
        int loadedBulletIndex)
    {
        BulletInstance previousResolvedBullet = null;

        for (int index = deckManager.LoadedBullets.Count - 1;
             index >= loadedBulletIndex;
             index--)
        {
            previousResolvedBullet = ResolveShotBullet(
                deckManager.LoadedBullets[index],
                previousResolvedBullet);
        }

        return previousResolvedBullet;
    }

    private LineRenderer GetOrCreateRangePreviewLine()
    {
        if (rangePreviewLine != null)
        {
            return rangePreviewLine;
        }

        Shader shader = Shader.Find("Loaded/Enemy Warning Flow");

        if (shader == null)
        {
            Debug.LogWarning(
                "The player bullet range preview shader was not found.",
                this);
            return null;
        }

        rangePreviewMaterial = new Material(shader)
        {
            name = "Player Bullet Range Preview (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        rangePreviewMaterial.SetFloat("_BaseIntensity", 0.65f);
        rangePreviewMaterial.SetFloat("_OverallAlpha", 0.62f);
        rangePreviewMaterial.SetFloat("_GridColumns", 14f);
        rangePreviewMaterial.SetFloat("_GridRows", 2f);
        rangePreviewMaterial.SetFloat("_GridLineWidth", 0.08f);
        rangePreviewMaterial.SetFloat("_GridSoftness", 0.025f);
        rangePreviewMaterial.SetFloat("_GridIntensity", 1.8f);
        rangePreviewMaterial.SetFloat("_GridScrollSpeed", 1.6f);
        rangePreviewMaterial.SetFloat("_BeamRepeat", 3f);
        rangePreviewMaterial.SetFloat("_BeamWidth", 0.35f);
        rangePreviewMaterial.SetFloat("_BeamSoftness", 0.14f);
        rangePreviewMaterial.SetFloat("_BeamIntensity", 2.4f);
        rangePreviewMaterial.SetFloat("_BeamScrollSpeed", 0.65f);
        rangePreviewMaterial.SetFloat("_PulseAmount", 0.12f);
        rangePreviewMaterial.SetFloat("_PulseFrequency", 2f);
        rangePreviewMaterial.SetFloat("_EdgeSoftness", 0.22f);
        rangePreviewMaterial.SetFloat("_EndFade", 0.035f);
        rangePreviewMaterial.SetFloat("_DashEnabled", 0f);
        rangePreviewDashedMaterial = new Material(rangePreviewMaterial)
        {
            name = "Player Bullet Range Preview Dashed (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        rangePreviewDashedMaterial.SetFloat("_DashEnabled", 1f);
        rangePreviewDashedMaterial.SetFloat("_DashFill", 0.72f);
        rangePreviewDashedMaterial.SetFloat("_DashSoftness", 0.04f);
        rangePreviewLine = CreateRangePreviewLine(
            "Line | Bullet Range Preview");
        return rangePreviewLine;
    }

    private LineRenderer GetOrCreateSecondaryRangePreviewLine()
    {
        if (secondaryRangePreviewLine != null)
        {
            return secondaryRangePreviewLine;
        }

        if (GetOrCreateRangePreviewLine() == null)
        {
            return null;
        }

        secondaryRangePreviewLine = CreateRangePreviewLine(
            "Line | Bullet Range Preview Secondary");
        return secondaryRangePreviewLine;
    }

    private LineRenderer CreateRangePreviewLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.widthMultiplier = RangePreviewLineWidth;
        line.numCapVertices = 2;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.enabled = false;

        SpriteRenderer playerRenderer =
            GetComponentInChildren<SpriteRenderer>();

        if (playerRenderer != null)
        {
            line.sortingLayerID = playerRenderer.sortingLayerID;
            line.sortingOrder = playerRenderer.sortingOrder + 20;
        }
        else
        {
            line.sortingOrder = 20;
        }

        line.sharedMaterial = rangePreviewMaterial;
        return line;
    }

    private void ApplyRangePreviewColors(BulletInstance bullet)
    {
        if (rangePreviewMaterial == null || bullet == null)
        {
            return;
        }

        ApplyRangePreviewColor(
            rangePreviewMaterial,
            bullet.SecondaryLineColor);
        ApplyRangePreviewColor(
            rangePreviewDashedMaterial,
            bullet.SecondaryLineColor);
    }

    private static void ApplyRangePreviewColor(
        Material material,
        Color secondaryLineColor)
    {
        if (material == null)
        {
            return;
        }

        Color baseColor = secondaryLineColor;
        Color gridColor = secondaryLineColor;
        Color beamColor = secondaryLineColor;
        baseColor.a *= 0.55f;
        gridColor.a *= 0.9f;
        material.SetColor(BaseColorId, baseColor);
        material.SetColor(GridColorId, gridColor);
        material.SetColor(BeamColorId, beamColor);
    }

    private void HideLoadedBulletRangePreview()
    {
        if (rangePreviewLine != null)
        {
            rangePreviewLine.enabled = false;
        }

        if (secondaryRangePreviewLine != null)
        {
            secondaryRangePreviewLine.enabled = false;
        }
    }

    private IEnumerator ShootLoadedBullets(int horizontalDirection)
    {
        reservedDamageByEnemy.Clear();
        pendingEffectDefeats.Clear();
        isFiring = true;
        playerMove.SetShooting(true);
        bool firedAnyBullet = false;
        bool consumesTurn = false;
        BulletInstance previousResolvedBullet = null;
        BulletRuntimeStateSnapshot previousPreFireState = default;
        bool hasPreviousPreFireState = false;
        float stackedDamageBonus = 0f;
        initialLoadedBulletCount = deckManager.LoadedBullets.Count;
        bulletsFiredThisCylinder = 0;
        criticalShotsThisCylinder = 0;
        bulletDestroyedThisCylinder = false;
        pendingSaverGold = 0;
        int physicalBulletIndex = 0;
        relicManager?.NotifyCylinderStarted(
            initialLoadedBulletCount,
            waveManager == null ? null : waveManager.ActiveEnemies,
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth);
        combatFeedback?.BeginFiringSequence();
        combatFeedback?.BeginCylinder();
        bool saverRefundsTurn = false;
        int initialBulletIndex = deckManager.LoadedBullets.Count - 1;
        BulletInstance initialResolvedBullet = ResolveShotBullet(
            deckManager.LoadedBullets[initialBulletIndex],
            null);
        bool initialBulletIsPowderPouch = FindSpecialEffect(
            initialResolvedBullet,
            BulletEffectType.PowderPouch) != null;
        bool fireIntoAir = initialBulletIsPowderPouch
            ? !HasViableFutureShot(
                initialBulletIndex - 1,
                initialResolvedBullet,
                horizontalDirection)
            : !HasViableShotTarget(
                initialResolvedBullet,
                horizontalDirection);

        while (deckManager.LoadedBullets.Count > 0)
        {
            while (GamePauseController.IsPaused)
            {
                yield return null;
            }

            int bulletIndex = deckManager.LoadedBullets.Count - 1;
            BulletInstance bulletData = deckManager.LoadedBullets[bulletIndex];

            if (bulletData == null)
            {
                break;
            }

            BulletInstance resolvedBullet = ResolveShotBullet(
                bulletData,
                previousResolvedBullet);
            BulletEffectData powderEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.PowderPouch);
            bool hasViableTarget = fireIntoAir
                || (powderEffect == null
                    ? HasViableShotTarget(
                        resolvedBullet,
                        horizontalDirection)
                    : HasViableFutureShot(
                        bulletIndex - 1,
                        resolvedBullet,
                        horizontalDirection));

            if (!hasViableTarget)
            {
                break;
            }

            if (!deckManager.TryFireLoadedBullet(out BulletInstance firedBullet)
                || firedBullet != bulletData)
            {
                break;
            }

            firedAnyBullet = true;
            currentConsumedBullet = firedBullet;
            int currentPhysicalBulletIndex = physicalBulletIndex++;
            consumesTurn |= !resolvedBullet.DoesNotConsumeTurn;

            bool clonedPreviousShot = resolvedBullet != firedBullet;

            if (clonedPreviousShot && hasPreviousPreFireState)
            {
                firedBullet.ApplyRuntimeState(previousPreFireState);
            }

            BulletRuntimeStateSnapshot currentPreFireState =
                firedBullet.CaptureRuntimeState();

            if (powderEffect != null)
            {
                ApplyPowderPouch(firedBullet, powderEffect.Amount);
                ShowBulletFeedback(bulletData);
            }
            else
            {
                float damageMultiplier = GetSpecialDamageMultiplier(
                    firedBullet,
                    resolvedBullet);
                bool isStackingShot = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.StackNextShot) != null;
                BulletEffectData distributorEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.Distributor);

                if (distributorEffect != null)
                {
                    float storageEfficiency = Mathf.Max(
                        0f,
                        distributorEffect.Amount / 100f);
                    firedBullet.AddStoredDamageBonus(
                        stackedDamageBonus * storageEfficiency);
                    stackedDamageBonus = 0f;

                    foreach (BulletInstance loadedBullet
                             in deckManager.LoadedBullets)
                    {
                        loadedBullet?.AddTemporaryDamageBonus(
                            firedBullet.StoredDamageBonus);
                    }
                }

                if (!isStackingShot && distributorEffect == null
                    && stackedDamageBonus > 0f)
                {
                    damageMultiplier *= 1f + stackedDamageBonus;
                    stackedDamageBonus = 0f;
                }

                BulletEffectData chainEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.ChainFire);
                float criticalChanceBonus =
                    firedBullet.ConsumeTemporaryCriticalChanceBonus();
                criticalChanceBonus += GetSpecialCriticalChanceBonus(
                    firedBullet,
                    resolvedBullet);
                BulletEffectData shellEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.ShellCollector);
                int shellExtraShots = GetAvailableShellExtraShots(
                    firedBullet,
                    shellEffect);
                int shellCost = shellEffect == null
                    ? 0
                    : Mathf.Max(1, shellEffect.StackCount);
                int additionalShotCount = 0;
                bool keepFiring;

                do
                {
                    bool shotCompleted = false;
                    yield return FireSingleShot(
                        resolvedBullet,
                        horizontalDirection,
                        damageMultiplier,
                        criticalChanceBonus,
                        true,
                        fireIntoAir,
                        additionalShotCount == 0,
                        false,
                        currentPhysicalBulletIndex,
                        completed => shotCompleted = completed);

                    if (!shotCompleted)
                    {
                        break;
                    }

                    keepFiring = chainEffect != null
                        && RollChainFire(chainEffect, additionalShotCount);

                    if (keepFiring)
                    {
                        additionalShotCount++;

                        if (shotInterval > 0f)
                        {
                            yield return WaitForShotInterval();
                        }
                    }
                }
                while (keepFiring);

                for (int shellShotIndex = 0;
                     shellShotIndex < shellExtraShots;
                     shellShotIndex++)
                {
                    bool shotCompleted = false;
                    yield return FireSingleShot(
                        resolvedBullet,
                        horizontalDirection,
                        damageMultiplier * shellEffect.Amount / 100f,
                        criticalChanceBonus,
                        false,
                        fireIntoAir,
                        false,
                        false,
                        currentPhysicalBulletIndex,
                        completed => shotCompleted = completed);

                    if (!shotCompleted)
                    {
                        break;
                    }

                    firedBullet.ConsumeAbilityStacks(shellCost);

                    if (shotInterval > 0f)
                    {
                        yield return WaitForShotInterval();
                    }
                }

                double memorialMultiplier = relicManager == null
                    ? 0d
                    : relicManager.GetMemorialExtraShotMultiplier();

                if (memorialMultiplier > 0d)
                {
                    relicManager?.NotifyMemorialShotTriggered();
                    bool memorialCompleted = false;
                    yield return FireSingleShot(
                        resolvedBullet,
                        horizontalDirection,
                        (float)Math.Min(
                            float.MaxValue,
                            damageMultiplier * memorialMultiplier),
                        criticalChanceBonus,
                        false,
                        fireIntoAir,
                        false,
                        true,
                        currentPhysicalBulletIndex,
                        completed => memorialCompleted = completed);
                }

                BulletEffectData stackEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.StackNextShot);

                if (stackEffect != null)
                {
                    stackedDamageBonus += stackEffect.Amount / 100f;
                }

                HandlePostBulletAbility(firedBullet, resolvedBullet);
            }

            BulletEffectData saverEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Saver);

            if (saverEffect != null)
            {
                pendingSaverGold += Mathf.RoundToInt(saverEffect.Amount);
                saverRefundsTurn |= saverEffect.StackCount >= 2;
            }

            previousResolvedBullet = resolvedBullet;
            previousPreFireState = currentPreFireState;
            hasPreviousPreFireState = true;
            currentConsumedBullet = null;

            if (deckManager.LoadedBullets.Count > 0 && shotInterval > 0f)
            {
                yield return WaitForShotInterval();
            }
            else
            {
                yield return null;
            }
        }

        if (!bulletDestroyedThisCylinder && pendingSaverGold > 0)
        {
            currencyManager ??= FindFirstObjectByType<CurrencyManager>();
            currencyManager?.AddMoneyFromWorld(
                pendingSaverGold,
                transform.position);
        }

        if (stackedDamageBonus > 0f
            && deckManager.LoadedBullets.Count > 0)
        {
            int nextBulletIndex = deckManager.LoadedBullets.Count - 1;
            deckManager.LoadedBullets[nextBulletIndex]
                ?.AddTemporaryDamageBonus(stackedDamageBonus);
        }

        if (!bulletDestroyedThisCylinder && saverRefundsTurn)
        {
            consumesTurn = false;
        }

        bool shouldCompleteTurn = firedAnyBullet && consumesTurn;
        isFiring = false;
        playerMove.SetShooting(false);
        combatFeedback?.EndCylinder();
        currentConsumedBullet = null;
        reservedDamageByEnemy.Clear();
        pendingEffectDefeats.Clear();
        waveManager?.NotifyFiringSequenceCompleted();
        deckManager.CompleteFiringSequence();
        relicManager?.NotifyCylinderCompleted(deckManager);

        if (shouldCompleteTurn
            && deckManager.TotalBulletCount > 0
            && (waveManager == null || !waveManager.IsBattleCompleted))
        {
            playerMove.CompleteTurn();
        }
    }

    private IEnumerator FireSingleShot(
        BulletInstance bulletData,
        int horizontalDirection,
        float damageMultiplier,
        float criticalChanceBonus,
        bool generatesShells,
        bool allowEmptyShot,
        bool isBaseBullet,
        bool isRelicGenerated,
        int physicalBulletIndex,
        Action<bool> onCompleted)
    {
        if (bulletData == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        activeShotIndex = bulletsFiredThisCylinder;

        bool hasEnemyTarget = RefreshViableTargets(
            bulletData,
            horizontalDirection);
        bool isBoardWideShot = IsBoardWideShot(bulletData);
        IPlayerBulletBlocker bulletBlocker = null;
        bool hasBulletBlocker = !isBoardWideShot
            && waveManager.TryGetFirstBulletBlocker(
                transform.position,
                horizontalDirection,
                bulletData.MaxRange,
                out bulletBlocker);

        if (hasBulletBlocker)
        {
            RemoveTargetsBehindBlocker(bulletBlocker, horizontalDirection);
            hasEnemyTarget = targetBuffer.Count > 0;
        }

        bool hasViableTarget = hasEnemyTarget || hasBulletBlocker;

        if (!hasViableTarget && !allowEmptyShot)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        relicManager?.NotifyShotStarted(
            isBaseBullet,
            isRelicGenerated,
            physicalBulletIndex,
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth,
            isBaseBullet && activeShotIndex == 0,
            isBaseBullet && deckManager != null
                && deckManager.LoadedBullets.Count == 0);

        Vector3 endPoint;

        bool reachesBulletBlocker = false;

        if (hasEnemyTarget)
        {
            BuildHitTargets(bulletData);
            reachesBulletBlocker = hasBulletBlocker
                && hitBuffer.Count == targetBuffer.Count
                && hitBuffer.Count < bulletData.MaxHitCount
                && bulletData.RollPenetrationAfterHit(hitBuffer.Count);
            endPoint = reachesBulletBlocker
                ? bulletBlocker.WorldPosition
                : hitBuffer[hitBuffer.Count - 1].transform.position;
        }
        else if (hasBulletBlocker)
        {
            hitBuffer.Clear();
            reachesBulletBlocker = true;
            endPoint = bulletBlocker.WorldPosition;
        }
        else
        {
            hitBuffer.Clear();
            endPoint = GetMissEndPoint(
                horizontalDirection,
                bulletData.MaxRange);
        }

        bool isCritical = relicManager != null
            && relicManager.CurrentShotForcesCritical
            || bulletData.CanTriggerCritical(
                UnityEngine.Random.Range(0f, 100f),
                criticalChanceBonus);
        List<DamageReservation> shotReservations = hasViableTarget
            ? ReserveProjectedHitDamage(
                bulletData,
                horizontalDirection,
                isCritical,
                damageMultiplier)
            : null;

        Vector3 shotStartPoint = firePoint.position;
        Vector3 shotEndPoint = GetShotLineEndPoint(shotStartPoint, endPoint);
        BulletLine bulletLine = Instantiate(
            bulletLinePrefab,
            shotStartPoint,
            Quaternion.identity);

        if (!bulletLine.Initialize(
                bulletData,
                shotStartPoint,
                shotEndPoint))
        {
            ReleaseProjectedDamage(shotReservations);
            Destroy(bulletLine.gameObject);
            relicManager?.NotifyShotCancelled();
            onCompleted?.Invoke(false);
            yield break;
        }

        ShowBulletFeedback(bulletData);
        if (isCritical && hasViableTarget)
        {
            SoundManager.PlaySfx("SFX_Critical");
        }
        SoundManager.PlaySfx(isCritical
            ? "SFX_Player_Critical_Shoot"
            : "SFX_Player_Shoot");
        combatFeedback?.RecordShotCameraShake();
        RecordSuccessfulShot(bulletData, isRelicGenerated);
        BulletFired?.Invoke(bulletData);
        GameStatistics.RecordBulletFired(bulletData);
        combatPresentation?.PlayShot(
            firePoint,
            bulletData,
            isCritical,
            horizontalDirection);
        yield return ApplyShotScopedEffects(bulletData, horizontalDirection);
        yield return ApplyHitResults(
            bulletData,
            horizontalDirection,
            isCritical,
            damageMultiplier);
        yield return ApplyEyeOfTheStormDamage(
            bulletData,
            horizontalDirection);

        if (reachesBulletBlocker && bulletBlocker != null
            && bulletBlocker.IsBulletBlocking)
        {
            bulletBlocker.HandlePlayerBulletImpact();
        }
        ReleaseProjectedDamage(shotReservations);
        HandleShotResult(bulletData, isCritical, generatesShells);
        onCompleted?.Invoke(true);
    }

    private void RecordSuccessfulShot(
        BulletInstance firedBullet,
        bool isRelicGenerated)
    {
        if (!isRelicGenerated && bulletsFiredThisCylinder < int.MaxValue)
        {
            bulletsFiredThisCylinder++;
        }

        if (deckManager == null || isRelicGenerated)
        {
            return;
        }

        foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
        {
            loadedBullet?.RecordShotWhileLoaded();
        }

        relicManager?.TryTriggerClosedCircuit(deckManager, firedBullet);
    }

    private float GetCurrentCylinderBuild()
    {
        if (initialLoadedBulletCount <= 1)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            (float)Mathf.Max(1, bulletsFiredThisCylinder)
            / initialLoadedBulletCount);
    }

    private bool RefreshViableTargets(
        BulletInstance bullet,
        int horizontalDirection)
    {
        targetBuffer.Clear();

        if (bullet == null || waveManager == null)
        {
            return false;
        }

        if (IsBoardWideShot(bullet))
        {
            foreach (EnemyController enemy in waveManager.ActiveEnemies)
            {
                if (HasProjectedDurability(enemy))
                {
                    targetBuffer.Add(enemy);
                }
            }

            SortTargetsByTileIndex(targetBuffer);
            return targetBuffer.Count > 0;
        }

        waveManager.GetEnemiesInDirection(
            transform.position,
            horizontalDirection,
            bullet.MaxRange,
            targetBuffer);

        for (int targetIndex = targetBuffer.Count - 1;
             targetIndex >= 0;
             targetIndex--)
        {
            if (!HasProjectedDurability(targetBuffer[targetIndex]))
            {
                targetBuffer.RemoveAt(targetIndex);
            }
        }

        return targetBuffer.Count > 0;
    }

    private bool HasViableShotTarget(
        BulletInstance bullet,
        int horizontalDirection)
    {
        if (RefreshViableTargets(bullet, horizontalDirection))
        {
            return true;
        }

        if (IsBoardWideShot(bullet))
        {
            return false;
        }

        return bullet != null && waveManager != null
            && waveManager.TryGetFirstBulletBlocker(
                transform.position,
                horizontalDirection,
                bullet.MaxRange,
                out _);
    }

    private void RemoveTargetsBehindBlocker(
        IPlayerBulletBlocker blocker,
        int horizontalDirection)
    {
        if (blocker == null || boardManager == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int originIndex))
        {
            return;
        }

        int direction = horizontalDirection >= 0 ? 1 : -1;
        int blockerDistance = (blocker.TileIndex - originIndex) * direction;

        for (int index = targetBuffer.Count - 1; index >= 0; index--)
        {
            EnemyController target = targetBuffer[index];

            if (target == null || !boardManager.TryGetTileIndex(
                    target.transform.position,
                    out int targetIndex)
                || (targetIndex - originIndex) * direction >= blockerDistance)
            {
                targetBuffer.RemoveAt(index);
            }
        }
    }

    private bool HasViableFutureShot(
        int loadedBulletIndex,
        BulletInstance previousResolvedBullet,
        int horizontalDirection)
    {
        for (int bulletIndex = loadedBulletIndex;
             bulletIndex >= 0;
             bulletIndex--)
        {
            BulletInstance loadedBullet = deckManager.LoadedBullets[bulletIndex];
            BulletInstance resolvedBullet = ResolveShotBullet(
                loadedBullet,
                previousResolvedBullet);

            if (resolvedBullet == null)
            {
                continue;
            }

            if (FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.PowderPouch) == null
                && HasViableShotTarget(
                    resolvedBullet,
                    horizontalDirection))
            {
                return true;
            }

            previousResolvedBullet = resolvedBullet;
        }

        return false;
    }

    private bool HasProjectedDurability(EnemyController enemy)
    {
        if (enemy == null || enemy.CurrentHealth <= 0)
        {
            return false;
        }

        reservedDamageByEnemy.TryGetValue(enemy, out int reservedDamage);
        long projectedDurability = (long)enemy.CurrentHealth
            + enemy.CurrentShield
            - reservedDamage;
        return projectedDurability > 0;
    }

    private List<DamageReservation> ReserveProjectedHitDamage(
        BulletInstance bullet,
        int horizontalDirection,
        bool isCritical,
        float damageMultiplier)
    {
        List<DamageReservation> reservations =
            new List<DamageReservation>(hitBuffer.Count);

        foreach (EnemyController enemy in hitBuffer)
        {
            if (!HasProjectedDurability(enemy))
            {
                continue;
            }

            float targetDamageMultiplier = GetTargetDamageMultiplier(
                bullet,
                enemy,
                horizontalDirection);
            targetDamageMultiplier *= (float)(relicManager == null
                ? 1d
                : relicManager.GetTargetConditionalDamageMultiplier(
                    enemy.GetInstanceID(),
                    enemy.ActiveStatusTypeCount));
            int attackDamage = CalculateAttackDamage(
                bullet,
                isCritical,
                damageMultiplier * targetDamageMultiplier,
                activeShotIndex,
                deckManager != null
                    && deckManager.LoadedBullets.Count == 0);
            int predictedDamage = enemy.PredictAttackDamage(attackDamage);

            if (predictedDamage <= 0)
            {
                continue;
            }

            reservedDamageByEnemy.TryGetValue(
                enemy,
                out int existingReservation);
            long combinedReservation =
                (long)existingReservation + predictedDamage;
            reservedDamageByEnemy[enemy] = combinedReservation >= int.MaxValue
                ? int.MaxValue
                : (int)combinedReservation;
            reservations.Add(new DamageReservation(enemy, predictedDamage));
        }

        return reservations;
    }

    private void ReleaseProjectedDamage(
        IReadOnlyList<DamageReservation> reservations)
    {
        if (reservations == null)
        {
            return;
        }

        foreach (DamageReservation reservation in reservations)
        {
            EnemyController enemy = reservation.Enemy;

            if (ReferenceEquals(enemy, null)
                || !reservedDamageByEnemy.TryGetValue(
                    enemy,
                    out int reservedDamage))
            {
                continue;
            }

            int remainingReservation =
                Mathf.Max(0, reservedDamage - reservation.Damage);

            if (remainingReservation == 0)
            {
                reservedDamageByEnemy.Remove(enemy);
            }
            else
            {
                reservedDamageByEnemy[enemy] = remainingReservation;
            }
        }
    }

    private BulletInstance ResolveShotBullet(
        BulletInstance loadedBullet,
        BulletInstance previousResolvedBullet)
    {
        if (loadedBullet == null || previousResolvedBullet == null)
        {
            return loadedBullet;
        }

        return FindSpecialEffect(
                loadedBullet,
                BulletEffectType.ClonePreviousShot) == null
            ? loadedBullet
            : previousResolvedBullet;
    }

    private void ApplyPowderPouch(
        BulletInstance powderPouch,
        float criticalChanceBonus)
    {
        if (deckManager == null)
        {
            return;
        }

        foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
        {
            loadedBullet?.AddTemporaryCriticalChance(criticalChanceBonus);
        }

        if (deckManager.TryDestroyBullet(powderPouch))
        {
            HandleBulletDestroyed(powderPouch);
        }
    }

    private float GetSpecialDamageMultiplier(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet)
    {
        float multiplier = 1f;
        BulletEffectData jackpotEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Jackpot);

        if (jackpotEffect != null && deckManager.LoadedBullets.Count == 0)
        {
            multiplier *= Mathf.Max(1f, jackpotEffect.Amount / 100f);
        }

        BulletEffectData resonanceEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Resonance);

        if (resonanceEffect != null)
        {
            int otherResonanceCount = 0;

            foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
            {
                if (FindSpecialEffect(
                        loadedBullet,
                        BulletEffectType.Resonance) != null)
                {
                    otherResonanceCount++;
                }
            }

            multiplier *= 1f
                + otherResonanceCount * resonanceEffect.Amount / 100f;
        }

        BulletEffectData cloneEffect = FindSpecialEffect(
            firedBullet,
            BulletEffectType.ClonePreviousShot);

        if (cloneEffect != null && resolvedBullet != firedBullet)
        {
            multiplier *= Mathf.Max(1f, cloneEffect.Amount / 100f);
        }

        multiplier *= 1f + firedBullet.ConsumeTemporaryDamageBonus();

        BulletEffectData gildedEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Gilded);

        if (gildedEffect != null && currencyManager != null)
        {
            int goldUnit = Mathf.Max(1, gildedEffect.StackCount);
            multiplier *= 1f + currencyManager.CurrentMoney / goldUnit
                * gildedEffect.Amount / 100f;
        }

        BulletEffectData heartEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Heart);

        if (heartEffect != null && playerHealth != null)
        {
            int healthUnit = Mathf.Max(1, heartEffect.StackCount);
            multiplier *= 1f + playerHealth.MaxHealth / healthUnit
                * heartEffect.Amount / 100f;
        }

        BulletEffectData loaderEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Loader);

        if (loaderEffect != null)
        {
            int emptyChambers = Mathf.Max(
                0,
                deckManager.MaxReloadAmount - initialLoadedBulletCount);
            multiplier *= 1f
                + emptyChambers * loaderEffect.Amount / 100f;
        }

        BulletEffectData chargeEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Charge);

        if (chargeEffect != null)
        {
            int charges = Mathf.Min(
                firedBullet.ShotsObservedWhileLoaded,
                chargeEffect.StackCount);
            multiplier *= 1f + charges * chargeEffect.Amount / 100f;
        }

        BulletEffectData accumulatorEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Accumulator);

        if (accumulatorEffect != null)
        {
            multiplier *= 1f + firedBullet.AbilityStacks
                * accumulatorEffect.Amount / 100f;
        }

        BulletEffectData devourerEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Devourer);

        if (devourerEffect != null)
        {
            multiplier *= 1f + firedBullet.PermanentStacks
                * devourerEffect.Amount / 100f;
        }

        BulletEffectData legacyEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Legacy);

        if (legacyEffect != null)
        {
            multiplier *= 1f + firedBullet.PermanentStacks
                * legacyEffect.Amount / 100f;
        }

        BulletEffectData collectionEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Collection);

        if (collectionEffect != null)
        {
            multiplier *= 1f + CountDistinctOwnedBulletTypes()
                * collectionEffect.Amount / 100f;
        }

        BulletEffectData mixedGradeEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.MixedGrade);

        if (mixedGradeEffect != null)
        {
            int otherGradeCount = 0;

            foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
            {
                if (loadedBullet != null
                    && loadedBullet.Grade != firedBullet.Grade)
                {
                    otherGradeCount++;
                }
            }

            multiplier *= 1f + otherGradeCount
                * mixedGradeEffect.Amount / 100f;
        }

        BulletEffectData masterpieceEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Masterpiece);

        if (masterpieceEffect != null)
        {
            multiplier *= 1f + CountOwnedBulletsByGrade(
                    BulletGrade.Ace,
                    BulletGrade.Legendary)
                * masterpieceEffect.Amount / 100f;
        }

        BulletEffectData massProducedEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.MassProduced);

        if (massProducedEffect != null)
        {
            multiplier *= 1f + CountOwnedBulletsByGrade(
                    BulletGrade.Normal,
                    BulletGrade.Rare)
                * massProducedEffect.Amount / 100f;
        }

        BulletEffectData monopolyEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Monopoly);

        if (monopolyEffect != null)
        {
            multiplier *= 1f + GetMostCommonOwnedGradeCount()
                * monopolyEffect.Amount / 100f;
        }

        return multiplier;
    }

    private int CountDistinctOwnedBulletTypes()
    {
        deckManager.GetOwnedBullets(ownedBulletBuffer);
        ownedBulletTypeBuffer.Clear();

        foreach (BulletInstance bullet in ownedBulletBuffer)
        {
            if (bullet?.Data != null)
            {
                ownedBulletTypeBuffer.Add(bullet.Data);
            }
        }

        return ownedBulletTypeBuffer.Count;
    }

    private int CountOwnedBulletsByGrade(
        BulletGrade first,
        BulletGrade second)
    {
        deckManager.GetOwnedBullets(ownedBulletBuffer);
        int count = 0;

        foreach (BulletInstance bullet in ownedBulletBuffer)
        {
            if (bullet != null
                && (bullet.Grade == first || bullet.Grade == second))
            {
                count++;
            }
        }

        return count;
    }

    private int GetMostCommonOwnedGradeCount()
    {
        deckManager.GetOwnedBullets(ownedBulletBuffer);
        Array.Clear(ownedGradeCountBuffer, 0, ownedGradeCountBuffer.Length);

        foreach (BulletInstance bullet in ownedBulletBuffer)
        {
            if (bullet != null)
            {
                int index = Mathf.Clamp((int)bullet.Grade, 0, 3);
                ownedGradeCountBuffer[index]++;
            }
        }

        return Mathf.Max(
            ownedGradeCountBuffer[0],
            ownedGradeCountBuffer[1],
            ownedGradeCountBuffer[2],
            ownedGradeCountBuffer[3]);
    }

    private float GetSpecialCriticalChanceBonus(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet)
    {
        float bonus = 0f;
        BulletEffectData coagulationEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Coagulation);

        if (coagulationEffect != null && playerHealth != null
            && playerHealth.MaxHealth > 0)
        {
            float missingPercent = 100f
                * (playerHealth.MaxHealth - playerHealth.CurrentHealth)
                / playerHealth.MaxHealth;
            bonus += Mathf.Floor(
                    missingPercent
                    / Mathf.Max(1, coagulationEffect.StackCount))
                * coagulationEffect.Amount;
        }

        BulletEffectData focusEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Focus);

        if (focusEffect != null)
        {
            bonus += firedBullet.AbilityStacks * focusEffect.Amount;
        }

        return bonus;
    }

    private static int GetAvailableShellExtraShots(
        BulletInstance firedBullet,
        BulletEffectData shellEffect)
    {
        if (firedBullet == null || shellEffect == null)
        {
            return 0;
        }

        int shellCost = Mathf.Max(1, shellEffect.StackCount);
        int maxExtraShots = Mathf.Max(1, shellEffect.KnockbackDistance);
        int extraShots = Mathf.Min(
            firedBullet.AbilityStacks / shellCost,
            maxExtraShots);
        return extraShots;
    }

    private void HandlePostBulletAbility(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet)
    {
        BulletEffectData accumulatorEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Accumulator);

        if (accumulatorEffect != null)
        {
            float retentionRatio = Mathf.Clamp(
                accumulatorEffect.KnockbackDistance,
                0,
                100) / 100f;
            firedBullet.SetAbilityStacks(
                Mathf.CeilToInt(
                    firedBullet.AbilityStacks * retentionRatio));
        }
    }

    private void HandleShotResult(
        BulletInstance resolvedBullet,
        bool isCritical,
        bool generatesShells)
    {
        BulletInstance stateOwner = currentConsumedBullet ?? resolvedBullet;
        BulletEffectData focusEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Focus);

        if (focusEffect != null)
        {
            if (isCritical)
            {
                stateOwner.SetAbilityStacks(0);
            }
        }

        if (!isCritical)
        {
            GrantFocusStacksToRemainingLoadedBullets();
        }

        if (isCritical)
        {
            criticalShotsThisCylinder++;
            GrantAbilityStacksToOwned(
                BulletEffectType.Accumulator,
                1,
                stateOwner);

            BulletEffectData rebateEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Rebate);

            if (rebateEffect != null && rebateEffect.RollActivation())
            {
                currencyManager ??= FindFirstObjectByType<CurrencyManager>();
                currencyManager?.AddMoneyFromWorld(
                    Mathf.RoundToInt(rebateEffect.Amount),
                    transform.position);
            }
        }

        if (generatesShells)
        {
            GrantAbilityStacksToOwned(
                BulletEffectType.ShellCollector,
                1,
                stateOwner);
        }

        relicManager?.NotifyShotCompleted();
    }

    private void GrantFocusStacksToRemainingLoadedBullets()
    {
        if (deckManager == null)
        {
            return;
        }

        foreach (BulletInstance bullet in deckManager.LoadedBullets)
        {
            if (bullet == null)
            {
                continue;
            }

            BulletEffectData focusEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Focus);
            if (focusEffect != null)
            {
                bullet.AddAbilityStacks(
                    Mathf.Max(1, focusEffect.StackCount));
            }
        }
    }

    private void GrantAbilityStacksToOwned(
        BulletEffectType effectType,
        int amount,
        BulletInstance excludedBullet)
    {
        if (deckManager == null || amount <= 0)
        {
            return;
        }

        deckManager.GetOwnedBullets(ownedBulletBuffer);

        foreach (BulletInstance ownedBullet in ownedBulletBuffer)
        {
            if (ownedBullet != null && ownedBullet != excludedBullet
                && FindSpecialEffect(ownedBullet, effectType) != null)
            {
                ownedBullet.AddAbilityStacks(amount);
            }
        }
    }

    private static bool RollChainFire(
        BulletEffectData chainEffect,
        int additionalShotCount)
    {
        if (chainEffect == null
            || additionalShotCount >= chainEffect.StackCount)
        {
            return false;
        }

        float chance = Mathf.Clamp(
            chainEffect.ActivationChance
                - chainEffect.Amount * additionalShotCount,
            0f,
            100f);
        return chance >= 100f
            || chance > 0f
            && UnityEngine.Random.Range(0f, 100f) < chance;
    }

    private static BulletEffectData FindSpecialEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        if (bullet == null)
        {
            return null;
        }

        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect != null && effect.EffectType == effectType)
            {
                return effect;
            }
        }

        return null;
    }

    private void BuildHitTargets(BulletInstance bulletData)
    {
        hitBuffer.Clear();

        if (IsBoardWideShot(bulletData))
        {
            hitBuffer.AddRange(targetBuffer);
            return;
        }

        hitBuffer.Add(targetBuffer[0]);
        int hitCount = 1;

        for (int targetIndex = 1;
             targetIndex < targetBuffer.Count
             && hitCount < bulletData.MaxHitCount;
             targetIndex++)
        {
            if (!bulletData.RollPenetrationAfterHit(hitCount))
            {
                break;
            }

            hitBuffer.Add(targetBuffer[targetIndex]);
            hitCount++;
        }
    }

    private IEnumerator ApplyHitResults(
        BulletInstance bulletData,
        int horizontalDirection,
        bool isCritical,
        float damageMultiplier)
    {
        if (bulletData == null || hitBuffer.Count == 0)
        {
            yield break;
        }

        List<BulletHitTarget> shotTargets =
            new List<BulletHitTarget>(hitBuffer.Count);

        foreach (EnemyController hitTarget in hitBuffer)
        {
            if (hitTarget != null && hitTarget.CurrentHealth > 0)
            {
                shotTargets.Add(new BulletHitTarget(hitTarget));
            }
        }

        HashSet<int> processedDefeatIds = new HashSet<int>();

        for (int hitIndex = 0; hitIndex < shotTargets.Count; hitIndex++)
        {
            BulletHitTarget shotTarget = shotTargets[hitIndex];
            EnemyController enemy = shotTarget.Enemy;

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                yield return ApplyDefeatTriggeredAbilities(
                    bulletData,
                    enemy,
                    shotTarget.InstanceId,
                    horizontalDirection,
                    0,
                    shotTarget.InitialPosition,
                    processedDefeatIds);
                continue;
            }

            CombatPresentation.EnemySnapshot enemySnapshot =
                combatPresentation == null
                    ? default
                    : combatPresentation.CaptureEnemy(enemy);
            int sourceTileIndex = -1;
            boardManager.TryGetTileIndex(
                enemy.transform.position,
                out sourceTileIndex);
            int healthBeforeHit = enemy.CurrentHealth;
            int targetMaxHealth = enemy.MaxHealth;
            bool defeatPresented = false;
            float targetDamageMultiplier = GetTargetDamageMultiplier(
                bulletData,
                enemy,
                horizontalDirection);
            targetDamageMultiplier *= (float)(relicManager == null
                ? 1d
                : relicManager.GetTargetConditionalDamageMultiplier(
                    enemy.GetInstanceID(),
                    enemy.ActiveStatusTypeCount));
            int attackDamage = CalculateAttackDamage(
                bulletData,
                isCritical,
                damageMultiplier * targetDamageMultiplier,
                activeShotIndex,
                deckManager != null
                    && deckManager.LoadedBullets.Count == 0);

            if (hitIndex > 0 && !IsBoardWideShot(bulletData))
            {
                yield return ApplyConditionalEvents(
                    bulletData,
                    BulletConditionalTrigger.Penetration,
                    enemy,
                    horizontalDirection,
                    0);
            }

            if (isCritical)
            {
                yield return ApplyConditionalEvents(
                    bulletData,
                    BulletConditionalTrigger.CriticalHit,
                    enemy,
                    horizontalDirection,
                    0);
            }

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                bool preAttackEffectDefeatAlreadyRecorded =
                    pendingEffectDefeats.Remove(enemy);
                combatPresentation?.PlayImpact(
                    enemySnapshot,
                    horizontalDirection,
                    bulletData,
                    CombatImpactTier.Defeat,
                    combatFeedback?.NextFiringSequenceDefeatFeedbackMultiplier
                        ?? 1f);
                if (!preAttackEffectDefeatAlreadyRecorded)
                {
                    combatFeedback?.RecordDefeat(
                        enemySnapshot.Position,
                        horizontalDirection,
                        0,
                        targetMaxHealth,
                        isCritical,
                        waveManager != null
                            && waveManager.ActiveEnemies.Count <= 1,
                        GetCurrentCylinderBuild());
                }
                yield return ApplyDefeatTriggeredAbilities(
                    bulletData,
                    enemy,
                    shotTarget.InstanceId,
                    horizontalDirection,
                    0,
                    enemySnapshot.Position,
                    processedDefeatIds);
                continue;
            }

            int reportedDamage = enemy.PredictAttackDamage(attackDamage);
            int appliedDamage = enemy.ApplyAttackDamage(
                attackDamage,
                isCritical);
            if (appliedDamage > 0)
            {
                DamageDealt?.Invoke(reportedDamage);
                relicManager?.NotifyEnemyDamaged(enemy, reportedDamage);
            }
            bool defeatedByAttack = healthBeforeHit > 0
                && enemy.CurrentHealth <= 0;
            combatFeedback?.RecordDamage(
                reportedDamage,
                reportedDamage > appliedDamage);
            combatPresentation?.PlayImpact(
                enemySnapshot,
                horizontalDirection,
                bulletData,
                CombatImpactTierUtility.Resolve(
                    isCritical,
                    reportedDamage,
                    targetMaxHealth,
                    defeatedByAttack),
                defeatedByAttack
                    ? combatFeedback
                        ?.NextFiringSequenceDefeatFeedbackMultiplier ?? 1f
                    : 1f);
            defeatPresented = defeatedByAttack;

            if (defeatedByAttack)
            {
                combatFeedback?.RecordDefeat(
                    enemySnapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    isCritical,
                    waveManager != null && waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild(),
                    healthBeforeHit);
            }
            else if (appliedDamage > 0)
            {
                combatFeedback?.RecordHit(
                    enemySnapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    isCritical,
                    GetCurrentCylinderBuild());
            }
            ManagedEffectDefeatResult managedEffectDefeat = default;

            yield return ApplyWallImpactDamageTransfer(
                bulletData,
                sourceTileIndex,
                horizontalDirection,
                attackDamage,
                processedDefeatIds);

            IReadOnlyList<BulletEffectData> effects = bulletData.Effects;

            for (int effectIndex = 0;
                 effectIndex < effects.Count;
                effectIndex++)
            {
                BulletEffectData effect = effects[effectIndex];

                if (effect == null)
                {
                    continue;
                }

                if (IsShotScopedEffect(effect.EffectType)
                    || IsManagedSpecialEffect(effect.EffectType))
                {
                    continue;
                }

                if (!effect.RollActivation())
                {
                    continue;
                }

                bool effectApplied = false;
                yield return ApplyBulletEffect(
                    effect,
                    bulletData,
                    enemy,
                    horizontalDirection,
                    appliedDamage,
                    applied => effectApplied = applied);

                if (effectApplied)
                {
                    yield return ApplyConditionalEvents(
                        bulletData,
                        BulletConditionalTrigger.EffectApplied,
                        enemy,
                        horizontalDirection,
                        appliedDamage);
                }
            }

            yield return ApplyManagedTargetEffects(
                bulletData,
                enemy,
                horizontalDirection,
                result => managedEffectDefeat = result);

            bool effectDefeatAlreadyRecorded =
                pendingEffectDefeats.Remove(enemy);

            bool defeatedByManagedEffect =
                managedEffectDefeat.WasDefeated;

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                if (!defeatPresented)
                {
                    combatPresentation?.PlayImpact(
                        enemySnapshot,
                        horizontalDirection,
                        bulletData,
                        CombatImpactTier.Defeat,
                        combatFeedback
                            ?.NextFiringSequenceDefeatFeedbackMultiplier ?? 1f);
                    if (!effectDefeatAlreadyRecorded)
                    {
                        combatFeedback?.RecordDefeat(
                            defeatedByManagedEffect
                                ? managedEffectDefeat.WorldPosition
                                : enemySnapshot.Position,
                            horizontalDirection,
                            defeatedByManagedEffect
                                ? managedEffectDefeat.Damage
                                : appliedDamage,
                            defeatedByManagedEffect
                                ? managedEffectDefeat.TargetMaxHealth
                                : targetMaxHealth,
                            isCritical,
                            waveManager != null
                                && waveManager.ActiveEnemies.Count <= 1,
                            GetCurrentCylinderBuild(),
                            defeatedByManagedEffect
                                ? managedEffectDefeat.HealthBeforeDamage
                                : -1);
                    }
                }

                yield return ApplyDefeatTriggeredAbilities(
                    bulletData,
                    enemy,
                    shotTarget.InstanceId,
                    horizontalDirection,
                    appliedDamage,
                    enemySnapshot.Position,
                    processedDefeatIds);
            }
        }
    }

    private float GetTargetDamageMultiplier(
        BulletInstance bullet,
        EnemyController enemy,
        int horizontalDirection)
    {
        float multiplier = 1f;
        BulletEffectData rangefinderEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.Rangefinder);

        if (rangefinderEffect != null && boardManager.TryGetTileDistance(
                transform.position,
                enemy.transform.position,
                out int tileDistance))
        {
            multiplier *= 1f
                + tileDistance * rangefinderEffect.Amount / 100f;
        }

        BulletEffectData judgmentEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.Judgment);

        if (judgmentEffect != null)
        {
            multiplier *= 1f + enemy.TotalStatusStackCount
                * judgmentEffect.Amount / 100f;
        }

        return multiplier;
    }

    private IEnumerator ApplyEyeOfTheStormDamage(
        BulletInstance sourceBullet,
        int horizontalDirection)
    {
        if (relicManager == null
            || !relicManager.TryConsumeEyeOfTheStormDamage(
                out int stormDamage)
            || stormDamage <= 0 || waveManager == null)
        {
            yield break;
        }

        List<EnemyController> targets =
            new List<EnemyController>(waveManager.ActiveEnemies);

        foreach (EnemyController enemy in targets)
        {
            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            CombatPresentation.EnemySnapshot snapshot =
                combatPresentation == null
                    ? default
                    : combatPresentation.CaptureEnemy(enemy);
            int healthBeforeDamage = enemy.CurrentHealth;
            int targetMaxHealth = enemy.MaxHealth;
            int reportedDamage = enemy.PredictAttackDamage(stormDamage);
            int appliedDamage = enemy.ApplyAttackDamage(stormDamage, false);

            if (appliedDamage > 0)
            {
                DamageDealt?.Invoke(reportedDamage);
                combatFeedback?.RecordDamage(
                    reportedDamage,
                    reportedDamage > appliedDamage);
            }

            bool defeated = healthBeforeDamage > 0
                && enemy.CurrentHealth <= 0;
            combatPresentation?.PlayImpact(
                snapshot,
                horizontalDirection,
                sourceBullet,
                CombatImpactTierUtility.Resolve(
                    false,
                    reportedDamage,
                    targetMaxHealth,
                    defeated),
                defeated
                    ? combatFeedback
                        ?.NextFiringSequenceDefeatFeedbackMultiplier ?? 1f
                    : 1f);

            if (defeated)
            {
                combatFeedback?.RecordDefeat(
                    snapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    false,
                    waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild(),
                    healthBeforeDamage);
                relicManager.NotifyEnemyDefeated(
                    enemy,
                    null,
                    waveManager.ActiveEnemies,
                    boardManager);
            }
            else if (appliedDamage > 0)
            {
                combatFeedback?.RecordHit(
                    snapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    false,
                    GetCurrentCylinderBuild());
            }

            yield return null;
        }
    }

    private IEnumerator ApplyWallImpactDamageTransfer(
        BulletInstance bullet,
        int sourceTileIndex,
        int horizontalDirection,
        int sourceAttackDamage,
        HashSet<int> processedDefeatIds)
    {
        BulletEffectData effect = FindSpecialEffect(
            bullet,
            BulletEffectType.WallImpact);

        if (effect == null || sourceTileIndex < 0
            || sourceAttackDamage <= 0 || boardManager == null
            || waveManager == null)
        {
            yield break;
        }

        int direction = horizontalDirection >= 0 ? 1 : -1;

        int maxTransferDistance = Mathf.Clamp(
            effect.KnockbackDistance,
            1,
            3);

        for (int distance = 1;
             distance <= maxTransferDistance;
             distance++)
        {
            float transferPercent = GetWallImpactTransferPercent(
                effect,
                distance);

            if (transferPercent <= 0f)
            {
                continue;
            }

            int targetTileIndex = sourceTileIndex + direction * distance;

            if (targetTileIndex < 0
                || targetTileIndex >= boardManager.BoardCount
                || !waveManager.TryGetEnemyAtTile(
                    targetTileIndex,
                    out EnemyController targetEnemy)
                || targetEnemy == null || targetEnemy.CurrentHealth <= 0)
            {
                continue;
            }

            int transferDamage = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    sourceAttackDamage * transferPercent / 100f));
            CombatPresentation.EnemySnapshot targetSnapshot =
                combatPresentation == null
                    ? default
                    : combatPresentation.CaptureEnemy(targetEnemy);
            int healthBeforeTransfer = targetEnemy.CurrentHealth;
            int targetMaxHealth = targetEnemy.MaxHealth;
            int targetInstanceId = targetEnemy.GetInstanceID();
            int reportedDamage = targetEnemy.PredictAttackDamage(
                transferDamage);
            int appliedDamage = targetEnemy.ApplyAttackDamage(
                transferDamage,
                false);

            if (appliedDamage > 0)
            {
                DamageDealt?.Invoke(reportedDamage);
                relicManager?.NotifyEnemyDamaged(
                    targetEnemy,
                    reportedDamage);
            }

            bool defeated = healthBeforeTransfer > 0
                && targetEnemy.CurrentHealth <= 0;
            combatFeedback?.RecordDamage(
                reportedDamage,
                reportedDamage > appliedDamage);
            combatPresentation?.PlayImpact(
                targetSnapshot,
                horizontalDirection,
                bullet,
                CombatImpactTierUtility.Resolve(
                    false,
                    reportedDamage,
                    targetMaxHealth,
                    defeated),
                defeated
                    ? combatFeedback
                        ?.NextFiringSequenceDefeatFeedbackMultiplier ?? 1f
                    : 1f);

            if (defeated)
            {
                combatFeedback?.RecordDefeat(
                    targetSnapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    false,
                    waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild(),
                    healthBeforeTransfer);
                yield return ApplyDefeatTriggeredAbilities(
                    bullet,
                    targetEnemy,
                    targetInstanceId,
                    horizontalDirection,
                    reportedDamage,
                    targetSnapshot.Position,
                    processedDefeatIds);
            }
            else if (appliedDamage > 0)
            {
                combatFeedback?.RecordHit(
                    targetSnapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    false,
                    GetCurrentCylinderBuild());
            }
        }
    }

    private IEnumerator ApplyDefeatTriggeredAbilities(
        BulletInstance bullet,
        EnemyController enemy,
        int enemyInstanceId,
        int horizontalDirection,
        int appliedDamage,
        Vector3 worldPosition,
        HashSet<int> processedDefeatIds)
    {
        if (bullet == null || processedDefeatIds == null
            || !processedDefeatIds.Add(enemyInstanceId))
        {
            yield break;
        }

        relicManager?.NotifyEnemyDefeated(
            enemy,
            currentConsumedBullet ?? bullet,
            waveManager == null ? null : waveManager.ActiveEnemies,
            boardManager);

        yield return ApplyConditionalEvents(
            bullet,
            BulletConditionalTrigger.EnemyDefeated,
            enemy,
            horizontalDirection,
            appliedDamage,
            worldPosition);
        GrantDevourerStack(bullet);
    }

    private static float GetWallImpactTransferPercent(
        BulletEffectData effect,
        int distance)
    {
        if (effect == null)
        {
            return 0f;
        }

        return distance switch
        {
            1 => effect.Amount,
            2 => effect.SecondTransferPercent,
            3 => effect.ThirdTransferPercent,
            _ => 0f
        };
    }

    private IEnumerator ApplyManagedTargetEffects(
        BulletInstance bullet,
        EnemyController enemy,
        int horizontalDirection,
        Action<ManagedEffectDefeatResult> onCompleted)
    {
        if (bullet == null || enemy == null || enemy.CurrentHealth <= 0)
        {
            onCompleted?.Invoke(default);
            yield break;
        }

        BulletEffectData amplifierEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.StatusAmplifier);

        if (amplifierEffect != null && amplifierEffect.RollActivation())
        {
            enemy.MultiplyActiveStatusStacks(
                Mathf.Max(2, Mathf.RoundToInt(amplifierEffect.Amount)));
        }

        ManagedEffectDefeatResult defeatResult = default;
        BulletEffectData venomBurstEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.VenomBurst);

        if (venomBurstEffect != null && venomBurstEffect.RollActivation())
        {
            int poisonStacks = enemy.ConsumeStatusStacks(
                StatusEffectType.Poison);

            if (poisonStacks > 0)
            {
                long remainingPoisonDamage =
                    (long)poisonStacks * ((long)poisonStacks + 1) / 2;
                double scaledPoisonDamage = Math.Min(
                    int.MaxValue,
                    Math.Ceiling(
                        remainingPoisonDamage
                        * venomBurstEffect.Amount / 100d));
                int poisonDamage = (int)scaledPoisonDamage;
                int healthBeforePoison = enemy.CurrentHealth;
                int poisonTargetMaxHealth = enemy.MaxHealth;
                Vector3 poisonImpactPosition = enemy.transform.position;
                int appliedPoisonDamage = enemy.ApplyStatusDamageAmount(
                    poisonDamage,
                    true,
                    false);
                bool defeated = healthBeforePoison > 0
                    && enemy.CurrentHealth <= 0;

                if (defeated)
                {
                    defeatResult = new ManagedEffectDefeatResult(
                        poisonDamage,
                        healthBeforePoison,
                        poisonTargetMaxHealth,
                        poisonImpactPosition);
                }

                if (!defeated && appliedPoisonDamage > 0)
                {
                    combatFeedback?.RecordHit(
                        poisonImpactPosition,
                        horizontalDirection,
                        poisonDamage,
                        poisonTargetMaxHealth,
                        false,
                        GetCurrentCylinderBuild(),
                        false);
                }
            }

            if (!defeatResult.WasDefeated
                && enemy != null && enemy.CurrentHealth > 0
                && venomBurstEffect.KnockbackDistance > 0)
            {
                enemy.AddStatusEffect(
                    StatusEffectType.Poison,
                    venomBurstEffect.KnockbackDistance,
                    true);
            }
        }

        onCompleted?.Invoke(defeatResult);
        yield break;
    }

    private void GrantDevourerStack(BulletInstance resolvedBullet)
    {
        BulletEffectData devourerEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Devourer);

        if (devourerEffect != null)
        {
            (currentConsumedBullet ?? resolvedBullet)?.AddPermanentStacks(
                Mathf.Max(1, devourerEffect.StackCount));
        }
    }

    private IEnumerator ApplyShotScopedEffects(
        BulletInstance sourceBullet,
        int horizontalDirection)
    {
        if (sourceBullet == null)
        {
            yield break;
        }

        IReadOnlyList<BulletEffectData> effects = sourceBullet.Effects;

        foreach (BulletEffectData effect in effects)
        {
            if (effect == null || !IsShotScopedEffect(effect.EffectType)
                || !effect.RollActivation())
            {
                continue;
            }

            yield return ApplyBulletEffect(
                effect,
                sourceBullet,
                null,
                horizontalDirection,
                0,
                null);
        }
    }

    private IEnumerator ApplyBulletEffect(
        BulletEffectData effect,
        BulletInstance sourceBullet,
        EnemyController enemy,
        int horizontalDirection,
        int appliedDamage,
        Action<bool> onCompleted,
        Vector3? sourceWorldPosition = null)
    {
        if (effect == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (IsPlayerOnlyEffect(effect.EffectType)
            && effect.Target != BulletEffectTarget.FiringPlayer)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        bool applied = false;

        switch (effect.EffectType)
        {
            case BulletEffectType.LifeSteal:
                applied = playerHealth.Heal(appliedDamage);

                if (applied && enemy != null)
                {
                    enemy.ShowLifeStealStatus();
                }
                break;
            case BulletEffectType.IncreaseMaxHealth:
                applied = playerHealth.IncreaseMaxHealth(
                    Mathf.RoundToInt(effect.Amount));
                break;
            case BulletEffectType.DestroyBullet:
                BulletInstance destroyedBullet =
                    currentConsumedBullet ?? sourceBullet;
                applied = deckManager != null
                    && deckManager.TryDestroyBullet(destroyedBullet);

                if (applied)
                {
                    HandleBulletDestroyed(destroyedBullet);
                }
                break;
            case BulletEffectType.GainGold:
                currencyManager ??= FindFirstObjectByType<CurrencyManager>();
                applied = currencyManager != null
                    && currencyManager.AddMoneyFromWorld(
                        Mathf.RoundToInt(effect.Amount),
                        sourceWorldPosition
                        ?? (enemy != null
                            ? enemy.transform.position
                            : transform.position));
                break;
        }

        if (IsPlayerOnlyEffect(effect.EffectType))
        {
            onCompleted?.Invoke(applied);
            yield break;
        }

        switch (effect.Target)
        {
            case BulletEffectTarget.FiringPlayer:
                applied = ApplyEffectToPlayer(effect);
                break;
            case BulletEffectTarget.HitEnemy:
                yield return ApplyEffectToEnemy(
                    effect,
                    enemy,
                    horizontalDirection,
                    result => applied = result);
                break;
            case BulletEffectTarget.AllEnemies:
                if (waveManager != null)
                {
                    List<EnemyController> enemies =
                        new List<EnemyController>(waveManager.ActiveEnemies);

                    foreach (EnemyController activeEnemy in enemies)
                    {
                        bool appliedToEnemy = false;
                        yield return ApplyEffectToEnemy(
                            effect,
                            activeEnemy,
                            horizontalDirection,
                            result => appliedToEnemy = result);
                        applied |= appliedToEnemy;
                    }
                }
                break;
        }

        onCompleted?.Invoke(applied);
    }

    private bool ApplyEffectToPlayer(BulletEffectData effect)
    {
        switch (effect.EffectType)
        {
            case BulletEffectType.Poison:
                return playerHealth.AddStatusEffect(
                    StatusEffectType.Poison,
                    effect.StackCount);
            case BulletEffectType.Stun:
                return playerHealth.AddStatusEffect(
                    StatusEffectType.Stun,
                    effect.StackCount);
            case BulletEffectType.Mark:
                return playerHealth.AddStatusEffect(
                    StatusEffectType.Mark,
                    effect.StackCount);
            case BulletEffectType.Weakness:
                return playerHealth.AddStatusEffect(
                    StatusEffectType.Weakness,
                    effect.StackCount);
            default:
                return false;
        }
    }

    private IEnumerator ApplyEffectToEnemy(
        BulletEffectData effect,
        EnemyController enemy,
        int horizontalDirection,
        Action<bool> onCompleted)
    {
        if (effect == null || enemy == null || enemy.CurrentHealth <= 0)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        bool applied = false;

        switch (effect.EffectType)
        {
            case BulletEffectType.Poison:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Poison,
                    effect.StackCount,
                    true);
                break;
            case BulletEffectType.Stun:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Stun,
                    effect.StackCount,
                    true);
                break;
            case BulletEffectType.Mark:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Mark,
                    effect.StackCount,
                    true);
                break;
            case BulletEffectType.Knockback:
                applied = true;
                yield return playerMove.PushEnemyFromBullet(
                    enemy,
                    horizontalDirection,
                    effect.KnockbackDistance);
                break;
            case BulletEffectType.PositionSwap:
                applied = true;
                yield return playerMove.SwapPositionWithEnemy(enemy);
                break;
            case BulletEffectType.Weakness:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Weakness,
                    effect.StackCount,
                    true);
                break;
        }

        onCompleted?.Invoke(applied);
    }

    private IEnumerator ApplyConditionalEvents(
        BulletInstance sourceBullet,
        BulletConditionalTrigger trigger,
        EnemyController enemy,
        int horizontalDirection,
        int appliedDamage,
        Vector3? sourceWorldPosition = null)
    {
        if (sourceBullet == null)
        {
            yield break;
        }

        IReadOnlyList<BulletConditionalEventData> conditionalEvents =
            sourceBullet.ConditionalEvents;

        foreach (BulletConditionalEventData conditionalEvent in conditionalEvents)
        {
            if (conditionalEvent == null || conditionalEvent.Trigger != trigger)
            {
                continue;
            }

            IReadOnlyList<BulletEffectData> events = conditionalEvent.Events;

            foreach (BulletEffectData eventEffect in events)
            {
                if (eventEffect == null || !eventEffect.RollActivation())
                {
                    continue;
                }

                yield return ApplyBulletEffect(
                    eventEffect,
                    sourceBullet,
                    enemy,
                    horizontalDirection,
                    appliedDamage,
                    null,
                    sourceWorldPosition);
            }
        }
    }

    private static bool IsPlayerOnlyEffect(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.LifeSteal
            || effectType == BulletEffectType.IncreaseMaxHealth
            || effectType == BulletEffectType.DestroyBullet
            || effectType == BulletEffectType.GainGold;
    }

    private static bool IsShotScopedEffect(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.DestroyBullet;
    }

    private static bool IsManagedSpecialEffect(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.Jackpot
            || effectType == BulletEffectType.PowderPouch
            || effectType == BulletEffectType.StackNextShot
            || effectType == BulletEffectType.ClonePreviousShot
            || effectType == BulletEffectType.ChainFire
            || effectType == BulletEffectType.Resonance
            || effectType == BulletEffectType.Gilded
            || effectType == BulletEffectType.Coagulation
            || effectType == BulletEffectType.Heart
            || effectType == BulletEffectType.Saver
            || effectType == BulletEffectType.QuickDraw
            || effectType == BulletEffectType.Loader
            || effectType == BulletEffectType.Rangefinder
            || effectType == BulletEffectType.WallImpact
            || effectType == BulletEffectType.Judgment
            || effectType == BulletEffectType.StatusAmplifier
            || effectType == BulletEffectType.VenomBurst
            || effectType == BulletEffectType.Crescendo
            || effectType == BulletEffectType.Rebate
            || effectType == BulletEffectType.Distributor
            || effectType == BulletEffectType.Focus
            || effectType == BulletEffectType.Charge
            || effectType == BulletEffectType.Accumulator
            || effectType == BulletEffectType.ShellCollector
            || effectType == BulletEffectType.Devourer
            || effectType == BulletEffectType.Legacy
            || effectType == BulletEffectType.Collection
            || effectType == BulletEffectType.MixedGrade
            || effectType == BulletEffectType.Masterpiece
            || effectType == BulletEffectType.MassProduced
            || effectType == BulletEffectType.Monopoly;
    }

    private void HandleBulletDestroyed(BulletInstance destroyedBullet)
    {
        bulletDestroyedThisCylinder = true;
        SoundManager.PlaySfx("SFX_Bullet_Destroy");
        combatFeedback?.RecordBulletDestroyed(transform.position);
        relicManager?.NotifyBulletDestroyed(destroyedBullet);

        if (deckManager == null)
        {
            return;
        }

        deckManager.GetOwnedBullets(ownedBulletBuffer);

        foreach (BulletInstance ownedBullet in ownedBulletBuffer)
        {
            if (ownedBullet == null || ownedBullet == destroyedBullet)
            {
                continue;
            }

            BulletEffectData legacyEffect = FindSpecialEffect(
                ownedBullet,
                BulletEffectType.Legacy);

            if (legacyEffect != null)
            {
                ownedBullet.AddPermanentStacks(
                    Mathf.Max(1, legacyEffect.StackCount));
            }
        }
    }

    private void InitializeDamagePreviewState()
    {
        damagePreviewStates.Clear();
        previewDamageBonuses.Clear();
        previewCriticalBonuses.Clear();
        previewStoredBonuses.Clear();
        previewAbilityStacks.Clear();
        previewPermanentStacks.Clear();
        previewShotsObserved.Clear();
        previewPlayerTileIndex = boardManager.TryGetTileIndex(
            transform.position,
            out int playerTileIndex)
                ? playerTileIndex
                : -1;

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                DamagePreviewEnemyState state =
                    new DamagePreviewEnemyState(enemy);

                if (boardManager.TryGetTileIndex(
                        enemy.transform.position,
                        out int enemyTileIndex))
                {
                    state.TileIndex = enemyTileIndex;
                }

                damagePreviewStates[enemy] = state;
            }
        }

        foreach (BulletInstance bullet in deckManager.LoadedBullets)
        {
            if (bullet == null)
            {
                continue;
            }

            previewDamageBonuses[bullet] = bullet.TemporaryDamageBonus;
            previewCriticalBonuses[bullet] =
                bullet.TemporaryCriticalChanceBonus;
            previewStoredBonuses[bullet] = bullet.StoredDamageBonus;
            previewAbilityStacks[bullet] = bullet.AbilityStacks;
            previewPermanentStacks[bullet] = bullet.PermanentStacks;
            previewShotsObserved[bullet] = bullet.ShotsObservedWhileLoaded;
        }
    }

    private void SimulateLoadedBulletDamage(int hoveredBulletIndex)
    {
        IReadOnlyList<BulletInstance> loadedBullets =
            deckManager.LoadedBullets;
        int horizontalDirection = transform.localScale.x >= 0f ? 1 : -1;
        int initialLoadedCount = loadedBullets.Count;
        int previewBulletsFired = 0;
        int previewCriticalShots = 0;
        float stackedDamageBonus = 0f;
        BulletInstance previousResolvedBullet = null;
        BulletRuntimeStateSnapshot previousPreFireState = default;
        bool hasPreviousPreFireState = false;
        int initialIndex = loadedBullets.Count - 1;
        BulletInstance initialResolvedBullet = initialIndex < 0
            ? null
            : ResolveShotBullet(loadedBullets[initialIndex], null);
        bool initialIsPowder = FindSpecialEffect(
            initialResolvedBullet,
            BulletEffectType.PowderPouch) != null;
        bool fireIntoAir = initialIsPowder
            ? !HasPreviewViableFutureShot(
                initialIndex - 1,
                initialResolvedBullet,
                horizontalDirection)
            : !HasPreviewTargets(initialResolvedBullet, horizontalDirection);

        for (int bulletIndex = loadedBullets.Count - 1;
             bulletIndex >= hoveredBulletIndex;
             bulletIndex--)
        {
            BulletInstance firedBullet = loadedBullets[bulletIndex];

            if (firedBullet == null)
            {
                break;
            }

            BulletInstance resolvedBullet = ResolveShotBullet(
                firedBullet,
                previousResolvedBullet);

            if (resolvedBullet != firedBullet && hasPreviousPreFireState)
            {
                ApplyPreviewRuntimeState(
                    firedBullet,
                    previousPreFireState);
            }

            BulletRuntimeStateSnapshot currentPreFireState =
                CapturePreviewRuntimeState(firedBullet);
            BulletEffectData powderEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.PowderPouch);

            if (powderEffect != null)
            {
                if (!fireIntoAir && !HasPreviewViableFutureShot(
                        bulletIndex - 1,
                        resolvedBullet,
                        horizontalDirection))
                {
                    break;
                }

                for (int remainingIndex = 0;
                     remainingIndex < bulletIndex;
                     remainingIndex++)
                {
                    BulletInstance remainingBullet =
                        loadedBullets[remainingIndex];

                    if (remainingBullet != null)
                    {
                        previewCriticalBonuses[remainingBullet] =
                            GetPreviewCriticalBonus(remainingBullet)
                            + powderEffect.Amount;
                    }
                }

                GrantPreviewLegacyStacks(firedBullet);
                previousResolvedBullet = resolvedBullet;
                previousPreFireState = currentPreFireState;
                hasPreviousPreFireState = true;
                continue;
            }

            if (!fireIntoAir
                && !HasPreviewTargets(resolvedBullet, horizontalDirection))
            {
                break;
            }

            float damageMultiplier = GetPreviewSpecialDamageMultiplier(
                firedBullet,
                resolvedBullet,
                bulletIndex,
                initialLoadedCount,
                previewBulletsFired);
            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            bool relicForcesCritical = false;

            if (relicManager != null
                && relicManager.TryGetLoadedBulletRelicModifiers(
                    bulletIndex,
                    loadedBullets.Count,
                    initialLoadedCount,
                    out double relicDamageMultiplier,
                    out relicForcesCritical))
            {
                damageMultiplier = (float)Math.Min(
                    float.MaxValue,
                    Math.Max(0d, damageMultiplier)
                        * Math.Max(0d, relicDamageMultiplier));
            }
            bool isStackingShot = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.StackNextShot) != null;
            BulletEffectData distributorEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Distributor);

            if (distributorEffect != null)
            {
                float storedBonus = GetPreviewStoredBonus(firedBullet)
                    + stackedDamageBonus
                    * Mathf.Max(0f, distributorEffect.Amount / 100f);
                previewStoredBonuses[firedBullet] = storedBonus;
                stackedDamageBonus = 0f;

                for (int remainingIndex = 0;
                     remainingIndex < bulletIndex;
                     remainingIndex++)
                {
                    BulletInstance remainingBullet =
                        loadedBullets[remainingIndex];

                    if (remainingBullet != null)
                    {
                        previewDamageBonuses[remainingBullet] =
                            GetPreviewDamageBonus(remainingBullet)
                            + storedBonus;
                    }
                }
            }

            if (!isStackingShot && distributorEffect == null
                && stackedDamageBonus > 0f)
            {
                damageMultiplier *= 1f + stackedDamageBonus;
                stackedDamageBonus = 0f;
            }

            float criticalChance = resolvedBullet.CriticalChance
                + GetPreviewCriticalBonus(firedBullet)
                + GetPreviewSpecialCriticalChanceBonus(
                    firedBullet,
                    resolvedBullet);
            previewCriticalBonuses[firedBullet] = 0f;
            bool guaranteedCritical = relicForcesCritical
                || criticalChance >= 100f;
            BulletEffectData shellEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.ShellCollector);
            int shellExtraShots = GetPreviewShellExtraShots(
                firedBullet,
                shellEffect);
            int shellCost = shellEffect == null
                ? 0
                : Mathf.Max(1, shellEffect.StackCount);
            bool emphasized = bulletIndex == hoveredBulletIndex;

            SimulatePreviewShot(
                resolvedBullet,
                firedBullet,
                horizontalDirection,
                damageMultiplier,
                guaranteedCritical,
                true,
                emphasized,
                bulletIndex,
                ref previewBulletsFired,
                ref previewCriticalShots);

            BulletEffectData chainEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.ChainFire);
            int additionalShotCount = 0;

            while (IsGuaranteedChainShot(
                chainEffect,
                additionalShotCount))
            {
                if (!SimulatePreviewShot(
                        resolvedBullet,
                        firedBullet,
                        horizontalDirection,
                        damageMultiplier,
                        guaranteedCritical,
                        true,
                        emphasized,
                        bulletIndex,
                        ref previewBulletsFired,
                        ref previewCriticalShots))
                {
                    break;
                }

                additionalShotCount++;
            }

            for (int shellIndex = 0;
                 shellIndex < shellExtraShots;
                 shellIndex++)
            {
                if (!SimulatePreviewShot(
                        resolvedBullet,
                        firedBullet,
                        horizontalDirection,
                        damageMultiplier * shellEffect.Amount / 100f,
                        guaranteedCritical,
                        false,
                        emphasized,
                        bulletIndex,
                        ref previewBulletsFired,
                        ref previewCriticalShots))
                {
                    break;
                }
                previewAbilityStacks[firedBullet] =
                    GetPreviewAbilityStacks(firedBullet) - shellCost;
            }

            BulletEffectData stackEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.StackNextShot);

            if (stackEffect != null)
            {
                stackedDamageBonus += stackEffect.Amount / 100f;
            }

            if (HasGuaranteedEffect(
                    resolvedBullet,
                    BulletEffectType.DestroyBullet))
            {
                GrantPreviewLegacyStacks(firedBullet);
            }

            previousResolvedBullet = resolvedBullet;
            previousPreFireState = currentPreFireState;
            hasPreviousPreFireState = true;
        }
    }

    private bool SimulatePreviewShot(
        BulletInstance resolvedBullet,
        BulletInstance firedBullet,
        int horizontalDirection,
        float damageMultiplier,
        bool guaranteedCritical,
        bool generatesShells,
        bool emphasized,
        int firedBulletIndex,
        ref int previewBulletsFired,
        ref int previewCriticalShots)
    {
        if (!BuildGuaranteedPreviewHitTargets(
                resolvedBullet,
                horizontalDirection))
        {
            return false;
        }

        // Clone and other resolver effects borrow combat behavior, but the
        // preview segment belongs to the physical cylinder bullet that will
        // be consumed. Its own upgraded Secondary Line Color is authoritative.
        Color previewColor = firedBullet.SecondaryLineColor;

        for (int hitIndex = 0; hitIndex < hitBuffer.Count; hitIndex++)
        {
            EnemyController enemy = hitBuffer[hitIndex];

            if (enemy == null
                || !damagePreviewStates.TryGetValue(
                    enemy,
                    out DamagePreviewEnemyState state)
                || state.RemainingHealth <= 0)
            {
                continue;
            }

            float targetMultiplier = GetPreviewTargetDamageMultiplier(
                resolvedBullet,
                state,
                horizontalDirection);
            int attackDamage = CalculateAttackDamage(
                resolvedBullet,
                guaranteedCritical,
                damageMultiplier * targetMultiplier,
                previewBulletsFired,
                firedBulletIndex <= 0,
                false);
            int transferBaseDamage = attackDamage;

            if (hitIndex > 0 && !IsBoardWideShot(resolvedBullet))
            {
                ApplyGuaranteedPreviewConditionalEffects(
                    resolvedBullet,
                    BulletConditionalTrigger.Penetration,
                    state);
            }

            if (guaranteedCritical)
            {
                ApplyGuaranteedPreviewConditionalEffects(
                    resolvedBullet,
                    BulletConditionalTrigger.CriticalHit,
                    state);
            }

            if (state.StatusStacks[(int)StatusEffectType.Mark] > 0)
            {
                attackDamage = Mathf.CeilToInt(attackDamage * 1.5f);
            }

            ApplyPreviewDamage(
                state,
                attackDamage,
                previewColor,
                emphasized);

            ApplyPreviewWallImpactDamageTransfer(
                resolvedBullet,
                state,
                horizontalDirection,
                transferBaseDamage,
                previewColor,
                emphasized);

            if (state.RemainingHealth <= 0)
            {
                ApplyGuaranteedPreviewConditionalEffects(
                    resolvedBullet,
                    BulletConditionalTrigger.EnemyDefeated,
                    state);
                continue;
            }

            ApplyGuaranteedPreviewEffects(
                resolvedBullet,
                state,
                horizontalDirection,
                previewColor,
                emphasized);

            ApplyGuaranteedManagedPreviewEffects(
                resolvedBullet,
                state,
                previewColor,
                emphasized);

            if (state.RemainingHealth <= 0)
            {
                ApplyGuaranteedPreviewConditionalEffects(
                    resolvedBullet,
                    BulletConditionalTrigger.EnemyDefeated,
                    state);
            }
        }

        UpdatePreviewShotAbilities(
            firedBullet,
            resolvedBullet,
            guaranteedCritical,
            generatesShells,
            firedBulletIndex);

        if (previewBulletsFired < int.MaxValue)
        {
            previewBulletsFired++;
        }

        RecordPreviewShotForRemainingBullets(firedBulletIndex);

        if (guaranteedCritical)
        {
            previewCriticalShots++;
        }

        return true;
    }

    private bool BuildGuaranteedPreviewHitTargets(
        BulletInstance bullet,
        int horizontalDirection)
    {
        hitBuffer.Clear();

        if (!CollectPreviewTargets(bullet, horizontalDirection))
        {
            return false;
        }

        if (IsBoardWideShot(bullet))
        {
            hitBuffer.AddRange(targetBuffer);
            return hitBuffer.Count > 0;
        }

        hitBuffer.Add(targetBuffer[0]);

        for (int targetIndex = 1;
             targetIndex < targetBuffer.Count
             && targetIndex < bullet.MaxHitCount;
             targetIndex++)
        {
            int chanceIndex = targetIndex - 1;

            if (chanceIndex >= bullet.PenetrationChances.Count
                || bullet.PenetrationChances[chanceIndex] == null
                || bullet.PenetrationChances[chanceIndex].Chance < 100f)
            {
                break;
            }

            hitBuffer.Add(targetBuffer[targetIndex]);
        }

        return hitBuffer.Count > 0;
    }

    private bool HasPreviewTargets(
        BulletInstance bullet,
        int horizontalDirection)
    {
        return CollectPreviewTargets(bullet, horizontalDirection);
    }

    private bool CollectPreviewTargets(
        BulletInstance bullet,
        int horizontalDirection)
    {
        targetBuffer.Clear();

        if (bullet == null)
        {
            return false;
        }

        if (IsBoardWideShot(bullet))
        {
            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                if (state.Enemy != null && state.RemainingHealth > 0)
                {
                    targetBuffer.Add(state.Enemy);
                }
            }

            SortTargetsByTileIndex(targetBuffer);
            return targetBuffer.Count > 0;
        }

        if (previewPlayerTileIndex < 0)
        {
            return false;
        }

        int direction = horizontalDirection >= 0 ? 1 : -1;
        int blockerDistance = int.MaxValue;

        if (waveManager != null
            && waveManager.TryGetFirstBulletBlocker(
                transform.position,
                direction,
                bullet.MaxRange,
                out IPlayerBulletBlocker previewBlocker))
        {
            blockerDistance = Mathf.Abs(
                previewBlocker.TileIndex - previewPlayerTileIndex);
        }

        foreach (DamagePreviewEnemyState state
                 in damagePreviewStates.Values)
        {
            if (state.Enemy == null || state.RemainingHealth <= 0
                || state.TileIndex < 0)
            {
                continue;
            }

            int offset = state.TileIndex - previewPlayerTileIndex;

            if (offset * direction > 0
                && Mathf.Abs(offset) <= bullet.MaxRange
                && Mathf.Abs(offset) < blockerDistance)
            {
                targetBuffer.Add(state.Enemy);
            }
        }

        targetBuffer.Sort((first, second) =>
        {
            DamagePreviewEnemyState firstState =
                damagePreviewStates[first];
            DamagePreviewEnemyState secondState =
                damagePreviewStates[second];
            int distanceComparison = Mathf.Abs(
                    firstState.TileIndex - previewPlayerTileIndex)
                .CompareTo(Mathf.Abs(
                    secondState.TileIndex - previewPlayerTileIndex));
            return distanceComparison != 0
                ? distanceComparison
                : firstState.TileIndex.CompareTo(secondState.TileIndex);
        });

        return targetBuffer.Count > 0;
    }

    private bool HasPreviewViableFutureShot(
        int loadedBulletIndex,
        BulletInstance previousResolvedBullet,
        int horizontalDirection)
    {
        for (int bulletIndex = loadedBulletIndex;
             bulletIndex >= 0;
             bulletIndex--)
        {
            BulletInstance loadedBullet =
                deckManager.LoadedBullets[bulletIndex];
            BulletInstance resolvedBullet = ResolveShotBullet(
                loadedBullet,
                previousResolvedBullet);

            if (resolvedBullet == null)
            {
                continue;
            }

            if (FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.PowderPouch) == null
                && HasPreviewTargets(resolvedBullet, horizontalDirection))
            {
                return true;
            }

            previousResolvedBullet = resolvedBullet;
        }

        return false;
    }

    private float GetPreviewSpecialDamageMultiplier(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet,
        int firedBulletIndex,
        int initialLoadedCount,
        int previewBulletsFired)
    {
        float multiplier = 1f;
        BulletEffectData effect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Jackpot);

        if (effect != null && firedBulletIndex == 0)
        {
            multiplier *= Mathf.Max(1f, effect.Amount / 100f);
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Resonance);

        if (effect != null)
        {
            int count = 0;

            for (int index = 0; index < firedBulletIndex; index++)
            {
                if (FindSpecialEffect(
                        deckManager.LoadedBullets[index],
                        BulletEffectType.Resonance) != null)
                {
                    count++;
                }
            }

            multiplier *= 1f + count * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(
            firedBullet,
            BulletEffectType.ClonePreviousShot);

        if (effect != null && resolvedBullet != firedBullet)
        {
            multiplier *= Mathf.Max(1f, effect.Amount / 100f);
        }

        multiplier *= 1f + GetPreviewDamageBonus(firedBullet);
        previewDamageBonuses[firedBullet] = 0f;

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Gilded);

        if (effect != null && currencyManager != null)
        {
            multiplier *= 1f + currencyManager.CurrentMoney
                / Mathf.Max(1, effect.StackCount)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Heart);

        if (effect != null && playerHealth != null)
        {
            multiplier *= 1f + playerHealth.MaxHealth
                / Mathf.Max(1, effect.StackCount)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Loader);

        if (effect != null)
        {
            int emptyChambers = Mathf.Max(
                0,
                deckManager.MaxReloadAmount - initialLoadedCount);
            multiplier *= 1f
                + emptyChambers * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Charge);

        if (effect != null)
        {
            multiplier *= 1f + Mathf.Min(
                    GetPreviewShotsObserved(firedBullet),
                    effect.StackCount)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Accumulator);

        if (effect != null)
        {
            multiplier *= 1f + GetPreviewAbilityStacks(firedBullet)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Devourer);

        if (effect != null)
        {
            multiplier *= 1f + GetPreviewPermanentStacks(firedBullet)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Legacy);

        if (effect != null)
        {
            multiplier *= 1f + GetPreviewPermanentStacks(firedBullet)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Collection);

        if (effect != null)
        {
            multiplier *= 1f + CountDistinctOwnedBulletTypes()
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.MixedGrade);

        if (effect != null)
        {
            int otherGradeCount = 0;

            for (int index = 0; index < firedBulletIndex; index++)
            {
                BulletInstance remainingBullet =
                    deckManager.LoadedBullets[index];

                if (remainingBullet != null
                    && remainingBullet.Grade != firedBullet.Grade)
                {
                    otherGradeCount++;
                }
            }

            multiplier *= 1f
                + otherGradeCount * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Masterpiece);

        if (effect != null)
        {
            multiplier *= 1f + CountOwnedBulletsByGrade(
                    BulletGrade.Ace,
                    BulletGrade.Legendary)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.MassProduced);

        if (effect != null)
        {
            multiplier *= 1f + CountOwnedBulletsByGrade(
                    BulletGrade.Normal,
                    BulletGrade.Rare)
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Monopoly);

        if (effect != null)
        {
            multiplier *= 1f + GetMostCommonOwnedGradeCount()
                * effect.Amount / 100f;
        }

        return multiplier;
    }

    private float GetPreviewTargetDamageMultiplier(
        BulletInstance bullet,
        DamagePreviewEnemyState enemyState,
        int horizontalDirection)
    {
        float multiplier = 1f;
        BulletEffectData effect = FindSpecialEffect(
            bullet,
            BulletEffectType.Rangefinder);

        if (effect != null && enemyState.TileIndex >= 0
            && previewPlayerTileIndex >= 0)
        {
            int tileDistance = Mathf.Abs(
                enemyState.TileIndex - previewPlayerTileIndex);
            multiplier *= 1f
                + tileDistance * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(bullet, BulletEffectType.Judgment);

        if (effect != null)
        {
            multiplier *= 1f + enemyState.TotalStatusStackCount
                * effect.Amount / 100f;
        }

        return multiplier;
    }

    private void ApplyPreviewWallImpactDamageTransfer(
        BulletInstance bullet,
        DamagePreviewEnemyState sourceState,
        int horizontalDirection,
        int sourceAttackDamage,
        Color color,
        bool emphasized)
    {
        BulletEffectData effect = FindSpecialEffect(
            bullet,
            BulletEffectType.WallImpact);

        if (effect == null || sourceState == null
            || sourceState.TileIndex < 0 || sourceAttackDamage <= 0)
        {
            return;
        }

        int direction = horizontalDirection >= 0 ? 1 : -1;

        int maxTransferDistance = Mathf.Clamp(
            effect.KnockbackDistance,
            1,
            3);

        for (int distance = 1;
             distance <= maxTransferDistance;
             distance++)
        {
            float transferPercent = GetWallImpactTransferPercent(
                effect,
                distance);

            if (transferPercent <= 0f)
            {
                continue;
            }

            int targetTileIndex = sourceState.TileIndex
                + direction * distance;

            foreach (DamagePreviewEnemyState targetState
                     in damagePreviewStates.Values)
            {
                if (targetState == sourceState
                    || targetState.RemainingHealth <= 0
                    || targetState.TileIndex != targetTileIndex)
                {
                    continue;
                }

                int transferDamage = Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        sourceAttackDamage * transferPercent / 100f));

                if (targetState.StatusStacks[
                        (int)StatusEffectType.Mark] > 0)
                {
                    transferDamage = Mathf.CeilToInt(
                        transferDamage * 1.5f);
                }

                ApplyPreviewDamage(
                    targetState,
                    transferDamage,
                    color,
                    emphasized);
                break;
            }
        }
    }

    private void ApplyPreviewDamage(
        DamagePreviewEnemyState state,
        int damage,
        Color color,
        bool emphasized)
    {
        int appliedDamage = Mathf.Min(
            state.RemainingHealth,
            Mathf.Max(0, damage));

        if (appliedDamage <= 0)
        {
            return;
        }

        state.RemainingHealth -= appliedDamage;

        if (state.Segments.Count > 0)
        {
            int lastIndex = state.Segments.Count - 1;
            EnemyHealthBarFeedback.DamagePreviewSegment lastSegment =
                state.Segments[lastIndex];

            if (lastSegment.Emphasized == emphasized
                && Approximately(lastSegment.Color, color))
            {
                long combinedDamage =
                    (long)lastSegment.Damage + appliedDamage;
                state.Segments[lastIndex] =
                    new EnemyHealthBarFeedback.DamagePreviewSegment(
                        combinedDamage >= int.MaxValue
                            ? int.MaxValue
                            : (int)combinedDamage,
                        color,
                        emphasized);
                return;
            }
        }

        state.Segments.Add(
            new EnemyHealthBarFeedback.DamagePreviewSegment(
                appliedDamage,
                color,
                emphasized));
    }

    private static bool Approximately(Color first, Color second)
    {
        return Mathf.Approximately(first.r, second.r)
            && Mathf.Approximately(first.g, second.g)
            && Mathf.Approximately(first.b, second.b)
            && Mathf.Approximately(first.a, second.a);
    }

    private void ApplyGuaranteedPreviewEffects(
        BulletInstance bullet,
        DamagePreviewEnemyState hitState,
        int horizontalDirection,
        Color color,
        bool emphasized)
    {
        foreach (BulletEffectData effect in bullet.Effects)
        {
            if (effect == null || effect.ActivationChance < 100f
                || IsShotScopedEffect(effect.EffectType)
                || IsManagedSpecialEffect(effect.EffectType))
            {
                continue;
            }

            bool applied;

            if (effect.EffectType == BulletEffectType.Knockback)
            {
                applied = ApplyGuaranteedPreviewMovementEffect(
                    effect,
                    hitState,
                    horizontalDirection,
                    color,
                    emphasized,
                    false);
            }
            else if (effect.EffectType == BulletEffectType.PositionSwap)
            {
                applied = ApplyGuaranteedPreviewMovementEffect(
                    effect,
                    hitState,
                    horizontalDirection,
                    color,
                    emphasized,
                    true);
            }
            else
            {
                applied = ApplyGuaranteedPreviewEffect(effect, hitState);
            }

            if (applied)
            {
                ApplyGuaranteedPreviewConditionalEffects(
                    bullet,
                    BulletConditionalTrigger.EffectApplied,
                    hitState);
            }
        }
    }

    private bool ApplyGuaranteedPreviewEffect(
        BulletEffectData effect,
        DamagePreviewEnemyState hitState)
    {
        if (effect.Target == BulletEffectTarget.FiringPlayer)
        {
            return false;
        }

        bool applied = false;

        if (effect.Target == BulletEffectTarget.AllEnemies)
        {
            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                applied |= AddPreviewStatusEffect(state, effect);
            }

            return applied;
        }

        return AddPreviewStatusEffect(hitState, effect);
    }

    private bool ApplyGuaranteedPreviewMovementEffect(
        BulletEffectData effect,
        DamagePreviewEnemyState hitState,
        int horizontalDirection,
        Color color,
        bool emphasized,
        bool swapsPosition)
    {
        if (effect.Target == BulletEffectTarget.FiringPlayer)
        {
            return false;
        }

        if (effect.Target == BulletEffectTarget.AllEnemies)
        {
            bool applied = false;
            List<DamagePreviewEnemyState> states =
                new List<DamagePreviewEnemyState>(
                    damagePreviewStates.Values);

            foreach (DamagePreviewEnemyState state in states)
            {
                applied |= swapsPosition
                    ? ApplyPreviewPositionSwap(state)
                    : ApplyPreviewKnockback(
                        state,
                        horizontalDirection,
                        effect.KnockbackDistance,
                        color,
                        emphasized);
            }

            return applied;
        }

        return swapsPosition
            ? ApplyPreviewPositionSwap(hitState)
            : ApplyPreviewKnockback(
                hitState,
                horizontalDirection,
                effect.KnockbackDistance,
                color,
                emphasized);
    }

    private bool ApplyPreviewPositionSwap(
        DamagePreviewEnemyState enemyState)
    {
        if (enemyState == null || enemyState.RemainingHealth <= 0
            || enemyState.TileIndex < 0 || previewPlayerTileIndex < 0
            || enemyState.TileIndex == previewPlayerTileIndex)
        {
            return false;
        }

        int enemyTileIndex = enemyState.TileIndex;
        enemyState.TileIndex = previewPlayerTileIndex;
        previewPlayerTileIndex = enemyTileIndex;
        return true;
    }

    private bool ApplyPreviewKnockback(
        DamagePreviewEnemyState pushedState,
        int horizontalDirection,
        int maxTravelDistance,
        Color color,
        bool emphasized)
    {
        if (pushedState == null || pushedState.RemainingHealth <= 0
            || pushedState.TileIndex < 0 || maxTravelDistance <= 0)
        {
            return false;
        }

        int direction = horizontalDirection >= 0 ? 1 : -1;
        int restingTileIndex = pushedState.TileIndex;
        DamagePreviewEnemyState collidedState = null;

        for (int distance = 0; distance < maxTravelDistance; distance++)
        {
            int nextTileIndex = restingTileIndex + direction;

            if (nextTileIndex < 0
                || nextTileIndex >= boardManager.BoardCount)
            {
                break;
            }

            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                if (state != pushedState && state.RemainingHealth > 0
                    && state.TileIndex == nextTileIndex)
                {
                    collidedState = state;
                    break;
                }
            }

            if (collidedState != null)
            {
                break;
            }

            restingTileIndex = nextTileIndex;
        }

        pushedState.TileIndex = restingTileIndex;

        if (collidedState != null && playerMove != null)
        {
            float damageRatio = playerMove.PushCollisionDamageRatio;

            if (damageRatio <= 0f)
            {
                return true;
            }

            int pushedDamage = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    pushedState.Enemy.MaxHealth * damageRatio));
            int collidedDamage = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    collidedState.Enemy.MaxHealth * damageRatio));
            ApplyPreviewDamage(
                pushedState,
                pushedDamage,
                color,
                emphasized);
            ApplyPreviewDamage(
                collidedState,
                collidedDamage,
                color,
                emphasized);
        }

        return true;
    }

    private static bool AddPreviewStatusEffect(
        DamagePreviewEnemyState state,
        BulletEffectData effect)
    {
        if (state == null || state.RemainingHealth <= 0)
        {
            return false;
        }

        StatusEffectType statusType;

        switch (effect.EffectType)
        {
            case BulletEffectType.Poison:
                statusType = StatusEffectType.Poison;
                break;
            case BulletEffectType.Stun:
                statusType = StatusEffectType.Stun;
                break;
            case BulletEffectType.Mark:
                statusType = StatusEffectType.Mark;
                break;
            case BulletEffectType.Weakness:
                statusType = StatusEffectType.Weakness;
                break;
            default:
                return false;
        }

        int index = (int)statusType;
        long combined = (long)state.StatusStacks[index]
            + effect.StackCount;
        state.StatusStacks[index] = combined >= int.MaxValue
            ? int.MaxValue
            : (int)combined;
        return true;
    }

    private void ApplyGuaranteedPreviewConditionalEffects(
        BulletInstance bullet,
        BulletConditionalTrigger trigger,
        DamagePreviewEnemyState hitState)
    {
        foreach (BulletConditionalEventData conditionalEvent
                 in bullet.ConditionalEvents)
        {
            if (conditionalEvent == null
                || conditionalEvent.Trigger != trigger)
            {
                continue;
            }

            foreach (BulletEffectData effect in conditionalEvent.Events)
            {
                if (effect != null && effect.ActivationChance >= 100f)
                {
                    ApplyGuaranteedPreviewEffect(effect, hitState);
                }
            }
        }
    }

    private void ApplyGuaranteedManagedPreviewEffects(
        BulletInstance bullet,
        DamagePreviewEnemyState state,
        Color color,
        bool emphasized)
    {
        BulletEffectData amplifierEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.StatusAmplifier);

        if (amplifierEffect != null
            && amplifierEffect.ActivationChance >= 100f)
        {
            int multiplier = Mathf.Max(
                2,
                Mathf.RoundToInt(amplifierEffect.Amount));

            for (int index = 0; index < state.StatusStacks.Length; index++)
            {
                long multiplied =
                    (long)state.StatusStacks[index] * multiplier;
                state.StatusStacks[index] = multiplied >= int.MaxValue
                    ? int.MaxValue
                    : (int)multiplied;
            }
        }

        BulletEffectData venomEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.VenomBurst);

        if (venomEffect == null || venomEffect.ActivationChance < 100f)
        {
            return;
        }

        int poisonIndex = (int)StatusEffectType.Poison;
        int poisonStacks = state.StatusStacks[poisonIndex];
        state.StatusStacks[poisonIndex] = 0;

        if (poisonStacks > 0)
        {
            long remainingPoisonDamage =
                (long)poisonStacks * (poisonStacks + 1L) / 2L;
            double scaledDamage = Math.Min(
                int.MaxValue,
                Math.Ceiling(
                    remainingPoisonDamage * venomEffect.Amount / 100d));
            ApplyPreviewDamage(
                state,
                (int)scaledDamage,
                color,
                emphasized);
        }

        if (state.RemainingHealth > 0)
        {
            state.StatusStacks[poisonIndex] =
                venomEffect.KnockbackDistance;
        }
    }

    private void UpdatePreviewShotAbilities(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet,
        bool guaranteedCritical,
        bool generatesShells,
        int firedBulletIndex)
    {
        BulletEffectData focusEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Focus);

        if (focusEffect != null)
        {
            if (guaranteedCritical)
            {
                previewAbilityStacks[firedBullet] = 0;
            }
        }

        if (!guaranteedCritical)
        {
            GrantPreviewFocusStacksToRemainingBullets(firedBulletIndex);
        }

        if (guaranteedCritical)
        {
            GrantPreviewAbilityStacks(
                BulletEffectType.Accumulator,
                firedBullet);
        }

        if (generatesShells)
        {
            GrantPreviewAbilityStacks(
                BulletEffectType.ShellCollector,
                firedBullet);
        }
    }

    private void GrantPreviewFocusStacksToRemainingBullets(
        int firedBulletIndex)
    {
        IReadOnlyList<BulletInstance> loadedBullets =
            deckManager.LoadedBullets;
        int remainingCount = Mathf.Min(
            firedBulletIndex,
            loadedBullets.Count);

        for (int bulletIndex = 0;
             bulletIndex < remainingCount;
             bulletIndex++)
        {
            BulletInstance bullet = loadedBullets[bulletIndex];
            BulletEffectData focusEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Focus);

            if (bullet != null && focusEffect != null)
            {
                previewAbilityStacks[bullet] =
                    GetPreviewAbilityStacks(bullet)
                    + Mathf.Max(1, focusEffect.StackCount);
            }
        }
    }

    private void GrantPreviewAbilityStacks(
        BulletEffectType effectType,
        BulletInstance excludedBullet)
    {
        foreach (BulletInstance bullet in deckManager.LoadedBullets)
        {
            if (bullet != null && bullet != excludedBullet
                && FindSpecialEffect(bullet, effectType) != null)
            {
                previewAbilityStacks[bullet] =
                    GetPreviewAbilityStacks(bullet) + 1;
            }
        }
    }

    private int GetPreviewShellExtraShots(
        BulletInstance firedBullet,
        BulletEffectData shellEffect)
    {
        if (shellEffect == null)
        {
            return 0;
        }

        int shellCost = Mathf.Max(1, shellEffect.StackCount);
        int extraShots = Mathf.Min(
            GetPreviewAbilityStacks(firedBullet) / shellCost,
            Mathf.Max(1, shellEffect.KnockbackDistance));
        return extraShots;
    }

    private float GetPreviewSpecialCriticalChanceBonus(
        BulletInstance firedBullet,
        BulletInstance resolvedBullet)
    {
        float bonus = 0f;
        BulletEffectData effect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Coagulation);

        if (effect != null && playerHealth != null
            && playerHealth.MaxHealth > 0)
        {
            float missingPercent = 100f
                * (playerHealth.MaxHealth - playerHealth.CurrentHealth)
                / playerHealth.MaxHealth;
            bonus += Mathf.Floor(
                    missingPercent / Mathf.Max(1, effect.StackCount))
                * effect.Amount;
        }

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Focus);

        if (effect != null)
        {
            bonus += GetPreviewAbilityStacks(firedBullet) * effect.Amount;
        }

        return bonus;
    }

    private void GrantPreviewLegacyStacks(BulletInstance destroyedBullet)
    {
        foreach (BulletInstance bullet in deckManager.LoadedBullets)
        {
            if (bullet == null || bullet == destroyedBullet)
            {
                continue;
            }

            BulletEffectData legacyEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Legacy);

            if (legacyEffect != null)
            {
                previewPermanentStacks[bullet] =
                    GetPreviewPermanentStacks(bullet)
                    + Mathf.Max(1, legacyEffect.StackCount);
            }
        }
    }

    private static bool HasGuaranteedEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        BulletEffectData effect = FindSpecialEffect(bullet, effectType);
        return effect != null && effect.ActivationChance >= 100f;
    }

    private static bool IsGuaranteedChainShot(
        BulletEffectData chainEffect,
        int additionalShotCount)
    {
        return chainEffect != null
            && additionalShotCount < chainEffect.StackCount
            && chainEffect.ActivationChance
                - chainEffect.Amount * additionalShotCount >= 100f;
    }

    private float GetPreviewDamageBonus(BulletInstance bullet)
    {
        return bullet != null
            && previewDamageBonuses.TryGetValue(bullet, out float value)
                ? Mathf.Max(0f, value)
                : 0f;
    }

    private float GetPreviewCriticalBonus(BulletInstance bullet)
    {
        return bullet != null
            && previewCriticalBonuses.TryGetValue(bullet, out float value)
                ? Mathf.Max(0f, value)
                : 0f;
    }

    private float GetPreviewStoredBonus(BulletInstance bullet)
    {
        return bullet != null
            && previewStoredBonuses.TryGetValue(bullet, out float value)
                ? Mathf.Max(0f, value)
                : 0f;
    }

    private int GetPreviewAbilityStacks(BulletInstance bullet)
    {
        return bullet != null
            && previewAbilityStacks.TryGetValue(bullet, out int value)
                ? Mathf.Max(0, value)
                : 0;
    }

    private int GetPreviewPermanentStacks(BulletInstance bullet)
    {
        return bullet != null
            && previewPermanentStacks.TryGetValue(bullet, out int value)
                ? Mathf.Max(0, value)
                : 0;
    }

    private int GetPreviewShotsObserved(BulletInstance bullet)
    {
        return bullet != null
            && previewShotsObserved.TryGetValue(bullet, out int value)
                ? Mathf.Max(0, value)
                : 0;
    }

    private BulletRuntimeStateSnapshot CapturePreviewRuntimeState(
        BulletInstance bullet)
    {
        return new BulletRuntimeStateSnapshot(
            GetPreviewAbilityStacks(bullet),
            GetPreviewPermanentStacks(bullet),
            GetPreviewStoredBonus(bullet),
            GetPreviewCriticalBonus(bullet),
            GetPreviewDamageBonus(bullet),
            GetPreviewShotsObserved(bullet));
    }

    private void ApplyPreviewRuntimeState(
        BulletInstance bullet,
        BulletRuntimeStateSnapshot state)
    {
        if (bullet == null)
        {
            return;
        }

        previewAbilityStacks[bullet] = state.AbilityStacks;
        previewPermanentStacks[bullet] = state.PermanentStacks;
        previewStoredBonuses[bullet] = state.StoredDamageBonus;
        previewCriticalBonuses[bullet] =
            state.TemporaryCriticalChanceBonus;
        previewDamageBonuses[bullet] = state.TemporaryDamageBonus;
        previewShotsObserved[bullet] = state.ShotsObservedWhileLoaded;
    }

    private void RecordPreviewShotForRemainingBullets(
        int firedBulletIndex)
    {
        int remainingCount = Mathf.Min(
            firedBulletIndex,
            deckManager.LoadedBullets.Count);

        for (int index = 0; index < remainingCount; index++)
        {
            BulletInstance bullet = deckManager.LoadedBullets[index];

            if (bullet == null)
            {
                continue;
            }

            int currentCount = GetPreviewShotsObserved(bullet);
            previewShotsObserved[bullet] = currentCount == int.MaxValue
                ? int.MaxValue
                : currentCount + 1;
        }
    }

    private int CalculateAttackDamage(
        BulletInstance bulletData,
        bool isCritical,
        float damageMultiplier,
        int shotIndex,
        bool isLastLoadedShot,
        bool applyRuntimeRelicModifiers = true)
    {
        if (bulletData == null || bulletData.Damage <= 0)
        {
            return 0;
        }

        int damage = GetEffectiveBaseDamage(bulletData);

        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);

        if (isCritical)
        {
            damage = MultiplyDamageCeiling(
                damage,
                bulletData.CriticalDamageMultiplier);
        }

        int modifiedDamage = playerHealth.ModifyOutgoingAttackDamage(damage);
        double combinedMultiplier = Math.Max(
            0d,
            (double)damageMultiplier);

        if (applyRuntimeRelicModifiers && relicManager != null)
        {
            combinedMultiplier *=
                relicManager.GetConditionalFinalDamageMultiplier(
                    shotIndex == 0,
                    isLastLoadedShot);
        }

        return MultiplyDamageCeiling(modifiedDamage, combinedMultiplier);
    }

    private static int MultiplyDamageCeiling(int damage, double multiplier)
    {
        if (damage <= 0 || multiplier <= 0d || double.IsNaN(multiplier))
        {
            return 0;
        }

        double result = Math.Ceiling(damage * multiplier);
        return double.IsInfinity(result) || result >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Max(0d, result);
    }

    private int GetEffectiveBaseDamage(BulletInstance bullet)
    {
        BulletEffectData crescendoEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.Crescendo);

        if (crescendoEffect == null || deckManager == null)
        {
            return bullet.Damage;
        }

        int otherOwnedBulletCount = Mathf.Max(
            0,
            deckManager.TotalBulletCount
                - (deckManager.Contains(bullet) ? 1 : 0));

        return Mathf.Max(
            0,
            Mathf.CeilToInt(
                bullet.Damage
                - otherOwnedBulletCount * crescendoEffect.Amount));
    }

    private static bool IsBoardWideShot(BulletInstance bullet)
    {
        return FindSpecialEffect(bullet, BulletEffectType.QuickDraw) != null;
    }

    private void SortTargetsByTileIndex(List<EnemyController> targets)
    {
        if (boardManager == null)
        {
            return;
        }

        targets.Sort((first, second) =>
        {
            int firstIndex = 0;
            int secondIndex = 0;
            bool hasFirst = first != null && boardManager.TryGetTileIndex(
                first.transform.position,
                out firstIndex);
            bool hasSecond = second != null && boardManager.TryGetTileIndex(
                second.transform.position,
                out secondIndex);

            if (!hasFirst || !hasSecond)
            {
                return hasFirst == hasSecond ? 0 : hasFirst ? -1 : 1;
            }

            return firstIndex.CompareTo(secondIndex);
        });
    }

    private Vector3 GetMissEndPoint(int horizontalDirection, int maxRange)
    {
        if (boardManager.TryGetRangedTilePosition(
                transform.position,
                horizontalDirection,
                maxRange,
                out Vector3 rangedTilePosition))
        {
            return rangedTilePosition;
        }

        float fallbackDistance = Mathf.Max(
            boardManager.BoardDistance * Mathf.Max(1, maxRange),
            0.01f);
        return firePoint.position
            + Vector3.right * horizontalDirection * fallbackDistance;
    }

    private Vector3 GetShotLineEndPoint(
        Vector3 startPoint,
        Vector3 targetEndPoint)
    {
        Vector3 horizontalEndPoint = new Vector3(
            targetEndPoint.x,
            startPoint.y,
            startPoint.z);
        Vector3 horizontalShotVector = horizontalEndPoint - startPoint;
        float angleLimit = Mathf.Max(0f, maxRandomShotAngle);
        float randomAngle = UnityEngine.Random.Range(
            -angleLimit,
            angleLimit);
        Vector3 angledShotVector = Quaternion.AngleAxis(
            randomAngle,
            Vector3.forward) * horizontalShotVector;
        return startPoint + angledShotVector;
    }

    private IEnumerator WaitForShotInterval()
    {
        float elapsedTime = 0f;

        while (elapsedTime < shotInterval)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private void ShowBulletFeedback(BulletInstance bulletData)
    {
        if (bulletFeedbackImage == null || bulletData == null)
        {
            return;
        }

        if (bulletFeedbackCoroutine != null)
        {
            StopCoroutine(bulletFeedbackCoroutine);
            bulletFeedbackCoroutine = null;
        }

        Color feedbackColor = bulletData.PrimaryLineColor;
        feedbackColor.a = BulletFeedbackStartAlpha;
        bulletFeedbackImage.raycastTarget = false;
        bulletFeedbackImage.color = feedbackColor;
        bulletFeedbackImage.gameObject.SetActive(true);

        if (shotInterval <= 0f)
        {
            ResetBulletFeedback();
            return;
        }

        bulletFeedbackCoroutine = StartCoroutine(
            FadeBulletFeedback(feedbackColor));
    }

    private IEnumerator FadeBulletFeedback(Color startColor)
    {
        float fadeDuration = shotInterval;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            if (bulletFeedbackImage == null)
            {
                bulletFeedbackCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration);
            startColor.a = Mathf.Lerp(
                BulletFeedbackStartAlpha,
                0f,
                progress);
            bulletFeedbackImage.color = startColor;
        }

        ResetBulletFeedback();
        bulletFeedbackCoroutine = null;
    }

    private void ResetBulletFeedback()
    {
        if (bulletFeedbackImage == null)
        {
            return;
        }

        Color feedbackColor = bulletFeedbackImage.color;
        feedbackColor.a = 0f;
        bulletFeedbackImage.raycastTarget = false;
        bulletFeedbackImage.color = feedbackColor;
        bulletFeedbackImage.gameObject.SetActive(false);
    }

    private bool TryBeginAction()
    {
        if (lastActionFrame == Time.frameCount)
        {
            return false;
        }

        lastActionFrame = Time.frameCount;
        return true;
    }
}
