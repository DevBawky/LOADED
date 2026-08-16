using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum RelicLifetimeType
{
    RunPersistent = 0,
    Consumable = 1
}

public enum RelicEffectType
{
    None = 0,
    PreventLethalDamage = 1,
    MovementDamageMultiplier = 2,
    FirstShotFinalMultiplier = 5,
    LastShotFinalMultiplier = 6,
    PredatorHolster = 10,
    ClosedCircuit = 11,
    InfectiousIncubator = 12,
    EmptyBeat = 13,
    EyeOfTheStorm = 14,
    Carriage = 15,
    GoldPanner = 16,
    CrackedPrimer = 17,
    Scale = 18,
    FamilyWill = 19,
    LuckyChamber = 20,
    ExecutionersOath = 21,
    MutationCatalyst = 22,
    BrinkTrigger = 23
}

public enum RelicMovementStackReset
{
    AfterShot = 0,
    AfterCylinder = 1,
    BattleStart = 2,
    Never = 3
}

[Flags]
public enum PlayerMovementSource
{
    None = 0,
    NormalMove = 1 << 0,
    BulletPositionSwap = 1 << 1,
    ForcedMove = 1 << 2,
    All = NormalMove | BulletPositionSwap | ForcedMove
}

[Serializable]
public sealed class RelicEffectData
{
    [SerializeField] private RelicEffectType effectType;

    [Min(0f)]
    [Tooltip("조건을 만족했을 때 적용하는 최종 피해 배율입니다. 2를 입력하면 최종 피해 x2입니다.")]
    [SerializeField] private double finalDamageMultiplier = 2d;
    [Min(0f)]
    [Tooltip("이동 스택 1개마다 적용하는 최종 피해 배율입니다. 1.1을 입력하면 타일당 x1.1입니다.")]
    [SerializeField] private double movementDamageMultiplierPerStack = 1.1d;
    [Min(1)]
    [Tooltip("죽음 방지에 성공한 뒤 남길 체력입니다.")]
    [SerializeField] private int survivingHealth = 1;
    [Tooltip("이동 스택으로 인정할 플레이어 이동 원인입니다.")]
    [SerializeField] private PlayerMovementSource movementSources =
        PlayerMovementSource.NormalMove
        | PlayerMovementSource.BulletPositionSwap;
    [Tooltip("누적된 이동 스택을 언제 소비할지 결정합니다.")]
    [SerializeField] private RelicMovementStackReset movementStackReset =
        RelicMovementStackReset.AfterShot;

    [Min(1)]
    [Tooltip("폐쇄 회로가 발동하기 위해 필요한 실제 사격 횟수입니다.")]
    [SerializeField] private int circuitShotThreshold = 5;
    [Min(1)]
    [Tooltip("폐쇄 회로가 한 실린더에서 발동할 수 있는 최대 횟수입니다.")]
    [SerializeField] private int circuitMaxReloadsPerCylinder = 2;
    [Range(0f, 100f)]
    [Tooltip("감염성 배양기가 이전할 각 디버프의 비율입니다.")]
    [SerializeField] private double debuffTransferPercent = 50d;
    [Range(0f, 100f)]
    [Tooltip("폭풍의 눈이 복제할 최고 단일 피해의 비율입니다.")]
    [SerializeField] private double stormDamagePercent = 40d;
    [Min(1)]
    [Tooltip("마차가 무료 재장전 1회를 얻는 데 필요한 이동 타일 수입니다.")]
    [SerializeField] private int movementTilesPerFreeReload = 5;
    [Min(1)]
    [Tooltip("마차가 저장할 수 있는 무료 재장전의 최대 횟수입니다.")]
    [SerializeField] private int freeReloadStorageLimit = 2;
    [Range(0f, 100f)]
    [Tooltip("골드 1개마다 금덩이를 발견할 확률입니다.")]
    [SerializeField] private double goldNuggetChance = 10d;
    [Min(1)]
    [Tooltip("사금 선별기의 폭증을 발동하는 데 필요한 금덩이 수입니다.")]
    [SerializeField] private int nuggetsRequired = 3;
    [Range(0f, 100f)]
    [Tooltip("금이 간 뇌관의 초기 발동 확률입니다.")]
    [SerializeField] private double primerBaseChance = 5d;
    [Range(0f, 100f)]
    [Tooltip("금이 간 뇌관이 실패할 때 추가되는 확률입니다.")]
    [SerializeField] private double primerFailureChanceBonus = 5d;
    [Range(0f, 1000f)]
    [Tooltip("저울로 얻을 수 있는 최종 피해 증가의 상한입니다.")]
    [SerializeField] private double scaleMaximumDamagePercent = 100d;
    [Range(0f, 1000f)]
    [Tooltip("영구 파괴된 탄환 하나당 추모 사격의 위력입니다.")]
    [SerializeField] private double memorialDamagePercentPerBullet = 20d;
    [Range(0f, 1000f)]
    [Tooltip("추모 사격 위력의 상한입니다.")]
    [SerializeField] private double memorialMaximumDamagePercent = 100d;
    [Tooltip("처형자의 서약 연속 처치 단계별 최종 피해 배율입니다.")]
    [SerializeField] private double[] executionDamageMultipliers =
        { 1.5d, 2d, 3d, 5d };
    [Range(0f, 100f)]
    [Tooltip("활성 디버프 종류 하나당 변이 촉매 발동 확률입니다.")]
    [SerializeField] private double mutationChancePerDebuffType = 15d;
    [Range(0f, 100f)]
    [Tooltip("변이 촉매 발동 확률의 상한입니다.")]
    [SerializeField] private double mutationMaximumChance = 60d;
    [Range(0f, 100f)]
    [Tooltip("벼랑 끝의 방아쇠가 활성화되는 최대 체력 비율입니다.")]
    [SerializeField] private double brinkHealthThresholdPercent = 25d;
    [Range(0f, 100f)]
    [Tooltip("벼랑 끝의 방아쇠 초기 발동 확률입니다.")]
    [SerializeField] private double brinkBaseChance = 30d;
    [Range(0f, 100f)]
    [Tooltip("벼랑 끝의 방아쇠가 실패할 때 추가되는 확률입니다.")]
    [SerializeField] private double brinkFailureChanceBonus = 10d;

