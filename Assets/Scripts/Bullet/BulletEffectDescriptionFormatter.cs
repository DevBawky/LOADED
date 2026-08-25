using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

internal static class BulletEffectDescriptionFormatter
{
    public static string Build(
        IReadOnlyList<BulletEffectData> effects,
        IReadOnlyList<BulletConditionalEventData> conditionalEvents,
        IReadOnlyList<PenetrationChanceData> penetrationChances)
    {
        List<string> lines = new List<string>();

        AppendEffects(lines, effects, string.Empty);

        if (conditionalEvents != null)
        {
            foreach (BulletConditionalEventData conditionalEvent
                     in conditionalEvents)
            {
                if (conditionalEvent == null)
                {
                    continue;
                }

                AppendEffects(
                    lines,
                    conditionalEvent.Events,
                    GetTriggerPrefix(conditionalEvent.Trigger));
            }
        }

        string penetration = DescribePenetration(penetrationChances);

        if (!string.IsNullOrEmpty(penetration))
        {
            lines.Add(penetration);
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        foreach (string line in lines)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("• ").Append(line);
        }

        return builder.ToString();
    }

    internal static string DescribeEffect(BulletEffectData effect)
    {
        if (effect == null)
        {
            return string.Empty;
        }

        string target = GetTargetName(effect.Target);
        string description = effect.EffectType switch
        {
            BulletEffectType.Poison =>
                $"{target}에게 독 +{GetStacks(effect)} "
                + "(턴 종료마다 현재 스택만큼 피해 후 1 감소)",
            BulletEffectType.Stun =>
                $"{target}에게 기절 +{GetStacks(effect)} "
                + "(스택당 행동 1회 차단)",
            BulletEffectType.Mark =>
                $"{target}에게 표식 +{GetStacks(effect)} "
                + "(직접 공격 피해 50% 증가, 턴 종료마다 1 감소)",
            BulletEffectType.Knockback =>
                $"{target}을 최대 {Mathf.Max(0, effect.KnockbackDistance)}칸 밀치기",
            BulletEffectType.PositionSwap =>
                $"{target}과 위치 교환",
            BulletEffectType.LifeSteal =>
                "가한 실제 피해만큼 플레이어 체력 회복",
            BulletEffectType.Weakness =>
                $"{target}에게 약화 +{GetStacks(effect)} "
                + "(직접 공격 피해 30% 감소, 턴 종료마다 1 감소)",
            BulletEffectType.IncreaseMaxHealth =>
                $"플레이어 최대 체력 +{FormatNumber(effect.Amount)}",
            BulletEffectType.DestroyBullet =>
                "발사한 탄환 영구 파괴",
            BulletEffectType.GainGold =>
                $"골드 +{FormatNumber(effect.Amount)}",
            BulletEffectType.Jackpot =>
                "발사 직전 마지막 약실이면 최종 피해 "
                + $"x{FormatNumber(effect.Amount / 100f)}",
            BulletEffectType.PowderPouch =>
                "자신을 파괴하고 남은 탄환들의 치명타 확률 "
                + $"+{FormatNumber(effect.Amount)}%p",
            BulletEffectType.StackNextShot =>
                "다음 탄환의 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}% (누적)",
            BulletEffectType.ClonePreviousShot =>
                "직전 탄환의 피해와 효과를 복제하고 복제 피해 "
                + $"x{FormatNumber(effect.Amount / 100f)}",
            BulletEffectType.ChainFire => DescribeChainFire(effect),
            BulletEffectType.Resonance =>
                "실린더에 남은 다른 공명탄 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Gilded =>
                $"보유 골드 {Mathf.Max(1, effect.StackCount)}당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Coagulation =>
                $"잃은 체력 {Mathf.Max(1, effect.StackCount)}%당 "
                + $"치명타 확률 +{FormatNumber(effect.Amount)}%p",
            BulletEffectType.Heart =>
                $"최대 체력 {Mathf.Max(1, effect.StackCount)}당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Saver =>
                "이번 실린더에서 파괴된 탄환이 없으면 발사 종료 후 골드 "
                + $"+{FormatNumber(effect.Amount)}"
                + (effect.StackCount >= 2 ? ", 사격 턴 환급" : string.Empty),
            BulletEffectType.QuickDraw =>
                "방향과 관계없이 생존한 모든 적 공격",
            BulletEffectType.Loader =>
                "발사 시작 시 빈 약실 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Rangefinder =>
                "대상과의 거리 1칸당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.WallImpact => DescribeWallImpact(effect),
            BulletEffectType.Judgment =>
                "대상의 상태이상 스택 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.StatusAmplifier =>
                "명중 대상의 활성 상태이상 스택 "
                + $"x{Mathf.Max(2, Mathf.RoundToInt(effect.Amount))}",
            BulletEffectType.VenomBurst => DescribeVenomBurst(effect),
            BulletEffectType.Crescendo =>
                "기본 피해에서 자신을 제외한 보유 탄환 하나당 "
                + $"{FormatNumber(effect.Amount)} 감소",
            BulletEffectType.Rebate =>
                $"치명타 시 골드 +{FormatNumber(effect.Amount)}",
            BulletEffectType.Distributor =>
                "누적된 다음 탄환 피해 보너스의 "
                + $"{FormatNumber(effect.Amount)}%를 "
                + "실린더에 남은 모든 탄환에 분배",
            BulletEffectType.Focus =>
                "앞선 탄환이 비치명타면 집중 "
                + $"+{Mathf.Max(1, effect.StackCount)}, 집중 하나당 "
                + $"치명타 확률 +{FormatNumber(effect.Amount)}%p, "
                + "치명타 시 모두 소모",
            BulletEffectType.Charge => DescribeCharge(effect),
            BulletEffectType.Accumulator =>
                "다른 탄환이 치명타를 발생시키면 전력 +1, "
                + $"전력 하나당 최종 피해 +{FormatNumber(effect.Amount)}%, "
                + $"발사 후 {Mathf.Clamp(effect.KnockbackDistance, 0, 100)}% 보존",
            BulletEffectType.ShellCollector =>
                "다른 사격마다 탄피 +1, 탄피 "
                + $"{Mathf.Max(1, effect.StackCount)}개당 "
                + $"{FormatNumber(effect.Amount)}% 위력으로 추가 사격 "
                + $"(최대 {Mathf.Max(1, effect.KnockbackDistance)}회)",
            BulletEffectType.Devourer =>
                $"적 처치 시 영구 포식 +{Mathf.Max(1, effect.StackCount)}, "
                + $"포식 하나당 최종 피해 +{FormatNumber(effect.Amount)}%",
            BulletEffectType.Legacy =>
                "다른 탄환이 영구 파괴될 때 유산 "
                + $"+{Mathf.Max(1, effect.StackCount)}, 유산 하나당 "
                + $"최종 피해 +{FormatNumber(effect.Amount)}%",
            BulletEffectType.Collection =>
                "보유한 서로 다른 탄환 종류 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.MixedGrade =>
                "실린더에 남은 다른 등급 탄환 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Masterpiece =>
                "보유한 에이스·레전드리 탄환 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.MassProduced =>
                "보유한 노멀·레어 탄환 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Monopoly =>
                "가장 많이 보유한 등급의 탄환 하나당 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.Seismometer =>
                "플레이어 이동 1칸당 이동 스택 +1, 이동 스택 하나당 "
                + $"최종 피해 +{FormatNumber(effect.Amount)}%, 발사 후 초기화",
            BulletEffectType.ReverseShot =>
                "플레이어가 바라보는 반대 방향으로 발사",
            BulletEffectType.RecoilShot =>
                "발사 후 반대 방향으로 플레이어 "
                + $"{Mathf.Max(1, effect.KnockbackDistance)}칸 이동",
            BulletEffectType.Finale =>
                "실린더의 마지막 탄환을 "
                + $"{FormatNumber(effect.Amount)}% 확률로 추가 발사",
            BulletEffectType.Spread =>
                "이후 모든 탄환의 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}% (누적)",
            BulletEffectType.Alzheimer =>
                "이 실린더에서 앞서 발사한 모든 탄환 재발사",
            BulletEffectType.Concentration =>
                "이후 모든 탄환의 치명타 확률 "
                + $"+{FormatNumber(effect.Amount)}%p (누적)",
            BulletEffectType.Ritual => DescribeRitual(effect),
            BulletEffectType.Immersion =>
                "다음 탄환의 치명타 배율 "
                + $"+{FormatNumber(effect.Amount)} (누적)",
            BulletEffectType.Tracking =>
                "탄환 효과로 이동할 때 추적 +1, 발사 시 추적 하나당 "
                + $"무작위 적에게 표식 +{GetStacks(effect)} 후 모두 소모",
            BulletEffectType.Assassination =>
                "이번 턴에 이미 피격된 대상에게 최종 피해 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.FleshForBone =>
                $"발사 시 플레이어 체력 -{FormatNumber(effect.Amount)}, "
                + $"기본 피해 +{BulletEffectUtility.GetFleshForBoneBonusDamage(effect.Amount)}",
            BulletEffectType.HighRoller =>
                "잃은 체력 비율에 따라 최종 피해 최대 "
                + $"+{FormatNumber(effect.Amount)}%",
            BulletEffectType.RotatePlayer =>
                "발사 후 플레이어가 반대 방향을 바라봄",
            _ => string.Empty
        };

        return UsesActivationRoll(effect.EffectType)
            ? AppendActivation(description, effect.ActivationChance)
            : description;
    }

