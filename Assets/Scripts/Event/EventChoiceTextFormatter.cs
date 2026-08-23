using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

internal sealed class EventChoiceTextFormatter
{
    private readonly Color actionNameColor;
    private readonly Color upgradeKeywordColor;
    private readonly Color removeKeywordColor;
    private readonly Color freeKeywordColor;
    private readonly Color costKeywordColor;
    private readonly Color rewardNameColor;
    private readonly Color unavailableReasonColor;

    public EventChoiceTextFormatter(
        Color actionNameColor,
        Color upgradeKeywordColor,
        Color removeKeywordColor,
        Color freeKeywordColor,
        Color costKeywordColor,
        Color rewardNameColor,
        Color unavailableReasonColor)
    {
        this.actionNameColor = actionNameColor;
        this.upgradeKeywordColor = upgradeKeywordColor;
        this.removeKeywordColor = removeKeywordColor;
        this.freeKeywordColor = freeKeywordColor;
        this.costKeywordColor = costKeywordColor;
        this.rewardNameColor = rewardNameColor;
        this.unavailableReasonColor = unavailableReasonColor;
    }

    public string Format(
        string source,
        EventChoiceData choice,
        bool available,
        string unavailableReason)
    {
        source ??= string.Empty;
        Match actionMatch = Regex.Match(source, @"^\s*(\[[^\]]+\])");
        string action = actionMatch.Success
            ? actionMatch.Groups[1].Value
            : string.Empty;
        string body = actionMatch.Success
            ? source.Substring(actionMatch.Length).TrimStart()
            : source;

        string formatted = string.IsNullOrEmpty(action)
            ? HighlightBody(body, choice)
            : $"{Colorize(action, actionNameColor)} "
                + HighlightBody(body, choice);

        if (!available && !string.IsNullOrWhiteSpace(unavailableReason))
        {
            formatted += "\n<size=70%>"
                + Colorize(unavailableReason, unavailableReasonColor)
                + "</size>";
        }

        return formatted;
    }

    private string HighlightBody(string body, EventChoiceData choice)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        Dictionary<string, Color> namedRewards =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        foreach (EventEffect effect in choice?.effects
                     ?? Array.Empty<EventEffect>())
        {
            if (effect?.bullet != null)
            {
                AddHighlightName(
                    namedRewards,
                    effect.bullet.GetDisplayName(0),
                    rewardNameColor);
                AddHighlightName(
                    namedRewards,
                    effect.bullet.name,
                    rewardNameColor);
            }

            if (effect?.item != null)
            {
                AddHighlightName(
                    namedRewards,
                    string.IsNullOrWhiteSpace(effect.item.DisplayName)
                        ? effect.item.name
                        : effect.item.DisplayName,
                    rewardNameColor);
                AddHighlightName(
                    namedRewards,
                    effect.item.name,
                    rewardNameColor);
            }
        }

        List<string> patterns = namedRewards.Keys
            .OrderByDescending(value => value.Length)
            .Select(Regex.Escape)
            .ToList();
        patterns.Add(@"\d+\s*(?:골드|원)");
        patterns.Add(@"강화|제거|무료|비용|골드|탄환|아이템");
        Regex highlightPattern = new Regex(
            string.Join("|", patterns),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return highlightPattern.Replace(body, match =>
        {
            if (namedRewards.TryGetValue(match.Value, out Color rewardColor))
            {
                return Colorize(match.Value, rewardColor);
            }

            if (match.Value.Contains("강화"))
            {
                return Colorize(match.Value, upgradeKeywordColor);
            }

            if (match.Value.Contains("제거"))
            {
                return Colorize(match.Value, removeKeywordColor);
            }

            if (match.Value.Contains("무료"))
            {
                return Colorize(match.Value, freeKeywordColor);
            }

            if (Regex.IsMatch(match.Value, @"\d")
                || match.Value.Contains("비용")
                || match.Value.Contains("골드"))
            {
                return Colorize(match.Value, costKeywordColor);
            }

            return Colorize(match.Value, rewardNameColor);
        });
    }

    private static void AddHighlightName(
        IDictionary<string, Color> names,
        string value,
        Color color)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            names[value.Trim()] = color;
        }
    }

    private static string Colorize(string value, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>";
    }
}