    public RelicEffectType EffectType => effectType;
    public double FinalDamageMultiplier =>
        SanitizeNonNegative(finalDamageMultiplier);
    public double MovementDamageMultiplierPerStack =>
        SanitizeNonNegative(movementDamageMultiplierPerStack);
    public int SurvivingHealth => Mathf.Max(1, survivingHealth);
    public PlayerMovementSource MovementSources => movementSources;
    public RelicMovementStackReset MovementStackReset => movementStackReset;
    public int CircuitShotThreshold => Mathf.Max(1, circuitShotThreshold);
    public int CircuitMaxReloadsPerCylinder =>
        Mathf.Max(1, circuitMaxReloadsPerCylinder);
    public double DebuffTransferPercent => ClampPercent(debuffTransferPercent);
    public double StormDamagePercent => ClampPercent(stormDamagePercent);
    public int MovementTilesPerFreeReload =>
        Mathf.Max(1, movementTilesPerFreeReload);
    public int FreeReloadStorageLimit => Mathf.Max(1, freeReloadStorageLimit);
    public double GoldNuggetChance => ClampPercent(goldNuggetChance);
    public int NuggetsRequired => Mathf.Max(1, nuggetsRequired);
    public double PrimerBaseChance => ClampPercent(primerBaseChance);
    public double PrimerFailureChanceBonus =>
        ClampPercent(primerFailureChanceBonus);
    public double ScaleMaximumDamagePercent =>
        SanitizeNonNegative(scaleMaximumDamagePercent);
    public double MemorialDamagePercentPerBullet =>
        SanitizeNonNegative(memorialDamagePercentPerBullet);
    public double MemorialMaximumDamagePercent =>
        SanitizeNonNegative(memorialMaximumDamagePercent);
    public IReadOnlyList<double> ExecutionDamageMultipliers =>
        executionDamageMultipliers ?? Array.Empty<double>();
    public double MutationChancePerDebuffType =>
        ClampPercent(mutationChancePerDebuffType);
    public double MutationMaximumChance => ClampPercent(mutationMaximumChance);
    public double BrinkHealthThresholdPercent =>
        ClampPercent(brinkHealthThresholdPercent);
    public double BrinkBaseChance => ClampPercent(brinkBaseChance);
    public double BrinkFailureChanceBonus =>
        ClampPercent(brinkFailureChanceBonus);