    internal static string DescribePenetration(
        IReadOnlyList<PenetrationChanceData> penetrationChances)
    {
        if (penetrationChances == null || penetrationChances.Count == 0)
        {
            return string.Empty;
        }

        float firstChance = GetPenetrationChance(penetrationChances[0]);
        bool allEqual = true;

        for (int index = 1; index < penetrationChances.Count; index++)
        {
            if (!Mathf.Approximately(
                    firstChance,
                    GetPenetrationChance(penetrationChances[index])))
            {
                allEqual = false;
                break;
            }
        }

        if (allEqual)
        {
            string frequency = penetrationChances.Count == 1
                ? string.Empty
                : "각 ";
            return $"관통: 최대 {penetrationChances.Count}회 "
                + $"({frequency}{FormatNumber(firstChance)}%)";
        }

        StringBuilder chances = new StringBuilder();

        for (int index = 0; index < penetrationChances.Count; index++)
        {
            if (index > 0)
            {
                chances.Append(" / ");
            }

            chances.Append(index + 1)
                .Append("차 ")
                .Append(FormatNumber(
                    GetPenetrationChance(penetrationChances[index])))
                .Append('%');
        }

        return $"관통: 최대 {penetrationChances.Count}회 ({chances})";
    }

    private static void AppendEffects(
        List<string> lines,
        IReadOnlyList<BulletEffectData> effects,
        string prefix)
    {
        if (effects == null)
        {
            return;
        }

        foreach (BulletEffectData effect in effects)
        {
            string description = DescribeEffect(effect);

            if (!string.IsNullOrEmpty(description))
            {
                lines.Add(prefix + description);
            }
        }
    }

