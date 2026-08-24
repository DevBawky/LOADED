using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class EventSelectorTests
{
    [Test]
    public void Select_ExcludesCompletedEvent_EvenWhenRepeatable()
    {
        EventDefinition completed = CreateEvent("completed", false, 100f);
        EventDefinition available = CreateEvent("available", true, 1f);

        try
        {
            EventDefinition selected = EventSelector.Select(
                new[] { completed, available },
                default,
                new[] { completed.StableId });

            Assert.That(selected, Is.SameAs(available));
        }
        finally
        {
            Object.DestroyImmediate(completed);
            Object.DestroyImmediate(available);
        }
    }

    [Test]
    public void BulletGroup_RequiresDistinctTypesAndSameGrade()
    {
        BulletData first = CreateBullet(BulletGrade.Rare);
        BulletData second = CreateBullet(BulletGrade.Rare);
        BulletData ace = CreateBullet(BulletGrade.Ace);

        try
        {
            BulletInstance firstInstance = new BulletInstance(first, 0);
            BulletInstance secondInstance = new BulletInstance(second, 1);
            BulletInstance duplicateType = new BulletInstance(first, 2);
            BulletInstance aceInstance = new BulletInstance(ace, 3);

            Assert.That(EventRuntimeRules.IsValidBulletGroup(
                new[] { firstInstance, secondInstance },
                2,
                true,
                true), Is.True);
            Assert.That(EventRuntimeRules.IsValidBulletGroup(
                new[] { firstInstance, duplicateType },
                2,
                true,
                true), Is.False);
            Assert.That(EventRuntimeRules.IsValidBulletGroup(
                new[] { firstInstance, aceInstance },
                2,
                true,
                true), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(ace);
        }
    }

    [Test]
    public void EventNodeDestination_UsesConfiguredDestinationBands()
    {
        EventDefinition definition = CreateEvent("route", true, 1f);

        try
        {
            definition.normalBattleChancePercent = 100f;
            Assert.That(EventRuntimeRules.SelectNodeDestination(definition),
                Is.EqualTo(EventFollowUpDestination.NormalBattle));

            definition.normalBattleChancePercent = 0f;
            definition.eliteBattleChancePercent = 100f;
            Assert.That(EventRuntimeRules.SelectNodeDestination(definition),
                Is.EqualTo(EventFollowUpDestination.EliteBattle));

            definition.eliteBattleChancePercent = 0f;
            definition.shopChancePercent = 100f;
            Assert.That(EventRuntimeRules.SelectNodeDestination(definition),
                Is.EqualTo(EventFollowUpDestination.Shop));

            definition.shopChancePercent = 0f;
            Assert.That(EventRuntimeRules.SelectNodeDestination(definition),
                Is.EqualTo(EventFollowUpDestination.NodeMap));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [TestCase(EventFollowUpDestination.NodeMap, "Event")]
    [TestCase(EventFollowUpDestination.NormalBattle, "Battle")]
    [TestCase(EventFollowUpDestination.EliteBattle, "Battle")]
    [TestCase(EventFollowUpDestination.Shop, "Shop")]
    public void EventNodeDestination_MapsToEntryScene(
        EventFollowUpDestination destination,
        string expectedScene)
    {
        Assert.That(
            EventRuntimeRules.GetNodeEntrySceneName(destination),
            Is.EqualTo(expectedScene));
    }

    [Test]
    public void ActiveEventNodeScene_ResumesChosenEntryWithoutRerolling()
    {
        RunSaveData substitutedShop = new RunSaveData
        {
            activeEventId = string.Empty,
            eventFollowUpDestination =
                (int)EventFollowUpDestination.Shop
        };
        RunSaveData selectedChoiceEvent = new RunSaveData
        {
            activeEventId = "selected-event",
            eventFollowUpDestination =
                (int)EventFollowUpDestination.NormalBattle
        };

        Assert.That(
            NodeMapSaveSystem.ResolveActiveEventNodeScene(substitutedShop),
            Is.EqualTo("Shop"));
        Assert.That(
            NodeMapSaveSystem.ResolveActiveEventNodeScene(
                selectedChoiceEvent),
            Is.EqualTo("Event"));
    }

    [Test]
    public void AuthoredEventCatalog_LoadsAllNewEventsWithValidChoiceCounts()
    {
        string[] expectedIds =
        {
            "suspicious-equivalent-exchange",
            "blood-drinking-devil",
            "back-alley-gambler",
            "suspiciously-kind-tycoon",
            "fountain-of-wisdom",
            "golden-well",
            "confiscation",
            "poison-proof-body",
            "remains-of-war",
            "bogus-trial",
            "talkative-old-man"
        };
        EventDefinition[] definitions =
            Resources.LoadAll<EventDefinition>("Events");

        Assert.That(definitions, Is.Not.Empty);
        foreach (EventDefinition definition in definitions)
        {
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.normalBattleChancePercent
                + definition.eliteBattleChancePercent
                + definition.shopChancePercent, Is.LessThanOrEqualTo(100f),
                definition.name);
        }

        foreach (string expectedId in expectedIds)
        {
            EventDefinition definition = definitions.FirstOrDefault(
                candidate => candidate != null
                    && candidate.StableId == expectedId);
            Assert.That(definition, Is.Not.Null, expectedId);
            Assert.That(definition.choices, Has.Length.InRange(1, 3),
                expectedId);
        }

        EventDefinition poisonEvent = definitions.First(definition =>
            definition.StableId == "poison-proof-body");
        Assert.That(poisonEvent.choices[0].effects.Count(effect =>
            effect.type == EventEffectType.AddBullet
            && effect.bullet != null), Is.EqualTo(2));
        Assert.That(poisonEvent.choices[0].effects.Any(effect =>
            effect.type == EventEffectType.AddItem
            && effect.item != null), Is.True);
    }

    private static EventDefinition CreateEvent(
        string id,
        bool oncePerRun,
        float weight)
    {
        EventDefinition definition =
            ScriptableObject.CreateInstance<EventDefinition>();
        definition.eventId = id;
        definition.oncePerRun = oncePerRun;
        definition.baseWeight = weight;
        return definition;
    }

    private static BulletData CreateBullet(BulletGrade grade)
    {
        BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
        SerializedObject serialized = new SerializedObject(bullet);
        serialized.FindProperty("grade").enumValueIndex = (int)grade;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return bullet;
    }
}