    public string GetAbilityDescription()
    {
        return effectType switch
        {
            RelicEffectType.PreventLethalDamage =>
                $"죽음에 이르는 피해를 막고 체력을 {SurvivingHealth} 남깁니다.",
            RelicEffectType.MovementDamageMultiplier =>
                "인정되는 이동 1타일마다 다음 공격의 최종 피해가 "
                + $"x{FormatNumber(MovementDamageMultiplierPerStack)} 누적됩니다. "
                + $"스택 소비: {GetResetLabel(MovementStackReset)}.",
            RelicEffectType.FirstShotFinalMultiplier =>
                "한 실린더의 첫 사격 최종 피해가 "
                + $"x{FormatNumber(FinalDamageMultiplier)}가 됩니다.",
            RelicEffectType.LastShotFinalMultiplier =>
                "장전 탄환이 남지 않은 사격의 최종 피해가 "
                + $"x{FormatNumber(FinalDamageMultiplier)}가 됩니다.",
            RelicEffectType.PredatorHolster =>
                "적을 처치한 탄환을 다음 장전 순서로 옮기고 해당 재장전을 무료로 만듭니다.",
            RelicEffectType.ClosedCircuit =>
                $"실제 사격 {CircuitShotThreshold}회마다 가장 오래전에 사용한 탄환을 즉시 장전합니다. "
                + $"실린더당 최대 {CircuitMaxReloadsPerCylinder}회입니다.",
            RelicEffectType.InfectiousIncubator =>
                "디버프 상태의 적이 죽으면 남은 각 디버프의 "
                + $"{FormatNumber(DebuffTransferPercent)}%를 가장 가까운 적에게 이전합니다.",
            RelicEffectType.EmptyBeat =>
                "빈 실린더에 넣는 첫 탄환의 재장전은 턴을 소모하지 않습니다.",
            RelicEffectType.EyeOfTheStorm =>
                "한 실린더에서 모든 적을 공격하면 최고 단일 피해의 "
                + $"{FormatNumber(StormDamagePercent)}%를 모든 생존 적에게 가합니다.",
            RelicEffectType.Carriage =>
                $"실제 이동 {MovementTilesPerFreeReload}칸마다 무료 재장전 1회를 얻습니다. "
                + $"최대 {FreeReloadStorageLimit}회 저장합니다.",
            RelicEffectType.GoldPanner =>
                $"골드 1개마다 {FormatNumber(GoldNuggetChance)}% 확률로 금덩이를 얻습니다. "
                + $"{NuggetsRequired}개를 소비한 다음 탄환은 치명타 확정 및 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}가 됩니다.",
            RelicEffectType.CrackedPrimer =>
                $"{FormatNumber(PrimerBaseChance)}% 확률로 최종 피해 x{FormatNumber(FinalDamageMultiplier)}. "
                + $"실패 시 확률 +{FormatNumber(PrimerFailureChanceBonus)}%p, 성공 시 초기화합니다.",
            RelicEffectType.Scale =>
                "이전 실린더 사격 중 잃은 최대 체력 비율만큼 다음 실린더의 최종 피해가 증가합니다. "
                + $"최대 +{FormatNumber(ScaleMaximumDamagePercent)}%입니다.",
            RelicEffectType.FamilyWill =>
                "영구 파괴된 탄환 하나당 모든 전투의 첫 실린더에 "
                + $"{FormatNumber(MemorialDamagePercentPerBullet)}% 위력의 추모 사격을 추가합니다. "
                + $"최대 {FormatNumber(MemorialMaximumDamagePercent)}%입니다.",
            RelicEffectType.LuckyChamber =>
                "실린더의 무작위 약실 하나를 공개하고 해당 탄환의 최종 피해를 "
                + $"x{FormatNumber(FinalDamageMultiplier)}로 만듭니다.",
            RelicEffectType.ExecutionersOath =>
                "연속 처치에 성공할 때마다 다음 사격의 최종 피해 단계가 상승하고 실패하면 초기화됩니다.",
            RelicEffectType.MutationCatalyst =>
                "대상의 활성 디버프 종류당 "
                + $"{FormatNumber(MutationChancePerDebuffType)}% 확률로 현재 피해를 "
                + $"x{FormatNumber(FinalDamageMultiplier)}로 만듭니다. "
                + $"최대 확률은 {FormatNumber(MutationMaximumChance)}%입니다.",
            RelicEffectType.BrinkTrigger =>
                $"체력 {FormatNumber(BrinkHealthThresholdPercent)}% 이하에서 "
                + $"{FormatNumber(BrinkBaseChance)}% 확률로 최종 피해 x{FormatNumber(FinalDamageMultiplier)}. "
                + $"실패 시 확률 +{FormatNumber(BrinkFailureChanceBonus)}%p입니다.",
            _ => "효과가 없습니다."
        };
    }

    public double GetExecutionMultiplier(int streak)
    {
        if (streak <= 0 || ExecutionDamageMultipliers.Count == 0)
        {
            return 1d;
        }

        int index = Mathf.Min(streak, ExecutionDamageMultipliers.Count) - 1;
        return Math.Max(1d, SanitizeNonNegative(
            ExecutionDamageMultipliers[index]));
    }

