using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EnemyTurnActionType
{
    None,
    Move,
    Rotate,
    Wait,
    Reload,
    Fire,
    CreateQueue,
    RegisterAttack,
    PrepareAttack,
    Support
}

public enum EnemySupportType
{
    None,
    Heal,
    Shield
}

public enum BigBarrelStep
{
    RotateToPlayer,
    CreateBombQueue,
    RegisterBomb,
    PrepareBomb,
    ExecuteBomb,
    AdjustDistance,
    CreateShotgunQueue,
    RegisterShotgun,
    PrepareShotgun,
    ExecuteShotgun,
    Reload
}

[System.Serializable]
public class EnemyRandomSfxSettings
{
    [Tooltip("상황이 발생할 때 무작위로 선택할 효과음 목록입니다.")]
    [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
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

        int selectedClipIndex = UnityEngine.Random.Range(0, validClipCount);

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

public class EnemyController : MonoBehaviour, IStatusEffectTarget
{
    private const int InitialFacingDirection = -1;
    private static readonly int IdleAnimationStateHash =
        Animator.StringToHash("Base Layer.Idle");
    private static readonly int AttackAnimationStateHash =
        Animator.StringToHash("Base Layer.Attack");
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int BeamColorId =
        Shader.PropertyToID("_BeamColor");
    private static readonly int GridColorId =
        Shader.PropertyToID("_GridColor");
    private const int DefaultProjectileResolution = 32;
    private static Sprite defaultThrownProjectileSprite;

    [Header("Data")]
    [Tooltip("런타임에 WaveManager가 주입하는 적 데이터입니다.")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Transform canvasTransform;
    [SerializeField] private ActorMotion actorMotion;
    [SerializeField] private EnemyActionQueueUI actionQueueUI;
    [SerializeField] private StatusEffectController statusEffects;
    [SerializeField] private EnemyDamageNumberDisplay damageNumberDisplay;

    [Header("Audio")]
    [Tooltip("출력 Mixer와 2D/3D 설정의 기준 AudioSource입니다. 비어 있으면 2D Source를 자동 생성합니다.")]
    [SerializeField] private AudioSource sfxAudioSource;
    [Tooltip("일반 공격에 맞았을 때 사용할 효과음 설정입니다.")]
    [SerializeField] private EnemyRandomSfxSettings normalHitSfx =
        new EnemyRandomSfxSettings();
    [Tooltip("치명타 공격에 맞았을 때 사용할 효과음 설정입니다.")]
    [SerializeField] private EnemyRandomSfxSettings criticalHitSfx =
        new EnemyRandomSfxSettings();
    [Tooltip("적이 죽을 때 사용할 효과음 설정입니다.")]
    [SerializeField] private EnemyRandomSfxSettings deathSfx =
        new EnemyRandomSfxSettings();

    [Header("Runtime State")]
    [SerializeField] private GameObject avatarInstance;
    [SerializeField] private int currentHealth;
    [SerializeField] private int currentShield;
    [SerializeField] private int remainingSupportCharges;
    [SerializeField] private int recoveryTurnsRemaining;
    [SerializeField] private List<EnemyActionData> queuedAttackActions =
        new List<EnemyActionData>();
    [SerializeField] private bool isQueueCreated;
    [SerializeField] private bool isAttackPrepared;
    [SerializeField] private bool isRetreating;
    [SerializeField] private int preparedTargetTileIndex = -1;
    [SerializeField] private Vector3 preparedTargetPosition;
    [SerializeField] private EnemyController preparedSupportTarget;
    [SerializeField] private EnemySupportType preparedSupportType;
    [SerializeField] private EnemyTurnActionType lastTurnAction;
    [SerializeField] private bool isActing;
    [SerializeField] private BigBarrelStep bigBarrelStep =
        BigBarrelStep.RotateToPlayer;
    [SerializeField] private bool isBigBarrelPhaseTwo;
    [SerializeField] private bool bigBarrelActionUsesPhaseTwo;
    [SerializeField] private int preparedBigBarrelFuse;
    [SerializeField] private int bigBarrelReloadTurnsRemaining;
    [SerializeField] private List<int> preparedBombTargetTileIndices =
        new List<int>();
    [SerializeField] private List<int> preparedShotgunTileIndices =
        new List<int>();

    private BoardManager boardManager;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private WaveManager waveManager;
    private bool isInitialized;
    private LineRenderer attackTelegraphLine;
    private LineRenderer shieldIndicatorLine;
    private readonly List<Vector3> movePath = new List<Vector3>();
    private readonly List<EnemyController> attackTargetBuffer =
        new List<EnemyController>();
    private MaterialPropertyBlock lineColorProperties;
    private EnemyHealthBarFeedback healthBarFeedback;
    private BossHudController bossHud;
    private Animator avatarAnimator;
    private SpriteRenderer avatarSortingRenderer;
    private int avatarAnimationSequence;
    private readonly List<AudioSource> sfxAudioSourcePool =
        new List<AudioSource>();
    private readonly List<LineRenderer> bigBarrelTelegraphLines =
        new List<LineRenderer>();

    public event Action<EnemyController, EnemyTurnActionType> TurnActionCompleted;
    public event Action<EnemyController, EnemyAttackData> AttackExecuted;
    public event Action<EnemyController, int, int> HealthChanged;
    public event Action<EnemyController, int> ShieldChanged;
    public event Action<EnemyController> Defeated;

    public EnemyData Data => enemyData;
    public int CurrentHealth => currentHealth;
    public int CurrentShield => currentShield;
    public int RemainingSupportCharges => remainingSupportCharges;
    public int MaxHealth => enemyData == null ? 0 : enemyData.MaxHealth;
    public EnemyActionData LoadedAttackAction => queuedAttackActions.Count > 0
        ? queuedAttackActions[0]
        : null;
    public IReadOnlyList<EnemyActionData> QueuedAttackActions =>
        queuedAttackActions;
    public bool IsQueueCreated => isQueueCreated;
    public bool IsAttackPrepared => isAttackPrepared;
    public bool IsRetreating => isRetreating;
    public int PreparedTargetTileIndex => preparedTargetTileIndex;
    public EnemyTurnActionType LastTurnAction => lastTurnAction;
    public bool IsActing => isActing;

    private void Awake()
    {
        lineColorProperties = new MaterialPropertyBlock();

        if (statusEffects == null)
        {
            statusEffects = GetComponent<StatusEffectController>();
        }

        if (damageNumberDisplay == null)
        {
            damageNumberDisplay = GetComponent<EnemyDamageNumberDisplay>();
        }

        healthBarFeedback = GetComponent<EnemyHealthBarFeedback>();
        if (healthBarFeedback == null && healthFillImage != null)
        {
            healthBarFeedback =
                gameObject.AddComponent<EnemyHealthBarFeedback>();
        }

        healthBarFeedback?.Initialize(healthFillImage);
        InitializeSfxAudioSource();
        ResetRuntimeState();
        ApplyCanvasOrientation();
    }

    private void LateUpdate()
    {
        if (isAttackPrepared && enemyData != null
            && enemyData.BehaviorType != EnemyBehaviorType.BigBarrel
            && enemyData.BehaviorType != EnemyBehaviorType.Melee)
        {
            RefreshAttackTelegraph();
        }
    }

    public bool Initialize(
        EnemyData assignedEnemyData,
        BoardManager assignedBoardManager,
        PlayerMove assignedPlayerMove,
        PlayerHealth assignedPlayerHealth,
        WaveManager assignedWaveManager)
    {
        if (assignedEnemyData == null || assignedBoardManager == null
            || assignedPlayerMove == null || assignedPlayerHealth == null
            || assignedWaveManager == null || actorMotion == null
            || actionQueueUI == null)
        {
            Debug.LogError(
                "Enemy Data, Actor Motion, Action Queue UI, Board Manager, Player Move, Player Health, and Wave Manager must be assigned.",
                this);
            return false;
        }

        enemyData = assignedEnemyData;
        boardManager = assignedBoardManager;
        playerMove = assignedPlayerMove;
        playerHealth = assignedPlayerHealth;
        waveManager = assignedWaveManager;
        ApplyInitialFacingDirection();
        SpawnAvatar();
        ResetRuntimeState();

        if (enemyData.BehaviorType == EnemyBehaviorType.BigBarrel)
        {
            ConfigureBigBarrelHud();
        }

        ApplyCanvasOrientation();
        gameObject.name = string.IsNullOrWhiteSpace(enemyData.DisplayName)
            ? enemyData.name
            : enemyData.DisplayName;
        isInitialized = true;
        return true;
    }

    public void TakeTurn()
    {
        if (isActing)
        {
            return;
        }

        isActing = true;

        if (statusEffects != null && statusEffects.ConsumeStunTurn())
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (!isInitialized || enemyData == null || boardManager == null
            || playerMove == null || waveManager == null || actorMotion == null)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.BigBarrel)
        {
            TakeBigBarrelTurn();
            return;
        }

        if (isAttackPrepared && queuedAttackActions.Count == 0)
        {
            ClearAttackQueue();
        }

        if (isAttackPrepared)
        {
            isAttackPrepared = false;
            HideAttackTelegraph();
            StartCoroutine(FireAttackQueue());
            return;
        }

        // A prepared attack is already committed. Frontline priority is only
        // used while choosing a new action; it must never delay a telegraphed
        // attack when the player changes position before the enemy turn.
        if (!CanTakeFrontlineTurn())
        {
            if (TryGetTurnContext(
                    out int waitingDirectionToPlayer,
                    out _)
                && waitingDirectionToPlayer != 0
                && !IsFacing(waitingDirectionToPlayer))
            {
                RotateToward(waitingDirectionToPlayer);
            }
            else
            {
                CompleteAction(EnemyTurnActionType.Wait);
            }

            return;
        }

        if (recoveryTurnsRemaining > 0)
        {
            recoveryTurnsRemaining--;
            CompleteAction(EnemyTurnActionType.Reload);
            return;
        }

        if (!TryGetTurnContext(out int directionToPlayer, out int distanceToPlayer))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
        {
            TakeThrowerTurn();
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Porter)
        {
            TakePorterTurn(directionToPlayer, distanceToPlayer);
            return;
        }

        if (directionToPlayer != 0 && !IsFacing(directionToPlayer))
        {
            RotateToward(directionToPlayer);
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            TakeMeleeTurn(directionToPlayer, distanceToPlayer);
            return;
        }

        TakeGunnerTurn(directionToPlayer, distanceToPlayer);
    }

    private void OnDisable()
    {
        HideAttackTelegraph();
        bossHud?.Unbind(this);
        bossHud = null;

        if (shieldIndicatorLine != null)
        {
            shieldIndicatorLine.enabled = false;
        }

        isActing = false;
    }

    private void ConfigureBigBarrelHud()
    {
        if (canvasTransform != null)
        {
            canvasTransform.gameObject.SetActive(false);
        }

        bossHud = FindFirstObjectByType<BossHudController>(
            FindObjectsInactive.Include);

        if (bossHud == null)
        {
            Debug.LogWarning(
                "Panel | Boss를 관리하는 BossHudController를 찾지 못했습니다.",
                this);
            return;
        }

        if (bossHud.Bind(this, healthBarFeedback, statusEffects))
        {
            RefreshHealthUI();
        }
    }

    public bool ApplyDamage(int damage)
    {
        return ApplyAttackDamage(damage) > 0;
    }

    public int ApplyAttackDamage(int damage)
    {
        return ApplyAttackDamage(damage, false);
    }

    public int ApplyAttackDamage(int damage, bool isCritical)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        int modifiedDamage = PredictAttackDamage(damage);
        int appliedDamage = ApplyDamageInternal(
            modifiedDamage,
            isCritical,
            isCritical ? 1.45f : 1f);

        if (appliedDamage > 0 && currentHealth > 0)
        {
            PlayRandomSfx(
                isCritical
                    ? criticalHitSfx
                    : normalHitSfx);
        }

        int markBonusDamage = Mathf.Max(0, modifiedDamage - damage);

        // Damage popups communicate the attack's full power, not the amount
        // clamped by the enemy's remaining health.
        damageNumberDisplay?.ShowAttackDamage(damage, isCritical);
        damageNumberDisplay?.ShowMarkBonusDamage(markBonusDamage);
        return appliedDamage;
    }

    public int PredictAttackDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        return statusEffects == null
            ? damage
            : statusEffects.ModifyIncomingAttackDamage(damage);
    }

