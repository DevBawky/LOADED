using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public sealed class RelicManager : MonoBehaviour
{
    public const int MaximumRelicCount = 8;

    [SerializeField] private RelicData[] relicCatalog =
        Array.Empty<RelicData>();
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private List<RelicInstance> ownedRelics =
        new List<RelicInstance>();

    private int nextAcquisitionOrder;
    private readonly Dictionary<RelicInstance, int>
        movementStacksConsumedByShot =
            new Dictionary<RelicInstance, int>();
    private readonly Dictionary<RelicInstance, int>
        movementStacksConsumedByCylinder =
            new Dictionary<RelicInstance, int>();
    private bool isShotActive;
    private double activeShotDamageMultiplier = 1d;
    private bool cylinderActive;
    private bool activeShotCountsForRelics;
    private bool activeShotDefeatedEnemy;
    private bool activeShotForcesCritical;
    private int activePhysicalBulletIndex = -1;
    private int luckyChamberBulletIndex = -1;
    private int luckyChamberSelectionLoadedCount;
    private int circuitShotCount;
    private int circuitReloadCount;
    private int brinkFailureCount;
    private bool brinkTriggeredThisCylinder;
    private long currentCylinderHealthLost;
    private int currentCylinderMaxHealth;
    private double activeCylinderScaleDamagePercent;
    private double activeMemorialShotMultiplier;
    private long stormStrongestSingleDamage;
    private bool stormTriggeredThisCylinder;
    private readonly HashSet<int> stormRequiredEnemyIds =
        new HashSet<int>();
    private readonly HashSet<int> stormDamagedEnemyIds =
        new HashSet<int>();
    private readonly HashSet<int> processedEnemyDefeatIds =
        new HashSet<int>();
    private readonly HashSet<int> circuitReloadedBulletOrders =
        new HashSet<int>();
    private readonly List<int> pendingHolsterBulletOrders =
        new List<int>();
    private readonly Dictionary<int, double> activeTargetDamageMultipliers =
        new Dictionary<int, double>();

    public event Action InventoryChanged;
    public event Action<RelicInstance, RelicEffectData> RelicTriggered;
    public event Action<RelicInstance, RelicRemovalReason> RelicRemoved;
    public event Action<RelicCombatEventContext> CombatEventRaised;
    public event Action LuckyChamberSelectionChanged;
    public event Action<RelicInstance, double> RelicProbabilityEvaluated;

    public IReadOnlyList<RelicInstance> OwnedRelics => ownedRelics;
    public int Count => ownedRelics.Count;
    public bool IsFull => Count >= MaximumRelicCount;
    public bool CurrentShotForcesCritical => activeShotForcesCritical;
    public int LuckyChamberBulletIndex => luckyChamberBulletIndex;

    public bool IsLuckyChamberShot(int shotOrderIndex)
    {
        return shotOrderIndex >= 0
            && shotOrderIndex == luckyChamberBulletIndex;
    }

    public bool IsLuckyChamberLoadedBullet(
        int loadedBulletIndex,
        int initialLoadedCount)
    {
        if (loadedBulletIndex < 0 || initialLoadedCount <= 0
            || luckyChamberBulletIndex < 0)
        {
            return false;
        }

        // LoadedBullets fires from the highest index down, while the relic
        // records its choice as a zero-based firing-order index.
        int selectedLoadedIndex = initialLoadedCount
            - 1
            - luckyChamberBulletIndex;
        return loadedBulletIndex == selectedLoadedIndex;
    }

    public void EnsureLuckyChamberSelection(int loadedBulletCount)
    {
        int validLoadedCount = Mathf.Max(0, loadedBulletCount);
        bool hasLuckyChamber = validLoadedCount > 0
            && FindFirstEffect(RelicEffectType.LuckyChamber, out _, out _);

        if (!hasLuckyChamber)
        {
            bool changed = luckyChamberBulletIndex >= 0
                || luckyChamberSelectionLoadedCount != 0;
            luckyChamberBulletIndex = -1;
            luckyChamberSelectionLoadedCount = 0;
            if (changed)
            {
                LuckyChamberSelectionChanged?.Invoke();
                InventoryChanged?.Invoke();
            }
            return;
        }

        if (luckyChamberSelectionLoadedCount == validLoadedCount
            && luckyChamberBulletIndex >= 0
            && luckyChamberBulletIndex < validLoadedCount)
        {
            return;
        }

        luckyChamberSelectionLoadedCount = validLoadedCount;
        luckyChamberBulletIndex = UnityEngine.Random.Range(
            0,
            validLoadedCount);
        LuckyChamberSelectionChanged?.Invoke();
        InventoryChanged?.Invoke();
    }

    public string GetLuckyChamberBulletTooltip()
    {
        if (!FindFirstEffect(
                RelicEffectType.LuckyChamber,
                out _,
                out RelicEffectData effect))
        {
            return string.Empty;
        }

        double bonusPercent = Math.Max(
            0d,
            (effect.FinalDamageMultiplier - 1d) * 100d);
        return "<color=#58E879><b>행운의 약실 적용</b></color>\n"
            + $"<color=#A8F5B8>최종 피해 +{bonusPercent:0.#}%</color>";
    }

    public string GetRelicStatusText(RelicInstance relic)
    {
        if (relic?.Data == null)
        {
            return string.Empty;
        }

        foreach (RelicEffectData effect in relic.Data.Effects)
        {
            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case RelicEffectType.CrackedPrimer:
                    return FormatStatusNumber(Math.Min(
                        100d,
                        effect.PrimerBaseChance
                            + relic.PrimaryCounter
                            * effect.PrimerFailureChanceBonus)) + "%";
                case RelicEffectType.MovementDamageMultiplier:
                    return relic.MovementStacks > 0
                        ? relic.MovementStacks.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty;
                case RelicEffectType.LuckyChamber:
                    return string.Empty;
                case RelicEffectType.ClosedCircuit:
                    int remainingShots = circuitReloadCount
                            >= effect.CircuitMaxReloadsPerCylinder
                        ? 0
                        : Math.Max(
                            0,
                            effect.CircuitShotThreshold - circuitShotCount);
                    return remainingShots.ToString(CultureInfo.InvariantCulture);
                case RelicEffectType.ExecutionersOath:
                    int maximumStage = effect.ExecutionDamageMultipliers.Count;
                    int stage = Mathf.Min(relic.PrimaryCounter, maximumStage);
                    return stage.ToString(CultureInfo.InvariantCulture);
                case RelicEffectType.Carriage:
                    return relic.SecondaryCounter > 0
                        ? relic.SecondaryCounter.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty;
                case RelicEffectType.GoldPanner:
                    return relic.PrimaryCounter.ToString(
                        CultureInfo.InvariantCulture);
            }
        }

        return relic.Data.CanStack && relic.StackCount > 0
            ? relic.StackCount.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public bool TryGetLoadedBulletRelicModifiers(
        int loadedBulletIndex,
        int currentLoadedCount,
        int initialLoadedCount,
        out double damageMultiplier,
        out bool forcesCritical,
        List<string> stateLines = null)
    {
        damageMultiplier = 1d;
        forcesCritical = false;

        if (loadedBulletIndex < 0
            || loadedBulletIndex >= currentLoadedCount
            || initialLoadedCount <= 0)
        {
            return false;
        }

        bool enhanced = false;
        bool isNextShot = loadedBulletIndex == currentLoadedCount - 1;
        bool isFirstShot = loadedBulletIndex == initialLoadedCount - 1;
        bool isLastShot = loadedBulletIndex == 0;

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic?.Data == null || relic.IsSpent)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                double effectMultiplier = 1d;
                bool applies = false;
                bool guaranteesCritical = false;

                switch (effect.EffectType)
                {
                    case RelicEffectType.MovementDamageMultiplier:
                        applies = relic.MovementStacks > 0
                            && (effect.MovementStackReset
                                    != RelicMovementStackReset.AfterShot
                                || isNextShot);
                        if (applies)
                        {
                            int exponent = SaturatingMultiply(
                                relic.MovementStacks,
                                relic.StackCount);
                            effectMultiplier = Math.Pow(
                                effect.MovementDamageMultiplierPerStack,
                                exponent);
                        }
                        break;
                    case RelicEffectType.FirstShotFinalMultiplier:
                        applies = isFirstShot;
                        effectMultiplier = Math.Pow(
                            effect.FinalDamageMultiplier,
                            relic.StackCount);
                        break;
                    case RelicEffectType.LastShotFinalMultiplier:
                        applies = isLastShot;
                        effectMultiplier = Math.Pow(
                            effect.FinalDamageMultiplier,
                            relic.StackCount);
                        break;
                    case RelicEffectType.Scale:
                        double scaleDamagePercent = activeCylinderScaleDamagePercent
                            > 0d
                                ? activeCylinderScaleDamagePercent
                                : relic.StoredValue;
                        applies = scaleDamagePercent > 0d;
                        effectMultiplier = 1d
                            + scaleDamagePercent / 100d;
                        break;
                    case RelicEffectType.LuckyChamber:
                        applies = IsLuckyChamberLoadedBullet(
                            loadedBulletIndex,
                            initialLoadedCount);
                        effectMultiplier = effect.FinalDamageMultiplier;
                        break;
                    case RelicEffectType.ExecutionersOath:
                        applies = isNextShot && relic.PrimaryCounter > 0;
                        effectMultiplier = effect.GetExecutionMultiplier(
                            relic.PrimaryCounter);
                        break;
                    case RelicEffectType.GoldPanner:
                        applies = isNextShot
                            && relic.PrimaryCounter >= effect.NuggetsRequired;
                        effectMultiplier = effect.FinalDamageMultiplier;
                        guaranteesCritical = applies;
                        break;
                }

                if (!applies
                    || effectMultiplier <= 1d && !guaranteesCritical)
                {
                    continue;
                }

                enhanced = true;
                damageMultiplier = MultiplyMultiplier(
                    damageMultiplier,
                    effectMultiplier);
                forcesCritical |= guaranteesCritical;

                if (stateLines != null)
                {
                    string detail = guaranteesCritical
                        ? effectMultiplier > 1d
                            ? "치명타 확정, 최종 피해 x"
                                + FormatStatusNumber(effectMultiplier)
                            : "치명타 확정"
                        : $"최종 피해 x{FormatStatusNumber(effectMultiplier)}";
                    stateLines.Add($"{relic.Data.DisplayName}: {detail}");
                }
            }
        }

        return enhanced;
    }

    private void Awake()
    {
        ownedRelics ??= new List<RelicInstance>();
        BindPlayerMove(playerMove != null
            ? playerMove
            : FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include));
        RecalculateNextAcquisitionOrder();
    }

    private void OnDestroy()
    {
        BindPlayerMove(null);
    }

    public void BindPlayerMove(PlayerMove value)
    {
        if (playerMove != null)
        {
            playerMove.PlayerMoved -= HandlePlayerMoved;
        }

        playerMove = value;

        if (playerMove != null)
        {
            playerMove.PlayerMoved -= HandlePlayerMoved;
            playerMove.PlayerMoved += HandlePlayerMoved;
        }
    }

    public RelicAcquireResult TryAcquire(RelicData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Id))
        {
            return RelicAcquireResult.InvalidData;
        }

        RelicInstance duplicate = FindOwned(data.Id);

        if (duplicate != null)
        {
            if (duplicate.TryAddStack())
            {
                InventoryChanged?.Invoke();
                return RelicAcquireResult.Stacked;
            }

            return RelicAcquireResult.Duplicate;
        }

        if (IsFull)
        {
            return RelicAcquireResult.InventoryFull;
        }

        ownedRelics.Add(new RelicInstance(data, nextAcquisitionOrder++));
        InventoryChanged?.Invoke();
        return RelicAcquireResult.Acquired;
    }

    public RelicAcquireResult TryReplace(int index, RelicData replacement)
    {
        if (index < 0 || index >= ownedRelics.Count || replacement == null
            || string.IsNullOrWhiteSpace(replacement.Id))
        {
            return RelicAcquireResult.InvalidData;
        }

        RelicInstance duplicate = FindOwned(replacement.Id);

        if (duplicate != null && !ReferenceEquals(duplicate, ownedRelics[index]))
        {
            return RelicAcquireResult.Duplicate;
        }

        RelicInstance removed = ownedRelics[index];
        ownedRelics[index] = new RelicInstance(
            replacement,
            nextAcquisitionOrder++);
        RelicRemoved?.Invoke(removed, RelicRemovalReason.Replaced);
        InventoryChanged?.Invoke();
        return RelicAcquireResult.Acquired;
    }

    public bool TryRemoveAt(
        int index,
        RelicRemovalReason reason = RelicRemovalReason.Removed)
    {
        if (index < 0 || index >= ownedRelics.Count)
        {
            return false;
        }

        RelicInstance removed = ownedRelics[index];
        ownedRelics.RemoveAt(index);
        RelicRemoved?.Invoke(removed, reason);
        InventoryChanged?.Invoke();
        return true;
    }

    public RelicInstance FindOwned(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
        {
            return null;
        }

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic != null && string.Equals(
                    relic.Id,
                    relicId,
                    StringComparison.Ordinal))
            {
                return relic;
            }
        }

        return null;
    }

    public void GetUniformRewardChoices(
        int requestedCount,
        List<RelicData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        if (requestedCount <= 0)
        {
            return;
        }

        List<RelicData> pool = BuildAvailableRewardPool();
        int choiceCount = Mathf.Min(requestedCount, pool.Count);

        // Partial Fisher-Yates shuffle: every available relic has the same
        // probability and a reward screen never repeats the same relic.
        for (int index = 0; index < choiceCount; index++)
        {
            int selectedIndex = UnityEngine.Random.Range(index, pool.Count);
            (pool[index], pool[selectedIndex]) =
                (pool[selectedIndex], pool[index]);
            destination.Add(pool[index]);
        }
    }

    public void BeginBattle()
    {
        ClearShotSnapshot();
        ResetCylinderRuntime();
        processedEnemyDefeatIds.Clear();
        movementStacksConsumedByCylinder.Clear();

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic?.Data == null)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                switch (effect.EffectType)
                {
                    case RelicEffectType.FamilyWill:
                        relic.SetRuntimeFlag(true);
                        break;
                }
            }
        }

        InventoryChanged?.Invoke();
        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.BattleStarted));
    }

    public void ResumeBattle()
    {
        ClearShotSnapshot();
        ResetCylinderRuntime();
        processedEnemyDefeatIds.Clear();
    }

    public void EndBattle()
    {
        // The final enemy can complete the battle before PlayerShoot finishes
        // the current shot. Preserve that shot's defeat result until
        // NotifyShotCompleted updates effects such as Executioner's Oath.
        if (!isShotActive)
        {
            ClearShotSnapshot();
        }
        ResetCylinderRuntime();
        movementStacksConsumedByCylinder.Clear();
        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.BattleEnded));
    }

    public void NotifyCylinderStarted(
        int loadedBulletCount,
        IReadOnlyList<EnemyController> enemies,
        int currentHealth,
        int maxHealth)
    {
        cylinderActive = true;
        circuitShotCount = 0;
        circuitReloadCount = 0;
        brinkFailureCount = 0;
        brinkTriggeredThisCylinder = false;
        currentCylinderHealthLost = 0L;
        currentCylinderMaxHealth = Mathf.Max(1, maxHealth);
        circuitReloadedBulletOrders.Clear();
        stormRequiredEnemyIds.Clear();
        stormDamagedEnemyIds.Clear();
        stormStrongestSingleDamage = 0L;
        stormTriggeredThisCylinder = false;
        activeCylinderScaleDamagePercent = 0d;
        activeMemorialShotMultiplier = 0d;
        EnsureLuckyChamberSelection(loadedBulletCount);

        if (enemies != null)
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null && enemy.CurrentHealth > 0)
                {
                    stormRequiredEnemyIds.Add(enemy.GetInstanceID());
                }
            }
        }

        if (FindFirstEffect(
                RelicEffectType.Scale,
                out RelicInstance scaleRelic,
                out _))
        {
            activeCylinderScaleDamagePercent = scaleRelic.StoredValue;
            scaleRelic.SetStoredValue(0d);
        }

        if (FindFirstEffect(
                RelicEffectType.FamilyWill,
                out RelicInstance familyRelic,
                out RelicEffectData familyEffect)
            && familyRelic.RuntimeFlag && familyRelic.PrimaryCounter > 0)
        {
            double memorialPercent = Math.Min(
                familyEffect.MemorialMaximumDamagePercent,
                familyRelic.PrimaryCounter
                    * familyEffect.MemorialDamagePercentPerBullet);
            activeMemorialShotMultiplier = Math.Max(
                0d,
                memorialPercent / 100d);
        }

        currentCylinderMaxHealth = Mathf.Max(
            currentCylinderMaxHealth,
            currentHealth);
        InventoryChanged?.Invoke();
    }

    public void NotifyShotStarted(
        bool isBaseBullet = true,
        bool isRelicGenerated = false,
        int physicalBulletIndex = -1,
        int currentHealth = 0,
        int maxHealth = 1,
        bool isFirstLoadedShot = false,
        bool isLastLoadedShot = false)
    {
        movementStacksConsumedByShot.Clear();
        activeTargetDamageMultipliers.Clear();
        activeShotCountsForRelics = !isRelicGenerated;
        activeShotDefeatedEnemy = false;
        activeShotForcesCritical = false;
        activePhysicalBulletIndex = physicalBulletIndex;

        if (!isRelicGenerated)
        {
            foreach (RelicInstance relic in ownedRelics)
            {
                if (relic == null || relic.Data == null
                    || relic.MovementStacks <= 0)
                {
                    continue;
                }

                foreach (RelicEffectData effect in relic.Data.Effects)
                {
                    if (effect != null && effect.EffectType
                        == RelicEffectType.MovementDamageMultiplier
                        && effect.MovementStackReset
                            == RelicMovementStackReset.AfterShot)
                    {
                        movementStacksConsumedByShot[relic] =
                            relic.MovementStacks;
                        break;
                    }

                    if (effect != null && effect.EffectType
                        == RelicEffectType.MovementDamageMultiplier
                        && effect.MovementStackReset
                            == RelicMovementStackReset.AfterCylinder)
                    {
                        movementStacksConsumedByCylinder.TryGetValue(
                            relic,
                            out int previousConsumedStacks);
                        movementStacksConsumedByCylinder[relic] = Mathf.Max(
                            previousConsumedStacks,
                            relic.MovementStacks);
                        break;
                    }
                }
            }
        }

        activeShotDamageMultiplier = isRelicGenerated
            ? 1d
            : CalculateOutgoingAttackDamageMultiplier();

        if (!isRelicGenerated)
        {
            ApplyShotStartRelicModifiers(
                isBaseBullet,
                physicalBulletIndex,
                currentHealth,
                maxHealth);
            NotifyShotStartActivations(
                isBaseBullet,
                isFirstLoadedShot,
                isLastLoadedShot);
        }

        isShotActive = true;
        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.ShotStarted));
    }

    public void NotifyShotCompleted()
    {
        bool consumedMovement = movementStacksConsumedByShot.Count > 0;
        bool oathStageChanged = false;

        foreach (KeyValuePair<RelicInstance, int> entry
                 in movementStacksConsumedByShot)
        {
            entry.Key?.ConsumeMovementStacks(entry.Value);
        }

        if (activeShotCountsForRelics
            && FindFirstEffect(
                RelicEffectType.ExecutionersOath,
                out RelicInstance oathRelic,
                out _))
        {
            int previousStage = oathRelic.PrimaryCounter;
            oathRelic.SetPrimaryCounter(activeShotDefeatedEnemy
                ? oathRelic.PrimaryCounter == int.MaxValue
                    ? int.MaxValue
                    : oathRelic.PrimaryCounter + 1
                : 0);
            oathStageChanged = oathRelic.PrimaryCounter != previousStage;
        }

        ClearShotSnapshot();

        if (consumedMovement || oathStageChanged)
        {
            InventoryChanged?.Invoke();
        }

        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.ShotCompleted));
    }

    public void NotifyShotCancelled()
    {
        ClearShotSnapshot();
    }

    public void NotifyCylinderCompleted(DeckManager deckManager = null)
    {
        bool consumedMovement = movementStacksConsumedByCylinder.Count > 0;

        foreach (KeyValuePair<RelicInstance, int> entry
                 in movementStacksConsumedByCylinder)
        {
            entry.Key?.ConsumeMovementStacks(entry.Value);
        }

        movementStacksConsumedByCylinder.Clear();

        if (FindFirstEffect(
                RelicEffectType.Scale,
                out RelicInstance scaleRelic,
                out RelicEffectData scaleEffect))
        {
            double lostPercent = currentCylinderMaxHealth <= 0
                ? 0d
                : Math.Min(
                    scaleEffect.ScaleMaximumDamagePercent,
                    currentCylinderHealthLost * 100d
                        / currentCylinderMaxHealth);
            scaleRelic.SetStoredValue(lostPercent);
        }

        if (FindFirstEffect(
                RelicEffectType.FamilyWill,
                out RelicInstance familyRelic,
                out _)
            && familyRelic.RuntimeFlag)
        {
            familyRelic.SetRuntimeFlag(false);
        }

        if (deckManager != null && pendingHolsterBulletOrders.Count > 0)
        {
            foreach (int acquisitionOrder in pendingHolsterBulletOrders)
            {
                deckManager.QueueBulletForNextReload(acquisitionOrder);
            }
        }

        pendingHolsterBulletOrders.Clear();
        ResetCylinderRuntime();

        InventoryChanged?.Invoke();

        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.CylinderCompleted));
    }

    public double GetOutgoingAttackDamageMultiplier()
    {
        return isShotActive
            ? activeShotDamageMultiplier
            : CalculateOutgoingAttackDamageMultiplier();
    }

    public double GetConditionalFinalDamageMultiplier(
        bool isFirstShot,
        bool isLastLoadedShot)
    {
        double multiplier = GetOutgoingAttackDamageMultiplier();

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic == null || relic.Data == null)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                bool applies = effect.EffectType switch
                {
                    RelicEffectType.FirstShotFinalMultiplier => isFirstShot,
                    RelicEffectType.LastShotFinalMultiplier => isLastLoadedShot,
                    _ => false
                };

                if (!applies)
                {
                    continue;
                }

                multiplier *= Math.Pow(
                    effect.FinalDamageMultiplier,
                    relic.StackCount);

                if (double.IsInfinity(multiplier)
                    || multiplier >= double.MaxValue)
                {
                    return double.MaxValue;
                }
            }
        }

        return double.IsNaN(multiplier) ? 1d : Math.Max(0d, multiplier);
    }

    public double GetTargetConditionalDamageMultiplier(
        int targetInstanceId,
        int activeDebuffTypeCount)
    {
        if (!isShotActive || !activeShotCountsForRelics
            || targetInstanceId == 0 || activeDebuffTypeCount <= 0)
        {
            return 1d;
        }

        if (activeTargetDamageMultipliers.TryGetValue(
                targetInstanceId,
                out double cachedMultiplier))
        {
            return cachedMultiplier;
        }

        double multiplier = 1d;

        if (FindFirstEffect(
                RelicEffectType.MutationCatalyst,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            double chance = Math.Min(
                effect.MutationMaximumChance,
                activeDebuffTypeCount
                    * effect.MutationChancePerDebuffType);
            NotifyProbabilityEvaluated(relic, chance);

            if (RollPercent(chance))
            {
                multiplier = effect.FinalDamageMultiplier;
                Trigger(relic, effect);
            }
        }

        activeTargetDamageMultipliers[targetInstanceId] = multiplier;
        return multiplier;
    }

    public double GetMemorialExtraShotMultiplier()
    {
        return cylinderActive ? activeMemorialShotMultiplier : 0d;
    }

    public void NotifyMemorialShotTriggered()
    {
        if (cylinderActive && activeMemorialShotMultiplier > 0d
            && FindFirstEffect(
                RelicEffectType.FamilyWill,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            Trigger(relic, effect);
        }
    }

    public bool ShouldReloadConsumeTurn(
        BulletInstance loadedBullet,
        bool wasCylinderEmpty)
    {
        if (loadedBullet == null)
        {
            return false;
        }

        bool holsterReload = FindFirstEffect(
                RelicEffectType.PredatorHolster,
                out RelicInstance holsterRelic,
                out RelicEffectData holsterEffect)
            && holsterRelic.RemoveTrackedBullet(
                loadedBullet.AcquisitionOrder);
        if (holsterReload)
        {
            Trigger(holsterRelic, holsterEffect);
            InventoryChanged?.Invoke();
        }

        // 탄환 자체의 무료 장전 능력과 겹치더라도 홀스터 표식은 이번 장전에 소모한다.
        if (loadedBullet.DoesNotConsumeTurn || holsterReload)
        {
            return false;
        }

        if (wasCylinderEmpty
            && FindFirstEffect(
                RelicEffectType.EmptyBeat,
                out RelicInstance emptyBeatRelic,
                out RelicEffectData emptyBeatEffect))
        {
            Trigger(emptyBeatRelic, emptyBeatEffect);
            return false;
        }

        if (FindFirstEffect(
                RelicEffectType.Carriage,
                out RelicInstance carriageRelic,
                out _)
            && carriageRelic.TryConsumeSecondaryCounter())
        {
            InventoryChanged?.Invoke();
            return false;
        }

        return true;
    }

    public bool TryTriggerClosedCircuit(
        DeckManager deckManager,
        BulletInstance firedBullet)
    {
        if (!cylinderActive || !activeShotCountsForRelics
            || deckManager == null || firedBullet == null
            || circuitReloadedBulletOrders.Contains(
                firedBullet.AcquisitionOrder)
            || !FindFirstEffect(
                RelicEffectType.ClosedCircuit,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            return false;
        }

        circuitShotCount = circuitShotCount == int.MaxValue
            ? int.MaxValue
            : circuitShotCount + 1;

        if (circuitShotCount < effect.CircuitShotThreshold
            || circuitReloadCount >= effect.CircuitMaxReloadsPerCylinder
            || !deckManager.TryReloadOldestUsed(
                out BulletInstance reloadedBullet))
        {
            InventoryChanged?.Invoke();
            return false;
        }

        circuitShotCount -= effect.CircuitShotThreshold;
        circuitReloadCount++;
        circuitReloadedBulletOrders.Add(reloadedBullet.AcquisitionOrder);
        Trigger(relic, effect);
        InventoryChanged?.Invoke();
        return true;
    }

    public void NotifyEnemyDamaged(EnemyController enemy, int damage)
    {
        if (!cylinderActive || enemy == null || damage <= 0)
        {
            return;
        }

        stormDamagedEnemyIds.Add(enemy.GetInstanceID());
        stormStrongestSingleDamage = Math.Max(
            stormStrongestSingleDamage,
            damage);
    }

    public bool TryConsumeEyeOfTheStormDamage(out int damage)
    {
        damage = 0;

        if (!cylinderActive || stormTriggeredThisCylinder
            || stormRequiredEnemyIds.Count == 0
            || stormStrongestSingleDamage <= 0L
            || !stormRequiredEnemyIds.IsSubsetOf(stormDamagedEnemyIds)
            || !FindFirstEffect(
                RelicEffectType.EyeOfTheStorm,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            return false;
        }

        double result = Math.Ceiling(
            stormStrongestSingleDamage * effect.StormDamagePercent / 100d);
        damage = double.IsInfinity(result) || result >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Max(0d, result);
        stormTriggeredThisCylinder = damage > 0;

        if (stormTriggeredThisCylinder)
        {
            Trigger(relic, effect);
        }

        return stormTriggeredThisCylinder;
    }

    public void NotifyEnemyDefeated(
        EnemyController defeatedEnemy,
        BulletInstance killingBullet,
        IReadOnlyList<EnemyController> activeEnemies,
        BoardManager boardManager)
    {
        if (defeatedEnemy == null)
        {
            return;
        }

        if (isShotActive && activeShotCountsForRelics)
        {
            activeShotDefeatedEnemy = true;
        }

        if (!processedEnemyDefeatIds.Add(defeatedEnemy.GetInstanceID()))
        {
            return;
        }

        if (killingBullet != null
            && FindFirstEffect(
                RelicEffectType.PredatorHolster,
                out RelicInstance holsterRelic,
                out RelicEffectData holsterEffect))
        {
            holsterRelic.AddTrackedBullet(killingBullet.AcquisitionOrder);
            if (!pendingHolsterBulletOrders.Contains(
                    killingBullet.AcquisitionOrder))
            {
                pendingHolsterBulletOrders.Add(
                    killingBullet.AcquisitionOrder);
            }
            Trigger(holsterRelic, holsterEffect);
        }

        TransferDefeatedEnemyDebuffs(
            defeatedEnemy,
            activeEnemies,
            boardManager);
        InventoryChanged?.Invoke();
    }

    public void NotifyBulletDestroyed(BulletInstance destroyedBullet)
    {
        if (destroyedBullet == null)
        {
            return;
        }

        int acquisitionOrder = destroyedBullet.AcquisitionOrder;
        circuitReloadedBulletOrders.Remove(acquisitionOrder);
        pendingHolsterBulletOrders.Remove(acquisitionOrder);

        if (FindFirstEffect(
                RelicEffectType.PredatorHolster,
                out RelicInstance holsterRelic,
                out _))
        {
            holsterRelic.RemoveTrackedBullet(acquisitionOrder);
        }

        if (FindFirstEffect(
                RelicEffectType.FamilyWill,
                out RelicInstance familyRelic,
                out RelicEffectData familyEffect))
        {
            familyRelic.AddPrimaryCounter(1);
            Trigger(familyRelic, familyEffect);
        }

        InventoryChanged?.Invoke();
    }

    public void NotifyPlayerHealthLost(int amount, int maxHealth)
    {
        if (!cylinderActive || amount <= 0)
        {
            return;
        }

        currentCylinderHealthLost = SaturatingAddLong(
            currentCylinderHealthLost,
            amount);
        currentCylinderMaxHealth = Mathf.Max(
            currentCylinderMaxHealth,
            maxHealth);
    }

    public void NotifyGoldGained(int amount)
    {
        if (amount <= 0
            || !FindFirstEffect(
                RelicEffectType.GoldPanner,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            return;
        }

        int nuggetsFound = 0;
        NotifyProbabilityEvaluated(relic, effect.GoldNuggetChance);

        for (int index = 0; index < amount; index++)
        {
            if (RollPercent(effect.GoldNuggetChance))
            {
                nuggetsFound++;
            }
        }

        if (nuggetsFound <= 0)
        {
            return;
        }

        relic.AddPrimaryCounter(nuggetsFound);
        Trigger(relic, effect);
        InventoryChanged?.Invoke();
    }

    private double CalculateOutgoingAttackDamageMultiplier()
    {
        double multiplier = 1d;

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic == null || relic.Data == null
                || relic.MovementStacks <= 0)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null || effect.EffectType
                    != RelicEffectType.MovementDamageMultiplier)
                {
                    continue;
                }

                int exponent = SaturatingMultiply(
                    relic.MovementStacks,
                    relic.StackCount);
                double effectMultiplier = Math.Pow(
                    effect.MovementDamageMultiplierPerStack,
                    exponent);
                multiplier *= effectMultiplier;

                if (double.IsInfinity(multiplier)
                    || multiplier >= double.MaxValue)
                {
                    return double.MaxValue;
                }
            }
        }

        return double.IsNaN(multiplier) ? 1d : Math.Max(0d, multiplier);
    }

    private void ApplyShotStartRelicModifiers(
        bool isBaseBullet,
        int physicalBulletIndex,
        int currentHealth,
        int maxHealth)
    {
        if (activeCylinderScaleDamagePercent > 0d)
        {
            activeShotDamageMultiplier = MultiplyMultiplier(
                activeShotDamageMultiplier,
                1d + activeCylinderScaleDamagePercent / 100d);
        }

        if (physicalBulletIndex >= 0
            && physicalBulletIndex == luckyChamberBulletIndex
            && FindFirstEffect(
                RelicEffectType.LuckyChamber,
                out RelicInstance luckyRelic,
                out RelicEffectData luckyEffect))
        {
            activeShotDamageMultiplier = MultiplyMultiplier(
                activeShotDamageMultiplier,
                luckyEffect.FinalDamageMultiplier);
            Trigger(luckyRelic, luckyEffect);
        }

        if (isBaseBullet
            && FindFirstEffect(
                RelicEffectType.GoldPanner,
                out RelicInstance pannerRelic,
                out RelicEffectData pannerEffect)
            && pannerRelic.TryConsumePrimaryCounter(
                pannerEffect.NuggetsRequired))
        {
            activeShotForcesCritical = true;
            activeShotDamageMultiplier = MultiplyMultiplier(
                activeShotDamageMultiplier,
                pannerEffect.FinalDamageMultiplier);
            Trigger(pannerRelic, pannerEffect);
            InventoryChanged?.Invoke();
        }

        if (FindFirstEffect(
                RelicEffectType.CrackedPrimer,
                out RelicInstance primerRelic,
                out RelicEffectData primerEffect))
        {
            double chance = Math.Min(
                100d,
                primerEffect.PrimerBaseChance
                    + primerRelic.PrimaryCounter
                    * primerEffect.PrimerFailureChanceBonus);
            NotifyProbabilityEvaluated(primerRelic, chance);

            if (RollPercent(chance))
            {
                primerRelic.SetPrimaryCounter(0);
                activeShotDamageMultiplier = MultiplyMultiplier(
                    activeShotDamageMultiplier,
                    primerEffect.FinalDamageMultiplier);
                Trigger(primerRelic, primerEffect);
            }
            else
            {
                primerRelic.AddPrimaryCounter(1);
            }

            InventoryChanged?.Invoke();
        }

        if (FindFirstEffect(
                RelicEffectType.ExecutionersOath,
                out RelicInstance oathRelic,
                out RelicEffectData oathEffect))
        {
            activeShotDamageMultiplier = MultiplyMultiplier(
                activeShotDamageMultiplier,
                oathEffect.GetExecutionMultiplier(
                    oathRelic.PrimaryCounter));
        }

        double healthPercent = maxHealth <= 0
            ? 100d
            : Math.Clamp(currentHealth * 100d / maxHealth, 0d, 100d);

        if (isBaseBullet && !brinkTriggeredThisCylinder
            && FindFirstEffect(
                RelicEffectType.BrinkTrigger,
                out RelicInstance brinkRelic,
                out RelicEffectData brinkEffect)
            && healthPercent <= brinkEffect.BrinkHealthThresholdPercent)
        {
            double chance = Math.Min(
                100d,
                brinkEffect.BrinkBaseChance
                    + brinkFailureCount
                    * brinkEffect.BrinkFailureChanceBonus);
            NotifyProbabilityEvaluated(brinkRelic, chance);

            if (RollPercent(chance))
            {
                brinkTriggeredThisCylinder = true;
                activeShotDamageMultiplier = MultiplyMultiplier(
                    activeShotDamageMultiplier,
                    brinkEffect.FinalDamageMultiplier);
                Trigger(brinkRelic, brinkEffect);
            }
            else if (brinkFailureCount < int.MaxValue)
            {
                brinkFailureCount++;
            }
        }
    }

    private void NotifyShotStartActivations(
        bool isBaseBullet,
        bool isFirstLoadedShot,
        bool isLastLoadedShot)
    {
        // Extra/chain shots share the same damage snapshot. Presentation is
        // emitted only for the consumed chamber bullet so one effect cannot
        // spam the relic UI several times during a single physical shot.
        if (!isBaseBullet)
        {
            return;
        }

        TriggerIfOwned(
            RelicEffectType.FirstShotFinalMultiplier,
            isFirstLoadedShot);
        TriggerIfOwned(
            RelicEffectType.LastShotFinalMultiplier,
            isLastLoadedShot);
        TriggerIfOwned(
            RelicEffectType.Scale,
            activeCylinderScaleDamagePercent > 0d);

        if (FindFirstEffect(
                RelicEffectType.ExecutionersOath,
                out RelicInstance oathRelic,
                out RelicEffectData oathEffect)
            && oathEffect.GetExecutionMultiplier(oathRelic.PrimaryCounter) > 1d)
        {
            Trigger(oathRelic, oathEffect);
        }
    }

    private void TriggerIfOwned(RelicEffectType type, bool condition)
    {
        if (condition
            && FindFirstEffect(
                type,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            Trigger(relic, effect);
        }
    }

    public bool TryPreventLethalDamage(
        int incomingDamage,
        int currentHealth,
        out int survivingHealth)
    {
        survivingHealth = 0;

        if (incomingDamage < currentHealth || currentHealth <= 0)
        {
            return false;
        }

        List<RelicInstance> snapshot = new List<RelicInstance>(ownedRelics);

        foreach (RelicInstance relic in snapshot)
        {
            if (relic == null || relic.Data == null || relic.IsSpent)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null || effect.EffectType
                    != RelicEffectType.PreventLethalDamage)
                {
                    continue;
                }

                survivingHealth = Mathf.Clamp(
                    effect.SurvivingHealth,
                    1,
                    currentHealth);
                RaiseCombatEvent(new RelicCombatEventContext(
                    RelicCombatEventType.LethalDamageIncoming,
                    amount: incomingDamage));
                RelicTriggered?.Invoke(relic, effect);

                if (relic.Data.LifetimeType == RelicLifetimeType.Consumable
                    && relic.TryConsumeCharge() && relic.IsSpent)
                {
                    int index = ownedRelics.IndexOf(relic);

                    if (index >= 0)
                    {
                        TryRemoveAt(index, RelicRemovalReason.Consumed);
                    }
                }
                else
                {
                    InventoryChanged?.Invoke();
                }

                return true;
            }
        }

        return false;
    }

    public void CaptureRunState(List<RunRelicSaveData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic != null && relic.Data != null && !relic.IsSpent)
            {
                destination.Add(relic.CaptureState());
            }
        }
    }

    public bool RestoreRunState(
        IReadOnlyList<RunRelicSaveData> savedRelics,
        Func<string, RelicData> resolver = null)
    {
        ownedRelics.Clear();
        nextAcquisitionOrder = 0;
        ClearShotSnapshot();
        ResetCylinderRuntime();
        processedEnemyDefeatIds.Clear();
        pendingHolsterBulletOrders.Clear();
        movementStacksConsumedByCylinder.Clear();

        if (savedRelics == null || savedRelics.Count == 0)
        {
            InventoryChanged?.Invoke();
            return true;
        }

        List<RunRelicSaveData> ordered = new List<RunRelicSaveData>();

        foreach (RunRelicSaveData saved in savedRelics)
        {
            if (saved != null)
            {
                ordered.Add(saved);
            }
        }

        ordered.Sort((left, right) =>
            left.acquisitionOrder.CompareTo(right.acquisitionOrder));

        foreach (RunRelicSaveData saved in ordered)
        {
            if (ownedRelics.Count >= MaximumRelicCount)
            {
                break;
            }

            RelicData data = resolver?.Invoke(saved.relicId)
                ?? ResolveRelicData(saved.relicId);

            if (data == null || string.IsNullOrWhiteSpace(data.Id)
                || FindOwned(data.Id) != null)
            {
                ownedRelics.Clear();
                nextAcquisitionOrder = 0;
                return false;
            }

            RelicInstance relic = new RelicInstance(
                data,
                saved.acquisitionOrder);
            relic.RestoreState(saved);

            if (!relic.IsSpent)
            {
                ownedRelics.Add(relic);
            }
        }

        RecalculateNextAcquisitionOrder();
        InventoryChanged?.Invoke();
        return true;
    }

    public RelicData ResolveRelicData(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
        {
            return null;
        }

        foreach (RelicData data in relicCatalog)
        {
            if (data != null && string.Equals(
                    data.Id,
                    relicId,
                    StringComparison.Ordinal))
            {
                return data;
            }
        }

        RelicData[] resources = Resources.LoadAll<RelicData>("Relics");

        foreach (RelicData data in resources)
        {
            if (data != null && string.Equals(
                    data.Id,
                    relicId,
                    StringComparison.Ordinal))
            {
                return data;
            }
        }

        return null;
    }

    private List<RelicData> BuildAvailableRewardPool()
    {
        List<RelicData> pool = new List<RelicData>();
        HashSet<string> addedIds = new HashSet<string>(
            StringComparer.Ordinal);
        AddAvailableRelics(relicCatalog, pool, addedIds);
        AddAvailableRelics(
            Resources.LoadAll<RelicData>("Relics"),
            pool,
            addedIds);
        return pool;
    }

    private void AddAvailableRelics(
        IReadOnlyList<RelicData> source,
        List<RelicData> destination,
        HashSet<string> addedIds)
    {
        if (source == null)
        {
            return;
        }

        foreach (RelicData data in source)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id)
                || !addedIds.Add(data.Id))
            {
                continue;
            }

            RelicInstance owned = FindOwned(data.Id);

            if (owned != null
                && (!data.CanStack || owned.StackCount >= data.MaxStack))
            {
                continue;
            }

            destination.Add(data);
        }
    }

    private void HandlePlayerMoved(PlayerMovementContext context)
    {
        RecordPlayerMovement(context);
    }

    public void RecordPlayerMovement(PlayerMovementContext context)
    {
        if (context.Distance <= 0 || context.Source == PlayerMovementSource.None)
        {
            return;
        }

        bool changed = false;

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic == null || relic.Data == null)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect == null
                    || (effect.MovementSources & context.Source) == 0)
                {
                    continue;
                }

                if (effect.EffectType
                    == RelicEffectType.MovementDamageMultiplier)
                {
                    relic.AddMovementStacks(context.Distance);
                    Trigger(relic, effect);
                    changed = true;
                }
                else if (effect.EffectType == RelicEffectType.Carriage)
                {
                    int previousStoredReloads = relic.SecondaryCounter;
                    int totalDistance = SaturatingAddInt(
                        relic.PrimaryCounter,
                        context.Distance);
                    int earnedReloads = totalDistance
                        / effect.MovementTilesPerFreeReload;
                    int storedReloads = Mathf.Min(
                        effect.FreeReloadStorageLimit,
                        SaturatingAddInt(
                            relic.SecondaryCounter,
                            earnedReloads));
                    relic.SetPrimaryCounter(totalDistance
                        % effect.MovementTilesPerFreeReload);
                    relic.SetSecondaryCounter(storedReloads);
                    if (storedReloads > previousStoredReloads)
                    {
                        Trigger(relic, effect);
                    }
                    changed = true;
                }
            }
        }

        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.PlayerMoved,
            context));
        if (changed)
        {
            InventoryChanged?.Invoke();
        }
    }

    private void TransferDefeatedEnemyDebuffs(
        EnemyController defeatedEnemy,
        IReadOnlyList<EnemyController> activeEnemies,
        BoardManager boardManager)
    {
        if (defeatedEnemy == null || activeEnemies == null
            || !FindFirstEffect(
                RelicEffectType.InfectiousIncubator,
                out RelicInstance relic,
                out RelicEffectData effect))
        {
            return;
        }

        EnemyController nearest = null;
        int nearestDistance = int.MaxValue;
        int sourceTile = -1;
        bool hasSourceTile = boardManager != null
            && boardManager.TryGetTileIndex(
                defeatedEnemy.transform.position,
                out sourceTile);

        foreach (EnemyController candidate in activeEnemies)
        {
            if (candidate == null || candidate == defeatedEnemy
                || candidate.CurrentHealth <= 0)
            {
                continue;
            }

            int distance;

            if (hasSourceTile && boardManager.TryGetTileIndex(
                    candidate.transform.position,
                    out int candidateTile))
            {
                distance = Math.Abs(candidateTile - sourceTile);
            }
            else
            {
                distance = Mathf.RoundToInt(Mathf.Abs(
                    candidate.transform.position.x
                    - defeatedEnemy.transform.position.x) * 1000f);
            }

            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            return;
        }

        bool transferred = false;

        foreach (StatusEffectType type in Enum.GetValues(
                     typeof(StatusEffectType)))
        {
            int sourceStacks = defeatedEnemy.GetStatusStacks(type);

            if (sourceStacks <= 0)
            {
                continue;
            }

            int transferStacks = (int)Math.Min(
                int.MaxValue,
                Math.Ceiling(
                    sourceStacks * effect.DebuffTransferPercent / 100d));
            transferred |= nearest.AddStatusEffect(
                type,
                transferStacks,
                true);
        }

        if (transferred)
        {
            Trigger(relic, effect);
        }
    }

    private void ResetMovementStacks(RelicMovementStackReset timing)
    {
        bool changed = false;

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic == null || relic.Data == null
                || relic.MovementStacks <= 0)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect != null && effect.EffectType
                    == RelicEffectType.MovementDamageMultiplier
                    && effect.MovementStackReset == timing)
                {
                    relic.ResetMovementStacks();
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            InventoryChanged?.Invoke();
        }
    }

    private void RaiseCombatEvent(RelicCombatEventContext context)
    {
        CombatEventRaised?.Invoke(context);
    }

    private void ClearShotSnapshot()
    {
        movementStacksConsumedByShot.Clear();
        activeTargetDamageMultipliers.Clear();
        isShotActive = false;
        activeShotDamageMultiplier = 1d;
        activeShotCountsForRelics = false;
        activeShotDefeatedEnemy = false;
        activeShotForcesCritical = false;
        activePhysicalBulletIndex = -1;
    }

    private void ResetCylinderRuntime()
    {
        cylinderActive = false;
        luckyChamberBulletIndex = -1;
        luckyChamberSelectionLoadedCount = 0;
        circuitShotCount = 0;
        circuitReloadCount = 0;
        brinkFailureCount = 0;
        brinkTriggeredThisCylinder = false;
        currentCylinderHealthLost = 0L;
        currentCylinderMaxHealth = 0;
        activeCylinderScaleDamagePercent = 0d;
        activeMemorialShotMultiplier = 0d;
        stormStrongestSingleDamage = 0L;
        stormTriggeredThisCylinder = false;
        stormRequiredEnemyIds.Clear();
        stormDamagedEnemyIds.Clear();
        circuitReloadedBulletOrders.Clear();
    }

    private bool FindFirstEffect(
        RelicEffectType type,
        out RelicInstance foundRelic,
        out RelicEffectData foundEffect)
    {
        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic?.Data == null || relic.IsSpent)
            {
                continue;
            }

            foreach (RelicEffectData effect in relic.Data.Effects)
            {
                if (effect != null && effect.EffectType == type)
                {
                    foundRelic = relic;
                    foundEffect = effect;
                    return true;
                }
            }
        }

        foundRelic = null;
        foundEffect = null;
        return false;
    }

    private void Trigger(RelicInstance relic, RelicEffectData effect)
    {
        RelicTriggered?.Invoke(relic, effect);
        RaiseCombatEvent(new RelicCombatEventContext(
            RelicCombatEventType.RelicTriggered));
    }

    private void NotifyProbabilityEvaluated(
        RelicInstance relic,
        double chance)
    {
        if (relic == null || double.IsNaN(chance))
        {
            return;
        }

        RelicProbabilityEvaluated?.Invoke(
            relic,
            Math.Clamp(chance, 0d, 100d));
    }

    private static bool RollPercent(double chance)
    {
        return chance >= 100d || chance > 0d
            && UnityEngine.Random.Range(0f, 100f) < (float)chance;
    }

    private static double MultiplyMultiplier(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right)
            || left <= 0d || right <= 0d)
        {
            return 0d;
        }

        double result = left * right;
        return double.IsInfinity(result) ? double.MaxValue : result;
    }

    private static string FormatStatusNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static int SaturatingAddInt(int left, int right)
    {
        long value = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private static long SaturatingAddLong(long left, long right)
    {
        if (left < 0L || right < 0L)
        {
            return Math.Max(0L, left) + Math.Max(0L, right);
        }

        return left > long.MaxValue - right
            ? long.MaxValue
            : left + right;
    }

    private void RecalculateNextAcquisitionOrder()
    {
        nextAcquisitionOrder = 0;

        foreach (RelicInstance relic in ownedRelics)
        {
            if (relic != null)
            {
                nextAcquisitionOrder = Math.Max(
                    nextAcquisitionOrder,
                    relic.AcquisitionOrder + 1);
            }
        }
    }

    private static int SaturatingMultiply(int left, int right)
    {
        long value = (long)Mathf.Max(0, left) * Mathf.Max(0, right);
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }
}