    private static double SanitizeNonNegative(double value)
    {
        return double.IsNaN(value) ? 0d : Math.Max(0d, value);
    }

    private static double ClampPercent(double value)
    {
        return double.IsNaN(value) ? 0d : Math.Clamp(value, 0d, 100d);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string GetResetLabel(RelicMovementStackReset reset)
    {
        return reset switch
        {
            RelicMovementStackReset.AfterShot => "사격 후",
            RelicMovementStackReset.AfterCylinder => "실린더 종료 후",
            RelicMovementStackReset.BattleStart => "다음 전투 시작 시",
            RelicMovementStackReset.Never => "소비하지 않음",
            _ => reset.ToString()
        };
    }
}

[CreateAssetMenu(
    fileName = "New Relic",
    menuName = "LOADED/Relic")]
public sealed class RelicData : ScriptableObject
{
    [SerializeField] private string relicId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private RelicLifetimeType lifetimeType;
    [Min(1)]
    [SerializeField] private int initialCharges = 1;
    [SerializeField] private bool canStack;
    [Min(1)]
    [SerializeField] private int maxStack = 1;
    [SerializeField] private List<RelicEffectData> effects =
        new List<RelicEffectData>();

    public string Id => string.IsNullOrWhiteSpace(relicId)
        ? name
        : relicId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description ?? string.Empty;
    public Sprite Icon => icon;
    public RelicLifetimeType LifetimeType => lifetimeType;
    public int InitialCharges => lifetimeType == RelicLifetimeType.Consumable
        ? Mathf.Max(1, initialCharges)
        : 0;
    public bool CanStack => canStack;
    public int MaxStack => canStack ? Mathf.Max(2, maxStack) : 1;
    public IReadOnlyList<RelicEffectData> Effects =>
        effects ?? (IReadOnlyList<RelicEffectData>)Array.Empty<RelicEffectData>();

    public bool HasEffect(RelicEffectType type)
    {
        foreach (RelicEffectData effect in Effects)
        {
            if (effect != null && effect.EffectType == type)
            {
                return true;
            }
        }

        return false;
    }

    public string BuildEffectSummary()
    {
        StringBuilder builder = new StringBuilder();

        foreach (RelicEffectData effect in Effects)
        {
            if (effect == null || effect.EffectType == RelicEffectType.None)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append('•');
            builder.Append(' ');
            builder.Append(effect.GetAbilityDescription());
        }

        return builder.ToString();
    }
}

[Serializable]
public sealed class RelicInstance
{
    [SerializeField] private RelicData data;
    [SerializeField] private int stackCount;
    [SerializeField] private int remainingCharges;
    [SerializeField] private int movementStacks;
    [SerializeField] private long storedDamage;
    [SerializeField] private int primaryCounter;
    [SerializeField] private int secondaryCounter;
    [SerializeField] private double storedValue;
    [SerializeField] private bool runtimeFlag;
    [SerializeField] private List<int> trackedBulletAcquisitionOrders =
        new List<int>();
    [SerializeField] private int acquisitionOrder;

    public RelicInstance(RelicData data, int acquisitionOrder)
    {
        this.data = data;
        this.acquisitionOrder = Mathf.Max(0, acquisitionOrder);
        stackCount = 1;
        remainingCharges = data == null ? 0 : data.InitialCharges;
    }

    public RelicData Data => data;
    public string Id => data == null ? string.Empty : data.Id;
    public int StackCount => Mathf.Max(1, stackCount);
    public int RemainingCharges => Mathf.Max(0, remainingCharges);
    public int MovementStacks => Mathf.Max(0, movementStacks);
    public long StoredDamage => Math.Max(0L, storedDamage);
    public int PrimaryCounter => Mathf.Max(0, primaryCounter);
    public int SecondaryCounter => Mathf.Max(0, secondaryCounter);
    public double StoredValue => double.IsNaN(storedValue)
        ? 0d
        : Math.Max(0d, storedValue);
    public bool RuntimeFlag => runtimeFlag;
    public IReadOnlyList<int> TrackedBulletAcquisitionOrders =>
        trackedBulletAcquisitionOrders
        ?? (IReadOnlyList<int>)Array.Empty<int>();
    public int AcquisitionOrder => Mathf.Max(0, acquisitionOrder);
    public bool IsSpent => data != null
        && data.LifetimeType == RelicLifetimeType.Consumable
        && remainingCharges <= 0;

