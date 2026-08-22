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
        private int previewPlayerTileIndex = -1;
        private float previewCriticalDamageMultiplierBonus;

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
            previewCriticalDamageMultiplierBonus = 0f;
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
                    state.WasHitThisTurn = owner.firingSequence != null
                        && owner.firingSequence.WasEnemyHitThisTurn(enemy);
    
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
            float spreadDamageBonus = 0f;
            float concentrationCriticalChanceBonus = 0f;
            float pendingCriticalDamageMultiplierBonus = 0f;
            BulletInstance previousResolvedBullet = null;
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
    
                    GrantPreviewLegacyStacks(firedBullet);
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
    
                float damageMultiplier = GetPreviewSpecialDamageMultiplier(
                    firedBullet,
                    resolvedBullet,
                    bulletIndex,
                    initialLoadedCount,
                    previewBulletsFired);
                damageMultiplier *= 1f + spreadDamageBonus;
                previewCriticalDamageMultiplierBonus =
                    pendingCriticalDamageMultiplierBonus;
                pendingCriticalDamageMultiplierBonus = 0f;
                relicManager ??= FindFirstObjectByType<RelicManager>(
                    FindObjectsInactive.Include);
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
                                    playerHealth.CurrentHealth,
                                    playerHealth.MaxHealth));
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
                        resolvedBullet)
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
    
                SimulatePreviewShot(
                    resolvedBullet,
                    firedBullet,
                    shotDirection,
                    damageMultiplier,
                    guaranteedCritical,
                    true,
                    emphasized,
                    bulletIndex,
                    ref previewBulletsFired,
                    ref previewCriticalShots);

                for (int shotgunShotIndex = 1;
                     shotgunShotIndex < resolvedBullet.ShotCount;
                     shotgunShotIndex++)
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
                            ref previewBulletsFired,
                            ref previewCriticalShots))
                    {
                        break;
                    }
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
                            shotDirection,
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
                targetMultiplier *= (float)(relicManager == null
                    ? 1d
                    : relicManager
                        .GetPreviewTargetConditionalDamageMultiplier(
                            CountActiveStatusTypes(state),
                            CountPreviewActiveEnemies()));
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
                BulletEffectType.Seismometer);

            if (effect != null)
            {
                multiplier *= 1f + GetPreviewAbilityStacks(firedBullet)
                    * Mathf.Max(0f, effect.Amount) / 100f;
            }

            effect = FindSpecialEffect(
                resolvedBullet,
                BulletEffectType.HighRoller);

            if (effect != null && playerHealth != null)
            {
                multiplier *=
                    BulletEffectUtility.GetMissingHealthDamageMultiplier(
                        playerHealth.CurrentHealth,
                        playerHealth.MaxHealth,
                        effect.Amount);
            }

            effect = FindSpecialEffect(
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

            effect = FindSpecialEffect(
                bullet,
                BulletEffectType.Assassination);

            if (effect != null && enemyState.WasHitThisTurn)
            {
                multiplier *= 1f + Mathf.Max(0f, effect.Amount) / 100f;
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
            if (state == null || state.StatusStacks == null)
            {
                return 0;
            }

            int count = 0;

            foreach (int stacks in state.StatusStacks)
            {
                if (stacks > 0)
                {
                    count++;
                }
            }

            return count;
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
            bool emphasized)
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
            foreach (BulletInstance bullet in deckManager.LoadedBullets)
            {
                BulletEffectData ritualEffect = FindSpecialEffect(
                    bullet,
                    BulletEffectType.Ritual);

                if (bullet == null || ritualEffect == null)
                {
                    continue;
                }

                previewAbilityStacks[bullet] = guaranteedCritical
                    ? BulletEffectUtility.SaturatingAdd(
                        GetPreviewAbilityStacks(bullet),
                        Mathf.Max(1, ritualEffect.StackCount))
                    : 0;
            }

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

        private float GetPreviewRitualCriticalDamageMultiplierBonus()
        {
            double bonus = 0d;

            foreach (BulletInstance bullet in deckManager.LoadedBullets)
            {
                BulletEffectData effect = FindSpecialEffect(
                    bullet,
                    BulletEffectType.Ritual);

                if (bullet != null && effect != null)
                {
                    bonus += GetPreviewAbilityStacks(bullet)
                        * Mathf.Max(0f, effect.Amount);
                }
            }

            return bonus >= float.MaxValue ? float.MaxValue : (float)bonus;
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
                previewCriticalDamageMultiplierBonus
                    + GetPreviewRitualCriticalDamageMultiplierBonus());
        }

        private int CountDistinctOwnedBulletTypes()
        {
            return owner.CountDistinctOwnedBulletTypes();
        }

        private int CountOwnedBulletsByGrade(
            BulletGrade first,
            BulletGrade second)
        {
            return owner.CountOwnedBulletsByGrade(first, second);
        }

        private int GetMostCommonOwnedGradeCount()
        {
            return owner.GetMostCommonOwnedGradeCount();
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
