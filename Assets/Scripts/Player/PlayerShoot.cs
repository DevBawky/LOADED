using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Unity.Cinemachine;

public class PlayerShoot : MonoBehaviour
{
    public event Action<BulletInstance> BulletFired;
    public event Action<int> DamageDealt;
    public event Action<PlayerBehaviourAction> BehaviourActionStarted;

    private const float BulletFeedbackStartAlpha = 0.2f;

    [Serializable]
    private class RandomSfxSettings
    {
        [Tooltip("상황이 발생할 때 무작위로 선택할 효과음 목록입니다.")]
        [SerializeField] private List<AudioClip> clips =
            new List<AudioClip>();
        [Range(0f, 1f)]
        [Tooltip("이 효과음 묶음의 재생 볼륨입니다.")]
        [SerializeField] private float volume = 1f;
        [Range(0.01f, 3f)]
        [Tooltip("무작위 피치의 최솟값입니다.")]
        [SerializeField] private float minPitch = 0.95f;
        [Range(0.01f, 3f)]
        [Tooltip("무작위 피치의 최댓값입니다.")]
        [SerializeField] private float maxPitch = 1.05f;

        public float Volume => Mathf.Clamp01(volume);
        public float RandomPitch => UnityEngine.Random.Range(
            Mathf.Min(minPitch, maxPitch),
            Mathf.Max(minPitch, maxPitch));

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Count == 0)
            {
                return null;
            }