    private static string GetTriggerPrefix(BulletConditionalTrigger trigger)
    {
        return trigger switch
        {
            BulletConditionalTrigger.EnemyDefeated => "적 처치 시 ",
            BulletConditionalTrigger.CriticalHit => "치명타 시 ",
            BulletConditionalTrigger.Penetration => "관통 성공 시 ",
            BulletConditionalTrigger.EffectApplied => "효과 적용 시 ",
            _ => string.Empty
        };
    }

    private static string GetTargetName(BulletEffectTarget target)
    {
        return target switch
        {
            BulletEffectTarget.FiringPlayer => "플레이어",
            BulletEffectTarget.AllEnemies => "모든 적",
            _ => "명중한 적"
        };
    }

    private static int GetStacks(BulletEffectData effect)
    {
        return Mathf.Max(0, effect.StackCount);
    }

    private static string DescribeChainFire(BulletEffectData effect)
    {
        int maximumAdditionalShots = Mathf.Max(0, effect.StackCount);
        StringBuilder chances = new StringBuilder();

        for (int index = 0; index < maximumAdditionalShots; index++)
        {
            if (index > 0)
            {
                chances.Append('/');
            }

            float chance = Mathf.Clamp(
                effect.ActivationChance - effect.Amount * index,
                0f,
                100f);
            chances.Append(FormatNumber(chance)).Append('%');
        }

        return "추가 탄환 소모 없이 연속 사격"
            + (chances.Length > 0 ? $" ({chances})" : string.Empty);
    }