    public void ShowDamagePreview(
        IReadOnlyList<EnemyHealthBarFeedback.DamagePreviewSegment> segments)
    {
        healthBarFeedback?.ShowDamagePreview(
            currentHealth,
            MaxHealth,
            segments);
    }

    public void ClearDamagePreview()
    {
        healthBarFeedback?.ClearDamagePreview();
    }

    public bool ApplyStatusDamage(int damage)
    {
        int appliedDamage = ApplyDamageInternal(damage, false, 0.45f);

        // Status damage popups show the effect's full damage, even when the
        // target has less health remaining than the requested damage.
        if (appliedDamage > 0)
        {
            damageNumberDisplay?.ShowPoisonDamage(damage);
        }

        return appliedDamage > 0;
    }

    public bool ApplyCollisionDamage(int damage)
    {
        int appliedDamage = ApplyDamageInternal(damage, false, 1.2f);
        damageNumberDisplay?.ShowAttackDamage(appliedDamage, false);
        return appliedDamage > 0;
    }

    public int ApplyExplosionDamage(int normalDamage, int bossDamage)
    {
        int damage = enemyData != null
            && enemyData.BehaviorType == EnemyBehaviorType.BigBarrel
                ? bossDamage
                : normalDamage;
        int appliedDamage = ApplyDamageInternal(damage, false, 1.1f);

        if (appliedDamage > 0)
        {
            damageNumberDisplay?.ShowAttackDamage(appliedDamage, false);
        }

        return appliedDamage;
    }

    public int ApplyEnvironmentalDamage(int damage)
    {
        int appliedDamage = ApplyDamageInternal(damage, false, 0.8f);

        if (appliedDamage > 0)
        {
            damageNumberDisplay?.ShowAttackDamage(appliedDamage, false);
        }

        return appliedDamage;
    }

    public IEnumerator FlyTo(
        Vector3 targetPosition,
        float duration)
    {
        if (actorMotion == null)
        {
            yield break;
        }

        yield return actorMotion.FlyTo(targetPosition, duration);
        ApplyCanvasOrientation();
    }

    public IEnumerator FlyIntoCollision(
        Vector3 impactPosition,
        Vector3 restingPosition,
        float flightDuration,
        float settleDuration)
    {
        if (actorMotion == null)
        {
            yield break;
        }

        yield return actorMotion.FlyIntoCollision(
            impactPosition,
            restingPosition,
            flightDuration,
            settleDuration);
        ApplyCanvasOrientation();
    }

    public bool AddStatusEffect(StatusEffectType type, int stacks)
    {
        bool applied = currentHealth > 0 && statusEffects != null
            && statusEffects.Add(type, stacks);

        if (applied)
        {
            damageNumberDisplay?.ShowStatus(type);
        }

        return applied;
    }

    public int ActiveStatusTypeCount => statusEffects == null
        ? 0
        : statusEffects.ActiveStatusTypeCount;

    public int GetStatusStacks(StatusEffectType type)
    {
        return statusEffects == null ? 0 : statusEffects.GetStacks(type);
    }

    public int ConsumeStatusStacks(StatusEffectType type)
    {
        return statusEffects == null ? 0 : statusEffects.Consume(type);
    }

    public bool MultiplyActiveStatusStacks(int multiplier)
    {
        return currentHealth > 0 && statusEffects != null
            && statusEffects.MultiplyActiveStacks(multiplier);
    }

    public void ShowLifeStealStatus()
    {
        damageNumberDisplay?.ShowLifeStealStatus();
    }

    private int ApplyDamageInternal(
        int damage,
        bool isCritical,
        float impactStrength)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        int absorbedDamage = Mathf.Min(currentShield, damage);

        if (absorbedDamage > 0)
        {
            currentShield -= absorbedDamage;
            RefreshShieldIndicator();
            ShieldChanged?.Invoke(this, currentShield);
        }

