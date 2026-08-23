using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EventChoiceTextFormatterTests
{
    private readonly List<ScriptableObject> createdAssets =
        new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (ScriptableObject asset in createdAssets)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        createdAssets.Clear();
    }

    [Test]
    public void Format_HighlightsActionAndStandardKeywords()
    {
        EventChoiceTextFormatter formatter = CreateFormatter();

        string formatted = formatter.Format(
            "[거래] 탄환을 무료로 강화하고 20 골드를 낸다.",
            new EventChoiceData(),
            true,
            string.Empty);

        Assert.That(formatted, Is.EqualTo(
            "<color=#FF0000>[거래]</color> "
            + "<color=#00FFFF>탄환</color>을 "
            + "<color=#FFEB04>무료</color>로 "
            + "<color=#00FF00>강화</color>하고 "
            + "<color=#FF00FF>20 골드</color>를 낸다."));
    }

    [Test]
    public void Format_AppendsUnavailableReason()
    {
        EventChoiceTextFormatter formatter = CreateFormatter();

        string formatted = formatter.Format(
            "[구매] 아이템을 얻는다.",
            new EventChoiceData(),
            false,
            "공간이 부족합니다.");

        Assert.That(formatted, Does.EndWith(
            "\n<size=70%><color=#808080>공간이 부족합니다.</color></size>"));
    }

    [Test]
    public void Format_HighlightsConfiguredRewardNameBeforeGenericKeyword()
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        createdAssets.Add(item);
        item.name = "GoldenApple";
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("displayName").stringValue = "황금 아이템";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EventChoiceData choice = new EventChoiceData
        {
            effects = new[]
            {
                new EventEffect
                {
                    type = EventEffectType.AddItem,
                    item = item
                }
            }
        };

        string formatted = CreateFormatter().Format(
            "[획득] 황금 아이템을 받는다.",
            choice,
            true,
            string.Empty);

        Assert.That(formatted,
            Does.Contain("<color=#00FFFF>황금 아이템</color>"));
    }

    private static EventChoiceTextFormatter CreateFormatter()
    {
        return new EventChoiceTextFormatter(
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.gray);
    }
}
