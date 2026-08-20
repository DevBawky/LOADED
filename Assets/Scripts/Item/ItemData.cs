using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class TooltipTextFormatter
{
    private const int MaximumCachedEntries = 512;
    public const string DamageColor = "#FF8066";
    public const string CriticalColor = "#FFB85C";
    public const string RangeColor = "#62D9FF";
    public const string StackColor = "#75A7FF";
    public const string BonusColor = "#67E480";
    public const string HealthColor = "#72E58D";
    public const string GoldColor = "#FFD45C";
    public const string PoisonColor = "#72D66B";
    public const string StunColor = "#FFE066";
    public const string MarkColor = "#FF78C8";
    public const string WeaknessColor = "#B58CFF";
    public const string DebuffColor = "#FF7A90";
    public const string LifeStealColor = "#FF6B7A";

    private static readonly Regex HighlightPattern = new Regex(
        @"(?<poison>중독|독)"
        + @"|(?<stun>기절)"
        + @"|(?<mark>표식)"
        + @"|(?<weakness>약화)"
        + @"|(?<debuff>상태\s*이상|디버프)"
        + @"|(?<lifesteal>흡혈)"
        + @"|(?<damage>최종\s*대미지|대미지|피해)"
        + @"|(?<critical>치명타|치명|크리티컬)"
        + @"|(?<range>유효\s*범위|사거리|범위|거리|약실)"
        + @"|(?<stack>스택|중첩|누적|포식|유산|집중|충전|축전|탄피)"
        + @"|(?<gold>골드)"
        + @"|(?<health>최대\s*체력|잃은\s*체력|체력)"
        + @"|(?<bonus>보너스|증가|추가)"
        + @"|(?<criticalStat>확률|배율|배수|퍼센트|위력)"
        + @"|(?<money>\$[ \t]*\d+(?:\.\d+)?)"
        + @"|(?<rangeValue>[+-]?[ \t]*\d+(?:\.\d+)?[ \t]*칸)"
        + @"|(?<percent>[+-]?[ \t]*\d+(?:\.\d+)?[ \t]*%p?|x[ \t]*\d+(?:\.\d+)?)"
        + @"|(?<number>[+-]?[ \t]*\d+(?:\.\d+)?)",
        RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, string> FormattedCache =
        new Dictionary<string, string>();

    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (FormattedCache.TryGetValue(text, out string formatted))
        {
            return formatted;
        }

        StringBuilder builder = new StringBuilder(text.Length + 64);
        int plainTextStart = 0;
        int colorDepth = 0;

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '<')
            {
                continue;
            }

            int tagEnd = text.IndexOf('>', index + 1);

            if (tagEnd < 0)
            {
                break;
            }

            AppendPlainText(
                builder,
                text.Substring(plainTextStart, index - plainTextStart),
                colorDepth == 0);

            string tag = text.Substring(index, tagEnd - index + 1);
            builder.Append(tag);

            if (tag.StartsWith("<color", System.StringComparison.OrdinalIgnoreCase))
            {
                colorDepth++;
            }
            else if (tag.Equals(
                "</color>",
                System.StringComparison.OrdinalIgnoreCase))
            {
                colorDepth = Mathf.Max(0, colorDepth - 1);
            }

            index = tagEnd;
            plainTextStart = tagEnd + 1;
        }

        AppendPlainText(
            builder,
            text.Substring(plainTextStart),
            colorDepth == 0);
        formatted = builder.ToString();

        if (FormattedCache.Count >= MaximumCachedEntries)
        {
            FormattedCache.Clear();
        }

        FormattedCache[text] = formatted;
        return formatted;
    }

    private static void AppendPlainText(
        StringBuilder builder,
        string text,
        bool applyHighlight)
    {
        builder.Append(applyHighlight
            ? HighlightPattern.Replace(text, ReplaceMatch)
            : text);
    }

    private static string ReplaceMatch(Match match)
    {
        string color = CriticalColor;

        if (match.Groups["poison"].Success)
        {
            color = PoisonColor;
        }
        else if (match.Groups["stun"].Success)
        {
            color = StunColor;
        }
        else if (match.Groups["mark"].Success)
        {
            color = MarkColor;
        }
        else if (match.Groups["weakness"].Success)
        {
            color = WeaknessColor;
        }
        else if (match.Groups["debuff"].Success)
        {
            color = DebuffColor;
        }
        else if (match.Groups["lifesteal"].Success)
        {
            color = LifeStealColor;
        }
        else if (match.Groups["damage"].Success)
        {
            color = DamageColor;
        }
        else if (match.Groups["range"].Success
            || match.Groups["rangeValue"].Success)
        {
            color = RangeColor;
        }
        else if (match.Groups["stack"].Success)
        {
            color = StackColor;
        }
        else if (match.Groups["health"].Success)
        {
            color = HealthColor;
        }
        else if (match.Groups["gold"].Success
            || match.Groups["money"].Success)
        {
            color = GoldColor;
        }
        else if (match.Groups["bonus"].Success)
        {
            color = BonusColor;
        }

        return $"<color={color}>{match.Value}</color>";
    }
}

