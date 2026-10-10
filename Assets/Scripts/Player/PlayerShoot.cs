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
    internal event Action PhysicalBulletResolved;
    internal event Action<int> ShotgunVolleyPresented;
    public event Action<int> DamageDealt;
    public event Action<PlayerBehaviourAction> BehaviourActionStarted;
    public event Action<BulletInstance> LoadedBulletEjected;
    public event Action LoadedBulletDamagePreviewShown;

    private readonly struct PlayerAttackTarget
    {
        public PlayerAttackTarget(EnemyController enemy)
        {
            Enemy = enemy;
            NeutralTarget = null;
        }

        public PlayerAttackTarget(IPlayerAttackTarget neutralTarget)
        {
            Enemy = null;
            NeutralTarget = neutralTarget;
        }

        public EnemyController Enemy { get; }
        public IPlayerAttackTarget NeutralTarget { get; }
        public bool IsEnemy => Enemy != null;
        public bool IsAlive => IsEnemy
            ? Enemy.CurrentHealth > 0
            : PlayerAttackTargetRegistry.IsAlive(NeutralTarget)
                && NeutralTarget.IsTargetable;
        public Transform TargetTransform => IsEnemy
            ? Enemy.transform
            : NeutralTarget?.TargetTransform;
        public Vector3 ImpactPoint => IsEnemy
            ? Enemy.HoverRenderer == null
                ? Enemy.transform.position
                : Enemy.HoverRenderer.bounds.center
            : NeutralTarget == null
                ? Vector3.zero
                : NeutralTarget.ImpactPoint;
        public int LaneIndex => IsEnemy
            ? Enemy.CurrentLaneIndex
            : NeutralTarget == null ? 0 : NeutralTarget.LaneIndex;
        public int InstanceId => IsEnemy
            ? Enemy.GetInstanceID()
            : TargetTransform == null ? 0 : TargetTransform.GetInstanceID();
        public int TotalStatusStackCount => IsEnemy
            ? Enemy.TotalStatusStackCount
            : NeutralTarget == null ? 0 : NeutralTarget.TotalStatusStackCount;
        public int ActiveStatusTypeCount => IsEnemy
            ? Enemy.ActiveStatusTypeCount
            : NeutralTarget == null ? 0 : NeutralTarget.ActiveStatusTypeCount;
    }

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
        public BulletHitTarget(PlayerAttackTarget target)
        {
            Target = target;
            InstanceId = target.InstanceId;
            InitialPosition = target.TargetTransform == null
                ? Vector3.zero
                : target.TargetTransform.position;
        }

        public PlayerAttackTarget Target { get; }
        public int InstanceId { get; }
        public Vector3 InitialPosition { get; }
    }

    private readonly struct ManagedEffectDefeatResult
    {
        public ManagedEffectDefeatResult(
            int damage,
            int healthBeforeDamage,
            int targetMaxHealth,
            Vector3 worldPosition,
            CombatFeedbackController.DefeatPresentationCue presentationCue =
                default,
            bool presentationScheduled = false)
        {
            Damage = Mathf.Max(0, damage);
            HealthBeforeDamage = Mathf.Max(0, healthBeforeDamage);
            TargetMaxHealth = Mathf.Max(0, targetMaxHealth);
            WorldPosition = worldPosition;
            PresentationCue = presentationCue;
            PresentationScheduled = presentationScheduled;
            WasDefeated = true;
        }

        public bool WasDefeated { get; }
        public int Damage { get; }
        public int HealthBeforeDamage { get; }
        public int TargetMaxHealth { get; }
        public Vector3 WorldPosition { get; }
        public CombatFeedbackController.DefeatPresentationCue PresentationCue
        {
            get;
        }
        public bool PresentationScheduled { get; }
    }

    private sealed class DamagePreviewEnemyState
    {
        public DamagePreviewEnemyState(EnemyController enemy)
        {
            Enemy = enemy;
            RemainingHealth = enemy == null ? 0 : enemy.CurrentHealth;
            MaxHealth = enemy == null ? 1 : Mathf.Max(1, enemy.MaxHealth);
            LaneIndex = enemy == null ? 0 : enemy.CurrentLaneIndex;
            StatusStacks = new int[
                StatusEffectController.StackableStatusTypeCount];
            Segments = new List<
                EnemyHealthBarFeedback.DamagePreviewSegment>();

            if (enemy == null)
            {
                return;
            }

            for (int index = 0; index < StatusStacks.Length; index++)
            {
                StatusStacks[index] = enemy.GetStatusStacks(
                    StatusEffectController.GetStackableStatusType(index));
            }

            IsExposed = enemy.IsExposed;
        }

        public DamagePreviewEnemyState(IPlayerAttackTarget neutralTarget)
        {
            NeutralTarget = neutralTarget;
            RemainingHealth = PlayerAttackTargetRegistry.IsAlive(neutralTarget)
                ? Mathf.Max(0, neutralTarget.CurrentDurability)
                : 0;
            MaxHealth = PlayerAttackTargetRegistry.IsAlive(neutralTarget)
                ? Mathf.Max(1, neutralTarget.MaxDurability)
                : 1;
            LaneIndex = PlayerAttackTargetRegistry.IsAlive(neutralTarget)
                ? neutralTarget.LaneIndex
                : 0;
            StatusStacks = new int[
                StatusEffectController.StackableStatusTypeCount];
            Segments = new List<
                EnemyHealthBarFeedback.DamagePreviewSegment>();
        }

        public EnemyController Enemy { get; }
        public IPlayerAttackTarget NeutralTarget { get; }
        public bool IsEnemy => Enemy != null;
        public bool IsValid => IsEnemy
            ? Enemy != null && RemainingHealth > 0
            : PlayerAttackTargetRegistry.IsAlive(NeutralTarget)
                && NeutralTarget.IsTargetable
                && RemainingHealth > 0;
        public Transform TargetTransform => IsEnemy
            ? Enemy.transform
            : NeutralTarget?.TargetTransform;
        public int InstanceId => IsEnemy
            ? Enemy.GetInstanceID()
            : TargetTransform == null ? 0 : TargetTransform.GetInstanceID();
        public int RemainingHealth { get; set; }
        public int MaxHealth { get; }
        public int TileIndex { get; set; } = -1;
        public int LaneIndex { get; }
        public int[] StatusStacks { get; }
        public bool WasHitThisTurn { get; set; }
        public bool IsExposed { get; set; }
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

                return count + (IsExposed ? 1 : 0);
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

                if (IsExposed && total < int.MaxValue)
                {
                    total++;
                }

                return total >= int.MaxValue ? int.MaxValue : (int)total;
            }
        }

        public void ShowDamagePreview(BulletData bullet)
        {
            if (Enemy != null)
            {
                Enemy.ShowDamagePreview(Segments, bullet);
                return;
            }

            if (NeutralTarget is IPlayerAttackTargetPresentation presentation)
            {
                presentation.ShowDamagePreview(Segments, bullet);
            }
        }

        public void ClearDamagePreview()
        {
            if (Enemy != null)
            {
                Enemy.ClearDamagePreview();
                return;
            }

            if (NeutralTarget is IPlayerAttackTargetPresentation presentation)
            {
                presentation.ClearDamagePreview();
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
    [SerializeField] private PlayerAttackTargetRegistry attackTargetRegistry;
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.05f;

    [Header("Shot Presentation")]
    [Min(0f)]
    [SerializeField] private float maxRandomShotAngle = 5f;

    private int lastActionFrame = -1;
    private bool isFiring;
    private BulletShotFeedbackView bulletFeedbackView;
    private FiringSequenceController firingSequence;
    private readonly List<PlayerAttackTarget> targetBuffer =
        new List<PlayerAttackTarget>();
    private readonly List<PlayerAttackTarget> hitBuffer =
        new List<PlayerAttackTarget>();
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
    private int sniperBulletsFiredThisCylinder;
    private int criticalShotsThisCylinder;
    private int activeShotIndex;
    private bool bulletDestroyedThisCylinder;
    private int pendingSaverGold;
    private bool pendingEmergencyReload;
    private PlayerShotRangePreview rangePreview;
    private BulletProjectileView activeProjectileView;
    private readonly List<BulletProjectileView> activeVolleyProjectileViews =
        new List<BulletProjectileView>();

    public bool IsFiring => isFiring;
    internal bool TestPendingEmergencyReload => pendingEmergencyReload;

    internal void RestoreTestShotState(bool emergencyReload)
    {
        if (!BattleTestContext.IsActive || isFiring) return;
        pendingEmergencyReload = emergencyReload;
        firingSequence?.ResetTurnTargetHistory();
        ClearLoadedBulletDamagePreview();
    }

    public int InitialLoadedBulletCount => isFiring
        ? Mathf.Max(0, initialLoadedBulletCount)
        : deckManager == null ? 0 : deckManager.LoadedBullets.Count;
    public int BulletsFiredThisCylinder => isFiring
        ? Mathf.Max(0, bulletsFiredThisCylinder)
        : 0;
    public int CriticalShotsThisCylinder => isFiring
        ? Mathf.Max(0, criticalShotsThisCylinder)
        : 0;
    public CylinderFiringOrder FiringOrder => new CylinderFiringOrder(
        waveManager != null && waveManager.ActiveRules.ReversesCylinder);

    private void Awake()
    {
        CombatAccessibilitySettings.Ensure(gameObject);
        currencyManager ??= FindFirstObjectByType<CurrencyManager>();
        combatPresentation ??= GetComponent<CombatPresentation>();
        combatFeedback ??= GetComponent<CombatFeedbackController>();
        relicManager ??= FindFirstObjectByType<RelicManager>(
            FindObjectsInactive.Include);
        attackTargetRegistry ??=
            FindFirstObjectByType<PlayerAttackTargetRegistry>();

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
            waveManager,
            relicManager);
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
        EndFiringSequence();
        CancelActiveProjectile();

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

        int horizontalDirection = playerMove == null
            ? 0
            : enemy.transform.position.x >= playerMove.transform.position.x
                ? 1
                : -1;
        CombatPresentation.EnemySnapshot snapshot = combatPresentation == null
            ? default
            : combatPresentation.CaptureEnemy(enemy);
        Vector3 defeatPosition = snapshot.Captured
            ? snapshot.Position
            : enemy.transform.position;
        CombatFeedbackController.DefeatPresentationCue presentationCue =
            combatFeedback == null
                ? default
                : combatFeedback.RecordDefeat(
                    defeatPosition,
                    horizontalDirection,
                    damage,
                    enemy.MaxHealth,
                    false,
                    waveManager != null
                        && waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild(),
                    healthBeforeDamage,
                    true,
                    enemy.LastDamageAbsorbed);
        snapshot.OverkillStrength = presentationCue.OverkillStrength;
        if (!presentationCue.SuppressPresentation)
        {
            combatPresentation?.PlayImpact(
                snapshot,
                horizontalDirection,
                currentConsumedBullet,
                CombatImpactTier.Defeat,
                presentationCue.FeedbackMultiplier > 0f
                    ? presentationCue.FeedbackMultiplier
                    : 1f,
                combatFeedback == null
                    ? 0f
                    : combatFeedback.GetRemainingDefeatPresentationDelay(
                        presentationCue),
                presentationCue.WasFinalEnemy);
        }
        pendingEffectDefeats[enemy] = new ManagedEffectDefeatResult(
            damage,
            healthBeforeDamage,
            enemy.MaxHealth,
            defeatPosition,
            presentationCue,
            true);
    }

    private void HandleBattleCompleted()
    {
        combatFeedback?.ResetCombo(true);
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
            || LoadingTransitionController.IsTransitioning)
        {
            playerMove?.ClearBufferedInput();
            return;
        }

        PlayerShootInputAction inputAction =
            PlayerShootInputReader.Read(eventSystem);

        if (inputAction != PlayerShootInputAction.None
            && (cylinderUI == null || !cylinderUI.IsDragging))
        {
            if (playerMove == null)
            {
                ExecuteInputAction(inputAction);
                return;
            }

            playerMove.BufferInputAction(ToBehaviourAction(inputAction));
        }

        TryExecuteBufferedInputAction();
    }

    private void TryExecuteBufferedInputAction()
    {
        if (playerMove == null || isFiring
            || cylinderUI != null && cylinderUI.IsDragging
            || !playerMove.TryPeekBufferedInput(
                out PlayerBehaviourAction action))
        {
            return;
        }

        PlayerShootInputAction inputAction = action switch
        {
            PlayerBehaviourAction.Reload => PlayerShootInputAction.Reload,
            PlayerBehaviourAction.Shoot => PlayerShootInputAction.Shoot,
            _ => PlayerShootInputAction.None
        };

        bool canExecute = inputAction switch
        {
            PlayerShootInputAction.Reload =>
                playerMove.CanStartInstantAction,
            PlayerShootInputAction.Shoot => playerMove.CanStartAction,
            _ => false
        };

        if (!canExecute
            || !playerMove.TryConsumeBufferedInput(action))
        {
            return;
        }

        ExecuteInputAction(inputAction);
    }

    private void ExecuteInputAction(PlayerShootInputAction inputAction)
    {
        switch (inputAction)
        {
            case PlayerShootInputAction.Reload:
                Reload();
                break;
            case PlayerShootInputAction.Shoot:
                Shoot();
                break;
        }
    }

    private static PlayerBehaviourAction ToBehaviourAction(
        PlayerShootInputAction inputAction)
    {
        return inputAction == PlayerShootInputAction.Reload
            ? PlayerBehaviourAction.Reload
            : PlayerBehaviourAction.Shoot;
    }

    public void Reload()
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || isFiring
            || cylinderUI != null && cylinderUI.IsDragging)
        {
            return;
        }

        if (deckManager == null || playerMove == null)
        {
            Debug.LogError("Deck Manager and Player Move must be assigned in the Inspector.", this);
            return;
        }

        if (!playerMove.CanStartInstantAction)
        {
            return;
        }

        if (deckManager.ReloadableBulletCount <= 0
            || deckManager.LoadedBullets.Count
                >= deckManager.MaxReloadAmount
            || !CanPayReloadHealthCost()
            || !TryBeginAction())
        {
            return;
        }

        bool wasCylinderEmpty = deckManager.LoadedBullets.Count == 0;

        if (deckManager.TryReload(out BulletInstance loadedBullet))
        {
            playerMove.RecordInstantActionStarted();
            BehaviourActionStarted?.Invoke(PlayerBehaviourAction.Reload);
            SoundManager.PlaySfx("SFX_Player_Reload");
            combatPresentation?.PlayReload(loadedBullet, cylinderUI);

            if (waveManager != null
                && waveManager.ActiveRules.UsesBloodReload)
            {
                playerHealth?.SpendHealth(
                    BattleRuleContext.BloodReloadHealthCost,
                    false);
            }

            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            bool usesEmergencyReload = pendingEmergencyReload;
            pendingEmergencyReload = false;
            bool consumesTurn = !usesEmergencyReload && (relicManager == null
                ? loadedBullet == null
                    || !loadedBullet.DoesNotConsumeReloadTurn
                : relicManager.ShouldReloadConsumeTurn(
                    loadedBullet,
                    wasCylinderEmpty));

            if (consumesTurn
                && (playerHealth == null || !playerHealth.IsDefeated))
            {
                playerMove.CompleteTurn();
            }
        }
    }

    private bool CanPayReloadHealthCost()
    {
        if (waveManager == null
            || !waveManager.ActiveRules.UsesBloodReload)
        {
            return true;
        }

        return playerHealth != null
            && playerHealth.CurrentHealth
                > BattleRuleContext.BloodReloadHealthCost;
    }

    public void Shoot()
    {
        if (GamePauseController.IsPaused || isFiring
            || LoadingTransitionController.IsTransitioning
            || cylinderUI != null && cylinderUI.IsDragging)
        {
            return;
        }

        if (deckManager == null || playerMove == null || playerHealth == null
            || boardManager == null || waveManager == null
            || firePoint == null)
        {
            Debug.LogError(
                "Deck Manager, Player Move, Player Health, Board Manager, Wave Manager, and Fire Point must be assigned in the Inspector.",
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
        int firstBulletIndex = FiringOrder.GetFirstIndex(
            deckManager.LoadedBullets.Count);
        BulletInstance firstBullet = deckManager.LoadedBullets[firstBulletIndex];

        if (firstBullet == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                playerMove.CurrentLaneIndex,
                out _)
            || !TryBeginAction())
        {
            return;
        }

        ClearLoadedBulletDamagePreview();
        BeginFiringSequence();
        StartCoroutine(firingSequence.Execute(horizontalDirection));
    }

    internal void BeginFiringSequence()
    {
        isFiring = true;
        playerMove.SetShooting(true);
        BehaviourActionStarted?.Invoke(PlayerBehaviourAction.Shoot);
    }

    internal void EndFiringSequence()
    {
        isFiring = false;
        playerMove?.SetShooting(false);
    }

    private void CancelActiveProjectile()
    {
        if (activeProjectileView != null)
        {
            activeProjectileView.CancelTravel();
        }

        activeProjectileView = null;

        foreach (BulletProjectileView projectile in activeVolleyProjectileViews)
        {
            if (projectile != null)
            {
                projectile.CancelTravel();
            }
        }

        activeVolleyProjectileViews.Clear();
    }

    public bool TryEjectLoadedBullet(int loadedBulletIndex)
    {
        if (GamePauseController.IsPaused
            || LoadingTransitionController.IsTransitioning
            || isFiring
            || cylinderUI != null && cylinderUI.IsDragging)
        {
            return false;
        }

        if (deckManager == null || playerMove == null)
        {
            Debug.LogError(
                "Deck Manager and Player Move must be assigned in the Inspector.",
                this);
            return false;
        }

        if (!playerMove.CanStartAction
            || loadedBulletIndex < 0
            || loadedBulletIndex >= deckManager.LoadedBullets.Count
            || !TryBeginAction())
        {
            return false;
        }

        ClearLoadedBulletDamagePreview();

        if (!deckManager.TryEjectLoadedBullet(
                loadedBulletIndex,
                out BulletInstance ejectedBullet))
        {
            return false;
        }

        LoadedBulletEjected?.Invoke(ejectedBullet);
        return true;
    }

    public bool ShowLoadedBulletDamagePreview(int loadedBulletIndex)
    {
        ClearLoadedBulletDamagePreview();

        if (isFiring || deckManager == null
            || playerHealth == null || boardManager == null
            || waveManager == null && attackTargetRegistry == null
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
            criticalDamageMultiplierBonus,
            waveManager == null
                ? 1f
                : waveManager.ActiveRules.DamageMultiplier);
    }

    private static bool IsBoardWideShot(BulletInstance bullet)
    {
        return BulletEffectUtility.IsBoardWideShot(bullet);
    }

    internal static bool CanPlayerEffectTargetLane(
        int sourceLaneIndex,
        int targetLaneIndex,
        bool isBoardWide)
    {
        return isBoardWide || sourceLaneIndex == targetLaneIndex;
    }

    internal static bool ShouldWaitForEnemyReplacement(
        CombatPacingMode pacingMode,
        bool isBattleCompleted,
        bool isPlayerDefeated,
        int livingEnemyCount,
        bool hasRemainingEnemiesToSpawn)
    {
        return pacingMode == CombatPacingMode.DuelClock
            && !isBattleCompleted
            && !isPlayerDefeated
            && livingEnemyCount <= 0
            && hasRemainingEnemiesToSpawn;
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
                first.CurrentLaneIndex,
                out firstIndex);
            bool hasSecond = second != null && boardManager.TryGetTileIndex(
                second.transform.position,
                second.CurrentLaneIndex,
                out secondIndex);

            if (!hasFirst || !hasSecond)
            {
                return hasFirst == hasSecond ? 0 : hasFirst ? -1 : 1;
            }

            return boardManager.GetColumnIndex(firstIndex, first.CurrentLaneIndex)
                .CompareTo(boardManager.GetColumnIndex(secondIndex, second.CurrentLaneIndex));
        });
    }

    private Vector3 GetMissEndPoint(int horizontalDirection, int maxRange)
    {
        if (boardManager.TryGetRangedTilePosition(
                transform.position,
                playerMove == null ? 0 : playerMove.CurrentLaneIndex,
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

    private void ShowBulletFeedback(BulletInstance bulletData)
    {
        bulletFeedbackView?.Show(bulletData, shotInterval);
    }

    private void SortTargetsByTileIndex(List<PlayerAttackTarget> targets)
    {
        if (boardManager == null)
        {
            return;
        }

        targets.Sort((first, second) =>
        {
            int firstIndex = 0;
            int secondIndex = 0;
            bool hasFirst = first.TargetTransform != null
                && boardManager.TryGetTileIndex(
                    first.TargetTransform.position,
                    first.LaneIndex,
                    out firstIndex);
            bool hasSecond = second.TargetTransform != null
                && boardManager.TryGetTileIndex(
                    second.TargetTransform.position,
                    second.LaneIndex,
                    out secondIndex);

            if (!hasFirst || !hasSecond)
            {
                return hasFirst == hasSecond ? 0 : hasFirst ? -1 : 1;
            }

            return boardManager.GetColumnIndex(firstIndex, first.LaneIndex)
                .CompareTo(boardManager.GetColumnIndex(
                    secondIndex,
                    second.LaneIndex));
        });
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
