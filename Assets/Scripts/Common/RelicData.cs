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
    BrinkTrigger = 23,
    AdvancedScope = 24,
    RunningSpur = 25
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
    [Min(0)]
    [Tooltip("모든 탄환 사거리에 추가되는 거리(타일).")]
    [SerializeField] private int shotRangeBonus = 1;

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
    public int ShotRangeBonus => Mathf.Max(0, shotRangeBonus);

    public string GetAbilityDescription()
    {
        return effectType switch
        {
            RelicEffectType.PreventLethalDamage =>
                $"치명 피해 1회 방지, 체력 {SurvivingHealth} 유지",
            RelicEffectType.MovementDamageMultiplier =>
                "이동 1칸당 다음 공격 최종 피해 "
                + $"x{FormatNumber(MovementDamageMultiplierPerStack)}\n"
                + $"스택 소모: {GetResetLabel(MovementStackReset)}",
            RelicEffectType.FirstShotFinalMultiplier =>
                "실린더 첫 사격 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}",
            RelicEffectType.LastShotFinalMultiplier =>
                "실린더 마지막 사격 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}",
            RelicEffectType.PredatorHolster =>
                "적 처치 시 다음 장전 탄환 1발 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}",
            RelicEffectType.ClosedCircuit =>
                "피해 시 뒤의 가장 가까운 적에게 피해 "
                + $"{FormatNumber(DebuffTransferPercent)}% 전이",
            RelicEffectType.InfectiousIncubator =>
                "디버프 보유 적 대상 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}\n"
                + "해당 적 처치 시 디버프 "
                + $"{FormatNumber(DebuffTransferPercent)}%를 가장 가까운 적에게 이전",
            RelicEffectType.EmptyBeat =>
                "재장전 시 턴 소모 없음 "
                + $"({FormatNumber(PrimerBaseChance)}%)",
            RelicEffectType.EyeOfTheStorm =>
                "실린더 내 모든 적 공격 시 최고 단일 피해의 "
                + $"{FormatNumber(StormDamagePercent)}%를 생존 적 전체에 적용",
            RelicEffectType.Carriage =>
                $"발차기 피해 x{FormatNumber(FinalDamageMultiplier)}\n"
                + $"{MovementTilesPerFreeReload}칸 이동마다 무료 재장전 +1 "
                + $"(최대 {FreeReloadStorageLimit}회 저장)",
            RelicEffectType.GoldPanner =>
                $"적 처치 골드 x{NuggetsRequired} "
                + $"({FormatNumber(GoldNuggetChance)}%)",
            RelicEffectType.CrackedPrimer =>
                "탄환 발사 시 추가 발사 "
                + $"({FormatNumber(PrimerBaseChance)}%)",
            RelicEffectType.Scale =>
                "생존 적 수가 적을수록 최종 피해 증가\n"
                + $"증가량: max(0, {FormatNumber(ScaleMaximumDamagePercent)}"
                + $"-{FormatNumber(PrimerFailureChanceBonus)}×적 수)%",
            RelicEffectType.FamilyWill =>
                "보스 처치 시 모든 탄환 최종 피해 영구 "
                + $"+{FormatNumber(MemorialDamagePercentPerBullet)}%",
            RelicEffectType.LuckyChamber =>
                "6발 장전 시 무작위 탄환 1발의 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}",
            RelicEffectType.ExecutionersOath =>
                "적 처치 시 다음 탄환 최종 피해 "
                + $"x{FormatNumber(FinalDamageMultiplier)}\n"
                + "연속 처치 시 효과 유지",
            RelicEffectType.MutationCatalyst =>
                "디버프 보유 적 명중 시 무작위 디버프 +1 "
                + $"({FormatNumber(MutationMaximumChance)}%)",
            RelicEffectType.BrinkTrigger =>
                $"체력 {FormatNumber(BrinkHealthThresholdPercent)}% 이하: "
                + $"모든 탄환 최종 피해 x{FormatNumber(FinalDamageMultiplier)}",
            RelicEffectType.AdvancedScope =>
                $"모든 탄환 사거리 +{ShotRangeBonus}",
            RelicEffectType.RunningSpur =>
                "이동 시 턴 소모 없음 "
                + $"({FormatNumber(PrimerBaseChance)}%)",
            _ => "효과 없음"
        };
    }

    public double GetExecutionMultiplier(int streak)
    {
        return streak <= 0
            ? 1d
            : Math.Max(1d, FinalDamageMultiplier);
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
    public string Description
    {
        get
        {
            string summary = BuildEffectSummary();
            return string.IsNullOrWhiteSpace(summary)
                ? description ?? string.Empty
                : summary;
        }
    }
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
