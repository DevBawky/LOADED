using System.Linq;

internal static class BattleTestCatalogFilters
{
    internal static readonly string[] RelicCategories = { "효과: 전체", "공격", "이동", "장전", "상태이상", "생존", "재화", "성장", "기타" };

    internal static bool Matches(BulletData bullet, int grade, int type) =>
        (grade == 0 || (int)bullet.Grade == grade - 1) && (type == 0 || (int)bullet.BulletType == type - 1);

    internal static bool Matches(RelicData relic, int lifetime, int effect) =>
        (lifetime == 0 || (int)relic.LifetimeType == lifetime - 1)
        && (effect == 0 || relic.Effects.Any(value => value != null && Category(value.EffectType) == effect)
            || effect == 8 && relic.Effects.Count == 0);

    internal static string RelicLabel(RelicData relic) =>
        (relic.LifetimeType == RelicLifetimeType.Consumable ? "소모형" : "지속형") + " · "
        + string.Join(" / ", relic.Effects.Where(value => value != null)
            .Select(value => RelicCategories[Category(value.EffectType)]).Distinct());

    private static int Category(RelicEffectType type) => type switch
    {
        RelicEffectType.MovementDamageMultiplier or RelicEffectType.Carriage or RelicEffectType.RunningSpur => 2,
        RelicEffectType.EmptyBeat or RelicEffectType.LuckyChamber => 3,
        RelicEffectType.InfectiousIncubator or RelicEffectType.MutationCatalyst => 4,
        RelicEffectType.PreventLethalDamage or RelicEffectType.BrinkTrigger => 5,
        RelicEffectType.GoldPanner => 6,
        RelicEffectType.FamilyWill => 7,
        RelicEffectType.None => 8,
        _ => 1
    };
}
