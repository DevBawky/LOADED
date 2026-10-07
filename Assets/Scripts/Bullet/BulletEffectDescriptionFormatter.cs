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
            foreach (BulletConditionalEventData conditionalEvent in conditionalEvents)
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
                $"{target}에게 독 +{GetStacks(effect)}을 부여합니다.",
            BulletEffectType.Stun =>
                $"{target}에게 기절 +{GetStacks(effect)}을 부여합니다.",
            BulletEffectType.Mark =>
                $"{target}에게 표식 +{GetStacks(effect)}을 부여합니다.",
            BulletEffectType.Knockback =>
                $"{target}을 최대 {Mathf.Max(0, effect.KnockbackDistance)}칸 밀칩니다.",
            BulletEffectType.PositionSwap =>
                $"{target}과 위치를 교환합니다.",
            BulletEffectType.LifeSteal =>
                "가한 실제 피해만큼 플레이어 체력을 회복합니다.",
            BulletEffectType.Weakness =>
                $"{target}에게 약화 +{GetStacks(effect)}을 부여합니다.",
            BulletEffectType.IncreaseMaxHealth =>
                $"플레이어의 최대 체력과 현재 체력을 각각 {FormatNumber(effect.Amount)} 증가시킵니다.",
            BulletEffectType.DestroyBullet =>
                $"{FormatNumber(Mathf.Clamp(effect.ActivationChance, 0f, 100f))}% "
                + "확률로 탄환이 영구 파괴됩니다.",
            BulletEffectType.GainGold =>
                $"골드를 {FormatNumber(effect.Amount)} 획득합니다.",
            BulletEffectType.Jackpot =>
                $"발사할 때 {FormatNumber(effect.ActivationChance)}% 확률로 대박이 납니다. "
                + $"대박 시 최종 피해 x{FormatNumber(effect.Amount / 100f)}, "
                + $"골드 +{Mathf.Max(0, effect.StackCount)}. "
                + "한 번의 실제 발사당 한 번만 추첨합니다.",
            BulletEffectType.PowderPouch =>
                "발사한 탄환을 영구 파괴합니다. "
                + "남은 탄환들의 치명타 확률을 "
                + $"+{FormatNumber(effect.Amount)}%p 증가시킵니다.",
            BulletEffectType.StackNextShot =>
                "다음 탄환의 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다. "
                + "피해 보너스는 누적됩니다.",
            BulletEffectType.ClonePreviousShot =>
                "직전 탄환의 피해와 효과를 복제합니다. "
                + "복제된 피해에는 "
                + $"x{FormatNumber(effect.Amount / 100f)} 배율을 적용합니다.",
            BulletEffectType.ChainFire => DescribeChainFire(effect),
            BulletEffectType.Resonance =>
                "직전 탄환이 부여한 상태이상 종류를 명중 대상에게 "
                + $"각각 {Mathf.Max(0, effect.StackCount)}스택 부여합니다.",
            BulletEffectType.Gilded =>
                $"보유 골드 {Mathf.Max(1, effect.StackCount)}당 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다.",
            BulletEffectType.Coagulation =>
                $"발사 후 잃은 체력의 {FormatNumber(effect.Amount)}%를 회복합니다. "
                + $"보유한 혈투 탄환 하나당 회복 비율 +{Mathf.Max(0, effect.StackCount)}%p "
                + $"(최대 {Mathf.Max(0, effect.KnockbackDistance)}%).",
            BulletEffectType.Heart =>
                $"최대 체력 {Mathf.Max(1, effect.StackCount)}당 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다.",
            BulletEffectType.Saver => DescribeSaver(effect),
            BulletEffectType.QuickDraw =>
                "방향과 관계없이 생존한 모든 적을 공격합니다.",
            BulletEffectType.Loader =>
                "발사 후 다음 재장전이 턴을 소모하지 않습니다.",
            BulletEffectType.Rangefinder =>
                "대상과의 거리 1칸당 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다.",
            BulletEffectType.WallImpact => DescribeWallImpact(effect),
            BulletEffectType.Judgment =>
                "대상의 상태이상 스택 하나당 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다.",
            BulletEffectType.StatusAmplifier =>
                "명중 대상의 활성 상태이상 스택을 "
                + $"x{Mathf.Max(2, Mathf.RoundToInt(effect.Amount))}배로 증가시킵니다.",
            BulletEffectType.VenomBurst => DescribeVenomBurst(effect),
            BulletEffectType.Crescendo =>
                "명중 시 독·표식·약화·기절 중 하나를 무작위로 "
                + $"{Mathf.Max(0, effect.StackCount)}스택 부여합니다.",
            BulletEffectType.Rebate =>
                $"치명타 발생 시 골드를 {FormatNumber(effect.Amount)} 획득합니다.",
            BulletEffectType.Distributor =>
                "누적된 다음 탄환 피해 보너스의 "
                + $"{FormatNumber(effect.Amount)}%를 실린더에 남은 모든 탄환에 분배합니다.",
            BulletEffectType.Focus =>
                "앞선 탄환이 비치명타이면 남은 탄환들이 "
                + $"집중 스택 +{Mathf.Max(1, effect.StackCount)}을 획득합니다. "
                + "집중 스택 하나당 치명타 확률이 "
                + $"+{FormatNumber(effect.Amount)}%p 증가합니다. "
                + "치명타 발생 시 모든 집중 스택을 소모합니다.",
            BulletEffectType.Charge => DescribeCharge(effect),
            BulletEffectType.Accumulator =>
                "다른 탄환이 치명타를 발생시키면 전력 스택 +1을 획득합니다. "
                + "전력 스택 하나당 최종 피해가 "
                + $"+{FormatNumber(effect.Amount)}% 증가합니다. "
                + $"발사 후 전력 스택의 {Mathf.Clamp(effect.KnockbackDistance, 0, 100)}%를 보존합니다.",
            BulletEffectType.ShellCollector =>
                "다른 사격이 끝날 때마다 탄피 스택 +1을 획득합니다. "
                + $"탄피 스택 {Mathf.Max(1, effect.StackCount)}개를 소모하여 "
                + $"{FormatNumber(effect.Amount)}% 위력으로 추가 사격합니다. "
                + $"한 번에 최대 {Mathf.Max(1, effect.KnockbackDistance)}회 추가 사격합니다.",
            BulletEffectType.Devourer =>
                $"적 처치 시 영구 포식 스택 +{Mathf.Max(1, effect.StackCount)}을 획득합니다. "
                + "포식 스택 하나당 최종 피해가 "
                + $"+{FormatNumber(effect.Amount)}% 증가합니다.",
            BulletEffectType.Legacy =>
                "다른 탄환이 영구 파괴될 때 유산 스택 "
                + $"+{Mathf.Max(1, effect.StackCount)}을 획득합니다. "
                + "유산 스택 하나당 최종 피해가 "
                + $"+{FormatNumber(effect.Amount)}% 증가합니다.",
            BulletEffectType.Collection =>
                "이 탄환으로 적을 처치하면 생존한 적 중 현재 체력이 가장 낮은 "
                + $"적에게 즉시 다시 발사합니다. 최대 {Mathf.Max(0, effect.StackCount)}회 연쇄합니다.",
            BulletEffectType.MixedGrade =>
                "명중 대상에게 독·표식·약화를 "
                + $"각각 {Mathf.Max(0, effect.StackCount)}스택 부여합니다.",
            BulletEffectType.Masterpiece =>
                "치명타가 확정됩니다. 보유한 에이스·레전드리 탄환 하나당 "
                + $"치명타 배율 +{FormatNumber(effect.Amount)} "
                + $"(최대 {Mathf.Max(0, effect.StackCount)}개).",
            BulletEffectType.MassProduced =>
                "보유한 노멀·레어 탄환 하나당 추가 공격합니다 "
                + $"(최대 {Mathf.Max(0, effect.StackCount)}회).",
            BulletEffectType.Monopoly =>
                "명중 지점에서 플레이어에게 되돌아오며 같은 레인의 귀환 경로에 "
                + $"있는 적에게 원래 피해의 {FormatNumber(effect.Amount)}%를 줍니다.",
            BulletEffectType.Seismometer =>
                "플레이어가 1칸 이동할 때마다 이동 스택 +1을 획득합니다. "
                + "이동 스택 하나당 최종 피해가 "
                + $"+{FormatNumber(effect.Amount)}% 증가합니다. "
                + "발사 후 이동 스택을 초기화합니다.",
            BulletEffectType.ReverseShot =>
                "플레이어가 바라보는 반대 방향으로 발사합니다.",
            BulletEffectType.RecoilShot =>
                "발사 후 플레이어가 반대 방향으로 "
                + $"{Mathf.Max(1, effect.KnockbackDistance)}칸 이동합니다.",
            BulletEffectType.Finale =>
                "실린더의 마지막 탄환이면 "
                + $"{FormatNumber(effect.Amount)}% 확률로 추가 발사합니다.",
            BulletEffectType.Spread =>
                "이후 모든 탄환의 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다. "
                + "피해 보너스는 누적됩니다.",
            BulletEffectType.Alzheimer =>
                "현재 실린더에서 앞서 발사한 모든 탄환의 효과를 다시 적용합니다.",
            BulletEffectType.Concentration =>
                "이후 모든 탄환의 치명타 확률을 "
                + $"+{FormatNumber(effect.Amount)}%p 증가시킵니다. "
                + "치명타 확률 보너스는 누적됩니다.",
            BulletEffectType.Ritual => DescribeRitual(effect),
            BulletEffectType.Immersion =>
                "다음 탄환의 치명타 배율을 "
                + $"+{FormatNumber(effect.Amount)} 증가시킵니다. "
                + "치명타 배율 보너스는 누적됩니다.",
            BulletEffectType.Tracking =>
                "모든 레인에서 상태이상이 있는 모든 적을 공격합니다.",
            BulletEffectType.Assassination =>
                "상태이상 스택이 가장 많은 적을 조준하고, 스택당 최종 피해를 "
                + $"+{FormatNumber(effect.Amount)}% 증가시킵니다.",
            BulletEffectType.FleshForBone =>
                $"발사 시 플레이어가 체력 {FormatNumber(effect.Amount)}을 잃습니다. "
                + "이 탄환의 기본 피해를 "
                + $"+{BulletEffectUtility.GetFleshForBoneBonusDamage(effect.Amount)} 증가시킵니다.",
            BulletEffectType.HighRoller =>
                "잃은 체력 비율에 따라 최종 피해가 최대 "
                + $"+{FormatNumber(effect.Amount)}% 증가합니다.",
            BulletEffectType.RotatePlayer =>
                "발사 후 플레이어가 반대 방향을 바라봅니다.",
            BulletEffectType.Mastery =>
                "최대 체력이 가장 높은 적을 조준하고, 이번 실린더에서 앞서 발사한 "
                + $"저격 탄환 1발당 최종 피해를 x{FormatNumber(effect.Amount)}로 증가시킵니다.",
            BulletEffectType.Cataclysm =>
                "모든 레인의 모든 적을 공격합니다.",
            BulletEffectType.FocusedShotgun =>
                "8개 팰릿을 발사합니다. 여러 레인에 적이 있으면 기준 레인에 4발, "
                + "나머지 레인에 남은 팰릿을 균등 분배합니다.",
            BulletEffectType.Vanguard =>
                $"실린더에서 첫 번째로 발사되면 최종 피해 +{FormatNumber(effect.Amount)}%.",
            BulletEffectType.Finisher =>
                $"실린더에서 마지막으로 발사되면 최종 피해 +{FormatNumber(effect.Amount)}%.",
            BulletEffectType.SpecterReturn =>
                $"발사 후 {FormatNumber(effect.ActivationChance)}% 확률로 무덤 대신 덱 맨 위로 돌아갑니다.",
            BulletEffectType.Necromancy =>
                $"함께 장전된 다른 유령 탄환 하나당 최종 피해 +{FormatNumber(effect.Amount)}% "
                + $"(최대 {Mathf.Max(0, effect.StackCount)}개).",
            BulletEffectType.Hunt =>
                "현재 플레이어 레인에서 현재 체력이 가장 낮은 적을 조준합니다.",
            BulletEffectType.LockOn =>
                "직전 물리 탄환의 주 대상이 생존해 있으면 다시 조준하고, "
                + $"같은 대상에게 최종 피해 +{FormatNumber(effect.Amount)}%.",
            BulletEffectType.Execution =>
                "모든 레인에서 체력 비율이 가장 낮은 적을 조준합니다. "
                + "보스가 아닌 적이 공격 전 최대 체력의 25% 미만이면 즉시 처형합니다.",
            BulletEffectType.Blink =>
                "발사 후 점유되지 않은 무작위 타일로 순간이동합니다.",
            BulletEffectType.RandomPelletDamage =>
                $"각 팰릿의 기본 피해를 {FormatNumber(effect.Amount)}~{Mathf.Max(0, effect.StackCount)} 사이에서 독립 결정합니다.",
            _ => string.Empty
        };

        string activatedDescription = UsesActivationRoll(effect.EffectType)
            ? AppendActivation(description, effect.ActivationChance)
            : description;
        return BreakAfterFormalSentenceEndings(activatedDescription);
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
            if (penetrationChances.Count == 1)
            {
                return BreakAfterFormalSentenceEndings(
                    $"관통은 최대 1회입니다. "
                    + $"관통 성공 확률은 {FormatNumber(firstChance)}%입니다.");
            }

            return BreakAfterFormalSentenceEndings(
                $"관통은 최대 {penetrationChances.Count}회입니다. "
                + $"각 관통의 성공 확률은 {FormatNumber(firstChance)}%입니다.");
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

        return BreakAfterFormalSentenceEndings(
            $"관통은 최대 {penetrationChances.Count}회입니다. "
            + $"단계별 성공 확률은 {chances}입니다.");
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

    private static string DescribeSaver(BulletEffectData effect)
    {
        string description = "이번 실린더에서 탄환이 파괴되지 않으면 발사 종료 후 골드를 "
            + $"{FormatNumber(effect.Amount)} 획득합니다.";

        if (effect.StackCount >= 2)
        {
            description += " 같은 조건을 충족하면 사격에 사용한 턴을 돌려받습니다.";
        }

        return description;
    }

    private static string DescribeChainFire(BulletEffectData effect)
    {
        int maximumAdditionalShots = Mathf.Max(0, effect.StackCount);
        StringBuilder chances = new StringBuilder();

        for (int index = 0; index < maximumAdditionalShots; index++)
        {
            if (index > 0)
            {
                chances.Append(" / ");
            }

            float chance = Mathf.Clamp(
                effect.ActivationChance - effect.Amount * index,
                0f,
                100f);
            chances.Append(FormatNumber(chance)).Append('%');
        }

        string description = "추가 탄환을 소모하지 않고 연속 사격합니다.";
        return chances.Length > 0
            ? description + $" 추가 사격별 성공 확률은 {chances}입니다."
            : description;
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

        return "명중한 적 뒤쪽으로 최종 피해를 전이합니다. "
            + $"거리별 전이 비율은 {transfers}입니다.";
    }

    private static string DescribeVenomBurst(BulletEffectData effect)
    {
        string description = "명중 대상의 독 스택을 모두 소비합니다. "
            + "남은 독 피해의 "
            + $"{FormatNumber(effect.Amount)}%를 즉시 적용합니다.";

        if (effect.KnockbackDistance > 0)
        {
            description += $" 대상이 생존하면 독 +{effect.KnockbackDistance}을 부여합니다.";
        }

        return description;
    }

    private static string DescribeCharge(BulletEffectData effect)
    {
        string description = "실린더에 있는 동안 앞서 발사된 탄환 하나당 최종 피해가 "
            + $"+{FormatNumber(effect.Amount)}% 증가합니다.";

        if (effect.StackCount == int.MaxValue)
        {
            return description + " 충전 스택에는 상한이 없습니다.";
        }

        return description
            + $" 최대 충전 스택은 {Mathf.Max(0, effect.StackCount)}입니다.";
    }

    private static string DescribeRitual(BulletEffectData effect)
    {
        return $"발사할 때 최대 체력을 {Mathf.Max(1, effect.StackCount)} 지불합니다. "
            + $"이후 이번 런 동안 이 탄환의 기본 피해가 영구적으로 "
            + $"+{FormatNumber(effect.Amount)} 증가합니다.";
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
            : $"{description} 발동 확률은 {FormatNumber(clampedChance)}%입니다.";
    }

    private static string BreakAfterFormalSentenceEndings(string description)
    {
        return string.IsNullOrEmpty(description)
            ? string.Empty
            : description.Replace("니다. ", "니다.\n• ");
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