public enum ItemEffectType
{
    Heal = 0,
    ReshuffleDeck = 1,
    SwapWithFrontEnemy = 2,
    PoisonAllEnemies = 3,
    StunAllEnemies = 4
}

[CreateAssetMenu(fileName = "New Item", menuName = "Loaded/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Min(0)]
    [SerializeField] private int price;

    [Header("Immediate Effect")]
    [SerializeField] private ItemEffectType effectType;
    [Min(0)]
    [Tooltip("Heal amount. Ignored by effects that do not use a numeric value.")]
    [SerializeField] private int effectAmount;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Price => Mathf.Max(0, price);
    public ItemEffectType EffectType => effectType;
    public int EffectAmount => Mathf.Max(0, effectAmount);

    public bool TryApply(
        PlayerHealth playerHealth,
        DeckManager deckManager,
        PlayerMove playerMove,
        WaveManager waveManager)
    {
        return effectType switch
        {
            ItemEffectType.Heal => playerHealth != null
                && playerHealth.Heal(EffectAmount),
            ItemEffectType.ReshuffleDeck => deckManager != null
                && deckManager.ReshuffleDeck(),
            ItemEffectType.SwapWithFrontEnemy =>
                TrySwapWithFrontEnemy(playerMove, waveManager),
            ItemEffectType.PoisonAllEnemies => ApplyStatusToAllEnemies(
                waveManager,
                StatusEffectType.Poison,
                EffectAmount),
            ItemEffectType.StunAllEnemies => ApplyStatusToAllEnemies(
                waveManager,
                StatusEffectType.Stun,
                EffectAmount),
            _ => false
        };
    }

    private static bool TrySwapWithFrontEnemy(
        PlayerMove playerMove,
        WaveManager waveManager)
    {
        if (playerMove == null || waveManager == null
            || !playerMove.CanStartAction)
        {
            return false;
        }

        int direction = playerMove.transform.localScale.x >= 0f ? 1 : -1;
        List<EnemyController> targets = new List<EnemyController>();
        waveManager.GetEnemiesInDirection(
            playerMove.transform.position,
            direction,
            int.MaxValue,
            targets);

        if (targets.Count == 0 || targets[0] == null)
        {
            return false;
        }

        playerMove.StartCoroutine(
            playerMove.SwapPositionWithEnemy(targets[0]));
        return true;
    }

    private static bool ApplyStatusToAllEnemies(
        WaveManager waveManager,
        StatusEffectType statusType,
        int stacks)
    {
        if (waveManager == null || stacks <= 0)
        {
            return false;
        }

        bool appliedAny = false;

        foreach (EnemyController enemy in waveManager.ActiveEnemies)
        {
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                appliedAny |= enemy.AddStatusEffect(
                    statusType,
                    stacks,
                    true);
            }
        }

        return appliedAny;
    }
}