        int healthDamage = damage - absorbedDamage;
        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - healthDamage);
        int appliedDamage = absorbedDamage + previousHealth - currentHealth;

        if (currentHealth != previousHealth)
        {
            RefreshHealthUI(true, isCritical, impactStrength);
            HealthChanged?.Invoke(this, currentHealth, enemyData.MaxHealth);
        }

        if (currentHealth == 0)
        {
            PlayDeathSfx();
            ClearAttackQueue();
            StopAllCoroutines();
            isActing = false;
            waveManager?.NotifyBigBarrelDefeated(this);

            Defeated?.Invoke(this);
            Destroy(gameObject);
        }

        return appliedDamage;
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

    private void PlayRandomSfx(EnemyRandomSfxSettings settings)
    {
        if (!TryGetRandomSfx(settings, out AudioClip clip,
                out float volume, out float pitch))
        {
            return;
        }

        AudioSource source = GetAvailableSfxAudioSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
    }

    private void PlayDeathSfx()
    {
        if (!TryGetRandomSfx(deathSfx, out AudioClip clip,
                out float volume, out float pitch))
        {
            return;
        }

        GameObject audioObject = new GameObject("Enemy Death SFX");
        audioObject.transform.position = transform.position;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(sfxAudioSource, source);
        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        float playbackDuration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
        Destroy(audioObject, playbackDuration + 0.1f);
    }

    private static bool TryGetRandomSfx(
        EnemyRandomSfxSettings settings,
        out AudioClip clip,
        out float volume,
        out float pitch)
    {
        clip = settings?.GetRandomClip();
        volume = settings == null ? 1f : settings.Volume;
        pitch = settings == null ? 1f : settings.RandomPitch;
        return clip != null;
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

        GameObject audioObject = new GameObject("Enemy SFX Source");
        audioObject.transform.SetParent(transform, false);
        AudioSource pooledSource = audioObject.AddComponent<AudioSource>();
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

    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= MaxHealth)
        {
            return false;
        }

        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        RefreshHealthUI();
        HealthChanged?.Invoke(this, currentHealth, MaxHealth);
        return true;
    }

    public bool AddShield(int amount)
    {
        return AddShield(amount, null, new Color(0.2f, 0.8f, 1f, 1f));
    }

    public bool AddShield(
        int amount,
        Material indicatorMaterial,
        Color indicatorColor)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return false;
        }

        currentShield += amount;
        RefreshShieldIndicator(indicatorMaterial, indicatorColor);
        ShieldChanged?.Invoke(this, currentShield);
        return true;
    }

    private void TakeMeleeTurn(int directionToPlayer, int distanceToPlayer)
    {
        if (isRetreating)
        {
            if (enemyData.PreferredDistance <= 0
                || distanceToPlayer >= enemyData.PreferredDistance)
            {
                isRetreating = false;
            }
            else if (TryMoveAwayFromPlayer(
                         directionToPlayer,
                         distanceToPlayer))
            {
                return;
            }
            else
            {
                isRetreating = false;
            }
        }

        if (GetAvailableAttackCount(EnemyActionType.MeleeAttack) == 0)
        {
            ClearAttackQueue();
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        if (!isQueueCreated)
        {
            CreateAttackQueue();
            return;
        }

        if (queuedAttackActions.Count == 0)
        {
            RegisterAction(EnemyActionType.MeleeAttack, 0);
            return;
        }

        if (distanceToPlayer > 1)
        {
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        PrepareCurrentAttackQueue();
    }

    private void TakeGunnerTurn(int directionToPlayer, int distanceToPlayer)
    {
        int definedAttackCount = GetAvailableAttackCount(
            EnemyActionType.RangedAttack);

        if (definedAttackCount == 0)
        {
            ClearAttackQueue();
            MoveTowardPlayer(directionToPlayer);
            return;
        }

        if (!isQueueCreated)
        {
            CreateAttackQueue();
            return;
        }

        if (queuedAttackActions.Count == 0)
        {
            RegisterAction(EnemyActionType.RangedAttack, 0);
            return;
        }

        if (CanPrepareGunnerAttack(directionToPlayer, distanceToPlayer))
        {
            PrepareCurrentAttackQueue();
            return;
        }

        CompleteAction(EnemyTurnActionType.Wait);
    }

    private void TakeThrowerTurn()
    {
        if (GetAvailableAttackCount(EnemyActionType.RangedAttack) == 0)
        {
            ClearAttackQueue();
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (!isQueueCreated)
        {
            CreateAttackQueue();
            return;
        }

        if (queuedAttackActions.Count == 0)
        {
            RegisterAction(EnemyActionType.RangedAttack, 0);
            return;
        }

        if (!CaptureThrowerTargetTile())
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        PrepareCurrentAttackQueue();
    }

    private void TakeBigBarrelTurn()
    {
        if (!TryGetTurnContext(
                out int directionToPlayer,
                out int distanceToPlayer))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        switch (bigBarrelStep)
        {
            case BigBarrelStep.RotateToPlayer:
                TryEnterBigBarrelPhaseTwo();
                bigBarrelStep = BigBarrelStep.CreateBombQueue;

                if (directionToPlayer != 0 && !IsFacing(directionToPlayer))
                {
                    RotateToward(directionToPlayer);
                }
                else
                {
                    CompleteAction(EnemyTurnActionType.Wait);
                }
                break;

            case BigBarrelStep.CreateBombQueue:
                bigBarrelActionUsesPhaseTwo = isBigBarrelPhaseTwo;
                bigBarrelStep = BigBarrelStep.RegisterBomb;
                CreateAttackQueue();
                break;

            case BigBarrelStep.RegisterBomb:
                RegisterBigBarrelAction(
                    EnemyActionType.ExplosiveThrow,
                    BigBarrelStep.PrepareBomb,
                    BigBarrelStep.AdjustDistance);
                break;

            case BigBarrelStep.PrepareBomb:
                CaptureBigBarrelBombTargets();
                preparedBigBarrelFuse = bigBarrelActionUsesPhaseTwo
                    ? enemyData.BigBarrel.PhaseTwoBombFuseTurns
                    : enemyData.BigBarrel.BombFuseTurns;
                bigBarrelStep = BigBarrelStep.ExecuteBomb;
                PrepareCurrentAttackQueue();
                break;

            case BigBarrelStep.ExecuteBomb:
                isAttackPrepared = false;
                HideAttackTelegraph();
                StartCoroutine(FireBigBarrelBombs());
                break;

            case BigBarrelStep.AdjustDistance:
                TryEnterBigBarrelPhaseTwo();
                bigBarrelStep = BigBarrelStep.CreateShotgunQueue;
                AdjustBigBarrelDistance(
                    directionToPlayer,
                    distanceToPlayer);
                break;

            case BigBarrelStep.CreateShotgunQueue:
                bigBarrelStep = BigBarrelStep.RegisterShotgun;
                CreateAttackQueue();
                break;

            case BigBarrelStep.RegisterShotgun:
                RegisterBigBarrelAction(
                    EnemyActionType.ShotgunAttack,
                    BigBarrelStep.PrepareShotgun,
                    BigBarrelStep.Reload);
                break;

            case BigBarrelStep.PrepareShotgun:
                CaptureBigBarrelShotgunTargets();
                bigBarrelStep = BigBarrelStep.ExecuteShotgun;
                PrepareCurrentAttackQueue();
                break;

            case BigBarrelStep.ExecuteShotgun:
                isAttackPrepared = false;
                HideAttackTelegraph();
                StartCoroutine(FireBigBarrelShotgun());
                break;

            case BigBarrelStep.Reload:
                TakeBigBarrelReloadTurn();
                break;
        }
    }

    private void RegisterBigBarrelAction(
        EnemyActionType actionType,
        BigBarrelStep successStep,
        BigBarrelStep failureStep)
    {
        if (!isQueueCreated
            || !TryAppendAction(
                actionType,
                0,
                out Image attackIcon,
                out _))
        {
            ClearAttackQueue();
            bigBarrelStep = failureStep;
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        bigBarrelStep = successStep;
        StartCoroutine(RevealRegisteredAction(attackIcon));
    }

    private void CaptureBigBarrelBombTargets()
    {
        preparedBombTargetTileIndices.Clear();
        List<int> candidates = new List<int>();

        if (!boardManager.TryGetTileIndex(
                transform.position,
                out int bossTileIndex))
        {
            return;
        }

        for (int tileIndex = 0;
             tileIndex < boardManager.BoardCount;
             tileIndex++)
        {
            if (tileIndex != bossTileIndex
                && (waveManager.BombManager == null
                    || !waveManager.BombManager.IsTileOccupied(tileIndex)))
            {
                candidates.Add(tileIndex);
            }
        }

        int requestedCount = bigBarrelActionUsesPhaseTwo ? 3 : 2;

        while (preparedBombTargetTileIndices.Count < requestedCount
               && candidates.Count > 0)
        {
            int candidateIndex = UnityEngine.Random.Range(0, candidates.Count);
            preparedBombTargetTileIndices.Add(candidates[candidateIndex]);
            candidates.RemoveAt(candidateIndex);
        }
    }

    private void CaptureBigBarrelShotgunTargets()
    {
        preparedShotgunTileIndices.Clear();

        if (!boardManager.TryGetTileIndex(
                transform.position,
                out int bossTileIndex))
        {
            return;
        }

        int leftTile = bossTileIndex - 1;
        int rightTile = bossTileIndex + 1;

        if (leftTile >= 0)
        {
            preparedShotgunTileIndices.Add(leftTile);
        }

        if (rightTile < boardManager.BoardCount)
        {
            preparedShotgunTileIndices.Add(rightTile);
        }
    }

    private void AdjustBigBarrelDistance(
        int directionToPlayer,
        int distanceToPlayer)
    {
        int preferredDistance = enemyData.PreferredDistance;

        if (directionToPlayer == 0 || distanceToPlayer == preferredDistance)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        int moveDirection = distanceToPlayer < preferredDistance
            ? -directionToPlayer
            : directionToPlayer;

        if (!boardManager.TryGetAdjacentTilePosition(
                transform.position,
                moveDirection,
                out Vector3 targetPosition)
            || !boardManager.TryGetTileIndex(
                targetPosition,
                out int targetTileIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            || targetTileIndex == playerTileIndex
            || waveManager.IsTileOccupied(targetTileIndex, this)
            || waveManager.IsTileReservedForSpawn(targetTileIndex)
            || waveManager.BombManager != null
            && waveManager.BombManager.IsTileThreatened(targetTileIndex, 1))
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        StartCoroutine(MoveRoutine(new[] { targetPosition }, false));
    }

    private IEnumerator FireBigBarrelBombs()
    {
        EnemyActionData action = PopFirstQueuedAction();
        yield return PlayAvatarAttackAnimation();

        foreach (int targetTileIndex in preparedBombTargetTileIndices)
        {
            if (currentHealth <= 0
                || targetTileIndex < 0
                || targetTileIndex >= boardManager.BoardCount
                || !boardManager.TryGetTilePosition(
                    targetTileIndex,
                    out Vector3 targetPosition)
                || waveManager.BombManager == null
                || waveManager.BombManager.IsTileOccupied(targetTileIndex))
            {
                continue;
            }

            yield return PlayBigBarrelProjectile(targetPosition);
            waveManager.BombManager.TrySpawnBomb(
                enemyData,
                targetTileIndex,
                preparedBigBarrelFuse,
                out _);
        }

        if (action != null && action.AttackData != null)
        {
            AttackExecuted?.Invoke(this, action.AttackData);
        }

        FinishBigBarrelAttack(BigBarrelStep.AdjustDistance);
    }

    private IEnumerator FireBigBarrelShotgun()
    {
        EnemyActionData action = PopFirstQueuedAction();
        yield return PlayAvatarAttackAnimation();

        foreach (int targetTileIndex in preparedShotgunTileIndices)
        {
            if (currentHealth <= 0)
            {
                break;
            }

            if (boardManager.TryGetTileIndex(
                    playerMove.transform.position,
                    out int playerTileIndex)
                && playerTileIndex == targetTileIndex)
            {
                playerHealth.ApplyDamage(enemyData.BigBarrel.ShotgunDamage);
            }

            if (waveManager.TryGetEnemyAtTile(
                    targetTileIndex,
                    out EnemyController target,
                    this))
            {
                target.ApplyEnvironmentalDamage(
                    enemyData.BigBarrel.ShotgunDamage);
            }
        }

        CombatCameraShake.Play(enemyData.BigBarrel.BossHitCameraShake);

        if (action != null && action.AttackData != null)
        {
            AttackExecuted?.Invoke(this, action.AttackData);
        }

        bigBarrelReloadTurnsRemaining = enemyData.RecoveryTurns;
        FinishBigBarrelAttack(bigBarrelReloadTurnsRemaining > 0
            ? BigBarrelStep.Reload
            : BigBarrelStep.RotateToPlayer);
    }

    private IEnumerator PlayBigBarrelProjectile(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float duration = enemyData.ThrownProjectileDuration;
        GameObject projectile = enemyData.ThrownProjectilePrefab == null
            ? CreateDefaultThrownProjectile(startPosition)
            : Instantiate(
                enemyData.ThrownProjectilePrefab,
                startPosition,
                Quaternion.identity);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsed += Time.deltaTime;
            float progress = duration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);
            position += Vector3.up * (Mathf.Sin(progress * Mathf.PI)
                * enemyData.BigBarrel.BombArcHeight);

            if (projectile != null)
            {
                projectile.transform.position = position;
            }
        }

        if (projectile != null)
        {
            projectile.transform.position = targetPosition;
            Destroy(projectile);
        }
    }

    private EnemyActionData PopFirstQueuedAction()
    {
        if (queuedAttackActions.Count == 0)
        {
            return null;
        }

        EnemyActionData action = queuedAttackActions[0];
        queuedAttackActions.RemoveAt(0);
        actionQueueUI.RemoveFirstIcon();
        return action;
    }

    private void FinishBigBarrelAttack(BigBarrelStep nextStep)
    {
        isQueueCreated = false;
        isAttackPrepared = false;
        preparedBombTargetTileIndices.Clear();
        preparedShotgunTileIndices.Clear();
        actionQueueUI.ResetDisplay();
        bigBarrelStep = nextStep;
        CompleteAction(EnemyTurnActionType.Fire);
    }

    private void TakeBigBarrelReloadTurn()
    {
        if (bigBarrelReloadTurnsRemaining <= 0)
        {
            bigBarrelStep = BigBarrelStep.RotateToPlayer;
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        bigBarrelReloadTurnsRemaining--;

        if (bigBarrelReloadTurnsRemaining == 0)
        {
            bigBarrelStep = BigBarrelStep.RotateToPlayer;
        }

        EnemyActionData reloadAction = FindAction(EnemyActionType.Reload);

        if (reloadAction == null)
        {
            CompleteAction(EnemyTurnActionType.Reload);
            return;
        }

        actionQueueUI.ResetDisplay();
        actionQueueUI.ShowQueue();

        if (actionQueueUI.AddAttackIcon(reloadAction, out Image icon))
        {
            StartCoroutine(RevealBigBarrelReload(icon));
        }
        else
        {
            CompleteAction(EnemyTurnActionType.Reload);
        }
    }

    private IEnumerator RevealBigBarrelReload(Image icon)
    {
        yield return actionQueueUI.RevealIcon(
            icon,
            enemyData.QueueElementRevealDuration);
        CompleteAction(EnemyTurnActionType.Reload);
    }

    private void TryEnterBigBarrelPhaseTwo()
    {
        if (isBigBarrelPhaseTwo || MaxHealth <= 0
            || (float)currentHealth / MaxHealth
            > enemyData.BigBarrel.PhaseTwoHealthRatio)
        {
            return;
        }

        isBigBarrelPhaseTwo = true;
        StartCoroutine(PlayBigBarrelPhaseTransition());
    }

    private IEnumerator PlayBigBarrelPhaseTransition()
    {
        SpriteRenderer[] renderers = avatarInstance == null
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : avatarInstance.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] originalColors = new Color[renderers.Length];

        for (int index = 0; index < renderers.Length; index++)
        {
            originalColors[index] = renderers[index].color;
            renderers[index].color = Color.Lerp(
                originalColors[index],
                new Color(1f, 0.25f, 0.05f, 1f),
                0.7f);
        }

        yield return actionQueueUI.PlayPhaseTransition(
            enemyData.QueueElementRevealDuration,
            new Color(1f, 0.25f, 0.05f, 1f));

        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                renderers[index].color = originalColors[index];
            }
        }
    }

    private void TakePorterTurn(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (remainingSupportCharges <= 0)
        {
            if (!TryMoveAwayFromPlayer(directionToPlayer, distanceToPlayer))
            {
                CompleteAction(EnemyTurnActionType.Wait);
            }

            return;
        }

        if (!TrySelectSupportTarget(
                out EnemyController supportTarget,
                out EnemySupportType supportType))
        {
            if (!TryMoveAwayFromPlayer(directionToPlayer, distanceToPlayer))
            {
                CompleteAction(EnemyTurnActionType.Wait);
            }

            return;
        }

        if (!isQueueCreated)
        {
            CreateAttackQueue();
            return;
        }

        if (queuedAttackActions.Count == 0)
        {
            RegisterAction(EnemyActionType.Support, 0);
            return;
        }

        preparedSupportTarget = supportTarget;
        preparedSupportType = supportType;
        preparedTargetPosition = supportTarget.transform.position;
        PrepareCurrentAttackQueue();
    }

    private bool TryAppendAction(
        EnemyActionType actionType,
        int actionIndex,
        out Image attackIcon,
        out EnemyActionData appendedAction)
    {
        attackIcon = null;
        appendedAction = null;

        if (queuedAttackActions.Count >= enemyData.MaxQueuedAttacks)
        {
            return false;
        }

        EnemyActionData attackAction = GetAvailableAction(
            actionType,
            actionIndex,
            actionType == EnemyActionType.MeleeAttack
            || actionType == EnemyActionType.RangedAttack);

        if (attackAction == null
            || !actionQueueUI.AddAttackIcon(attackAction, out attackIcon))
        {
            return false;
        }

        queuedAttackActions.Add(attackAction);
        appendedAction = attackAction;
        return true;
    }

    private void CreateAttackQueue()
    {
        isQueueCreated = true;
        isAttackPrepared = false;
        StartCoroutine(RevealAttackQueue());
    }

    private void RegisterAction(
        EnemyActionType actionType,
        int actionIndex)
    {
        if (!isQueueCreated
            || !TryAppendAction(
                actionType,
                actionIndex,
                out Image attackIcon,
                out EnemyActionData appendedAction))
        {
            ClearAttackQueue();
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (enemyData.BehaviorType != EnemyBehaviorType.Melee
            && appendedAction.ActionType == EnemyActionType.RangedAttack)
        {
            StartCoroutine(PlayAvatarAttackAnimation());
        }

        StartCoroutine(RevealRegisteredAction(attackIcon));
    }

    private void ApplyInitialFacingDirection()
    {
        Vector3 localScale = transform.localScale;
        float scaleMagnitude = Mathf.Abs(localScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        localScale.x = scaleMagnitude * InitialFacingDirection;
        transform.localScale = localScale;
    }

    private IEnumerator RevealAttackQueue()
    {
        yield return actionQueueUI.RevealQueue(
            enemyData.QueueElementRevealDuration);
        CompleteAction(EnemyTurnActionType.CreateQueue);
    }

    private IEnumerator RevealRegisteredAction(Image attackIcon)
    {
        yield return actionQueueUI.RevealIcon(
            attackIcon,
            enemyData.QueueElementRevealDuration);
        CompleteAction(EnemyTurnActionType.RegisterAttack);
    }

    private void PrepareCurrentAttackQueue()
    {
        isAttackPrepared = true;
        actionQueueUI.SetPrepared(true);
        RefreshAttackTelegraph();
        CompleteAction(EnemyTurnActionType.PrepareAttack);
    }

    private void RefreshAttackTelegraph()
    {
        if (!isAttackPrepared || enemyData == null
            || enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            HideAttackTelegraph();
            return;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.BigBarrel)
        {
            if (bigBarrelStep == BigBarrelStep.ExecuteShotgun)
            {
                CreateBigBarrelShotgunTelegraphs();
            }

            return;
        }

        Material telegraphMaterial = enemyData.BehaviorType switch
        {
            EnemyBehaviorType.Thrower =>
                enemyData.ThrowerTelegraphMaterial,
            EnemyBehaviorType.Porter =>
                enemyData.SupportTelegraphMaterial,
            _ => enemyData.GunnerTelegraphMaterial
        };

        if (telegraphMaterial == null)
        {
            HideAttackTelegraph();
            return;
        }

        LineRenderer lineRenderer = GetOrCreateAttackTelegraphLine();
        lineRenderer.sharedMaterial = telegraphMaterial;
        lineRenderer.widthMultiplier = enemyData.TelegraphLineWidth;
        lineRenderer.sortingOrder = enemyData.TelegraphSortingOrder;
        Color telegraphColor = enemyData.BehaviorType
            == EnemyBehaviorType.Porter
                ? preparedSupportType == EnemySupportType.Heal
                    ? enemyData.SupportHealColor
                    : enemyData.SupportShieldColor
                : Color.white;
        lineRenderer.startColor = telegraphColor;
        lineRenderer.endColor = telegraphColor;

        if (enemyData.BehaviorType == EnemyBehaviorType.Porter)
        {
            ApplyLineShaderColor(lineRenderer, telegraphColor);
        }

        if (avatarSortingRenderer != null)
        {
            lineRenderer.sortingLayerID =
                avatarSortingRenderer.sortingLayerID;
        }

        bool positionsApplied = enemyData.BehaviorType switch
        {
            EnemyBehaviorType.Thrower =>
                ApplyThrowerTelegraphPositions(lineRenderer),
            EnemyBehaviorType.Porter =>
                ApplySupportTelegraphPositions(lineRenderer),
            _ => ApplyGunnerTelegraphPositions(lineRenderer)
        };
        lineRenderer.enabled = positionsApplied;
    }

    private LineRenderer GetOrCreateAttackTelegraphLine()
    {
        if (attackTelegraphLine != null)
        {
            return attackTelegraphLine;
        }

        GameObject telegraphObject = new GameObject(
            "Line | Attack Telegraph");
        telegraphObject.transform.SetParent(transform, false);
        attackTelegraphLine = telegraphObject.AddComponent<LineRenderer>();
        attackTelegraphLine.useWorldSpace = true;
        attackTelegraphLine.loop = false;
        attackTelegraphLine.alignment = LineAlignment.View;
        attackTelegraphLine.textureMode = LineTextureMode.Stretch;
        attackTelegraphLine.startColor = Color.white;
        attackTelegraphLine.endColor = Color.white;
        attackTelegraphLine.numCapVertices = 2;
        attackTelegraphLine.enabled = false;
        return attackTelegraphLine;
    }

    private bool ApplyGunnerTelegraphPositions(LineRenderer lineRenderer)
    {
        if (boardManager == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int attackerTileIndex))
        {
            return false;
        }

        int attackDirection = transform.localScale.x >= 0f ? 1 : -1;
        int endTileIndex = attackDirection > 0
            ? boardManager.BoardCount - 1
            : 0;

        if (endTileIndex == attackerTileIndex
            || !boardManager.TryGetTilePosition(
                endTileIndex,
                out Vector3 endPosition))
        {
            return false;
        }

        Vector3 startPosition = transform.position;
        float verticalOffset = enemyData.TelegraphVerticalOffset;
        startPosition.y += verticalOffset;
        endPosition.y = startPosition.y;
        endPosition.z = startPosition.z;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
        return true;
    }

    private bool ApplyThrowerTelegraphPositions(LineRenderer lineRenderer)
    {
        if (preparedTargetTileIndex < 0)
        {
            return false;
        }

        int segmentCount = enemyData.ThrowerTelegraphSegments;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = preparedTargetPosition;
        float verticalOffset = enemyData.TelegraphVerticalOffset;
        startPosition.y += verticalOffset;
        targetPosition.y += verticalOffset;
        targetPosition.z = startPosition.z;
        lineRenderer.positionCount = segmentCount;

        for (int segmentIndex = 0;
             segmentIndex < segmentCount;
             segmentIndex++)
        {
            float progress = segmentCount <= 1
                ? 1f
                : (float)segmentIndex / (segmentCount - 1);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);
            position += Vector3.up * (Mathf.Sin(progress * Mathf.PI)
                * enemyData.ThrownProjectileArcHeight);
            lineRenderer.SetPosition(segmentIndex, position);
        }

        return true;
    }

    private bool ApplySupportTelegraphPositions(LineRenderer lineRenderer)
    {
        if (preparedSupportTarget == null
            || preparedSupportTarget.CurrentHealth <= 0)
        {
            return false;
        }

        Vector3 startPosition = transform.position;
        Vector3 endPosition = preparedSupportTarget.transform.position;
        float verticalOffset = enemyData.TelegraphVerticalOffset;
        startPosition.y += verticalOffset;
        endPosition.y += verticalOffset;
        endPosition.z = startPosition.z;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
        return true;
    }

    private void HideAttackTelegraph()
    {
        if (attackTelegraphLine != null)
        {
            attackTelegraphLine.enabled = false;
        }

        foreach (LineRenderer line in bigBarrelTelegraphLines)
        {
            if (line != null)
            {
                line.gameObject.SetActive(false);
                Destroy(line.gameObject);
            }
        }

        bigBarrelTelegraphLines.Clear();
    }

    private void CreateBigBarrelShotgunTelegraphs()
    {
        ClearBigBarrelTelegraphsOnly();
        Color color = new Color(1f, 0.08f, 0.04f, 0.78f);

        foreach (int tileIndex in preparedShotgunTileIndices)
        {
            LineRenderer line = BoardTelegraphUtility.CreateTileRange(
                transform,
                "Line | Shotgun Target",
                boardManager,
                tileIndex,
                tileIndex,
                enemyData.BigBarrel.ShotgunTelegraphMaterial,
                color,
                enemyData.TelegraphVerticalOffset * 0.5f,
                enemyData.TelegraphSortingOrder);

            if (line != null)
            {
                bigBarrelTelegraphLines.Add(line);
            }
        }
    }

    private void ClearBigBarrelTelegraphsOnly()
    {
        foreach (LineRenderer line in bigBarrelTelegraphLines)
        {
            if (line != null)
            {
                line.gameObject.SetActive(false);
                Destroy(line.gameObject);
            }
        }

        bigBarrelTelegraphLines.Clear();
    }

    private void RefreshShieldIndicator(
        Material indicatorMaterial = null,
        Color? indicatorColor = null)
    {
        if (currentShield <= 0)
        {
            if (shieldIndicatorLine != null)
            {
                shieldIndicatorLine.enabled = false;
            }

            return;
        }

        if (shieldIndicatorLine == null)
        {
            GameObject indicatorObject = new GameObject(
                "Line | Shield Indicator");
            indicatorObject.transform.SetParent(transform, false);
            shieldIndicatorLine =
                indicatorObject.AddComponent<LineRenderer>();
            shieldIndicatorLine.useWorldSpace = false;
            shieldIndicatorLine.loop = true;
            shieldIndicatorLine.alignment = LineAlignment.View;
            shieldIndicatorLine.textureMode = LineTextureMode.Stretch;
            shieldIndicatorLine.numCapVertices = 2;
            shieldIndicatorLine.positionCount = 24;
            shieldIndicatorLine.widthMultiplier = 0.06f;
            shieldIndicatorLine.sortingOrder = enemyData == null
                ? 19
                : enemyData.TelegraphSortingOrder - 1;

            if (avatarSortingRenderer != null)
            {
                shieldIndicatorLine.sortingLayerID =
                    avatarSortingRenderer.sortingLayerID;
            }

            for (int pointIndex = 0;
                 pointIndex < shieldIndicatorLine.positionCount;
                 pointIndex++)
            {
                float radians = pointIndex
                    / (float)shieldIndicatorLine.positionCount
                    * Mathf.PI
                    * 2f;
                shieldIndicatorLine.SetPosition(
                    pointIndex,
                    new Vector3(
                        Mathf.Cos(radians) * 0.58f,
                        Mathf.Sin(radians) * 0.82f + 0.1f,
                        -0.05f));
            }
        }

        if (indicatorMaterial != null)
        {
            shieldIndicatorLine.sharedMaterial = indicatorMaterial;
        }

        Color color = indicatorColor
            ?? new Color(0.2f, 0.8f, 1f, 1f);
        shieldIndicatorLine.startColor = color;
        shieldIndicatorLine.endColor = color;
        ApplyLineShaderColor(shieldIndicatorLine, color);
        shieldIndicatorLine.enabled =
            shieldIndicatorLine.sharedMaterial != null;
    }

    private void ApplyLineShaderColor(
        LineRenderer lineRenderer,
        Color color)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineColorProperties ??= new MaterialPropertyBlock();
        lineColorProperties.Clear();
        Color baseColor = color;
        baseColor.a *= 0.55f;
        Color beamColor = color * 1.35f;
        beamColor.a = color.a;
        Color gridColor = color;
        gridColor.a *= 0.9f;
        lineColorProperties.SetColor(BaseColorId, baseColor);
        lineColorProperties.SetColor(BeamColorId, beamColor);
        lineColorProperties.SetColor(GridColorId, gridColor);
        lineRenderer.SetPropertyBlock(lineColorProperties);
    }

    private bool CanPrepareGunnerAttack(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (directionToPlayer == 0 || distanceToPlayer <= 0)
        {
            return false;
        }

        attackTargetBuffer.Clear();
        waveManager.GetEnemiesInDirection(
            transform.position,
            directionToPlayer,
            distanceToPlayer,
            attackTargetBuffer);
        return attackTargetBuffer.Count == 0;
    }

    private bool CaptureThrowerTargetTile()
    {
        if (!boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out preparedTargetTileIndex))
        {
            preparedTargetTileIndex = -1;
            preparedTargetPosition = Vector3.zero;
            return false;
        }

        preparedTargetPosition = playerMove.transform.position;
        return true;
    }

    private IEnumerator FireAttackQueue()
    {
        while (queuedAttackActions.Count > 0)
        {
            EnemyActionData attackAction = queuedAttackActions[0];
            queuedAttackActions.RemoveAt(0);
            yield return ExecuteQueuedAttack(attackAction);
            actionQueueUI.RemoveFirstIcon();

            if (queuedAttackActions.Count > 0)
            {
                yield return WaitForQueuedActionInterval();
            }
        }

        isRetreating = enemyData.BehaviorType == EnemyBehaviorType.Melee
            && enemyData.PreferredDistance > 0;
        recoveryTurnsRemaining = enemyData.BehaviorType
            == EnemyBehaviorType.Gunner
            || enemyData.BehaviorType == EnemyBehaviorType.Thrower
                ? enemyData.RecoveryTurns
                : 0;
        isQueueCreated = false;
        isAttackPrepared = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        preparedSupportTarget = null;
        preparedSupportType = EnemySupportType.None;
        actionQueueUI.ResetDisplay();
        CompleteAction(enemyData.BehaviorType == EnemyBehaviorType.Porter
            ? EnemyTurnActionType.Support
            : EnemyTurnActionType.Fire);
    }

    private IEnumerator ExecuteQueuedAttack(EnemyActionData attackAction)
    {
        if (enemyData.BehaviorType == EnemyBehaviorType.Porter
            && attackAction != null
            && attackAction.ActionType == EnemyActionType.Support)
        {
            ExecutePreparedSupport();
            yield break;
        }

        if (!TryGetAttackData(attackAction, out EnemyAttackData attackData))
        {
            yield break;
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Melee)
        {
            yield return PlayAvatarAttackAnimation();
        }

        if (enemyData.BehaviorType == EnemyBehaviorType.Thrower)
        {
            yield return ExecuteThrowerAttack(attackData);
            yield break;
        }

        if (!TryGetAttackTarget(
                attackData,
                out EnemyController enemyTarget,
                out bool targetsPlayer,
                out Vector3 targetPosition))
        {
            AttackExecuted?.Invoke(this, attackData);
            yield break;
        }

        if (attackData.AttackEffectPrefab != null)
        {
            Instantiate(
                attackData.AttackEffectPrefab,
                targetPosition,
                Quaternion.identity);
        }

        ApplyAttackToTarget(
            attackData,
            enemyTarget,
            targetsPlayer);
        AttackExecuted?.Invoke(this, attackData);
    }

    private bool TrySelectSupportTarget(
        out EnemyController selectedTarget,
        out EnemySupportType supportType)
    {
        selectedTarget = null;
        supportType = EnemySupportType.None;

        if (waveManager == null || playerMove == null)
        {
            return false;
        }

        float lowestHealthRatio = float.MaxValue;
        int bestHealPlayerDistance = int.MaxValue;
        int bestHealTileIndex = int.MaxValue;

        foreach (EnemyController candidate in waveManager.ActiveEnemies)
        {
            if (!IsValidSupportTarget(candidate)
                || candidate.CurrentHealth >= candidate.MaxHealth)
            {
                continue;
            }

            float healthRatio = candidate.MaxHealth <= 0
                ? 1f
                : (float)candidate.CurrentHealth / candidate.MaxHealth;

            if (healthRatio > enemyData.SupportHealThreshold)
            {
                continue;
            }

            GetSupportSortValues(
                candidate,
                out int playerDistance,
                out int tileIndex);

            if (healthRatio < lowestHealthRatio
                || Mathf.Approximately(healthRatio, lowestHealthRatio)
                && (playerDistance < bestHealPlayerDistance
                    || playerDistance == bestHealPlayerDistance
                    && tileIndex < bestHealTileIndex))
            {
                selectedTarget = candidate;
                lowestHealthRatio = healthRatio;
                bestHealPlayerDistance = playerDistance;
                bestHealTileIndex = tileIndex;
            }
        }

        if (selectedTarget != null)
        {
            supportType = EnemySupportType.Heal;
            return true;
        }

        int bestShieldPlayerDistance = int.MaxValue;
        int bestShieldTileIndex = int.MaxValue;

        foreach (EnemyController candidate in waveManager.ActiveEnemies)
        {
            if (!IsValidSupportTarget(candidate)
                || candidate.CurrentShield > 0)
            {
                continue;
            }

            GetSupportSortValues(
                candidate,
                out int playerDistance,
                out int tileIndex);

            if (playerDistance < bestShieldPlayerDistance
                || playerDistance == bestShieldPlayerDistance
                && tileIndex < bestShieldTileIndex)
            {
                selectedTarget = candidate;
                bestShieldPlayerDistance = playerDistance;
                bestShieldTileIndex = tileIndex;
            }
        }

        if (selectedTarget == null)
        {
            return false;
        }

        supportType = EnemySupportType.Shield;
        return true;
    }

    private bool IsValidSupportTarget(EnemyController candidate)
    {
        return candidate != null
            && candidate != this
            && candidate.CurrentHealth > 0;
    }

    private void GetSupportSortValues(
        EnemyController candidate,
        out int playerDistance,
        out int tileIndex)
    {
        playerDistance = int.MaxValue;
        tileIndex = int.MaxValue;

        if (boardManager == null || candidate == null)
        {
            return;
        }

        if (boardManager.TryGetTileDistance(
                candidate.transform.position,
                playerMove.transform.position,
                out int measuredPlayerDistance))
        {
            playerDistance = measuredPlayerDistance;
        }

        if (boardManager.TryGetTileIndex(
                candidate.transform.position,
                out int measuredTileIndex))
        {
            tileIndex = measuredTileIndex;
        }
    }

    private void ExecutePreparedSupport()
    {
        if (remainingSupportCharges <= 0
            || preparedSupportTarget == null
            || preparedSupportTarget.CurrentHealth <= 0)
        {
            return;
        }

        bool applied = preparedSupportType switch
        {
            EnemySupportType.Heal =>
                preparedSupportTarget.Heal(enemyData.SupportHealAmount),
            EnemySupportType.Shield =>
                preparedSupportTarget.CurrentShield <= 0
                && preparedSupportTarget.AddShield(
                    enemyData.SupportShieldAmount,
                    enemyData.SupportTelegraphMaterial,
                    enemyData.SupportShieldColor),
            _ => false
        };

        if (applied)
        {
            remainingSupportCharges--;
        }
    }

    private IEnumerator ExecuteThrowerAttack(EnemyAttackData attackData)
    {
        if (preparedTargetTileIndex < 0)
        {
            AttackExecuted?.Invoke(this, attackData);
            yield break;
        }

        yield return PlayThrownProjectile(preparedTargetPosition);

        if (attackData.AttackEffectPrefab != null)
        {
            Instantiate(
                attackData.AttackEffectPrefab,
                preparedTargetPosition,
                Quaternion.identity);
        }

        bool targetsPlayer = boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex)
            && playerTileIndex == preparedTargetTileIndex;
        EnemyController enemyTarget = null;

        if (!targetsPlayer)
        {
            waveManager.TryGetEnemyAtTile(
                preparedTargetTileIndex,
                out enemyTarget,
                this);
        }

        ApplyAttackToTarget(
            attackData,
            enemyTarget,
            targetsPlayer);
        AttackExecuted?.Invoke(this, attackData);
    }

    private IEnumerator PlayThrownProjectile(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float duration = enemyData.ThrownProjectileDuration;
        float arcHeight = enemyData.ThrownProjectileArcHeight;
        GameObject projectile = enemyData.ThrownProjectilePrefab == null
            ? CreateDefaultThrownProjectile(startPosition)
            : Instantiate(
                enemyData.ThrownProjectilePrefab,
                startPosition,
                Quaternion.identity);

        if (duration <= 0f)
        {
            if (projectile != null)
            {
                projectile.transform.position = targetPosition;
                Destroy(projectile);
            }

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            yield return null;

            if (GamePauseController.IsPaused)
            {
                continue;
            }

            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);
            position += Vector3.up
                * (Mathf.Sin(progress * Mathf.PI) * arcHeight);

            if (projectile != null)
            {
                projectile.transform.position = position;
            }
        }

        if (projectile != null)
        {
            projectile.transform.position = targetPosition;
            Destroy(projectile);
        }
    }

    private GameObject CreateDefaultThrownProjectile(Vector3 position)
    {
        GameObject projectile = new GameObject("Sprite | Thrown Projectile");
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one
            * enemyData.ThrownProjectileSize;
        SpriteRenderer projectileRenderer =
            projectile.AddComponent<SpriteRenderer>();
        projectileRenderer.sprite = enemyData.ThrownProjectileSprite != null
            ? enemyData.ThrownProjectileSprite
            : GetDefaultThrownProjectileSprite();
        projectileRenderer.color = enemyData.ThrownProjectileColor;

        if (avatarSortingRenderer != null)
        {
            projectileRenderer.sortingLayerID =
                avatarSortingRenderer.sortingLayerID;
            projectileRenderer.sortingOrder =
                GetHighestAvatarSortingOrder() + 1;
        }

        return projectile;
    }

    private static Sprite GetDefaultThrownProjectileSprite()
    {
        if (defaultThrownProjectileSprite != null)
        {
            return defaultThrownProjectileSprite;
        }

        int resolution = DefaultProjectileResolution;
        Texture2D texture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false);
        texture.name = "Runtime Default Thrown Projectile";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[resolution * resolution];
        Vector2 center = Vector2.one * ((resolution - 1) * 0.5f);
        float radius = resolution * 0.46f;
        float edgeWidth = 1.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.SmoothStep(
                    radius - edgeWidth,
                    radius,
                    distance);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        defaultThrownProjectileSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            resolution);
        defaultThrownProjectileSprite.name =
            "Runtime Default Thrown Projectile";
        defaultThrownProjectileSprite.hideFlags = HideFlags.HideAndDontSave;
        return defaultThrownProjectileSprite;
    }

    private void ApplyAttackToTarget(
        EnemyAttackData attackData,
        EnemyController enemyTarget,
        bool targetsPlayer)
    {
        int attackDamage = statusEffects == null
            ? attackData.Damage
            : statusEffects.ModifyOutgoingAttackDamage(
                attackData.Damage);

        if (targetsPlayer)
        {
            playerHealth.ApplyDamage(attackDamage);

            if (!playerHealth.IsDefeated)
            {
                ApplyAttackStatusEffects(playerHealth, attackData);
            }
        }
        else if (enemyTarget != null)
        {
            enemyTarget.ApplyAttackDamage(attackDamage);

            if (enemyTarget != null && enemyTarget.CurrentHealth > 0)
            {
                ApplyAttackStatusEffects(enemyTarget, attackData);
            }
        }
    }

    private bool TryGetAttackTarget(
        EnemyAttackData attackData,
        out EnemyController enemyTarget,
        out bool targetsPlayer,
        out Vector3 targetPosition)
    {
        enemyTarget = null;
        targetsPlayer = false;
        targetPosition = Vector3.zero;

        if (attackData == null || boardManager == null
            || playerMove == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int attackerIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        int attackDirection = transform.localScale.x >= 0f ? 1 : -1;
        int attackRange = enemyData.BehaviorType == EnemyBehaviorType.Melee
            ? attackData.Range
            : boardManager.BoardCount;
        int playerOffset = playerIndex - attackerIndex;
        int distanceToPlayer = Mathf.Abs(playerOffset);
        bool playerInAttackLine = playerOffset * attackDirection > 0
            && distanceToPlayer <= attackRange;

        attackTargetBuffer.Clear();
        waveManager.GetEnemiesInDirection(
            transform.position,
            attackDirection,
            attackRange,
            attackTargetBuffer);

        EnemyController closestEnemy = attackTargetBuffer.Count == 0
            ? null
            : attackTargetBuffer[0];
        int closestEnemyDistance = int.MaxValue;

        if (closestEnemy != null
            && boardManager.TryGetTileDistance(
                transform.position,
                closestEnemy.transform.position,
                out int measuredEnemyDistance))
        {
            closestEnemyDistance = measuredEnemyDistance;
        }

        if (closestEnemy != null
            && (!playerInAttackLine
                || closestEnemyDistance < distanceToPlayer))
        {
            if (enemyData.BehaviorType == EnemyBehaviorType.Gunner)
            {
                return false;
            }

            enemyTarget = closestEnemy;
            targetPosition = closestEnemy.transform.position;
            return true;
        }

        if (!playerInAttackLine)
        {
            return false;
        }

        targetsPlayer = true;
        targetPosition = playerMove.transform.position;
        return true;
    }

    private static void ApplyAttackStatusEffects(
        PlayerHealth target,
        EnemyAttackData attackData)
    {
        target.AddStatusEffect(
            StatusEffectType.Mark,
            attackData.MarkDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Poison,
            attackData.PoisonStackCount);
        target.AddStatusEffect(
            StatusEffectType.Stun,
            attackData.StunDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Weakness,
            attackData.WeaknessDurationTurns);
    }

    private static void ApplyAttackStatusEffects(
        EnemyController target,
        EnemyAttackData attackData)
    {
        target.AddStatusEffect(
            StatusEffectType.Mark,
            attackData.MarkDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Poison,
            attackData.PoisonStackCount);
        target.AddStatusEffect(
            StatusEffectType.Stun,
            attackData.StunDurationTurns);
        target.AddStatusEffect(
            StatusEffectType.Weakness,
            attackData.WeaknessDurationTurns);
    }

    private IEnumerator WaitForQueuedActionInterval()
    {
        float elapsedTime = 0f;

        while (elapsedTime < enemyData.QueuedActionInterval)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }
    }

    private int GetAvailableAttackCount(EnemyActionType attackActionType)
    {
        int count = 0;

        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData != null
                && actionData.ActionType == attackActionType
                && TryGetAttackData(actionData, out _))
            {
                count++;
            }
        }

        return count;
    }

    private EnemyActionData GetAvailableAction(
        EnemyActionType actionType,
        int actionIndex,
        bool requiresAttackData)
    {
        int currentIndex = 0;

        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData == null
                || actionData.ActionType != actionType
                || requiresAttackData
                && !TryGetAttackData(actionData, out _))
            {
                continue;
            }

            if (currentIndex == actionIndex)
            {
                return actionData;
            }

            currentIndex++;
        }

        return null;
    }

    private void ClearAttackQueue()
    {
        queuedAttackActions.Clear();
        isQueueCreated = false;
        isAttackPrepared = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        preparedSupportTarget = null;
        preparedSupportType = EnemySupportType.None;
        HideAttackTelegraph();

        if (actionQueueUI != null)
        {
            actionQueueUI.ResetDisplay();
        }
    }

    private void RotateToward(int directionToPlayer)
    {
        StartCoroutine(RotateRoutine(directionToPlayer));
    }

    private void MoveTowardPlayer(int directionToPlayer)
    {
        EnemyActionData approachAction = FindAction(EnemyActionType.Approach);

        if (directionToPlayer == 0 || approachAction == null
            || approachAction.MovementDistance <= 0)
        {
            CompleteAction(EnemyTurnActionType.Wait);
            return;
        }

        if (TryBuildMovePath(
                directionToPlayer,
                approachAction.MovementDistance,
                out Vector3[] path))
        {
            StartCoroutine(MoveRoutine(path, false));
        }
        else
        {
            CompleteAction(EnemyTurnActionType.Wait);
        }
    }

    private bool TryMoveAwayFromPlayer(
        int directionToPlayer,
        int distanceToPlayer)
    {
        if (directionToPlayer == 0)
        {
            return false;
        }

        EnemyActionData retreatAction = FindAction(EnemyActionType.Retreat);
        int movementDistance = retreatAction != null
            && retreatAction.MovementDistance > 0
                ? retreatAction.MovementDistance
                : 1;
        int distanceToPreferred = enemyData.PreferredDistance - distanceToPlayer;
        movementDistance = Mathf.Min(movementDistance, distanceToPreferred);

        if (!TryBuildMovePath(
                -directionToPlayer,
                movementDistance,
                out Vector3[] path))
        {
            return false;
        }

        StartCoroutine(MoveRoutine(path, true));
        return true;
    }

    private bool TryBuildMovePath(
        int direction,
        int movementDistance,
        out Vector3[] path)
    {
        movePath.Clear();
        Vector3 currentPosition = transform.position;

        for (int step = 0; step < movementDistance; step++)
        {
            if (!boardManager.TryGetAdjacentTilePosition(
                    currentPosition,
                    direction,
                    out Vector3 targetPosition)
                || !boardManager.TryGetTileIndex(targetPosition, out int targetIndex)
                || !boardManager.TryGetTileIndex(
                    playerMove.transform.position,
                    out int playerIndex)
                || targetIndex == playerIndex
                || waveManager.IsTileOccupied(targetIndex, this)
                || waveManager.IsTileReservedForSpawn(targetIndex))
            {
                break;
            }

            currentPosition = targetPosition;
            movePath.Add(targetPosition);
        }

        path = movePath.ToArray();
        return path.Length > 0;
    }

    private IEnumerator MoveRoutine(Vector3[] path, bool updateRetreatState)
    {
        yield return actorMotion.MoveAlongPath(path);

        if (updateRetreatState
            && boardManager.TryGetTileDistance(
                transform.position,
                playerMove.transform.position,
                out int updatedDistanceToPlayer)
            && updatedDistanceToPlayer >= enemyData.PreferredDistance)
        {
            isRetreating = false;
        }

        CompleteAction(EnemyTurnActionType.Move);
    }

    private IEnumerator RotateRoutine(int directionToPlayer)
    {
        yield return actorMotion.RotateToDirection(directionToPlayer);
        ApplyCanvasOrientation();
        CompleteAction(EnemyTurnActionType.Rotate);
    }

    private bool TryGetTurnContext(
        out int directionToPlayer,
        out int distanceToPlayer)
    {
        directionToPlayer = 0;
        distanceToPlayer = 0;

        if (!boardManager.TryGetTileIndex(transform.position, out int enemyIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerIndex))
        {
            return false;
        }

        directionToPlayer = playerIndex > enemyIndex
            ? 1
            : playerIndex < enemyIndex ? -1 : 0;
        distanceToPlayer = Mathf.Abs(playerIndex - enemyIndex);
        return true;
    }

    private bool CanTakeFrontlineTurn()
    {
        if (enemyData == null
            || enemyData.BehaviorType != EnemyBehaviorType.Melee
            && enemyData.BehaviorType != EnemyBehaviorType.Gunner)
        {
            return true;
        }

        if (boardManager == null || playerMove == null || waveManager == null
            || !boardManager.TryGetTileIndex(
                transform.position,
                out int selfTileIndex)
            || !boardManager.TryGetTileIndex(
                playerMove.transform.position,
                out int playerTileIndex))
        {
            return false;
        }

        int selfOffset = selfTileIndex - playerTileIndex;

        if (selfOffset == 0)
        {
            return true;
        }

        foreach (EnemyController otherEnemy in waveManager.ActiveEnemies)
        {
            if (otherEnemy == null || otherEnemy == this
                || otherEnemy.CurrentHealth <= 0
                || !boardManager.TryGetTileIndex(
                    otherEnemy.transform.position,
                    out int otherTileIndex))
            {
                continue;
            }

            int otherOffset = otherTileIndex - playerTileIndex;
            bool isOnSameSide = selfOffset * otherOffset > 0;
            bool isCloserToPlayer =
                Mathf.Abs(otherOffset) < Mathf.Abs(selfOffset);

            if (isOnSameSide && isCloserToPlayer)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsFacing(int direction)
    {
        int facingDirection = transform.localScale.x >= 0f ? 1 : -1;
        return facingDirection == direction;
    }

    private EnemyActionData FindAction(EnemyActionType actionType)
    {
        foreach (EnemyActionData actionData in enemyData.Actions)
        {
            if (actionData != null && actionData.ActionType == actionType)
            {
                return actionData;
            }
        }

        return null;
    }

    private bool TryGetAttackData(
        EnemyActionData attackAction,
        out EnemyAttackData attackData)
    {
        attackData = attackAction == null ? null : attackAction.AttackData;
        return attackData != null && attackData.Range >= 0;
    }

    private void ResetRuntimeState()
    {
        HideAttackTelegraph();
        currentHealth = enemyData == null ? 0 : enemyData.MaxHealth;
        currentShield = 0;
        RefreshShieldIndicator();
        remainingSupportCharges = enemyData == null
            ? 0
            : enemyData.MaxSupportCharges;
        recoveryTurnsRemaining = 0;
        queuedAttackActions.Clear();
        isQueueCreated = false;
        isAttackPrepared = false;
        isRetreating = false;
        preparedTargetTileIndex = -1;
        preparedTargetPosition = Vector3.zero;
        preparedSupportTarget = null;
        preparedSupportType = EnemySupportType.None;
        bigBarrelStep = BigBarrelStep.RotateToPlayer;
        isBigBarrelPhaseTwo = false;
        bigBarrelActionUsesPhaseTwo = false;
        preparedBigBarrelFuse = 0;
        bigBarrelReloadTurnsRemaining = 0;
        preparedBombTargetTileIndices.Clear();
        preparedShotgunTileIndices.Clear();
        lastTurnAction = EnemyTurnActionType.None;
        isActing = false;

        if (statusEffects != null)
        {
            statusEffects.Clear();
        }

        if (actionQueueUI != null)
        {
            actionQueueUI.ResetDisplay();
        }

        RefreshHealthUI();
    }

    private void SpawnAvatar()
    {
        avatarAnimationSequence++;

        if (avatarInstance != null)
        {
            Destroy(avatarInstance);
        }

        avatarInstance = null;
        avatarAnimator = null;
        avatarSortingRenderer = null;

        if (enemyData == null || enemyData.Avatar == null)
        {
            Debug.LogWarning(
                $"EnemyData '{(enemyData == null ? "None" : enemyData.name)}' has no Avatar prefab.",
                this);
            return;
        }

        avatarInstance = Instantiate(enemyData.Avatar);
        avatarInstance.name = enemyData.Avatar.name;
        avatarInstance.transform.SetParent(transform, false);
        avatarInstance.transform.localPosition = Vector3.zero;
        avatarInstance.transform.localRotation = Quaternion.identity;
        avatarAnimator = avatarInstance.GetComponentInChildren<Animator>(true);
        avatarSortingRenderer =
            avatarInstance.GetComponentInChildren<SpriteRenderer>(true);

        if (avatarAnimator == null)
        {
            Debug.LogWarning(
                $"Enemy Avatar '{enemyData.Avatar.name}' has no Animator.",
                avatarInstance);
        }
        else if (avatarAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                $"Enemy Avatar '{enemyData.Avatar.name}' has no Animator Controller.",
                avatarAnimator);
        }
        else
        {
            if (!avatarAnimator.HasState(0, IdleAnimationStateHash))
            {
                Debug.LogWarning(
                    $"Enemy Avatar '{enemyData.Avatar.name}' has no Base Layer.Idle state.",
                    avatarAnimator);
            }

            if (!avatarAnimator.HasState(0, AttackAnimationStateHash))
            {
                Debug.LogWarning(
                    $"Enemy Avatar '{enemyData.Avatar.name}' has no Base Layer.Attack state.",
                    avatarAnimator);
            }
        }

        PlayAvatarIdle();
    }

    private IEnumerator PlayAvatarAttackAnimation()
    {
        if (avatarAnimator == null
            || avatarAnimator.runtimeAnimatorController == null
            || !avatarAnimator.HasState(0, AttackAnimationStateHash))
        {
            yield break;
        }

        int animationSequence = ++avatarAnimationSequence;
        avatarAnimator.Play(AttackAnimationStateHash, 0, 0f);
        avatarAnimator.Update(0f);
        AnimatorStateInfo attackState =
            avatarAnimator.GetCurrentAnimatorStateInfo(0);
        float duration = Mathf.Max(
            0f,
            attackState.length / Mathf.Max(0.01f, avatarAnimator.speed));
        float elapsedTime = 0f;

        while (elapsedTime < duration && avatarAnimator != null
            && animationSequence == avatarAnimationSequence)
        {
            yield return null;

            if (!GamePauseController.IsPaused)
            {
                elapsedTime += Time.deltaTime;
            }
        }

        if (animationSequence == avatarAnimationSequence)
        {
            PlayAvatarIdle();
        }
    }

    private void PlayAvatarIdle()
    {
        if (avatarAnimator == null
            || avatarAnimator.runtimeAnimatorController == null
            || !avatarAnimator.HasState(0, IdleAnimationStateHash))
        {
            return;
        }

        avatarAnimator.Play(IdleAnimationStateHash, 0, 0f);
        avatarAnimator.Update(0f);
    }

    private int GetHighestAvatarSortingOrder()
    {
        int highestSortingOrder = avatarSortingRenderer == null
            ? 0
            : avatarSortingRenderer.sortingOrder;

        if (avatarInstance == null)
        {
            return highestSortingOrder;
        }

        foreach (SpriteRenderer renderer in
                 avatarInstance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            highestSortingOrder = Mathf.Max(
                highestSortingOrder,
                renderer.sortingOrder);
        }

        return highestSortingOrder;
    }

    private void RefreshHealthUI(
        bool playDamageFeedback = false,
        bool isCritical = false,
        float impactStrength = 1f)
    {
        if (healthFillImage == null)
        {
            return;
        }

        int maxHealth = enemyData == null ? 0 : enemyData.MaxHealth;
        float normalizedHealth = maxHealth <= 0
            ? 0f
            : (float)currentHealth / maxHealth;

        if (healthBarFeedback == null)
        {
            healthFillImage.fillAmount = normalizedHealth;
            return;
        }

        if (playDamageFeedback)
        {
            healthBarFeedback.PlayDamage(
                normalizedHealth,
                isCritical,
                impactStrength);
            return;
        }

        healthBarFeedback.SetValueImmediate(normalizedHealth);
    }

    private void ApplyCanvasOrientation()
    {
        if (actorMotion != null)
        {
            actorMotion.RefreshOrientationLock();
            return;
        }

        if (canvasTransform == null)
        {
            return;
        }

        Vector3 canvasScale = canvasTransform.localScale;
        float scaleMagnitude = Mathf.Abs(canvasScale.x);

        if (scaleMagnitude <= Mathf.Epsilon)
        {
            scaleMagnitude = 1f;
        }

        float enemyDirection = transform.localScale.x >= 0f ? 1f : -1f;
        canvasScale.x = scaleMagnitude * enemyDirection;
        canvasTransform.localScale = canvasScale;
    }

    private void CompleteAction(EnemyTurnActionType actionType)
    {
        if (statusEffects != null && currentHealth > 0)
        {
            statusEffects.ProcessTurnEnd();
        }

        lastTurnAction = actionType;
        isActing = false;
        TurnActionCompleted?.Invoke(this, actionType);
    }
}
