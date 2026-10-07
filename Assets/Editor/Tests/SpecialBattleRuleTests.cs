using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SpecialBattleRuleTests
{
    [Test]
    public void AuthoredAssets_ProvideUnifiedNodeIconsAndDedicatedBombProfile()
    {
        NodeMapSettings settings = AssetDatabase.LoadAssetAtPath<
            NodeMapSettings>("Assets/Resources/NodeMapSettings.asset");
        SpecialBattleBombProfile profile = AssetDatabase.LoadAssetAtPath<
            SpecialBattleBombProfile>(
                "Assets/Resources/SpecialBattle/"
                + "SpecialBattleBombProfile.asset");

        Assert.That(settings, Is.Not.Null);
        AssertIcon(
            settings,
            NodeMapNodeType.Start,
            "Assets/Sprites/UI/NodeMap/NodeIcon_Start.png");
        AssertIcon(
            settings,
            NodeMapNodeType.Shop,
            "Assets/Sprites/UI/NodeMap/NodeIcon_Store.png");
        AssertIcon(
            settings,
            NodeMapNodeType.Treasure,
            "Assets/Sprites/UI/NodeMap/NodeIcon_Reward.png");
        AssertIcon(
            settings,
            NodeMapNodeType.Event,
            "Assets/Sprites/UI/NodeMap/NodeIcon_Event.png");
        AssertIcon(
            settings,
            NodeMapNodeType.SpecialBattle,
            "Assets/Sprites/UI/NodeMap/NodeIcon_Special.png");
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.ProfileId, Is.EqualTo("special.death-bomb"));
        Assert.That(profile.BombPrefab, Is.Not.Null);
        Assert.That(profile.FuseTurns, Is.InRange(1, 3));
    }

    private static void AssertIcon(
        NodeMapSettings settings,
        NodeMapNodeType type,
        string expectedPath)
    {
        Sprite icon = settings.GetIcon(type);
        Assert.That(icon, Is.Not.Null, type.ToString());
        Assert.That(
            AssetDatabase.GetAssetPath(icon),
            Is.EqualTo(expectedPath),
            type.ToString());
        Assert.That(icon.rect.width, Is.EqualTo(64f), type.ToString());
        Assert.That(icon.rect.height, Is.EqualTo(64f), type.ToString());
    }

    [Test]
    public void DamageSurge_ScalesPositiveDamageByOnePointFiveWithCeiling()
    {
        BattleRuleContext rules = new BattleRuleContext(
            SpecialBattleRule.DamageSurge);

        Assert.That(rules.ScaleDamage(1), Is.EqualTo(2));
        Assert.That(rules.ScaleDamage(10), Is.EqualTo(15));
        Assert.That(rules.ScaleDamage(0), Is.Zero);
    }

    [Test]
    public void CylinderFiringOrder_ChangesTraversalWithoutChangingIndices()
    {
        CylinderFiringOrder normal = new CylinderFiringOrder(false);
        CylinderFiringOrder reversed = new CylinderFiringOrder(true);

        Assert.That(normal.GetFirstIndex(4), Is.EqualTo(3));
        Assert.That(normal.IsLastIndex(0, 4), Is.True);
        Assert.That(normal.IsAfter(1, 2), Is.True);
        Assert.That(reversed.GetFirstIndex(4), Is.Zero);
        Assert.That(reversed.IsLastIndex(3, 4), Is.True);
        Assert.That(reversed.IsAfter(2, 1), Is.True);
    }

    [Test]
    public void RunSaveNormalization_RejectsUnknownSpecialRule()
    {
        RunSaveData save = new RunSaveData
        {
            specialBattleRule = 999
        };

        RunSaveSystem.NormalizeSaveData(save);

        Assert.That(
            save.specialBattleRule,
            Is.EqualTo((int)SpecialBattleRule.None));
    }

    [Test]
    public void NodeMap_GeneratesTwoOrThreeSpecialBattlesInUniqueColumns()
    {
        NodeMapGenerationRule[] rules =
        {
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.NormalBattle,
                weight = 50,
                minimumCount = 1,
                maximumCount = -1
            },
            new NodeMapGenerationRule
            {
                nodeType = NodeMapNodeType.SpecialBattle,
                weight = 8,
                minimumCount = 2,
                maximumCount = 3
            }
        };

        NodeMapRunData map = NodeMapGenerator.Generate(
            7331,
            0,
            12,
            4,
            2,
            rules,
            2,
            2,
            1);
        List<NodeMapNodeData> specialNodes = map.nodes
            .Where(node => node.type == NodeMapNodeType.SpecialBattle)
            .ToList();

        Assert.That(specialNodes.Count, Is.InRange(2, 3));
        Assert.That(
            specialNodes.Select(node => node.column).Distinct().Count(),
            Is.EqualTo(specialNodes.Count));
        Assert.That(
            specialNodes.Select(node => node.specialBattleRule)
                .Distinct().Count(),
            Is.EqualTo(specialNodes.Count));
        Assert.That(
            specialNodes.All(node => node.specialBattleRule
                >= SpecialBattleRule.DeathBombs
                && node.specialBattleRule <= SpecialBattleRule.BloodReload),
            Is.True);
        Assert.That(
            specialNodes.All(node => node.battleIndex >= 0
                && node.battleIndex < 2),
            Is.True);
    }

    [Test]
    public void NodeMap_SpecialBattleGenerationIsDeterministic()
    {
        NodeMapRunData first = NodeMapGenerator.Generate(91, 0);
        NodeMapRunData second = NodeMapGenerator.Generate(91, 0);

        CollectionAssert.AreEqual(
            first.nodes.Select(node =>
                $"{node.id}:{node.type}:{node.specialBattleRule}:{node.battleIndex}"),
            second.nodes.Select(node =>
                $"{node.id}:{node.type}:{node.specialBattleRule}:{node.battleIndex}"));
    }
}
