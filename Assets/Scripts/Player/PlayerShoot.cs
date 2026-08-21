using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public partial class PlayerShoot : MonoBehaviour
{
    public event Action<BulletInstance> BulletFired;
    public event Action<int> DamageDealt;
    public event Action<PlayerBehaviourAction> BehaviourActionStarted;
    public event Action LoadedBulletDamagePreviewShown;

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
        public bool WasHitThisTurn { get; set; }
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
    private BulletShotFeedbackView bulletFeedbackView;
    private FiringSequenceController firingSequence;
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
    private DamagePreviewController damagePreview;
    private BulletInstance currentConsumedBullet;
    private int initialLoadedBulletCount;
    private int bulletsFiredThisCylinder;
    private int criticalShotsThisCylinder;
    private int activeShotIndex;
    private bool bulletDestroyedThisCylinder;
    private int pendingSaverGold;
    private PlayerShotRangePreview rangePreview;

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

        bulletFeedbackView = GetComponent<BulletShotFeedbackView>();

        if (bulletFeedbackView == null)
        {
            bulletFeedbackView = gameObject.AddComponent<BulletShotFeedbackView>();
        }

        bulletFeedbackView.Initialize(bulletFeedbackImage);
        rangePreview = new PlayerShotRangePreview(
            transform,
            firePoint,
            boardManager,
            waveManager);
        damagePreview = new DamagePreviewController(this);
        firingSequence = new FiringSequenceController(this);

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        bulletFeedbackView.Hide();
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

        if (playerMove != null)
        {
            playerMove.PlayerMoved += HandlePlayerMoved;
            playerMove.TurnCompleted += HandleTurnCompleted;
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

        if (playerMove != null)
        {
            playerMove.PlayerMoved -= HandlePlayerMoved;
            playerMove.TurnCompleted -= HandleTurnCompleted;
        }
        ClearLoadedBulletDamagePreview();
        isFiring = false;

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        bulletFeedbackView?.Hide();
        reservedDamageByEnemy.Clear();
        pendingEffectDefeats.Clear();
    }

    private void OnDestroy()
    {
        rangePreview?.Dispose();
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
            boardManager,
            deckManager);

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
        firingSequence?.ResetTurnTargetHistory();
    }

    private void HandleTurnCompleted()
    {
        firingSequence?.ResetTurnTargetHistory();
    }

    private void HandlePlayerMoved(PlayerMovementContext context)
    {
        firingSequence?.RecordPlayerMovement(context);
    }

    private void Update()
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || isFiring)
        {
            return;
        }

        switch (PlayerShootInputReader.Read(eventSystem))
        {
            case PlayerShootInputAction.Reload:
                Reload();
                break;
            case PlayerShootInputAction.Shoot:
                Shoot();
                break;
            case PlayerShootInputAction.EjectNextBullet:
                EjectNextLoadedBullet();
                break;
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
                ? loadedBullet == null
                    || !loadedBullet.DoesNotConsumeReloadTurn
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
        StartCoroutine(firingSequence.Execute(horizontalDirection));
    }

    public void EjectNextLoadedBullet()
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
            Debug.LogError(
                "Deck Manager and Player Move must be assigned in the Inspector.",
                this);
            return;
        }

        if (!playerMove.CanStartAction
            || deckManager.LoadedBullets.Count == 0)
        {
            return;
        }

        ClearLoadedBulletDamagePreview();

        if (!deckManager.TryEjectNextLoadedBullet(out _))
        {
            return;
        }

        BehaviourActionStarted?.Invoke(
            PlayerBehaviourAction.EjectNextBullet);
        playerMove.CompleteTurn();
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
        bool displayedAnyDamage = damagePreview.Show(loadedBulletIndex);

        if (displayedAnyDamage)
        {
            LoadedBulletDamagePreviewShown?.Invoke();
        }

        return displayedAnyDamage;
    }

    public void ClearLoadedBulletDamagePreview()
    {
        HideLoadedBulletRangePreview();
        damagePreview?.Clear();
    }

    public bool ShowLoadedBulletRangePreview(int loadedBulletIndex)
    {
        if (isFiring || deckManager == null
            || loadedBulletIndex < 0
            || loadedBulletIndex >= deckManager.LoadedBullets.Count)
        {
            rangePreview?.Hide();
            return false;
        }

        return rangePreview != null
            && rangePreview.Show(
                deckManager.LoadedBullets,
                loadedBulletIndex);
    }

    private void HideLoadedBulletRangePreview()
    {
        rangePreview?.Hide();
    }
    private float GetCurrentCylinderBuild()
    {
        return firingSequence.GetCurrentCylinderBuild();
    }

    private BulletInstance ResolveShotBullet(
        BulletInstance loadedBullet,
        BulletInstance previousResolvedBullet)
    {
        return BulletEffectUtility.ResolveShot(
            loadedBullet,
            previousResolvedBullet);
    }

    private int CountDistinctOwnedBulletTypes()
    {
        return firingSequence.CountDistinctOwnedBulletTypes();
    }

    private int CountOwnedBulletsByGrade(
        BulletGrade first,
        BulletGrade second)
    {
        return firingSequence.CountOwnedBulletsByGrade(first, second);
    }

    private int GetMostCommonOwnedGradeCount()
    {
        return firingSequence.GetMostCommonOwnedGradeCount();
    }

    private static BulletEffectData FindSpecialEffect(
        BulletInstance bullet,
        BulletEffectType effectType)
    {
        return BulletEffectUtility.Find(bullet, effectType);
    }
    private int CalculateAttackDamage(
        BulletInstance bulletData,
        bool isCritical,
        float damageMultiplier,
        int shotIndex,
        bool isLastLoadedShot,
        bool applyRuntimeRelicModifiers = true,
        float criticalDamageMultiplierBonus = 0f)
    {
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);

        return PlayerAttackDamageCalculator.Calculate(
            bulletData,
            isCritical,
            damageMultiplier,
            shotIndex,
            isLastLoadedShot,
            playerHealth,
            relicManager,
            deckManager,
            applyRuntimeRelicModifiers,
            criticalDamageMultiplierBonus);
    }

    private static bool IsBoardWideShot(BulletInstance bullet)
    {
        return BulletEffectUtility.IsBoardWideShot(bullet);
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
        bulletFeedbackView?.Show(bulletData, shotInterval);
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
