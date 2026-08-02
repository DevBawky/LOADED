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
    private const float BulletFeedbackStartAlpha = 0.2f;

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
    [Min(0f)]
    [SerializeField] private float shotInterval = 0.2f;

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
        currencyManager ??= FindFirstObjectByType<CurrencyManager>();
        combatPresentation ??= GetComponent<CombatPresentation>();

        if (combatPresentation == null)
        {
            combatPresentation = gameObject.AddComponent<CombatPresentation>();
        }

        if (playerMove != null)
        {
            playerMove.SetShooting(false);
        }

        ResetBulletFeedback();
        ResetCameraRecoil();
        currentConsumedBullet = null;
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

        ResetBulletFeedback();
        ResetCameraRecoil();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused || isFiring)
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
        if (GamePauseController.IsPaused || isFiring
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

        StartCoroutine(ShootLoadedBullets(horizontalDirection));
    }

    private IEnumerator ShootLoadedBullets(int horizontalDirection)
    {
        isFiring = true;
        playerMove.SetShooting(true);
        bool firedAnyBullet = false;
        bool consumesTurn = false;
        BulletInstance previousResolvedBullet = null;
        float stackedDamageBonus = 0f;
        initialLoadedBulletCount = deckManager.LoadedBullets.Count;
        bulletsFiredThisCylinder = 0;
        criticalShotsThisCylinder = 0;
        bulletDestroyedThisCylinder = false;
        pendingSaverGold = 0;
        bool quickDrawActive = initialLoadedBulletCount <= 3
            && ContainsLoadedEffect(BulletEffectType.QuickDraw);

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

            if (!deckManager.TryFireLoadedBullet(out BulletInstance firedBullet)
                || firedBullet != bulletData)
            {
                break;
            }

            firedAnyBullet = true;
            currentConsumedBullet = firedBullet;

            BulletInstance resolvedBullet = ResolveShotBullet(
                bulletData,
                previousResolvedBullet);
            consumesTurn |= !quickDrawActive
                && !resolvedBullet.DoesNotConsumeTurn;
            BulletEffectData powderEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.PowderPouch);

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
                    firedBullet.AddStoredDamageBonus(stackedDamageBonus);
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
            }

            bulletsFiredThisCylinder++;
            previousResolvedBullet = resolvedBullet;
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

        if (firedAnyBullet && consumesTurn)
        {
            playerMove.CompleteTurn();
        }

        isFiring = false;
        playerMove.SetShooting(false);
        currentConsumedBullet = null;
    }

    private IEnumerator FireSingleShot(
        BulletInstance bulletData,
        int horizontalDirection,
        float damageMultiplier,
        float criticalChanceBonus,
        bool generatesShells,
        Action<bool> onCompleted)
    {
        if (bulletData == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        waveManager.GetEnemiesInDirection(
            transform.position,
            horizontalDirection,
            bulletData.MaxRange,
            targetBuffer);

        Vector3 endPoint;

        if (targetBuffer.Count > 0)
        {
            BuildHitTargets(bulletData);
            endPoint = hitBuffer[hitBuffer.Count - 1].transform.position;
        }
        else
        {
            hitBuffer.Clear();
            endPoint = GetMissEndPoint(horizontalDirection, bulletData.MaxRange);
        }

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
            Destroy(bulletLine.gameObject);
            onCompleted?.Invoke(false);
            yield break;
        }

        bool isCritical = bulletData.CanTriggerCritical(
            UnityEngine.Random.Range(0f, 100f),
            criticalChanceBonus);
        ShowBulletFeedback(bulletData);
        GenerateRecoil(bulletData);
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
        HandleShotResult(bulletData, isCritical, generatesShells);
        onCompleted?.Invoke(true);
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
                bulletsFiredThisCylinder,
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

        return multiplier;
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
            firedBullet.SetAbilityStacks(
                Mathf.CeilToInt(firedBullet.AbilityStacks * 0.5f));
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
                stateOwner.AddAbilityStacks(1);
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

    private bool ContainsLoadedEffect(BulletEffectType effectType)
    {
        foreach (BulletInstance loadedBullet in deckManager.LoadedBullets)
        {
            if (FindSpecialEffect(loadedBullet, effectType) != null)
            {
                return true;
            }
        }

        return false;
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
                    isCritical,
                    true);
                yield return ApplyConditionalEvents(
                    bulletData,
                    BulletConditionalTrigger.EnemyDefeated,
                    enemy,
                    horizontalDirection,
                    0);
                GrantDevourerStack(bulletData);
                continue;
            }

            int appliedDamage = enemy.ApplyAttackDamage(
                attackDamage,
                isCritical);
            bool defeatedByAttack = appliedDamage >= healthBeforeHit;
            combatPresentation?.PlayImpact(
                enemySnapshot,
                horizontalDirection,
                bulletData,
                isCritical,
                defeatedByAttack);
            defeatPresented = defeatedByAttack;
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
                result => defeatedByManagedEffect = result);

            if (enemy == null || enemy.CurrentHealth <= 0)
            {
                if (!defeatPresented)
                {
                    combatPresentation?.PlayImpact(
                        enemySnapshot,
                        horizontalDirection,
                        bulletData,
                        isCritical,
                        true);
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
                    (long)poisonStacks * (poisonStacks + 1) / 2;
                int poisonDamage = (int)Math.Min(
                    int.MaxValue,
                    remainingPoisonDamage);
                int healthBeforePoison = enemy.CurrentHealth;
                enemy.ApplyStatusDamage(poisonDamage);
                defeated = poisonDamage >= healthBeforePoison;
            }
        }

        onCompleted?.Invoke(defeated);
        yield break;
    }

    private void GrantDevourerStack(BulletInstance resolvedBullet)
    {
        if (FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Devourer) != null)
        {
            (currentConsumedBullet ?? resolvedBullet)?.AddPermanentStacks(1);
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
                    effect.StackCount);
                break;
            case BulletEffectType.Stun:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Stun,
                    effect.StackCount);
                break;
            case BulletEffectType.Mark:
                applied = enemy.AddStatusEffect(
                    StatusEffectType.Mark,
                    effect.StackCount);
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
                    effect.StackCount);
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
            || effectType == BulletEffectType.Legacy;
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
            if (ownedBullet != null && ownedBullet != destroyedBullet
                && FindSpecialEffect(
                    ownedBullet,
                    BulletEffectType.Legacy) != null)
            {
                ownedBullet.AddPermanentStacks(1);
            }
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
