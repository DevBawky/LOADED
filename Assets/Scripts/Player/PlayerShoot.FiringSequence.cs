using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerShoot
{
    internal static bool ShouldWaitBeforeAdditionalShot(
        bool hasRequiredShotgunShot,
        float interval)
    {
        return !hasRequiredShotgunShot && interval > 0f;
    }

    private sealed class FiringSequenceController
    {
        private readonly struct ReplayShot
        {
            public ReplayShot(
                BulletInstance bullet,
                int direction,
                float damageMultiplier,
                float criticalChanceBonus,
                float criticalDamageMultiplierBonus)
            {
                Bullet = bullet;
                Direction = direction;
                DamageMultiplier = damageMultiplier;
                CriticalChanceBonus = criticalChanceBonus;
                CriticalDamageMultiplierBonus =
                    criticalDamageMultiplierBonus;
            }

            public BulletInstance Bullet { get; }
            public int Direction { get; }
            public float DamageMultiplier { get; }
            public float CriticalChanceBonus { get; }
            public float CriticalDamageMultiplierBonus { get; }
        }

        private readonly PlayerShoot owner;
        private readonly HashSet<int> enemiesHitThisTurn = new HashSet<int>();
        private readonly List<ReplayShot> replayShots = new List<ReplayShot>();
        private float activeCriticalDamageMultiplierBonus;

        private DeckManager deckManager => owner.deckManager;
        private CurrencyManager currencyManager
        {
            get => owner.currencyManager;
            set => owner.currencyManager = value;
        }
        private PlayerMove playerMove => owner.playerMove;
        private PlayerHealth playerHealth => owner.playerHealth;
        private BoardManager boardManager => owner.boardManager;
        private WaveManager waveManager => owner.waveManager;
        private Transform firePoint => owner.firePoint;
        private BulletLine bulletLinePrefab => owner.bulletLinePrefab;
        private CombatPresentation combatPresentation =>
            owner.combatPresentation;
        private CombatFeedbackController combatFeedback =>
            owner.combatFeedback;
        private Transform transform => owner.transform;
        private float shotInterval => owner.shotInterval;
        private List<EnemyController> targetBuffer => owner.targetBuffer;
        private List<EnemyController> hitBuffer => owner.hitBuffer;
        private List<BulletInstance> ownedBulletBuffer =>
            owner.ownedBulletBuffer;
        private HashSet<BulletData> ownedBulletTypeBuffer =>
            owner.ownedBulletTypeBuffer;
        private Dictionary<EnemyController, int> reservedDamageByEnemy =>
            owner.reservedDamageByEnemy;
        private Dictionary<EnemyController, ManagedEffectDefeatResult>
            pendingEffectDefeats => owner.pendingEffectDefeats;
        private int[] ownedGradeCountBuffer => owner.ownedGradeCountBuffer;

        private RelicManager relicManager
        {
            get => owner.relicManager;
            set => owner.relicManager = value;
        }

        private BulletInstance currentConsumedBullet
        {
            get => owner.currentConsumedBullet;
            set => owner.currentConsumedBullet = value;
        }

        private int initialLoadedBulletCount
        {
            get => owner.initialLoadedBulletCount;
            set => owner.initialLoadedBulletCount = value;
        }

        private int bulletsFiredThisCylinder
        {
            get => owner.bulletsFiredThisCylinder;
            set => owner.bulletsFiredThisCylinder = value;
        }

        private int criticalShotsThisCylinder
        {
            get => owner.criticalShotsThisCylinder;
            set => owner.criticalShotsThisCylinder = value;
        }

        private int activeShotIndex
        {
            get => owner.activeShotIndex;
            set => owner.activeShotIndex = value;
        }

        private bool bulletDestroyedThisCylinder
        {
            get => owner.bulletDestroyedThisCylinder;
            set => owner.bulletDestroyedThisCylinder = value;
        }

        private int pendingSaverGold
        {
            get => owner.pendingSaverGold;
            set => owner.pendingSaverGold = value;
        }

        private bool isFiring
        {
            get => owner.isFiring;
            set => owner.isFiring = value;
        }

        public FiringSequenceController(PlayerShoot owner)
        {
            this.owner = owner;
        }

        public IEnumerator Execute(int horizontalDirection)
        {
            return ShootLoadedBullets(horizontalDirection);
        }

        public void ResetTurnTargetHistory()
        {
            enemiesHitThisTurn.Clear();
        }

        public bool WasEnemyHitThisTurn(EnemyController enemy)
        {
            return enemy != null
                && enemiesHitThisTurn.Contains(enemy.GetInstanceID());
        }

        public void RecordPlayerMovement(PlayerMovementContext context)
        {
            if (deckManager == null || context.Distance <= 0)
            {
                return;
            }

            deckManager.GetOwnedBullets(ownedBulletBuffer);
            List<BulletInstance> observedBullets =
                new List<BulletInstance>(ownedBulletBuffer);

            foreach (BulletInstance bullet in observedBullets)
            {
                if (bullet == null)
                {
                    continue;
                }

                if (FindSpecialEffect(
                        bullet,
                        BulletEffectType.Seismometer) != null)
                {
                    bullet.AddAbilityStacks(context.Distance);
                }

                if ((context.Source & (PlayerMovementSource.BulletPositionSwap
                        | PlayerMovementSource.ForcedMove)) != 0
                    && FindSpecialEffect(
                        bullet,
                        BulletEffectType.Tracking) != null)
                {
                    bullet.AddAbilityStacks(1);
                }
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
            float spreadDamageBonus = 0f;
            float concentrationCriticalChanceBonus = 0f;
            float pendingCriticalDamageMultiplierBonus = 0f;
            float finaleExtraShotChance = GetLoadedFinaleExtraShotChance();
            replayShots.Clear();
            activeCriticalDamageMultiplierBonus = 0f;
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
            int initialShotDirection = BulletEffectUtility.ResolveShotDirection(
                initialResolvedBullet,
                horizontalDirection);
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
                    initialShotDirection);
    
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
                int shotDirection = BulletEffectUtility.ResolveShotDirection(
                    resolvedBullet,
                    horizontalDirection);
                BulletEffectData powderEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.PowderPouch);
                bool hasViableTarget = fireIntoAir
                    || (powderEffect == null
                        ? HasViableShotTarget(
                            resolvedBullet,
                            shotDirection)
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
                    damageMultiplier *= 1f + spreadDamageBonus;
                    activeCriticalDamageMultiplierBonus =
                        pendingCriticalDamageMultiplierBonus;
                    pendingCriticalDamageMultiplierBonus = 0f;
                    float shotCriticalDamageMultiplierBonus =
                        activeCriticalDamageMultiplierBonus;
                    ApplyFleshForBoneCost(resolvedBullet);
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
                    bool primerReusePending = false;
                    float criticalChanceBonus =
                        firedBullet.ConsumeTemporaryCriticalChanceBonus();
                    criticalChanceBonus += GetSpecialCriticalChanceBonus(
                        firedBullet,
                        resolvedBullet);
                    criticalChanceBonus += concentrationCriticalChanceBonus;
                    BulletEffectData shellEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.ShellCollector);
                    int shellExtraShots = GetAvailableShellExtraShots(
                        firedBullet,
                        shellEffect);
                    int shellCost = shellEffect == null
                        ? 0
                        : Mathf.Max(1, shellEffect.StackCount);
                    int shotgunAdditionalShotCount = Mathf.Max(
                        0,
                        resolvedBullet.ShotCount - 1);
                    int additionalShotCount = 0;
                    int chainAdditionalShotCount = 0;
                    bool keepFiring;
    
                    do
                    {
                        bool shotCompleted = false;
                        yield return FireSingleShot(
                            resolvedBullet,
                            shotDirection,
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

                        if (additionalShotCount == 0)
                        {
                            primerReusePending = relicManager != null
                                && relicManager.TryReuseFiredBullet();
                        }

                        bool hasRequiredShotgunShot =
                            additionalShotCount < shotgunAdditionalShotCount;
                        bool hasPrimerReuseShot =
                            !hasRequiredShotgunShot && primerReusePending;
                        bool hasChainShot = !hasRequiredShotgunShot
                            && !hasPrimerReuseShot
                            && chainEffect != null
                            && RollChainFire(
                                chainEffect,
                                chainAdditionalShotCount);
                        keepFiring = hasRequiredShotgunShot
                            || hasPrimerReuseShot
                            || hasChainShot;

                        if (keepFiring)
                        {
                            additionalShotCount++;

                            if (hasPrimerReuseShot)
                            {
                                primerReusePending = false;
                            }
                            else if (hasChainShot)
                            {
                                chainAdditionalShotCount++;
                            }
    
                            if (ShouldWaitBeforeAdditionalShot(
                                hasRequiredShotgunShot,
                                shotInterval))
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
                            shotDirection,
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
    
                    if (deckManager.LoadedBullets.Count == 0
                        && finaleExtraShotChance > 0f
                        && UnityEngine.Random.Range(0f, 100f)
                            < finaleExtraShotChance)
                    {
                        bool finaleCompleted = false;
                        yield return FireSingleShot(
                            resolvedBullet,
                            shotDirection,
                            damageMultiplier,
                            criticalChanceBonus,
                            false,
                            fireIntoAir,
                            false,
                            true,
                            currentPhysicalBulletIndex,
                            completed => finaleCompleted = completed);
                    }

                    BulletEffectData alzheimerEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Alzheimer);

                    if (alzheimerEffect != null
                        && alzheimerEffect.RollActivation())
                    {
                        BulletInstance originalConsumedBullet =
                            currentConsumedBullet;

                        foreach (ReplayShot replayShot in replayShots)
                        {
                            if (replayShot.Bullet == null)
                            {
                                continue;
                            }

                            currentConsumedBullet = replayShot.Bullet;
                            activeCriticalDamageMultiplierBonus =
                                replayShot.CriticalDamageMultiplierBonus;
                            bool replayCompleted = false;
                            yield return FireSingleShot(
                                replayShot.Bullet,
                                replayShot.Direction,
                                replayShot.DamageMultiplier,
                                replayShot.CriticalChanceBonus,
                                false,
                                true,
                                false,
                                true,
                                currentPhysicalBulletIndex,
                                completed => replayCompleted = completed);
                        }

                        currentConsumedBullet = originalConsumedBullet;
                    }

                    replayShots.Add(new ReplayShot(
                        resolvedBullet,
                        shotDirection,
                        damageMultiplier,
                        criticalChanceBonus,
                        shotCriticalDamageMultiplierBonus));

                    BulletEffectData recoilEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.RecoilShot);

                    if (recoilEffect != null && playerMove != null)
                    {
                        yield return playerMove.PushPlayerFromBullet(
                            -shotDirection,
                            Mathf.Max(1, recoilEffect.KnockbackDistance));
                    }

                    ApplyTrackingMarks(firedBullet, resolvedBullet);

                    BulletEffectData spreadEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Spread);

                    if (spreadEffect != null)
                    {
                        spreadDamageBonus += Mathf.Max(0f, spreadEffect.Amount)
                            / 100f;
                    }

                    BulletEffectData concentrationEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Concentration);

                    if (concentrationEffect != null)
                    {
                        concentrationCriticalChanceBonus += Mathf.Max(
                            0f,
                            concentrationEffect.Amount);
                    }

                    BulletEffectData immersionEffect = FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Immersion);

                    if (immersionEffect != null)
                    {
                        pendingCriticalDamageMultiplierBonus += Mathf.Max(
                            0f,
                            immersionEffect.Amount);
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
            replayShots.Clear();
            activeCriticalDamageMultiplierBonus = 0f;
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
                    GetShotRange(bulletData),
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
                    && deckManager.LoadedBullets.Count == 0,
                currentConsumedBullet == null
                    ? -1
                    : currentConsumedBullet.AcquisitionOrder);
    
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
                    GetShotRange(bulletData));
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
            owner.BulletFired?.Invoke(bulletData);
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
            UpdateRitualFocus(isCritical && hasEnemyTarget);
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
    
        }
    
        public float GetCurrentCylinderBuild()
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
                GetShotRange(bullet),
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
                    GetShotRange(bullet),
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
                        BulletEffectUtility.ResolveShotDirection(
                            resolvedBullet,
                            horizontalDirection)))
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
                        enemy.ActiveStatusTypeCount,
                        CountActiveEnemies()));
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
            return BulletEffectUtility.ResolveShot(
                loadedBullet,
                previousResolvedBullet);
        }

        private float GetLoadedFinaleExtraShotChance()
        {
            if (deckManager == null)
            {
                return 0f;
            }

            float chance = 0f;

            foreach (BulletInstance bullet in deckManager.LoadedBullets)
            {
                BulletEffectData effect = FindSpecialEffect(
                    bullet,
                    BulletEffectType.Finale);

                if (effect != null)
                {
                    chance += Mathf.Max(0f, effect.Amount);
                }
            }

            return Mathf.Clamp(chance, 0f, 100f);
        }

        private void ApplyFleshForBoneCost(BulletInstance bullet)
        {
            BulletEffectData effect = FindSpecialEffect(
                bullet,
                BulletEffectType.FleshForBone);

            if (effect != null && playerHealth != null)
            {
                playerHealth.ApplyStatusDamage(
                    Mathf.Max(0, Mathf.RoundToInt(effect.Amount)),
                    false);
            }
        }

        private void ApplyTrackingMarks(
            BulletInstance firedBullet,
            BulletInstance resolvedBullet)
        {
            BulletEffectData effect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Tracking);
            int markCount = firedBullet == null ? 0 : firedBullet.AbilityStacks;

            if (effect == null || markCount <= 0 || waveManager == null)
            {
                return;
            }

            for (int markIndex = 0; markIndex < markCount; markIndex++)
            {
                targetBuffer.Clear();

                foreach (EnemyController enemy in waveManager.ActiveEnemies)
                {
                    if (enemy != null && enemy.CurrentHealth > 0)
                    {
                        targetBuffer.Add(enemy);
                    }
                }

                if (targetBuffer.Count == 0)
                {
                    break;
                }

                EnemyController target = targetBuffer[
                    UnityEngine.Random.Range(0, targetBuffer.Count)];
                target.AddStatusEffect(
                    StatusEffectType.Mark,
                    Mathf.Max(1, effect.StackCount),
                    true);
            }

            firedBullet.SetAbilityStacks(0);
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
            BulletEffectData seismometerEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Seismometer);

            if (seismometerEffect != null)
            {
                multiplier *= 1f + firedBullet.AbilityStacks
                    * Mathf.Max(0f, seismometerEffect.Amount) / 100f;
            }

            BulletEffectData highRollerEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.HighRoller);

            if (highRollerEffect != null && playerHealth != null)
            {
                multiplier *=
                    BulletEffectUtility.GetMissingHealthDamageMultiplier(
                        playerHealth.CurrentHealth,
                        playerHealth.MaxHealth,
                        highRollerEffect.Amount);
            }

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
    
        public int CountDistinctOwnedBulletTypes()
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
    
        public int CountOwnedBulletsByGrade(
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
    
        public int GetMostCommonOwnedGradeCount()
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

        private void UpdateRitualFocus(bool isCritical)
        {
            if (deckManager == null)
            {
                return;
            }

            deckManager.GetOwnedBullets(ownedBulletBuffer);
            List<BulletInstance> ritualBullets =
                new List<BulletInstance>(ownedBulletBuffer);

            foreach (BulletInstance bullet in ritualBullets)
            {
                BulletEffectData effect = FindSpecialEffect(
                    bullet,
                    BulletEffectType.Ritual);

                if (effect == null)
                {
                    continue;
                }

                if (isCritical)
                {
                    bullet.AddAbilityStacks(Mathf.Max(1, effect.StackCount));
                    continue;
                }

                bullet.SetAbilityStacks(0);

                if (effect.RollActivation()
                    && deckManager.TryDestroyBullet(bullet))
                {
                    HandleBulletDestroyed(bullet);
                }
            }
        }

        private float GetRitualCriticalDamageMultiplierBonus()
        {
            if (deckManager == null)
            {
                return 0f;
            }

            deckManager.GetOwnedBullets(ownedBulletBuffer);
            double bonus = 0d;

            foreach (BulletInstance bullet in ownedBulletBuffer)
            {
                BulletEffectData effect = FindSpecialEffect(
                    bullet,
                    BulletEffectType.Ritual);

                if (effect != null)
                {
                    bonus += bullet.AbilityStacks
                        * Mathf.Max(0f, effect.Amount);
                }
            }

            return bonus >= float.MaxValue ? float.MaxValue : (float)bonus;
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
            return BulletEffectUtility.Find(bullet, effectType);
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
                bool hadDebuffBeforeHit = enemy.ActiveStatusTypeCount > 0;
                bool defeatPresented = false;
                float targetDamageMultiplier = GetTargetDamageMultiplier(
                    bulletData,
                    enemy,
                    horizontalDirection);
                targetDamageMultiplier *= (float)(relicManager == null
                    ? 1d
                    : relicManager.GetTargetConditionalDamageMultiplier(
                        enemy.GetInstanceID(),
                        enemy.ActiveStatusTypeCount,
                        CountActiveEnemies()));
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
                        pendingEffectDefeats.TryGetValue(
                            enemy,
                            out ManagedEffectDefeatResult preAttackDefeat);
                    pendingEffectDefeats.Remove(enemy);
                    CombatFeedbackController.DefeatPresentationCue cue =
                        preAttackEffectDefeatAlreadyRecorded
                            ? preAttackDefeat.PresentationCue
                            : default;
                    if (!preAttackEffectDefeatAlreadyRecorded)
                    {
                        cue = RecordDefeat(
                            enemySnapshot.Position,
                            horizontalDirection,
                            0,
                            targetMaxHealth,
                            isCritical,
                            -1);
                    }
                    if (!preAttackEffectDefeatAlreadyRecorded
                        || !preAttackDefeat.PresentationScheduled)
                    {
                        PlayDefeatImpact(
                            enemySnapshot,
                            horizontalDirection,
                            bulletData,
                            cue);
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
                    enemiesHitThisTurn.Add(enemy.GetInstanceID());
                    owner.DamageDealt?.Invoke(reportedDamage);
                    relicManager?.NotifyEnemyDamaged(enemy, reportedDamage);
                }
                bool defeatedByAttack = healthBeforeHit > 0
                    && enemy.CurrentHealth <= 0;
                combatFeedback?.RecordDamage(
                    reportedDamage,
                    reportedDamage > appliedDamage);
                defeatPresented = defeatedByAttack;
    
                if (defeatedByAttack)
                {
                    CombatFeedbackController.DefeatPresentationCue cue =
                        RecordDefeat(
                        enemySnapshot.Position,
                        horizontalDirection,
                        reportedDamage,
                        targetMaxHealth,
                        isCritical,
                        healthBeforeHit);
                    PlayDefeatImpact(
                        enemySnapshot,
                        horizontalDirection,
                        bulletData,
                        cue);
                }
                else if (appliedDamage > 0)
                {
                    combatPresentation?.PlayImpact(
                        enemySnapshot,
                        horizontalDirection,
                        bulletData,
                        CombatImpactTierUtility.Resolve(
                            isCritical,
                            reportedDamage,
                            targetMaxHealth,
                            false));
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

                yield return ApplyClosedCircuitDamageTransfer(
                    bulletData,
                    sourceTileIndex,
                    horizontalDirection,
                    reportedDamage,
                    processedDefeatIds);

                if (hadDebuffBeforeHit && enemy.CurrentHealth > 0)
                {
                    relicManager?.TryApplyMutationCatalyst(enemy);
                }
    
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
    
                    if (BulletEffectUtility.IsShotScoped(effect.EffectType)
                        || BulletEffectUtility.IsManagedSpecial(
                            effect.EffectType))
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
                    pendingEffectDefeats.TryGetValue(
                        enemy,
                        out ManagedEffectDefeatResult recordedEffectDefeat);
                pendingEffectDefeats.Remove(enemy);
    
                bool defeatedByManagedEffect =
                    managedEffectDefeat.WasDefeated;
    
                if (enemy == null || enemy.CurrentHealth <= 0)
                {
                    if (!defeatPresented)
                    {
                        CombatFeedbackController.DefeatPresentationCue cue =
                            effectDefeatAlreadyRecorded
                                ? recordedEffectDefeat.PresentationCue
                                : default;
                        if (!effectDefeatAlreadyRecorded)
                        {
                            cue = RecordDefeat(
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
                                defeatedByManagedEffect
                                    ? managedEffectDefeat.HealthBeforeDamage
                                    : -1);
                        }
                        if (!effectDefeatAlreadyRecorded
                            || !recordedEffectDefeat.PresentationScheduled)
                        {
                            PlayDefeatImpact(
                                enemySnapshot,
                                horizontalDirection,
                                bulletData,
                                cue);
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

            BulletEffectData assassinationEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Assassination);

            if (assassinationEffect != null && enemy != null
                && enemiesHitThisTurn.Contains(enemy.GetInstanceID()))
            {
                multiplier *= 1f
                    + Mathf.Max(0f, assassinationEffect.Amount) / 100f;
            }
    
            return multiplier;
        }

        private int GetShotRange(BulletInstance bullet)
        {
            return relicManager == null
                ? bullet == null ? 1 : bullet.MaxRange
                : relicManager.GetShotRange(bullet);
        }

        private int CountActiveEnemies()
        {
            if (waveManager == null)
            {
                return 0;
            }

            int count = 0;

            foreach (EnemyController enemy in waveManager.ActiveEnemies)
            {
                if (enemy != null && enemy.CurrentHealth > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private CombatFeedbackController.DefeatPresentationCue RecordDefeat(
            Vector3 worldPosition,
            int horizontalDirection,
            int appliedDamage,
            int targetMaxHealth,
            bool wasCritical,
            int targetHealthBeforeDamage)
        {
            return combatFeedback == null
                ? default
                : combatFeedback.RecordDefeat(
                    worldPosition,
                    horizontalDirection,
                    appliedDamage,
                    targetMaxHealth,
                    wasCritical,
                    waveManager != null
                        && waveManager.ActiveEnemies.Count <= 1,
                    GetCurrentCylinderBuild(),
                    targetHealthBeforeDamage);
        }

        private void PlayDefeatImpact(
            CombatPresentation.EnemySnapshot snapshot,
            int horizontalDirection,
            BulletInstance bullet,
            CombatFeedbackController.DefeatPresentationCue cue)
        {
            float feedbackMultiplier = cue.FeedbackMultiplier > 0f
                ? cue.FeedbackMultiplier
                : 1f;
            combatPresentation?.PlayImpact(
                snapshot,
                horizontalDirection,
                bullet,
                CombatImpactTier.Defeat,
                feedbackMultiplier,
                combatFeedback == null
                    ? 0f
                    : combatFeedback.GetRemainingDefeatPresentationDelay(cue),
                cue.WasFinalEnemy);
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
                int enemyInstanceId = enemy.GetInstanceID();
                int reportedDamage = enemy.PredictAttackDamage(stormDamage);
                int appliedDamage = enemy.ApplyAttackDamage(stormDamage, false);
    
                if (appliedDamage > 0)
                {
                    owner.DamageDealt?.Invoke(reportedDamage);
                    combatFeedback?.RecordDamage(
                        reportedDamage,
                        reportedDamage > appliedDamage);
                }
    
                bool defeated = healthBeforeDamage > 0
                    && enemy.CurrentHealth <= 0;
                if (defeated)
                {
                    CombatFeedbackController.DefeatPresentationCue cue =
                        RecordDefeat(
                        snapshot.Position,
                        horizontalDirection,
                        reportedDamage,
                        targetMaxHealth,
                        false,
                        healthBeforeDamage);
                    PlayDefeatImpact(
                        snapshot,
                        horizontalDirection,
                        sourceBullet,
                        cue);
                    relicManager.NotifyEnemyDefeated(
                        enemy,
                        null,
                        waveManager.ActiveEnemies,
                        boardManager,
                        deckManager,
                        enemyInstanceId);
                }
                else if (appliedDamage > 0)
                {
                    combatPresentation?.PlayImpact(
                        snapshot,
                        horizontalDirection,
                        sourceBullet,
                        CombatImpactTierUtility.Resolve(
                            false,
                            reportedDamage,
                            targetMaxHealth,
                            false));
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
                float transferPercent =
                    BulletEffectUtility.GetWallImpactTransferPercent(
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
                    owner.DamageDealt?.Invoke(reportedDamage);
                    relicManager?.NotifyEnemyDamaged(
                        targetEnemy,
                        reportedDamage);
                }
    
                bool defeated = healthBeforeTransfer > 0
                    && targetEnemy.CurrentHealth <= 0;
                combatFeedback?.RecordDamage(
                    reportedDamage,
                    reportedDamage > appliedDamage);
                if (defeated)
                {
                    CombatFeedbackController.DefeatPresentationCue cue =
                        RecordDefeat(
                        targetSnapshot.Position,
                        horizontalDirection,
                        reportedDamage,
                        targetMaxHealth,
                        false,
                        healthBeforeTransfer);
                    PlayDefeatImpact(
                        targetSnapshot,
                        horizontalDirection,
                        bullet,
                        cue);
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
                    combatPresentation?.PlayImpact(
                        targetSnapshot,
                        horizontalDirection,
                        bullet,
                        CombatImpactTierUtility.Resolve(
                            false,
                            reportedDamage,
                            targetMaxHealth,
                            false));
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
    
        private IEnumerator ApplyClosedCircuitDamageTransfer(
            BulletInstance bullet,
            int sourceTileIndex,
            int horizontalDirection,
            int sourceDamage,
            HashSet<int> processedDefeatIds)
        {
            if (sourceTileIndex < 0 || boardManager == null
                || waveManager == null || relicManager == null
                || !relicManager.TryGetClosedCircuitTransferDamage(
                    sourceDamage,
                    out int transferDamage))
            {
                yield break;
            }

            int direction = horizontalDirection >= 0 ? 1 : -1;
            EnemyController target = null;
            int targetDistance = int.MaxValue;

            foreach (EnemyController candidate in waveManager.ActiveEnemies)
            {
                if (candidate == null || candidate.CurrentHealth <= 0
                    || !boardManager.TryGetTileIndex(
                        candidate.transform.position,
                        out int candidateTile))
                {
                    continue;
                }

                int offset = (candidateTile - sourceTileIndex) * direction;

                if (offset > 0 && offset < targetDistance)
                {
                    target = candidate;
                    targetDistance = offset;
                }
            }

            if (target == null)
            {
                yield break;
            }

            CombatPresentation.EnemySnapshot snapshot =
                combatPresentation == null
                    ? default
                    : combatPresentation.CaptureEnemy(target);
            int healthBeforeDamage = target.CurrentHealth;
            int targetMaxHealth = target.MaxHealth;
            int targetInstanceId = target.GetInstanceID();
            int reportedDamage = target.PredictAttackDamage(transferDamage);
            int appliedDamage = target.ApplyAttackDamage(
                transferDamage,
                false);

            if (appliedDamage > 0)
            {
                owner.DamageDealt?.Invoke(reportedDamage);
                relicManager.NotifyEnemyDamaged(target, reportedDamage);
            }

            bool defeated = healthBeforeDamage > 0
                && target.CurrentHealth <= 0;
            combatFeedback?.RecordDamage(
                reportedDamage,
                reportedDamage > appliedDamage);

            if (defeated)
            {
                CombatFeedbackController.DefeatPresentationCue cue =
                    RecordDefeat(
                        snapshot.Position,
                        horizontalDirection,
                        reportedDamage,
                        targetMaxHealth,
                        false,
                        healthBeforeDamage);
                PlayDefeatImpact(
                    snapshot,
                    horizontalDirection,
                    bullet,
                    cue);
                yield return ApplyDefeatTriggeredAbilities(
                    bullet,
                    target,
                    targetInstanceId,
                    horizontalDirection,
                    reportedDamage,
                    snapshot.Position,
                    processedDefeatIds);
            }
            else if (appliedDamage > 0)
            {
                combatPresentation?.PlayImpact(
                    snapshot,
                    horizontalDirection,
                    bullet,
                    CombatImpactTierUtility.Resolve(
                        false,
                        reportedDamage,
                        targetMaxHealth,
                        false));
                combatFeedback?.RecordHit(
                    snapshot.Position,
                    horizontalDirection,
                    reportedDamage,
                    targetMaxHealth,
                    false,
                    GetCurrentCylinderBuild());
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
                boardManager,
                deckManager,
                enemyInstanceId);
    
            yield return ApplyConditionalEvents(
                bullet,
                BulletConditionalTrigger.EnemyDefeated,
                enemy,
                horizontalDirection,
                appliedDamage,
                worldPosition);
            GrantDevourerStack(bullet);
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
                if (effect == null
                    || !BulletEffectUtility.IsShotScoped(effect.EffectType)
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
    

        private int CalculateAttackDamage(
            BulletInstance bullet,
            bool isCritical,
            float damageMultiplier,
            int shotIndex,
            bool isLastLoadedShot,
            bool applyRuntimeRelicModifiers = true)
        {
            return owner.CalculateAttackDamage(
                bullet,
                isCritical,
                damageMultiplier,
                shotIndex,
                isLastLoadedShot,
                applyRuntimeRelicModifiers,
                activeCriticalDamageMultiplierBonus
                    + GetRitualCriticalDamageMultiplierBonus());
        }

        private static bool IsBoardWideShot(BulletInstance bullet)
        {
            return BulletEffectUtility.IsBoardWideShot(bullet);
        }

        private void SortTargetsByTileIndex(
            List<EnemyController> targets)
        {
            owner.SortTargetsByTileIndex(targets);
        }

        private Vector3 GetMissEndPoint(
            int horizontalDirection,
            int maxRange)
        {
            return owner.GetMissEndPoint(horizontalDirection, maxRange);
        }

        private Vector3 GetShotLineEndPoint(
            Vector3 startPoint,
            Vector3 targetEndPoint)
        {
            return owner.GetShotLineEndPoint(startPoint, targetEndPoint);
        }

        private IEnumerator WaitForShotInterval()
        {
            return owner.WaitForShotInterval();
        }

        private void ShowBulletFeedback(BulletInstance bullet)
        {
            owner.ShowBulletFeedback(bullet);
        }
    }
}
