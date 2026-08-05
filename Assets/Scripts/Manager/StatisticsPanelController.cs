using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatisticsPanelController : MonoBehaviour
{
    private const string ValueObjectName = "Text | Value";
    private const string LabelObjectName = "Text | Element Name";

    private readonly Dictionary<string, TMP_Text> values =
        new Dictionary<string, TMP_Text>();

    public static void EnsureExists()
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate.name != "Panel | Statistics")
            {
                continue;
            }

            if (candidate.GetComponent<StatisticsPanelController>() == null)
            {
                candidate.gameObject.AddComponent<StatisticsPanelController>();
            }

            return;
        }
    }

    private void OnEnable()
    {
        BindValues();
        Refresh();
    }

    public void Refresh()
    {
        GameStatisticsData statistics = GameStatistics.Data;
        double winRate = statistics.totalPlays <= 0
            ? 0d
            : statistics.wins * 100d / statistics.totalPlays;

        SetValue("Element | Total Plays", Format(statistics.totalPlays));
        SetValue(
            "Element | Wins",
            $"{Format(statistics.wins)} ({winRate.ToString("F2", CultureInfo.InvariantCulture)}%)");
        SetValue("Element | Total Kills", Format(statistics.totalKills));
        SetValue("Element | Total Damage", Format(statistics.totalDamage));
        SetValue(
            "Element | Used Bullet",
            Format(statistics.totalBulletsFired));
        SetValue(
            "Element | Highest Cylinder Damage",
            Format(statistics.highestCylinderDamage));
        SetValue(
            "Element | Highest Damage",
            Format(statistics.highestSingleHitDamage));
        SetValue("Element | Many Use", GameStatistics.GetMostUsedBulletName());
        SetValue(
            "Element | Best Combo Kill",
            Format(statistics.highestComboKills));
        SetValue("Element | Use Money", Format(statistics.goldSpent));
    }

    private void BindValues()
    {
        values.Clear();
        Transform layout = FindDescendant(transform, "Layout | Statistics");

        if (layout == null)
        {
            Debug.LogWarning("Statistics layout was not found.", this);
            return;
        }

        foreach (Transform element in layout)
        {
            string canonicalName = GetCanonicalElementName(element.name);
            Transform valueTransform = FindDescendant(
                element,
                ValueObjectName);
            TMP_Text valueText = valueTransform == null
                ? null
                : valueTransform.GetComponent<TMP_Text>();

            if (valueText != null)
            {
                values[canonicalName] = valueText;
            }

            UpdateLabel(element, canonicalName);
        }
    }

    private static string GetCanonicalElementName(string elementName)
    {
        return elementName switch
        {
            "Element | Average Damage" => "Element | Total Plays",
            "Element | Average Cylinder Damage" => "Element | Wins",
            _ => elementName
        };
    }

    private static void UpdateLabel(Transform element, string canonicalName)
    {
        Transform labelTransform = FindDescendant(element, LabelObjectName);
        TMP_Text label = labelTransform == null
            ? null
            : labelTransform.GetComponent<TMP_Text>();

        if (label == null)
        {
            return;
        }

        label.text = canonicalName switch
        {
            "Element | Total Plays" => "총 플레이 수 :",
            "Element | Wins" => "승리 (승률) :",
            _ => label.text
        };
    }

    private void SetValue(string elementName, string value)
    {
        if (values.TryGetValue(elementName, out TMP_Text valueText))
        {
            valueText.text = value;
        }
    }

    private static string Format(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
            {
                return child;
            }

            Transform match = FindDescendant(child, name);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