            int validClipCount = 0;

            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                {
                    validClipCount++;
                }
            }

            if (validClipCount == 0)
            {
                return null;
            }

            int selectedClipIndex = UnityEngine.Random.Range(
                0,
                validClipCount);

            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (selectedClipIndex == 0)
                {
                    return clip;
                }

                selectedClipIndex--;
            }

            return null;
        }
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
    }

    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Transform firePoint;
    [FormerlySerializedAs("projectilePrefab")]
    [SerializeField] private BulletLine bulletLinePrefab;
    [SerializeField] private CinemachineBasicMultiChannelPerlin recoilNoise;
    [SerializeField] private Transform recoilCameraTransform;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private PlayerCylinderUI cylinderUI;
    [SerializeField] private Image bulletFeedbackImage;
    [SerializeField] private CombatPresentation combatPresentation;
    [SerializeField] private CombatFeedbackController combatFeedback;
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.2f;

    [Header("Audio")]
    [Tooltip("효과음 출력 설정의 기준으로 사용할 AudioSource입니다. 비어 있으면 2D AudioSource를 자동 생성합니다.")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private RandomSfxSettings reloadSfx =
        new RandomSfxSettings();
    [SerializeField] private RandomSfxSettings normalShotSfx =
        new RandomSfxSettings();
    [FormerlySerializedAs("shotSfx")]
    [SerializeField] private RandomSfxSettings criticalShotSfx =
        new RandomSfxSettings();

    [Header("Shot Presentation")]
    [Min(0f)]
    [SerializeField] private float maxRandomShotAngle = 5f;

    [Header("Camera Recoil")]
    [Min(0f)]
    [SerializeField] private float cameraRecoilScale = 0.02f;
    [Min(0f)]
    [SerializeField] private float recoilFrequencyGain = 0.8f;
    [Min(0f)]
    [SerializeField] private float recoilAttackDuration = 0.1f;
    [Min(0f)]
    [SerializeField] private float recoilRecoveryDuration = 0.45f;
    [SerializeField] private Vector3 cameraRestPosition = new Vector3(0f, 0f, -10f);

    private int lastActionFrame = -1;
    private bool isFiring;
    private Coroutine cameraRecoilCoroutine;
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
    private readonly List<AudioSource> sfxAudioSourcePool =
        new List<AudioSource>();
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
    private bool bulletDestroyedThisCylinder;
    private int pendingSaverGold;

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

        InitializeSfxAudioSource();
        ResetBulletFeedback();
        ResetCameraRecoil();
        reservedDamageByEnemy.Clear();
        currentConsumedBullet = null;
    }

    private void OnEnable()
    {
        EnemyController.PlayerIndirectDamageDealt +=
            HandlePlayerIndirectDamageDealt;

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

        if (cameraRecoilCoroutine != null)
        {
            StopCoroutine(cameraRecoilCoroutine);
            cameraRecoilCoroutine = null;
        }

        if (bulletFeedbackCoroutine != null)
        {
            StopCoroutine(bulletFeedbackCoroutine);
            bulletFeedbackCoroutine = null;
        }

        foreach (AudioSource source in sfxAudioSourcePool)
        {
            source?.Stop();
        }

        ResetBulletFeedback();
        ResetCameraRecoil();
        reservedDamageByEnemy.Clear();
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

        if (deckManager.TryReload(out BulletInstance loadedBullet))
        {
            BehaviourActionStarted?.Invoke(PlayerBehaviourAction.Reload);
            PlayRandomSfx(reloadSfx);
            combatPresentation?.PlayReload(loadedBullet, cylinderUI);

            if (loadedBullet == null || !loadedBullet.DoesNotConsumeTurn)
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

        return displayedAnyDamage;
    }

    public void ClearLoadedBulletDamagePreview()
    {
        foreach (EnemyController enemy in previewedEnemies)
        {
            if (enemy != null)
            {
                enemy.ClearDamagePreview();
            }
        }

        previewedEnemies.Clear();
    }

    private IEnumerator ShootLoadedBullets(int horizontalDirection)
    {
        reservedDamageByEnemy.Clear();
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
        combatFeedback?.BeginCylinder();
        bool saverRefundsTurn = false;
        int quickDrawThreshold = GetLoadedEffectMaximumStackCount(
            BulletEffectType.QuickDraw);
        bool quickDrawActive = quickDrawThreshold > 0
            && initialLoadedBulletCount <= quickDrawThreshold;
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
            consumesTurn |= !quickDrawActive
                && !resolvedBullet.DoesNotConsumeTurn;

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
                int shellExtraShots = GetAndConsumeShellExtraShots(
                    firedBullet,
                    shellEffect);
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
                        completed => shotCompleted = completed);

                    if (!shotCompleted)
                    {
                        break;
                    }

                    if (shotInterval > 0f)
                    {
                        yield return WaitForShotInterval();
                    }
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
                pendingSaverGold += saverEffect.Amount;
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
            currencyManager?.AddMoney(pendingSaverGold);
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

        if (firedAnyBullet && consumesTurn)
        {
            playerMove.CompleteTurn();
        }

        isFiring = false;
        playerMove.SetShooting(false);
        combatFeedback?.EndCylinder();
        currentConsumedBullet = null;
        reservedDamageByEnemy.Clear();
    }

    private IEnumerator FireSingleShot(
        BulletInstance bulletData,
        int horizontalDirection,
        float damageMultiplier,
        float criticalChanceBonus,
        bool generatesShells,
        bool allowEmptyShot,
        Action<bool> onCompleted)
    {
        if (bulletData == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        bool hasEnemyTarget = RefreshViableTargets(
            bulletData,
            horizontalDirection);
        bool hasBulletBlocker = waveManager.TryGetFirstBulletBlocker(
            transform.position,
            horizontalDirection,
            bulletData.MaxRange,
            out IPlayerBulletBlocker bulletBlocker);

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

        bool isCritical = bulletData.CanTriggerCritical(
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
            onCompleted?.Invoke(false);
            yield break;
        }

        ShowBulletFeedback(bulletData);
        PlayRandomSfx(isCritical ? criticalShotSfx : normalShotSfx);
        GenerateRecoil(bulletData);
        RecordSuccessfulShot();
        BulletFired?.Invoke(bulletData);
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

        if (reachesBulletBlocker && bulletBlocker != null
            && bulletBlocker.IsBulletBlocking)
        {
            bulletBlocker.HandlePlayerBulletImpact();
        }
        ReleaseProjectedDamage(shotReservations);
        HandleShotResult(bulletData, isCritical, generatesShells);
        onCompleted?.Invoke(true);
    }

    private void RecordSuccessfulShot()
    {
        if (bulletsFiredThisCylinder < int.MaxValue)
        {
            bulletsFiredThisCylinder++;
        }

        if (deckManager == null)
        {
            return;
        }

        foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
        {
            loadedBullet?.RecordShotWhileLoaded();
        }
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

    private void InitializeSfxAudioSource()
    {
        if (sfxAudioSource == null)
        {
            sfxAudioSource = GetComponent<AudioSource>();
        }

        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.spatialBlend = 0f;
        }

        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        sfxAudioSourcePool.Clear();
        sfxAudioSourcePool.Add(sfxAudioSource);
    }

    private void PlayRandomSfx(RandomSfxSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        AudioClip clip = settings.GetRandomClip();

        if (clip == null)
        {
            return;
        }

        AudioSource source = GetAvailableSfxAudioSource();
        source.Stop();
        source.clip = clip;
        source.volume = settings.Volume;
        source.pitch = settings.RandomPitch;
        source.Play();
    }

    private AudioSource GetAvailableSfxAudioSource()
    {
        foreach (AudioSource source in sfxAudioSourcePool)
        {
            if (source != null && !source.isPlaying)
            {
                return source;
            }
        }

        AudioSource pooledSource = gameObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(sfxAudioSource, pooledSource);
        pooledSource.playOnAwake = false;
        pooledSource.loop = false;
        sfxAudioSourcePool.Add(pooledSource);
        return pooledSource;
    }

    private static void CopyAudioSourceSettings(
        AudioSource source,
        AudioSource destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.outputAudioMixerGroup = source.outputAudioMixerGroup;
        destination.mute = source.mute;
        destination.bypassEffects = source.bypassEffects;
        destination.bypassListenerEffects = source.bypassListenerEffects;
        destination.bypassReverbZones = source.bypassReverbZones;
        destination.priority = source.priority;
        destination.panStereo = source.panStereo;
        destination.spatialBlend = source.spatialBlend;
        destination.reverbZoneMix = source.reverbZoneMix;
        destination.dopplerLevel = source.dopplerLevel;
        destination.spread = source.spread;
        destination.rolloffMode = source.rolloffMode;
        destination.minDistance = source.minDistance;
        destination.maxDistance = source.maxDistance;
        destination.ignoreListenerPause = source.ignoreListenerPause;
        destination.ignoreListenerVolume = source.ignoreListenerVolume;
        destination.velocityUpdateMode = source.velocityUpdateMode;
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
            int attackDamage = CalculateAttackDamage(
                bullet,
                isCritical,
                damageMultiplier * targetDamageMultiplier);
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

        BulletEffectData crescendoEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Crescendo);

        if (crescendoEffect != null)
        {
            multiplier *= 1f
                + criticalShotsThisCylinder
                * crescendoEffect.Amount / 100f;
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

    private int GetAndConsumeShellExtraShots(
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
        firedBullet.ConsumeAbilityStacks(extraShots * shellCost);
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
            else
            {
                stateOwner.AddAbilityStacks(
                    Mathf.Max(1, focusEffect.StackCount));
            }
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
                currencyManager?.AddMoney(rebateEffect.Amount);
            }
        }

        if (generatesShells)
        {
            GrantAbilityStacksToOwned(
                BulletEffectType.ShellCollector,
                1,
                stateOwner);
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

    private int GetLoadedEffectMaximumStackCount(
        BulletEffectType effectType)
    {
        int maximumStackCount = 0;

        foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
        {
            BulletEffectData effect = FindSpecialEffect(
                loadedBullet,
                effectType);

            if (effect != null)
            {
                maximumStackCount = Mathf.Max(
                    maximumStackCount,
                    effect.StackCount);
            }
        }

        return maximumStackCount;
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

        for (int hitIndex = 0; hitIndex < hitBuffer.Count; hitIndex++)
        {
            EnemyController enemy = hitBuffer[hitIndex];

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                continue;
            }

            CombatPresentation.EnemySnapshot enemySnapshot =
                combatPresentation == null
                    ? default
                    : combatPresentation.CaptureEnemy(enemy);
            int healthBeforeHit = enemy.CurrentHealth;
            int targetMaxHealth = enemy.MaxHealth;
            bool defeatPresented = false;
            float targetDamageMultiplier = GetTargetDamageMultiplier(
                bulletData,
                enemy,
                horizontalDirection);
            int attackDamage = CalculateAttackDamage(
                bulletData,
                isCritical,
                damageMultiplier * targetDamageMultiplier);

            if (hitIndex > 0)
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
                combatPresentation?.PlayImpact(
                    enemySnapshot,
                    horizontalDirection,
                    bulletData,
                    CombatImpactTier.Defeat);
                combatFeedback?.RecordDefeat(
                    enemySnapshot.Position,
                    horizontalDirection,
                    0,
                    targetMaxHealth,
                    isCritical,
                    waveManager != null && waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild());
                yield return ApplyConditionalEvents(
                    bulletData,
                    BulletConditionalTrigger.EnemyDefeated,
                    enemy,
                    horizontalDirection,
                    0);
                GrantDevourerStack(bulletData);
                continue;
            }

            int reportedDamage = enemy.PredictAttackDamage(attackDamage);
            int appliedDamage = enemy.ApplyAttackDamage(
                attackDamage,
                isCritical);
            if (appliedDamage > 0)
            {
                DamageDealt?.Invoke(reportedDamage);
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
                    defeatedByAttack));
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
                    GetCurrentCylinderBuild());
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
            bool defeatedByManagedEffect = false;

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
                result => defeatedByManagedEffect = result);

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                if (!defeatPresented)
                {
                    combatPresentation?.PlayImpact(
                        enemySnapshot,
                        horizontalDirection,
                        bulletData,
                        CombatImpactTier.Defeat);
                }

                yield return ApplyConditionalEvents(
                    bulletData,
                    BulletConditionalTrigger.EnemyDefeated,
                    enemy,
                    horizontalDirection,
                    appliedDamage);

                if (defeatedByAttack || defeatedByManagedEffect)
                {
                    GrantDevourerStack(bulletData);
                }
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
            multiplier *= 1f + enemy.ActiveStatusTypeCount
                * judgmentEffect.Amount / 100f;
        }

        BulletEffectData wallImpactEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.WallImpact);

        if (wallImpactEffect != null
            && IsEnemyBlocked(enemy, horizontalDirection))
        {
            multiplier *= 1f + wallImpactEffect.Amount / 100f;
        }

        return multiplier;
    }

    private bool IsEnemyBlocked(
        EnemyController enemy,
        int horizontalDirection)
    {
        if (enemy == null || boardManager == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                enemy.transform.position,
                out int enemyTileIndex))
        {
            return false;
        }

        int nextTileIndex = enemyTileIndex
            + (horizontalDirection >= 0 ? 1 : -1);
        return nextTileIndex < 0
            || nextTileIndex >= boardManager.BoardCount
            || waveManager.IsTileOccupied(nextTileIndex, enemy);
    }

    private IEnumerator ApplyManagedTargetEffects(
        BulletInstance bullet,
        EnemyController enemy,
        int horizontalDirection,
        Action<bool> onCompleted)
    {
        if (bullet == null || enemy == null || enemy.CurrentHealth <= 0)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        BulletEffectData amplifierEffect = FindSpecialEffect(
            bullet,
            BulletEffectType.StatusAmplifier);

        if (amplifierEffect != null && amplifierEffect.RollActivation())
        {
            enemy.MultiplyActiveStatusStacks(
                Mathf.Max(2, amplifierEffect.Amount));
        }

        bool defeated = false;
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
                long scaledPoisonDamage = remainingPoisonDamage
                    >= int.MaxValue
                    ? int.MaxValue
                    : Math.Min(
                        int.MaxValue,
                        (remainingPoisonDamage * venomBurstEffect.Amount + 99)
                        / 100);
                int poisonDamage = (int)scaledPoisonDamage;
                int healthBeforePoison = enemy.CurrentHealth;
                int poisonTargetMaxHealth = enemy.MaxHealth;
                Vector3 poisonImpactPosition = enemy.transform.position;
                int appliedPoisonDamage = enemy.ApplyStatusDamageAmount(
                    poisonDamage,
                    true);
                defeated = healthBeforePoison > 0
                    && enemy.CurrentHealth <= 0;

                if (defeated)
                {
                    combatFeedback?.RecordDefeat(
                        poisonImpactPosition,
                        horizontalDirection,
                        poisonDamage,
                        poisonTargetMaxHealth,
                        false,
                        waveManager != null
                            && waveManager.ActiveEnemies.Count <= 1,
                        GetCurrentCylinderBuild());
                }
                else if (appliedPoisonDamage > 0)
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

            if (!defeated && enemy != null && enemy.CurrentHealth > 0
                && venomBurstEffect.KnockbackDistance > 0)
            {
                enemy.AddStatusEffect(
                    StatusEffectType.Poison,
                    venomBurstEffect.KnockbackDistance,
                    true);
            }
        }

        onCompleted?.Invoke(defeated);
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
        Action<bool> onCompleted)
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
                applied = playerHealth.IncreaseMaxHealth(effect.Amount);
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
                    && currencyManager.AddMoney(effect.Amount);
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
        int appliedDamage)
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
                    null);
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
                previewBulletsFired,
                previewCriticalShots);
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
            bool guaranteedCritical = criticalChance >= 100f;
            BulletEffectData shellEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.ShellCollector);
            int shellExtraShots = GetPreviewShellExtraShots(
                firedBullet,
                shellEffect);
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
        // be consumed. Its own upgraded Primary Line Color is authoritative.
        Color previewColor = firedBullet.PrimaryLineColor;

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
                damageMultiplier * targetMultiplier);

            if (hitIndex > 0)
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
            generatesShells);

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

        if (bullet == null || previewPlayerTileIndex < 0)
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
        int previewBulletsFired,
        int previewCriticalShots)
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

        effect = FindSpecialEffect(resolvedBullet, BulletEffectType.Crescendo);

        if (effect != null)
        {
            multiplier *= 1f
                + previewCriticalShots * effect.Amount / 100f;
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
            multiplier *= 1f + enemyState.ActiveStatusTypeCount
                * effect.Amount / 100f;
        }

        effect = FindSpecialEffect(bullet, BulletEffectType.WallImpact);

        if (effect != null
            && IsPreviewEnemyBlocked(enemyState, horizontalDirection))
        {
            multiplier *= 1f + effect.Amount / 100f;
        }

        return multiplier;
    }

    private bool IsPreviewEnemyBlocked(
        DamagePreviewEnemyState enemyState,
        int horizontalDirection)
    {
        if (enemyState == null || enemyState.TileIndex < 0)
        {
            return false;
        }

        int nextTileIndex = enemyState.TileIndex
            + (horizontalDirection >= 0 ? 1 : -1);

        if (nextTileIndex < 0 || nextTileIndex >= boardManager.BoardCount)
        {
            return true;
        }

        foreach (DamagePreviewEnemyState otherState
                 in damagePreviewStates.Values)
        {
            if (otherState != enemyState && otherState.RemainingHealth > 0
                && otherState.TileIndex == nextTileIndex)
            {
                return true;
            }
        }

        return false;
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
            int multiplier = Mathf.Max(2, amplifierEffect.Amount);

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
            long scaledDamage = Math.Min(
                int.MaxValue,
                (remainingPoisonDamage * venomEffect.Amount + 99L) / 100L);
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
        bool generatesShells)
    {
        BulletEffectData focusEffect = FindSpecialEffect(
            resolvedBullet,
            BulletEffectType.Focus);

        if (focusEffect != null)
        {
            previewAbilityStacks[firedBullet] = guaranteedCritical
                ? 0
                : GetPreviewAbilityStacks(firedBullet)
                    + Mathf.Max(1, focusEffect.StackCount);
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
        previewAbilityStacks[firedBullet] =
            GetPreviewAbilityStacks(firedBullet) - extraShots * shellCost;
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
        float damageMultiplier)
    {
        if (bulletData == null || bulletData.Damage <= 0)
        {
            return 0;
        }

        int damage = bulletData.Damage;

        if (isCritical)
        {
            damage = Mathf.CeilToInt(
                damage * bulletData.CriticalDamageMultiplier);
        }

        int modifiedDamage = playerHealth.ModifyOutgoingAttackDamage(damage);
        return Mathf.CeilToInt(
            modifiedDamage * Mathf.Max(0f, damageMultiplier));
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

    private void GenerateRecoil(BulletInstance bulletData)
    {
        if (bulletData.RecoilStrength <= 0f || cameraRecoilScale <= 0f)
        {
            return;
        }

        if (recoilNoise == null || recoilCameraTransform == null)
        {
            Debug.LogError(
                "Recoil Noise and Recoil Camera Transform must be assigned in the Inspector.",
                this);
            return;
        }

        if (cameraRecoilCoroutine != null)
        {
            StopCoroutine(cameraRecoilCoroutine);
        }

        float targetAmplitudeGain = bulletData.RecoilStrength * cameraRecoilScale;
        cameraRecoilCoroutine = StartCoroutine(
            PlayCameraRecoil(targetAmplitudeGain));
    }

    private IEnumerator PlayCameraRecoil(float targetAmplitudeGain)
    {
        float currentAmplitudeGain = recoilNoise.AmplitudeGain;
        recoilNoise.FrequencyGain = recoilFrequencyGain;

        yield return ChangeAmplitudeGain(
            currentAmplitudeGain,
            targetAmplitudeGain,
            recoilAttackDuration);
        yield return ChangeAmplitudeGain(
            targetAmplitudeGain,
            0f,
            recoilRecoveryDuration,
            true);

        ResetCameraRecoil();
        cameraRecoilCoroutine = null;
    }

    private IEnumerator ChangeAmplitudeGain(
        float startGain,
        float targetGain,
        float duration,
        bool returnCameraToRestPosition = false)
    {
        Vector3 startCameraPosition = recoilCameraTransform.position;

        if (duration <= 0f)
        {
            recoilNoise.AmplitudeGain = targetGain;

            if (returnCameraToRestPosition)
            {
                recoilCameraTransform.position = cameraRestPosition;
            }

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = GetSmootherStep(progress);
            recoilNoise.AmplitudeGain = Mathf.Lerp(
                startGain,
                targetGain,
                smoothProgress);

            if (returnCameraToRestPosition)
            {
                recoilCameraTransform.position = Vector3.Lerp(
                    startCameraPosition,
                    cameraRestPosition,
                    smoothProgress);
            }
        }

        recoilNoise.AmplitudeGain = targetGain;

        if (returnCameraToRestPosition)
        {
            recoilCameraTransform.position = cameraRestPosition;
        }
    }

    private float GetSmootherStep(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress * progress * progress
            * (progress * (progress * 6f - 15f) + 10f);
    }

    private void ResetCameraRecoil()
    {
        if (recoilNoise != null)
        {
            recoilNoise.AmplitudeGain = 0f;
            recoilNoise.FrequencyGain = 0f;
        }

        if (recoilCameraTransform != null)
        {
            recoilCameraTransform.position = cameraRestPosition;
            recoilCameraTransform.localRotation = Quaternion.identity;
        }
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
