using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerShoot
{
    private sealed class DamagePreviewController
    {
        private readonly PlayerShoot owner;
        private readonly List<EnemyController> targetBuffer =
            new List<EnemyController>();
        private readonly List<EnemyController> hitBuffer =
            new List<EnemyController>();
        private readonly Dictionary<EnemyController, DamagePreviewEnemyState>
            damagePreviewStates =
                new Dictionary<EnemyController, DamagePreviewEnemyState>();
        private readonly HashSet<EnemyController> previewedEnemies =
            new HashSet<EnemyController>();
        private readonly Dictionary<BulletInstance, float>
            previewDamageBonuses =
                new Dictionary<BulletInstance, float>();
        private readonly Dictionary<BulletInstance, float>
            previewCriticalBonuses =
                new Dictionary<BulletInstance, float>();
        private readonly Dictionary<BulletInstance, float>
            previewStoredBonuses =
                new Dictionary<BulletInstance, float>();
        private readonly Dictionary<BulletInstance, int> previewAbilityStacks =
            new Dictionary<BulletInstance, int>();
        private readonly Dictionary<BulletInstance, int>
            previewPermanentStacks =
                new Dictionary<BulletInstance, int>();
        private readonly Dictionary<BulletInstance, int> previewShotsObserved =
            new Dictionary<BulletInstance, int>();
        private readonly List<BulletInstance> previewOwnedBullets =
            new List<BulletInstance>();
        private readonly List<BulletInstance> previewRemainingLoadedBullets =
            new List<BulletInstance>();
        private readonly HashSet<BulletData> previewOwnedBulletTypeBuffer =
            new HashSet<BulletData>();
        private readonly int[] previewOwnedGradeCountBuffer = new int[4];
        private int previewPlayerTileIndex = -1;
        private int previewPlayerLaneIndex;
        private float previewCriticalDamageMultiplierBonus;
        private PlayerCombatPreviewResources previewResources;
        private int activeResonanceStatusMask;
        private bool previewShotDefeatedEnemy;
        private EnemyController forcedPreviewTarget;
        private EnemyController previewLastPhysicalTarget;
        private EnemyController previewPreviousPhysicalTarget;
        private bool previewIsFirstShotOfPhysicalBullet;

        private DeckManager deckManager => owner.deckManager;
        private CurrencyManager currencyManager => owner.currencyManager;
        private PlayerMove playerMove => owner.playerMove;
        private PlayerHealth playerHealth => owner.playerHealth;
        private BoardManager boardManager => owner.boardManager;
        private WaveManager waveManager => owner.waveManager;
        private Transform transform => owner.transform;
        private RelicManager relicManager
        {
            get => owner.relicManager;
            set => owner.relicManager = value;
        }

        public DamagePreviewController(PlayerShoot owner)
        {
            this.owner = owner;
        }

        public bool Show(int loadedBulletIndex)
        {
            Clear();
            InitializeDamagePreviewState();
            SimulateLoadedBulletDamage(loadedBulletIndex);
            bool displayedAnyDamage = false;
            BulletData previewBullet = loadedBulletIndex >= 0
                && loadedBulletIndex < deckManager.LoadedBullets.Count
                    ? deckManager.LoadedBullets[loadedBulletIndex]
                        ?.Data
                    : null;

            foreach (DamagePreviewEnemyState state in damagePreviewStates.Values)
            {
                if (state.Enemy == null || state.Segments.Count == 0)
                {
                    continue;
                }

                state.Enemy.ShowDamagePreview(
                    state.Segments,
                    previewBullet);
                previewedEnemies.Add(state.Enemy);
                displayedAnyDamage = true;
            }

            return displayedAnyDamage;
        }

        public void Clear()
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

        private void InitializeDamagePreviewState()
        {
            damagePreviewStates.Clear();
            previewDamageBonuses.Clear();
            previewCriticalBonuses.Clear();
            previewStoredBonuses.Clear();
            previewAbilityStacks.Clear();
            previewPermanentStacks.Clear();
            previewShotsObserved.Clear();
            previewOwnedBullets.Clear();
            previewCriticalDamageMultiplierBonus = 0f;
            previewLastPhysicalTarget = null;
            previewPreviousPhysicalTarget = null;
            previewIsFirstShotOfPhysicalBullet = false;
            relicManager ??= FindFirstObjectByType<RelicManager>(
                FindObjectsInactive.Include);
            previewResources = new PlayerCombatPreviewResources(
                currencyManager == null ? 0 : currencyManager.CurrentMoney,
                playerHealth == null ? 0 : playerHealth.CurrentHealth,
                playerHealth == null ? 0 : playerHealth.MaxHealth,
                new RelicLethalDamagePreviewState(
                    relicManager == null ? null : relicManager.OwnedRelics));
            deckManager.GetOwnedBullets(previewOwnedBullets);
            previewPlayerTileIndex = boardManager.TryGetTileIndex(
                transform.position,
                playerMove == null ? 0 : playerMove.CurrentLaneIndex,
                out int playerTileIndex)
                    ? playerTileIndex
                    : -1;
            previewPlayerLaneIndex = playerMove == null
                ? 0
                : playerMove.CurrentLaneIndex;
    
            foreach (EnemyController enemy in waveManager.ActiveEnemies)
            {
                if (enemy != null && enemy.CurrentHealth > 0)
                {
                    DamagePreviewEnemyState state =
                        new DamagePreviewEnemyState(enemy);
                    state.WasHitThisTurn = owner.firingSequence != null
                        && owner.firingSequence.WasEnemyHitThisTurn(enemy);
    
                    if (boardManager.TryGetTileIndex(
                            enemy.transform.position,
                            enemy.CurrentLaneIndex,
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
            int previewSniperBulletsFired = 0;
            int previewCriticalShots = 0;
            float stackedDamageBonus = 0f;
            float spreadDamageBonus = 0f;
            float concentrationCriticalChanceBonus = 0f;
            float pendingCriticalDamageMultiplierBonus = 0f;
            BulletInstance previousResolvedBullet = null;
            int previousStatusMask = 0;
            BulletRuntimeStateSnapshot previousPreFireState = default;
            bool hasPreviousPreFireState = false;
            int initialIndex = loadedBullets.Count - 1;
            BulletInstance initialResolvedBullet = initialIndex < 0
                ? null
                : ResolveShotBullet(loadedBullets[initialIndex], null);
            int initialShotDirection = BulletEffectUtility.ResolveShotDirection(
                initialResolvedBullet,
                horizontalDirection);
            bool initialIsPowder = FindSpecialEffect(
                initialResolvedBullet,
                BulletEffectType.PowderPouch) != null;
            bool fireIntoAir = initialIsPowder
                ? !HasPreviewViableFutureShot(
                    initialIndex - 1,
                    initialResolvedBullet,
                    horizontalDirection)
                : !HasPreviewTargets(
                    initialResolvedBullet,
                    initialShotDirection);
    
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
                int shotDirection = BulletEffectUtility.ResolveShotDirection(
                    resolvedBullet,
                    horizontalDirection);
    
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
    
                    if (previewOwnedBullets.Remove(firedBullet))
                    {
                        GrantPreviewLegacyStacks(firedBullet);
                    }
                    horizontalDirection =
                        BulletEffectUtility.ResolveFacingDirectionAfterShot(
                            resolvedBullet,
                            horizontalDirection);
                    previousResolvedBullet = resolvedBullet;
                    previousPreFireState = currentPreFireState;
                    hasPreviousPreFireState = true;
                    continue;
                }
    
                if (!fireIntoAir
                    && !HasPreviewTargets(resolvedBullet, shotDirection))
                {
                    break;
                }
    
                BulletDynamicCombatContext damageContext =
                    CreatePreviewDynamicCombatContext(
                        firedBullet,
                        resolvedBullet,
                        bulletIndex,
                        initialLoadedCount,
                        GetPreviewDamageBonus(firedBullet),
                        0f);
                float damageMultiplier =
                    BulletDynamicCombatRules.CalculateDamageMultiplier(
                        firedBullet,
                        resolvedBullet,
                        damageContext);
                damageMultiplier *= BulletEffectUtility
                    .GetPositionDamageMultiplier(
                        resolvedBullet,
                        bulletIndex == initialLoadedCount - 1,
                        bulletIndex == 0);
                damageMultiplier *= BulletEffectUtility
                    .GetRandomPelletDamageMultiplier(
                        resolvedBullet,
                        true);
                previewDamageBonuses[firedBullet] = 0f;
                damageMultiplier *= 1f + spreadDamageBonus;
                previewCriticalDamageMultiplierBonus =
                    pendingCriticalDamageMultiplierBonus
                    + BulletEffectUtility.GetMasterpieceCriticalDamageBonus(
                        FindSpecialEffect(
                            resolvedBullet,
                            BulletEffectType.Masterpiece),
                        damageContext.OwnedHighGradeCount);
                pendingCriticalDamageMultiplierBonus = 0f;
                activeResonanceStatusMask = previousStatusMask;
                ApplyPreviewFleshForBoneCost(resolvedBullet);
                bool relicForcesCritical = false;
    
                if (relicManager != null
                    && relicManager.TryGetLoadedBulletRelicModifiers(
                        firedBullet,
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

                if (relicManager != null)
                {
                    damageMultiplier = (float)Math.Min(
                        float.MaxValue,
                        Math.Max(0d, damageMultiplier)
                            * relicManager
                                .GetPreviewHealthConditionalDamageMultiplier(
                                    previewResources.CurrentHealth,
                                    previewResources.MaxHealth));
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
    
                float temporaryCriticalChanceBonus =
                    GetPreviewCriticalBonus(firedBullet);
                BulletDynamicCombatContext criticalContext =
                    CreatePreviewDynamicCombatContext(
                        firedBullet,
                        resolvedBullet,
                        bulletIndex,
                        initialLoadedCount,
                        0f,
                        temporaryCriticalChanceBonus);
                float criticalChance = resolvedBullet.CriticalChance
                    + BulletDynamicCombatRules.CalculateCriticalChanceBonus(
                        resolvedBullet,
                        criticalContext)
                    + concentrationCriticalChanceBonus;
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
    
                previewIsFirstShotOfPhysicalBullet = true;
                PrepareFocusedPreviewTarget(resolvedBullet, 0);
                bool firedPhysicalShot = SimulatePreviewShot(
                    resolvedBullet,
                    firedBullet,
                    shotDirection,
                    damageMultiplier,
                    guaranteedCritical,
                    true,
                    emphasized,
                    bulletIndex,
                    previewSniperBulletsFired,
                    ref previewBulletsFired,
                    ref previewCriticalShots);
                forcedPreviewTarget = null;
                previewIsFirstShotOfPhysicalBullet = false;
                bool defeatedWithThisBullet = previewShotDefeatedEnemy;

                for (int shotgunShotIndex = 1;
                     shotgunShotIndex < resolvedBullet.ShotCount;
                     shotgunShotIndex++)
                {
                    PrepareFocusedPreviewTarget(
                        resolvedBullet,
                        shotgunShotIndex);
                    if (!SimulatePreviewShot(
                            resolvedBullet,
                            firedBullet,
                            shotDirection,
                            damageMultiplier,
                            guaranteedCritical,
                            true,
                            emphasized,
                            bulletIndex,
                            previewSniperBulletsFired,
                            ref previewBulletsFired,
                            ref previewCriticalShots))
                    {
                        forcedPreviewTarget = null;
                        break;
                    }

                    forcedPreviewTarget = null;

                    defeatedWithThisBullet |= previewShotDefeatedEnemy;
                }

                int massProducedShots = BulletEffectUtility
                    .GetMassProducedAdditionalShots(
                        FindSpecialEffect(
                            resolvedBullet,
                            BulletEffectType.MassProduced),
                        damageContext.OwnedLowGradeCount);

                for (int massShotIndex = 0;
                     massShotIndex < massProducedShots;
                     massShotIndex++)
                {
                    if (!SimulatePreviewShot(
                            resolvedBullet,
                            firedBullet,
                            shotDirection,
                            damageMultiplier,
                            guaranteedCritical,
                            true,
                            emphasized,
                            bulletIndex,
                            previewSniperBulletsFired,
                            ref previewBulletsFired,
                            ref previewCriticalShots))
                    {
                        break;
                    }

                    defeatedWithThisBullet |= previewShotDefeatedEnemy;
                }
    
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
                            shotDirection,
                            damageMultiplier,
                            guaranteedCritical,
                            true,
                            emphasized,
                            bulletIndex,
                            previewSniperBulletsFired,
                            ref previewBulletsFired,
                            ref previewCriticalShots))
                    {
                        break;
                    }
    
                    additionalShotCount++;
                    defeatedWithThisBullet |= previewShotDefeatedEnemy;
                }

                BulletEffectData collectionEffect = FindSpecialEffect(
                    resolvedBullet,
                    BulletEffectType.Collection);

                for (int collectionIndex = 0;
                     defeatedWithThisBullet
                     && collectionEffect != null
                     && collectionIndex < Mathf.Max(
                         0,
                         collectionEffect.StackCount);
                     collectionIndex++)
                {
                    forcedPreviewTarget = SelectLowestHealthPreviewTarget();

                    if (forcedPreviewTarget == null
                        || !SimulatePreviewShot(
                            resolvedBullet,
                            firedBullet,
                            shotDirection,
                            damageMultiplier,
                            guaranteedCritical,
                            false,
                            emphasized,
                            bulletIndex,
                            previewSniperBulletsFired,
                            ref previewBulletsFired,
                            ref previewCriticalShots))
                    {
                        forcedPreviewTarget = null;
                        break;
                    }

                    forcedPreviewTarget = null;
                    defeatedWithThisBullet = previewShotDefeatedEnemy;
                }
    
                for (int shellIndex = 0;
                     shellIndex < shellExtraShots;
                     shellIndex++)
                {
                    if (!SimulatePreviewShot(
                            resolvedBullet,
                            firedBullet,
                            shotDirection,
                            damageMultiplier * shellEffect.Amount / 100f,
                            guaranteedCritical,
                            false,
                            emphasized,
                            bulletIndex,
                            previewSniperBulletsFired,
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
    
                if (HasGuaranteedEffect(
                        resolvedBullet,
                        BulletEffectType.DestroyBullet))
                {
                    if (previewOwnedBullets.Remove(firedBullet))
                    {
                        GrantPreviewLegacyStacks(firedBullet);
                    }
                }

                ApplyPreviewPostBulletAbilities(firedBullet, resolvedBullet);

                if (firedPhysicalShot
                    && firedBullet.BulletType == BulletType.Sniper
                    && previewSniperBulletsFired < int.MaxValue)
                {
                    previewSniperBulletsFired++;
                }

                horizontalDirection =
                    BulletEffectUtility.ResolveFacingDirectionAfterShot(
                        resolvedBullet,
                        horizontalDirection);
    
                previousResolvedBullet = resolvedBullet;
                previousStatusMask = BulletEffectUtility
                    .GetInflictedStatusMask(resolvedBullet);

                if (FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Resonance) != null)
                {
                    previousStatusMask |= activeResonanceStatusMask;
                }
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
            int previewSniperBulletsFired,
            ref int previewBulletsFired,
            ref int previewCriticalShots)
        {
            previewShotDefeatedEnemy = false;

            if (!BuildGuaranteedPreviewHitTargets(
                    resolvedBullet,
                    horizontalDirection))
            {
                return false;
            }

            if (previewIsFirstShotOfPhysicalBullet)
            {
                previewPreviousPhysicalTarget = previewLastPhysicalTarget;
                previewLastPhysicalTarget = hitBuffer.Count > 0
                    ? hitBuffer[0]
                    : null;
            }
    
            // Clone and other resolver effects borrow combat behavior, but the
            // preview segment belongs to the physical cylinder bullet that will
            // be consumed. Its own upgraded Secondary Line Color is authoritative.
            Color previewColor = firedBullet.SecondaryLineColor;
            bool shotHasCriticalOutcome = guaranteedCritical;
    
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
                    previewSniperBulletsFired);
                targetMultiplier *= (float)(relicManager == null
                    ? 1d
                    : relicManager
                        .GetPreviewTargetConditionalDamageMultiplier(
                            CountActiveStatusTypes(state),
                            CountPreviewActiveEnemies()));
                bool targetIsCritical =
                    PlayerAttackDamageCalculator.ResolveCriticalForTarget(
                        guaranteedCritical,
                        state.IsExposed);
                shotHasCriticalOutcome |= targetIsCritical;
                int attackDamage = CalculateAttackDamage(
                    resolvedBullet,
                    targetIsCritical,
                    damageMultiplier * targetMultiplier,
                    previewBulletsFired,
                    firedBulletIndex <= 0,
                    false);
                if (FindSpecialEffect(
                        resolvedBullet,
                        BulletEffectType.Execution) != null
                    && !enemy.IsBoss
                    && enemy.MaxHealth > 0
                    && (long)state.RemainingHealth * 4L < enemy.MaxHealth)
                {
                    attackDamage = int.MaxValue;
                }
                int transferBaseDamage = attackDamage;
    
                if (hitIndex > 0 && !IsBoardWideShot(resolvedBullet))
                {
                    ApplyGuaranteedPreviewConditionalEffects(
                        resolvedBullet,
                        BulletConditionalTrigger.Penetration,
                        state);
                }
    
                if (targetIsCritical)
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
    
                int appliedDamage = ApplyPreviewDamage(
                    state,
                    attackDamage,
                    previewColor,
                    emphasized);
                state.IsExposed = false;
                state.WasHitThisTurn = true;
    
                ApplyPreviewWallImpactDamageTransfer(
                    resolvedBullet,
                    state,
                    horizontalDirection,
                    transferBaseDamage,
                    previewColor,
                    emphasized);

                ApplyPreviewClosedCircuitDamageTransfer(
                    state,
                    horizontalDirection,
                    attackDamage,
                    previewColor,
                    emphasized);
    
                ApplyGuaranteedPreviewEffects(
                    resolvedBullet,
                    state,
                    horizontalDirection,
                    previewColor,
                    emphasized,
                    appliedDamage);
    
                ApplyGuaranteedManagedPreviewEffects(
                    resolvedBullet,
                    state,
                    previewColor,
                    emphasized);
    
                if (state.RemainingHealth <= 0)
                {
                    previewShotDefeatedEnemy = true;
                    ApplyGuaranteedPreviewConditionalEffects(
                        resolvedBullet,
                        BulletConditionalTrigger.EnemyDefeated,
                        state,
                        appliedDamage);
                }
            }
    
            SimulatePreviewReturnShot(
                resolvedBullet,
                damageMultiplier,
                guaranteedCritical,
                previewColor,
                emphasized,
                firedBulletIndex,
                previewBulletsFired,
                previewSniperBulletsFired);

            UpdatePreviewShotAbilities(
                firedBullet,
                resolvedBullet,
                shotHasCriticalOutcome,
                generatesShells,
                firedBulletIndex);
    
            if (previewBulletsFired < int.MaxValue)
            {
                previewBulletsFired++;
            }
    
            RecordPreviewShotForRemainingBullets(firedBulletIndex);
    
            if (shotHasCriticalOutcome)
            {
                previewCriticalShots++;
            }
    
            return true;
        }

        private void SimulatePreviewReturnShot(
            BulletInstance bullet,
            float damageMultiplier,
            bool guaranteedCritical,
            Color color,
            bool emphasized,
            int firedBulletIndex,
            int previewBulletsFired,
            int previewSniperBulletsFired)
        {
            BulletEffectData returnEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Monopoly);

            if (returnEffect == null || hitBuffer.Count == 0
                || previewPlayerTileIndex < 0)
            {
                return;
            }

            EnemyController turnaroundEnemy = hitBuffer[hitBuffer.Count - 1];

            if (turnaroundEnemy == null
                || !damagePreviewStates.TryGetValue(
                    turnaroundEnemy,
                    out DamagePreviewEnemyState turnaroundState)
                || turnaroundState.TileIndex < 0)
            {
                return;
            }

            int minimumTile = Mathf.Min(
                previewPlayerTileIndex,
                turnaroundState.TileIndex);
            int maximumTile = Mathf.Max(
                previewPlayerTileIndex,
                turnaroundState.TileIndex);
            List<DamagePreviewEnemyState> returnTargets =
                new List<DamagePreviewEnemyState>();

            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                if (state.Enemy != null && state.RemainingHealth > 0
                    && state.LaneIndex == previewPlayerLaneIndex
                    && state.TileIndex >= minimumTile
                    && state.TileIndex <= maximumTile)
                {
                    returnTargets.Add(state);
                }
            }

            returnTargets.Sort((first, second) =>
                Mathf.Abs(second.TileIndex - previewPlayerTileIndex)
                    .CompareTo(Mathf.Abs(
                        first.TileIndex - previewPlayerTileIndex)));
            float returnMultiplier = damageMultiplier
                * BulletEffectUtility.GetReturnDamageMultiplier(returnEffect);

            foreach (DamagePreviewEnemyState state in returnTargets)
            {
                bool targetIsCritical =
                    PlayerAttackDamageCalculator.ResolveCriticalForTarget(
                        guaranteedCritical,
                        state.IsExposed);
                int damage = CalculateAttackDamage(
                    bullet,
                    targetIsCritical,
                    returnMultiplier * GetPreviewTargetDamageMultiplier(
                        bullet,
                        state,
                        previewSniperBulletsFired),
                    previewBulletsFired,
                    firedBulletIndex <= 0,
                    false);

                if (state.StatusStacks[(int)StatusEffectType.Mark] > 0)
                {
                    damage = Mathf.CeilToInt(damage * 1.5f);
                }

                ApplyPreviewDamage(state, damage, color, emphasized);
                state.WasHitThisTurn = true;

                if (state.RemainingHealth <= 0)
                {
                    previewShotDefeatedEnemy = true;
                }
            }
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
            if (FindSpecialEffect(
                    bullet,
                    BulletEffectType.FocusedShotgun) != null)
            {
                foreach (DamagePreviewEnemyState state
                         in damagePreviewStates.Values)
                {
                    if (state.Enemy != null && state.RemainingHealth > 0)
                    {
                        return true;
                    }
                }

                return false;
            }

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

            if (forcedPreviewTarget != null
                && damagePreviewStates.TryGetValue(
                    forcedPreviewTarget,
                    out DamagePreviewEnemyState forcedState)
                && forcedState.RemainingHealth > 0)
            {
                targetBuffer.Add(forcedPreviewTarget);
                return true;
            }
    
            if (IsBoardWideShot(bullet))
            {
                foreach (DamagePreviewEnemyState state
                         in damagePreviewStates.Values)
                {
                    if (state.Enemy != null && state.RemainingHealth > 0
                        && (bullet.BulletType != BulletType.Storm
                            || BulletEffectUtility.CanStormTarget(
                                bullet,
                                previewPlayerLaneIndex,
                                state.LaneIndex,
                                state.TotalStatusStackCount)))
                    {
                        targetBuffer.Add(state.Enemy);
                    }
                }
    
                SortTargetsByTileIndex(targetBuffer);
                return targetBuffer.Count > 0;
            }

            if (BulletEffectUtility.IsAutoTargetingShot(bullet))
            {
                EnemyController target = SelectPreviewSniperTarget(bullet);

                if (target != null)
                {
                    targetBuffer.Add(target);
                }

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
                    GetPreviewShotRange(bullet),
                    out IPlayerBulletBlocker previewBlocker))
            {
                blockerDistance = Mathf.Abs(
                    previewBlocker.TileIndex - previewPlayerTileIndex);
            }
    
            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                if (state.Enemy == null || state.RemainingHealth <= 0
                    || !CanPlayerEffectTargetLane(
                        previewPlayerLaneIndex,
                        state.LaneIndex,
                        false)
                    || state.TileIndex < 0)
                {
                    continue;
                }
    
                int offset = state.TileIndex - previewPlayerTileIndex;
    
                if (offset * direction > 0
                    && Mathf.Abs(offset) <= GetPreviewShotRange(bullet)
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

        private void PrepareFocusedPreviewTarget(
            BulletInstance bullet,
            int pelletIndex)
        {
            forcedPreviewTarget = null;
            if (FindSpecialEffect(
                    bullet,
                    BulletEffectType.FocusedShotgun) == null)
            {
                return;
            }

            List<int> occupiedLanes = new List<int>();
            foreach (DamagePreviewEnemyState state in damagePreviewStates.Values)
            {
                if (state.Enemy != null && state.RemainingHealth > 0
                    && !occupiedLanes.Contains(state.LaneIndex))
                {
                    occupiedLanes.Add(state.LaneIndex);
                }
            }

            if (occupiedLanes.Count == 0)
            {
                return;
            }

            occupiedLanes.Sort();
            int preferredLane = occupiedLanes.Contains(previewPlayerLaneIndex)
                ? previewPlayerLaneIndex
                : occupiedLanes[0];
            int targetLane = preferredLane;
            if (occupiedLanes.Count > 1 && pelletIndex >= 4)
            {
                List<int> otherLanes = occupiedLanes.FindAll(
                    lane => lane != preferredLane);
                targetLane = otherLanes[(pelletIndex - 4) % otherLanes.Count];
            }

            DamagePreviewEnemyState best = null;
            foreach (DamagePreviewEnemyState state in damagePreviewStates.Values)
            {
                if (state.Enemy == null || state.RemainingHealth <= 0
                    || state.LaneIndex != targetLane)
                {
                    continue;
                }

                if (best == null
                    || GetPreviewTileDistance(state)
                        < GetPreviewTileDistance(best)
                    || GetPreviewTileDistance(state)
                        == GetPreviewTileDistance(best)
                    && state.Enemy.GetInstanceID()
                        < best.Enemy.GetInstanceID())
                {
                    best = state;
                }
            }

            forcedPreviewTarget = best == null ? null : best.Enemy;
        }

        private EnemyController SelectPreviewSniperTarget(
            BulletInstance bullet)
        {
            if (FindSpecialEffect(bullet, BulletEffectType.LockOn) != null
                && previewLastPhysicalTarget != null
                && damagePreviewStates.TryGetValue(
                    previewLastPhysicalTarget,
                    out DamagePreviewEnemyState lockedState)
                && lockedState.RemainingHealth > 0)
            {
                return previewLastPhysicalTarget;
            }

            DamagePreviewEnemyState best = null;

            foreach (DamagePreviewEnemyState candidate
                     in damagePreviewStates.Values)
            {
                if (candidate.Enemy == null || candidate.RemainingHealth <= 0)
                {
                    continue;
                }

                if ((FindSpecialEffect(bullet, BulletEffectType.Hunt) != null
                        || FindSpecialEffect(
                            bullet,
                            BulletEffectType.LockOn) != null)
                    && candidate.LaneIndex != previewPlayerLaneIndex)
                {
                    continue;
                }

                if (best == null || IsPreferredPreviewSniperTarget(
                        bullet,
                        candidate,
                        best))
                {
                    best = candidate;
                }
            }

            return best == null ? null : best.Enemy;
        }

        private EnemyController SelectLowestHealthPreviewTarget()
        {
            DamagePreviewEnemyState best = null;

            foreach (DamagePreviewEnemyState candidate
                     in damagePreviewStates.Values)
            {
                if (candidate.Enemy == null || candidate.RemainingHealth <= 0)
                {
                    continue;
                }

                if (best == null
                    || candidate.RemainingHealth < best.RemainingHealth
                    || candidate.RemainingHealth == best.RemainingHealth
                    && candidate.Enemy.GetInstanceID()
                        < best.Enemy.GetInstanceID())
                {
                    best = candidate;
                }
            }

            return best == null ? null : best.Enemy;
        }

        private bool IsPreferredPreviewSniperTarget(
            BulletInstance bullet,
            DamagePreviewEnemyState candidate,
            DamagePreviewEnemyState current)
        {
            if (FindSpecialEffect(bullet, BulletEffectType.Assassination)
                != null)
            {
                int statusComparison = candidate.TotalStatusStackCount
                    .CompareTo(current.TotalStatusStackCount);
                if (statusComparison != 0)
                {
                    return statusComparison > 0;
                }

                int healthComparison = current.RemainingHealth.CompareTo(
                    candidate.RemainingHealth);
                if (healthComparison != 0)
                {
                    return healthComparison > 0;
                }
            }
            else if (FindSpecialEffect(bullet, BulletEffectType.Mastery)
                     != null)
            {
                int maxHealthComparison = candidate.Enemy.MaxHealth.CompareTo(
                    current.Enemy.MaxHealth);
                if (maxHealthComparison != 0)
                {
                    return maxHealthComparison > 0;
                }
            }
            else if (FindSpecialEffect(bullet, BulletEffectType.Hunt) != null
                     || FindSpecialEffect(
                         bullet,
                         BulletEffectType.LockOn) != null)
            {
                int healthComparison = current.RemainingHealth.CompareTo(
                    candidate.RemainingHealth);
                if (healthComparison != 0)
                {
                    return healthComparison > 0;
                }
            }
            else if (FindSpecialEffect(
                         bullet,
                         BulletEffectType.Execution) != null)
            {
                long left = (long)candidate.RemainingHealth
                    * Mathf.Max(1, current.Enemy.MaxHealth);
                long right = (long)current.RemainingHealth
                    * Mathf.Max(1, candidate.Enemy.MaxHealth);
                if (left != right)
                {
                    return left < right;
                }
            }
            else
            {
                int candidateDistance = GetPreviewTileDistance(candidate);
                int currentDistance = GetPreviewTileDistance(current);
                int distanceComparison = candidateDistance.CompareTo(
                    currentDistance);
                if (distanceComparison != 0)
                {
                    return distanceComparison > 0;
                }
            }

            return candidate.Enemy.GetInstanceID()
                < current.Enemy.GetInstanceID();
        }

        private int GetPreviewTileDistance(DamagePreviewEnemyState state)
        {
            if (state == null || boardManager == null
                || state.TileIndex < 0 || previewPlayerTileIndex < 0)
            {
                return 0;
            }

            int playerColumn = boardManager.GetColumnIndex(
                previewPlayerTileIndex,
                previewPlayerLaneIndex);
            int enemyColumn = boardManager.GetColumnIndex(
                state.TileIndex,
                state.LaneIndex);
            return Mathf.Abs(enemyColumn - playerColumn);
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
                    && HasPreviewTargets(
                        resolvedBullet,
                        BulletEffectUtility.ResolveShotDirection(
                            resolvedBullet,
                            horizontalDirection)))
                {
                    return true;
                }

                horizontalDirection =
                    BulletEffectUtility.ResolveFacingDirectionAfterShot(
                        resolvedBullet,
                        horizontalDirection);
    
                previousResolvedBullet = resolvedBullet;
            }
    
            return false;
        }
    
        private BulletDynamicCombatContext CreatePreviewDynamicCombatContext(
            BulletInstance firedBullet,
            BulletInstance resolvedBullet,
            int firedBulletIndex,
            int initialLoadedCount,
            float temporaryDamageBonus,
            float temporaryCriticalChanceBonus)
        {
            previewRemainingLoadedBullets.Clear();

            for (int index = 0; index < firedBulletIndex; index++)
            {
                BulletInstance remainingBullet =
                    deckManager.LoadedBullets[index];

                if (remainingBullet != null)
                {
                    previewRemainingLoadedBullets.Add(remainingBullet);
                }
            }

            BulletOwnedCompositionSnapshot composition =
                BulletOwnedCompositionSnapshot.Capture(
                    firedBullet,
                    previewRemainingLoadedBullets,
                    previewOwnedBullets,
                    previewOwnedBulletTypeBuffer,
                    previewOwnedGradeCountBuffer);
            return new BulletDynamicCombatContext(
                new BulletCombatResourceSnapshot(
                    previewResources.CurrentGold,
                    previewResources.CurrentHealth,
                    previewResources.MaxHealth),
                new BulletChamberSnapshot(
                    initialLoadedCount,
                    deckManager.MaxReloadAmount,
                    true,
                    firedBulletIndex == 0,
                    resolvedBullet != firedBullet),
                new BulletRuntimeCombatSnapshot(
                    GetPreviewAbilityStacks(firedBullet),
                    GetPreviewPermanentStacks(firedBullet),
                    GetPreviewShotsObserved(firedBullet),
                    temporaryDamageBonus,
                    temporaryCriticalChanceBonus),
                composition);
        }

        private void ApplyPreviewFleshForBoneCost(BulletInstance bullet)
        {
            BulletEffectData effect = FindSpecialEffect(
                bullet,
                BulletEffectType.FleshForBone);
            int healthCost = effect == null
                ? 0
                : Mathf.Max(0, Mathf.RoundToInt(effect.Amount));

            previewResources.ApplyHealthCost(healthCost);
        }
    
        private float GetPreviewTargetDamageMultiplier(
            BulletInstance bullet,
            DamagePreviewEnemyState enemyState,
            int previewSniperBulletsFired)
        {
            int tileDistance = enemyState.TileIndex < 0
                || previewPlayerTileIndex < 0
                    ? -1
                    : Mathf.Abs(
                        enemyState.TileIndex - previewPlayerTileIndex);
            float multiplier = BulletDynamicCombatRules
                .CalculateTargetDamageMultiplier(
                bullet,
                new BulletTargetDamageContext(
                    tileDistance,
                    enemyState.TotalStatusStackCount,
                    enemyState.WasHitThisTurn,
                    previewSniperBulletsFired));
            BulletEffectData lockOnEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.LockOn);
            if (lockOnEffect != null
                && enemyState != null
                && enemyState.Enemy == previewPreviousPhysicalTarget)
            {
                multiplier *= 1f + Mathf.Max(0f, lockOnEffect.Amount) / 100f;
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
                float transferPercent =
                    BulletEffectUtility.GetWallImpactTransferPercent(
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
                        || !CanPlayerEffectTargetLane(
                            sourceState.LaneIndex,
                            targetState.LaneIndex,
                            false)
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
    
        private int ApplyPreviewDamage(
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
                return 0;
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
                    return appliedDamage;
                }
            }
    
            state.Segments.Add(
                new EnemyHealthBarFeedback.DamagePreviewSegment(
                    appliedDamage,
                    color,
                    emphasized));
            return appliedDamage;
        }

        private int GetPreviewShotRange(BulletInstance bullet)
        {
            return relicManager == null
                ? bullet == null ? 1 : bullet.MaxRange
                : relicManager.GetShotRange(bullet);
        }

        private int CountPreviewActiveEnemies()
        {
            int count = 0;

            foreach (DamagePreviewEnemyState state
                     in damagePreviewStates.Values)
            {
                if (state.Enemy != null && state.RemainingHealth > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveStatusTypes(
            DamagePreviewEnemyState state)
        {
            return state == null ? 0 : state.ActiveStatusTypeCount;
        }

        private void ApplyPreviewClosedCircuitDamageTransfer(
            DamagePreviewEnemyState sourceState,
            int horizontalDirection,
            int sourceDamage,
            Color color,
            bool emphasized)
        {
            if (sourceState == null || sourceState.TileIndex < 0
                || relicManager == null
                || !relicManager.TryGetPreviewClosedCircuitTransferDamage(
                    sourceDamage,
                    out int transferDamage))
            {
                return;
            }

            int direction = horizontalDirection >= 0 ? 1 : -1;
            DamagePreviewEnemyState target = null;
            int targetDistance = int.MaxValue;

            foreach (DamagePreviewEnemyState candidate
                     in damagePreviewStates.Values)
            {
                if (candidate == sourceState || candidate.Enemy == null
                    || candidate.RemainingHealth <= 0
                    || !CanPlayerEffectTargetLane(
                        sourceState.LaneIndex,
                        candidate.LaneIndex,
                        false)
                    || candidate.TileIndex < 0)
                {
                    continue;
                }

                int offset = (candidate.TileIndex - sourceState.TileIndex)
                    * direction;

                if (offset > 0 && offset < targetDistance)
                {
                    target = candidate;
                    targetDistance = offset;
                }
            }

            if (target == null)
            {
                return;
            }

            if (target.StatusStacks[(int)StatusEffectType.Mark] > 0)
            {
                transferDamage = Mathf.CeilToInt(transferDamage * 1.5f);
            }

            ApplyPreviewDamage(
                target,
                transferDamage,
                color,
                emphasized);
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
            bool emphasized,
            int appliedDamage)
        {
            foreach (BulletEffectData effect in bullet.Effects)
            {
                if (effect == null || effect.ActivationChance < 100f
                    || BulletEffectUtility.IsShotScoped(effect.EffectType)
                    || BulletEffectUtility.IsManagedSpecial(effect.EffectType))
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
                    applied = ApplyGuaranteedPreviewEffect(
                        effect,
                        hitState,
                        appliedDamage);
                }
    
                if (applied)
                {
                    ApplyGuaranteedPreviewConditionalEffects(
                        bullet,
                        BulletConditionalTrigger.EffectApplied,
                        hitState,
                        appliedDamage);
                }
            }
        }
    
        private bool ApplyGuaranteedPreviewEffect(
            BulletEffectData effect,
            DamagePreviewEnemyState hitState,
            int appliedDamage = 0)
        {
            if (effect.Target == BulletEffectTarget.FiringPlayer)
            {
                return ApplyGuaranteedPreviewPlayerEffect(
                    effect,
                    appliedDamage);
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

        private bool ApplyGuaranteedPreviewPlayerEffect(
            BulletEffectData effect,
            int appliedDamage)
        {
            switch (effect.EffectType)
            {
                case BulletEffectType.LifeSteal:
                    return previewResources.TryHeal(appliedDamage);
                case BulletEffectType.IncreaseMaxHealth:
                    return previewResources.TryIncreaseMaxHealth(
                        Mathf.Max(0, Mathf.RoundToInt(effect.Amount)));
                case BulletEffectType.GainGold:
                    return previewResources.TryAddGold(
                        Mathf.Max(0, Mathf.RoundToInt(effect.Amount)));
                default:
                    return false;
            }
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
                        && state.LaneIndex == pushedState.LaneIndex
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
            DamagePreviewEnemyState hitState,
            int appliedDamage = 0)
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
                        ApplyGuaranteedPreviewEffect(
                            effect,
                            hitState,
                            appliedDamage);
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
            BulletEffectData mixedEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.MixedGrade);

            if (mixedEffect != null && mixedEffect.ActivationChance >= 100f)
            {
                AddPreviewStatusMask(
                    state,
                    BulletEffectUtility.MixedGradeStatusMask,
                    Mathf.Max(0, mixedEffect.StackCount));
            }

            BulletEffectData resonanceEffect = FindSpecialEffect(
                bullet,
                BulletEffectType.Resonance);

            if (resonanceEffect != null
                && resonanceEffect.ActivationChance >= 100f)
            {
                AddPreviewStatusMask(
                    state,
                    activeResonanceStatusMask,
                    Mathf.Max(0, resonanceEffect.StackCount));
            }

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

        private static void AddPreviewStatusMask(
            DamagePreviewEnemyState state,
            int statusMask,
            int stacks)
        {
            if (state == null || stacks <= 0)
            {
                return;
            }

            for (int index = 0;
                 index < StatusEffectController.StackableStatusTypeCount;
                 index++)
            {
                StatusEffectType statusType =
                    StatusEffectController.GetStackableStatusType(index);

                if (BulletEffectUtility.IncludesStatus(statusMask, statusType))
                {
                    AddPreviewStatusStacks(state, statusType, stacks);
                }
            }
        }

        private static void AddPreviewStatusStacks(
            DamagePreviewEnemyState state,
            StatusEffectType statusType,
            int stacks)
        {
            if (state == null || stacks <= 0)
            {
                return;
            }

            int index = (int)statusType;
            long combined = (long)state.StatusStacks[index] + stacks;
            state.StatusStacks[index] = combined >= int.MaxValue
                ? int.MaxValue
                : (int)combined;
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

        private void ApplyPreviewPostBulletAbilities(
            BulletInstance firedBullet,
            BulletInstance resolvedBullet)
        {
            BulletEffectData coagulationEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Coagulation);

            if (coagulationEffect != null)
            {
                int bloodCount = 0;

                foreach (BulletInstance bullet in previewOwnedBullets)
                {
                    if (bullet != null && bullet.BulletType == BulletType.Blood)
                    {
                        bloodCount++;
                    }
                }

                float recoveryPercent = BulletEffectUtility
                    .GetCoagulationRecoveryPercent(
                        coagulationEffect,
                        bloodCount);
                int missingHealth = Mathf.Max(
                    0,
                    previewResources.MaxHealth
                        - previewResources.CurrentHealth);
                previewResources.TryHeal(Mathf.CeilToInt(
                    missingHealth * recoveryPercent / 100f));
            }

            BulletEffectData ritualEffect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.Ritual);

            if (ritualEffect != null
                && previewResources.TrySpendMaxHealth(
                    Mathf.Max(1, ritualEffect.StackCount)))
            {
                previewPermanentStacks[firedBullet] =
                    BulletEffectUtility.SaturatingAdd(
                        GetPreviewPermanentStacks(firedBullet),
                        1);
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
    
        private void GrantPreviewLegacyStacks(BulletInstance destroyedBullet)
        {
            foreach (BulletInstance bullet in previewOwnedBullets)
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
                previewCriticalDamageMultiplierBonus);
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
    }
}