    private static string DescribeWallImpact(BulletEffectData effect)
    {
        int maximumDistance = Mathf.Clamp(effect.KnockbackDistance, 1, 3);
        StringBuilder transfers = new StringBuilder();

        for (int distance = 1; distance <= maximumDistance; distance++)
        {
            if (distance > 1)
            {
                transfers.Append(" / ");
            }

            transfers.Append(distance)
                .Append("칸 ")
                .Append(FormatNumber(
                    BulletEffectUtility.GetWallImpactTransferPercent(
                        effect,
                        distance)))
                .Append('%');
        }

        return $"명중한 적 뒤쪽에 최종 피해 전이 ({transfers})";
    }

    private static string DescribeVenomBurst(BulletEffectData effect)
    {
        string description = "명중 대상의 독을 모두 소비하고 남은 독 피해의 "
            + $"{FormatNumber(effect.Amount)}%를 즉시 적용";

        if (effect.KnockbackDistance > 0)
        {
            description += $", 생존 시 독 +{effect.KnockbackDistance}";
        }

        return description;
    }

    private static string DescribeCharge(BulletEffectData effect)
    {
        string maximumStacks = effect.StackCount == int.MaxValue
            ? "무제한"
            : Mathf.Max(0, effect.StackCount).ToString();
        return "실린더에 있는 동안 앞서 발사된 탄환 하나당 최종 피해 "
            + $"+{FormatNumber(effect.Amount)}% (최대 {maximumStacks}스택)";
    }

    private static string DescribeRitual(BulletEffectData effect)
    {
        string description =
            $"치명타 시 집중 +{Mathf.Max(1, effect.StackCount)}, "
            + $"집중 하나당 치명타 배율 +{FormatNumber(effect.Amount)}, "
            + "비치명타 시 집중 초기화";

        if (effect.ActivationChance > 0f)
        {
            description += ", "
                + $"{FormatNumber(Mathf.Clamp(effect.ActivationChance, 0f, 100f))}% "
                + "확률로 이 탄환 파괴";
        }

        return description;
    }

    private static bool UsesActivationRoll(BulletEffectType effectType)
    {
        return effectType == BulletEffectType.Poison
            || effectType == BulletEffectType.Stun
            || effectType == BulletEffectType.Mark
            || effectType == BulletEffectType.Knockback
            || effectType == BulletEffectType.PositionSwap
            || effectType == BulletEffectType.LifeSteal
            || effectType == BulletEffectType.Weakness
            || effectType == BulletEffectType.IncreaseMaxHealth
            || effectType == BulletEffectType.DestroyBullet
            || effectType == BulletEffectType.GainGold
            || effectType == BulletEffectType.StatusAmplifier
            || effectType == BulletEffectType.VenomBurst
            || effectType == BulletEffectType.Rebate
            || effectType == BulletEffectType.Alzheimer;
    }

    private static string AppendActivation(string description, float chance)
    {
        if (string.IsNullOrEmpty(description))
        {
            return string.Empty;
        }

        float clampedChance = Mathf.Clamp(chance, 0f, 100f);
        return clampedChance >= 100f
            ? description
            : $"{description} (발동 {FormatNumber(clampedChance)}%)";
    }

    private static float GetPenetrationChance(PenetrationChanceData chance)
    {
        return chance == null ? 0f : Mathf.Clamp(chance.Chance, 0f, 100f);
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }
}