    public bool TryAddStack()
    {
        if (data == null || !data.CanStack || stackCount >= data.MaxStack)
        {
            return false;
        }

        stackCount++;

        if (data.LifetimeType == RelicLifetimeType.Consumable)
        {
            remainingCharges = SaturatingAdd(
                remainingCharges,
                data.InitialCharges);
        }

        return true;
    }

    public bool TryConsumeCharge()
    {
        if (data == null || data.LifetimeType != RelicLifetimeType.Consumable
            || remainingCharges <= 0)
        {
            return false;
        }

        remainingCharges--;
        return true;
    }

    public void AddMovementStacks(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        movementStacks = SaturatingAdd(movementStacks, amount);
    }

    public void ResetMovementStacks()
    {
        movementStacks = 0;
    }

    public void ConsumeMovementStacks(int amount)
    {
        movementStacks = Mathf.Max(0, movementStacks - Mathf.Max(0, amount));
    }

    public void SetPrimaryCounter(int value)
    {
        primaryCounter = Mathf.Max(0, value);
    }

    public void AddPrimaryCounter(int amount)
    {
        primaryCounter = SaturatingAdd(primaryCounter, amount);
    }

    public bool TryConsumePrimaryCounter(int amount)
    {
        int cost = Mathf.Max(0, amount);

        if (primaryCounter < cost)
        {
            return false;
        }

        primaryCounter -= cost;
        return true;
    }

    public void SetSecondaryCounter(int value)
    {
        secondaryCounter = Mathf.Max(0, value);
    }

    public void AddSecondaryCounter(int amount)
    {
        secondaryCounter = SaturatingAdd(secondaryCounter, amount);
    }

    public bool TryConsumeSecondaryCounter(int amount = 1)
    {
        int cost = Mathf.Max(0, amount);

        if (secondaryCounter < cost)
        {
            return false;
        }

        secondaryCounter -= cost;
        return true;
    }

    public void SetStoredValue(double value)
    {
        storedValue = double.IsNaN(value) ? 0d : Math.Max(0d, value);
    }

    public void SetRuntimeFlag(bool value)
    {
        runtimeFlag = value;
    }

    public bool AddTrackedBullet(int acquisitionOrder)
    {
        trackedBulletAcquisitionOrders ??= new List<int>();
        int normalizedOrder = Mathf.Max(0, acquisitionOrder);

        if (trackedBulletAcquisitionOrders.Contains(normalizedOrder))
        {
            return false;
        }

        trackedBulletAcquisitionOrders.Add(normalizedOrder);
        return true;
    }

    public bool RemoveTrackedBullet(int acquisitionOrder)
    {
        return trackedBulletAcquisitionOrders != null
            && trackedBulletAcquisitionOrders.Remove(
                Mathf.Max(0, acquisitionOrder));
    }

    public void RestoreState(RunRelicSaveData state)
    {
        if (state == null)
        {
            return;
        }

        stackCount = Mathf.Clamp(
            state.stackCount,
            1,
            data == null ? 1 : data.MaxStack);
        remainingCharges = data != null
            && data.LifetimeType == RelicLifetimeType.Consumable
                ? Mathf.Max(0, state.remainingCharges)
                : 0;
        movementStacks = Mathf.Max(0, state.movementStacks);
        storedDamage = Math.Max(0L, state.storedDamage);
        primaryCounter = Mathf.Max(0, state.primaryCounter);
        secondaryCounter = Mathf.Max(0, state.secondaryCounter);
        storedValue = double.IsNaN(state.storedValue)
            ? 0d
            : Math.Max(0d, state.storedValue);
        runtimeFlag = state.runtimeFlag;
        trackedBulletAcquisitionOrders = state.trackedBulletAcquisitionOrders
            == null
                ? new List<int>()
                : new List<int>(state.trackedBulletAcquisitionOrders);
        acquisitionOrder = Mathf.Max(0, state.acquisitionOrder);
    }

    public RunRelicSaveData CaptureState()
    {
        return new RunRelicSaveData
        {
            relicId = Id,
            stackCount = StackCount,
            remainingCharges = RemainingCharges,
            movementStacks = MovementStacks,
            storedDamage = StoredDamage,
            primaryCounter = PrimaryCounter,
            secondaryCounter = SecondaryCounter,
            storedValue = StoredValue,
            runtimeFlag = RuntimeFlag,
            trackedBulletAcquisitionOrders =
                new List<int>(TrackedBulletAcquisitionOrders),
            acquisitionOrder = AcquisitionOrder
        };
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }
}
