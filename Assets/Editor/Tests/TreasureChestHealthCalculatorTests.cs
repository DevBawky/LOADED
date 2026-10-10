using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TreasureChestHealthCalculatorTests
{
    [TestCase(9786L, 9000)]
    [TestCase(999L, 900)]
    [TestCase(101L, 100)]
    [TestCase(57L, 50)]
    [TestCase(9L, 9)]
    [TestCase(0L, 1)]
    public void RoundDownToLeadingDigit_ProducesReadableHealth(
        long value,
        int expected)
    {
        Assert.That(
            TreasureChestHealthCalculator.RoundDownToLeadingDigit(value),
            Is.EqualTo(expected));
    }

    [Test]
    public void CalculateRoundedAdpc_AveragesActualCylinderCount()
    {
        int result = TreasureChestHealthCalculator.CalculateRoundedAdpc(
            totalDamage: 19572L,
            bulletCount: 12,
            cylinderCapacity: 6);

        Assert.That(result, Is.EqualTo(9000));
    }

    [Test]
    public void CalculateRoundedAdpc_SaturatesReadableHealthAtIntRange()
    {
        int result = TreasureChestHealthCalculator.CalculateRoundedAdpc(
            totalDamage: long.MaxValue,
            bulletCount: 1,
            cylinderCapacity: 1);

        Assert.That(result, Is.EqualTo(2000000000));
    }

    [Test]
    public void NormalizeSaveData_InitializesTreasureCombatCollections()
    {
        RunSaveData saveData = new RunSaveData
        {
            treasureChestMaxHealth = -10,
            treasureChestCurrentHealth = null,
            treasureChestDestroyed = null,
            treasurePendingRewardCount = -2,
            treasureOfferRelicIds = null
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.treasureChestMaxHealth, Is.Zero);
        Assert.That(saveData.treasureChestCurrentHealth, Is.Not.Null);
        Assert.That(saveData.treasureChestDestroyed, Is.Not.Null);
        Assert.That(saveData.treasurePendingRewardCount, Is.Zero);
        Assert.That(saveData.treasureOfferRelicIds, Is.Not.Null);
    }

    [Test]
    public void Withdraw_HidesSurvivingChestWithoutGrantingDestruction()
    {
        GameObject chestObject = new GameObject("Treasure Chest Test");
        Texture2D texture = new Texture2D(2, 2);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0f));
        TreasureChestTarget chest =
            chestObject.AddComponent<TreasureChestTarget>();

        try
        {
            chest.Configure(2, 0, sprite);
            chest.RestoreState(100, 75, false);
            int destroyedEventCount = 0;
            chest.Destroyed += _ => destroyedEventCount++;

            Assert.That(chest.Withdraw(), Is.True);
            Assert.That(chest.Withdraw(), Is.False);
            Assert.That(chest.IsTargetable, Is.False);
            Assert.That(chest.IsDestroyed, Is.False);
            Assert.That(chest.CurrentDurability, Is.EqualTo(75));
            Assert.That(destroyedEventCount, Is.Zero);
            Assert.That(
                chestObject.GetComponentInChildren<SpriteRenderer>(true)
                    .enabled,
                Is.False);
        }
        finally
        {
            DestroyGeneratedFallback(chest);
            Object.DestroyImmediate(chestObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ConfigureExternalSceneState_WithCombatHud_ShowsMainPanel()
    {
        GameObject stateObject = new GameObject("State Manager Test");
        GameObject mainGamePanel = new GameObject("Panel | MainGame Test");
        StateManager stateManager = stateObject.AddComponent<StateManager>();

        try
        {
            FieldInfo panelField = typeof(StateManager).GetField(
                "mainGamePanel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(panelField, Is.Not.Null);
            panelField.SetValue(stateManager, mainGamePanel);
            mainGamePanel.SetActive(false);

            stateManager.ConfigureExternalSceneState(
                2,
                3,
                GameFlowState.Treasure,
                true);

            Assert.That(mainGamePanel.activeSelf, Is.True);
            Assert.That(
                stateManager.CurrentState,
                Is.EqualTo(GameFlowState.Treasure));
            Assert.That(stateManager.CurrentStageIndex, Is.EqualTo(2));
            Assert.That(stateManager.CurrentBattleIndex, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(stateObject);
            Object.DestroyImmediate(mainGamePanel);
        }
    }

    [Test]
    public void DamagePreview_ShowsColoredHealthSegmentAndPredictedRemainder()
    {
        GameObject chestObject = new GameObject("Treasure Chest Preview Test");
        TreasureChestTarget chest =
            chestObject.AddComponent<TreasureChestTarget>();

        try
        {
            chest.RestoreState(100, 100, false);
            chest.ShowDamagePreview(
                new List<EnemyHealthBarFeedback.DamagePreviewSegment>
                {
                    new EnemyHealthBarFeedback.DamagePreviewSegment(
                        30,
                        Color.cyan,
                        true)
                },
                null);

            Transform previewTransform = chestObject.transform.Find(
                "Canvas | Chest Health/Image | Health Background/"
                + "Rect | Health Fill Area/Image | Damage Preview 1");
            Assert.That(previewTransform, Is.Not.Null);
            Assert.That(previewTransform.gameObject.activeSelf, Is.True);

            RectTransform previewRect = (RectTransform)previewTransform;
            Assert.That(previewRect.anchorMin.x, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(previewRect.anchorMax.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                previewTransform.GetComponent<Image>().color.a,
                Is.EqualTo(0.98f).Within(0.001f));

            TMP_Text healthText = chestObject.GetComponentInChildren<TMP_Text>();
            Assert.That(healthText.text, Is.EqualTo("70 / 100"));

            chest.ClearDamagePreview();

            Assert.That(previewTransform.gameObject.activeSelf, Is.False);
            Assert.That(healthText.text, Is.EqualTo("100 / 100"));
        }
        finally
        {
            DestroyGeneratedFallback(chest);
            Object.DestroyImmediate(chestObject);
        }
    }

    private static void DestroyGeneratedFallback(TreasureChestTarget chest)
    {
        const BindingFlags Flags = BindingFlags.Instance
            | BindingFlags.NonPublic;
        FieldInfo spriteField = typeof(TreasureChestTarget).GetField(
            "generatedSprite",
            Flags);
        FieldInfo textureField = typeof(TreasureChestTarget).GetField(
            "generatedTexture",
            Flags);
        Sprite generatedSprite = spriteField?.GetValue(chest) as Sprite;
        Texture2D generatedTexture = textureField?.GetValue(chest)
            as Texture2D;
        spriteField?.SetValue(chest, null);
        textureField?.SetValue(chest, null);

        if (generatedSprite != null)
        {
            Object.DestroyImmediate(generatedSprite);
        }
        if (generatedTexture != null)
        {
            Object.DestroyImmediate(generatedTexture);
        }
    }
}
