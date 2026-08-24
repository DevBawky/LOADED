using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DuelClockHudFormattingTests
{
    [Test]
    public void RemainingEnemyCountUsesRequestedFormat()
    {
        Assert.That(
            DuelClockHUD.FormatRemainingEnemyCount(3),
            Is.EqualTo("남은 적 수: 3"));
    }

    [Test]
    public void NextWaveProgressUsesAuthoredInterval()
    {
        Assert.That(
            DuelClockHUD.FormatNextWaveProgress(0, 5),
            Is.EqualTo("적 스폰까지 (0/5)"));
    }

    [Test]
    public void ExhaustedPoolUsesCompletedSpawnFormat()
    {
        Assert.That(
            DuelClockHUD.FormatAllEnemiesSpawned(),
            Is.EqualTo("모든 적 스폰됨"));
    }

    [Test]
    public void BeatPulseStrengthRemainsFiniteAtAnimationEndpoints()
    {
        float start = DuelClockHUD.CalculateBeatPulseStrength(0f);
        float middle = DuelClockHUD.CalculateBeatPulseStrength(0.5f);
        float end = DuelClockHUD.CalculateBeatPulseStrength(1f);

        Assert.That(float.IsNaN(start), Is.False);
        Assert.That(float.IsNaN(middle), Is.False);
        Assert.That(float.IsNaN(end), Is.False);
        Assert.That(start, Is.Zero);
        Assert.That(middle, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(end, Is.Zero);
        Assert.That(
            DuelClockHUD.CalculateBeatPulseStrength(float.NaN),
            Is.Zero);
    }

    [Test]
    public void InvalidHudScaleFallsBackToIdentity()
    {
        Vector3 sanitized = DuelClockHUD.SanitizeScale(
            new Vector3(float.NaN, float.NaN, float.NaN));

        Assert.That(sanitized, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void ProgressUsesPercentageFormat()
    {
        Assert.That(
            DuelClockHUD.FormatProgressPercentage(62),
            Is.EqualTo("62%"));
        Assert.That(
            DuelClockHUD.FormatProgressPercentage(100),
            Is.EqualTo("100%"));
    }

    [Test]
    public void SmoothedFillMovesTowardTargetWithoutOvershooting()
    {
        float result = DuelClockHUD.CalculateSmoothedFill(
            0.2f,
            0.8f,
            12f,
            1f / 60f);

        Assert.That(result, Is.GreaterThan(0.2f));
        Assert.That(result, Is.LessThan(0.8f));
    }

    [Test]
    public void ZeroFillSpeedSnapsToTarget()
    {
        Assert.That(
            DuelClockHUD.CalculateSmoothedFill(0.2f, 0.8f, 0f, 1f),
            Is.EqualTo(0.8f));
    }

    [Test]
    public void BeatFillReachesFullThenLerpsFromZeroToOverflow()
    {
        DuelClockFillAnimation animation = new DuelClockFillAnimation();
        animation.Reset(0.8f, 0L);
        animation.Observe(0.25f, 1L);
        float currentFill = 0.8f;
        bool beatReached = false;

        for (int frameIndex = 0;
             frameIndex < 120 && !beatReached;
             frameIndex++)
        {
            DuelClockFillFrame frame = animation.Advance(
                currentFill,
                12f,
                28f,
                0.08f,
                1f / 60f);
            currentFill = frame.FillAmount;
            beatReached = frame.BeatReached;
        }

        Assert.That(beatReached, Is.True);
        Assert.That(currentFill, Is.EqualTo(1f));

        DuelClockFillFrame resetFrame = animation.Advance(
            currentFill,
            12f,
            28f,
            0.08f,
            0.1f);
        Assert.That(resetFrame.FillAmount, Is.Zero);

        DuelClockFillFrame overflowFrame = animation.Advance(
            resetFrame.FillAmount,
            12f,
            28f,
            0.08f,
            1f / 60f);
        Assert.That(overflowFrame.FillAmount, Is.GreaterThan(0f));
        Assert.That(overflowFrame.FillAmount, Is.LessThan(0.25f));
    }

    [Test]
    public void RestoredClockStartsWithoutReplayingSavedBeats()
    {
        DuelClockFillAnimation animation = new DuelClockFillAnimation();
        animation.Reset(0.75f, 3L);

        DuelClockFillFrame frame = animation.Advance(
            0.75f,
            12f,
            28f,
            0.08f,
            1f / 60f);

        Assert.That(frame.FillAmount, Is.EqualTo(0.75f));
        Assert.That(frame.BeatReached, Is.False);
    }
}

public sealed class WaveManagerEnemyProgressTests
{
    [Test]
    public void EnemyProgressCountsDefeatedEnemiesAcrossAuthoredWaves()
    {
        EnemyWave[] waves =
        {
            CreateWave(2),
            CreateWave(1, 2)
        };

        EnemyBattleProgress initial = WaveManager.CalculateEnemyProgress(
            waves,
            0,
            2);
        EnemyBattleProgress afterOneDefeat =
            WaveManager.CalculateEnemyProgress(waves, 0, 1);
        EnemyBattleProgress secondWave =
            WaveManager.CalculateEnemyProgress(waves, 1, 2);

        Assert.That(initial.DefeatedCount, Is.Zero);
        Assert.That(initial.TotalCount, Is.EqualTo(5));
        Assert.That(initial.RemainingCount, Is.EqualTo(5));
        Assert.That(afterOneDefeat.DefeatedCount, Is.EqualTo(1));
        Assert.That(afterOneDefeat.RemainingCount, Is.EqualTo(4));
        Assert.That(secondWave.DefeatedCount, Is.EqualTo(3));
        Assert.That(secondWave.TotalCount, Is.EqualTo(5));
    }

    [Test]
    public void EnemyProgressBeforeFirstSpawnStartsAtZero()
    {
        EnemyBattleProgress progress = WaveManager.CalculateEnemyProgress(
            new[] { CreateWave(2), CreateWave(3) },
            -1,
            0);

        Assert.That(progress.DefeatedCount, Is.Zero);
        Assert.That(progress.TotalCount, Is.EqualTo(5));
    }

    [Test]
    public void DuelClockEnemyProgressCountsLivingAndUnspawnedEnemies()
    {
        EnemyBattleProgress progress =
            WaveManager.CalculateDuelClockEnemyProgress(8, 3, 2);

        Assert.That(progress.TotalCount, Is.EqualTo(8));
        Assert.That(progress.DefeatedCount, Is.EqualTo(3));
        Assert.That(progress.RemainingCount, Is.EqualTo(5));
    }

    [Test]
    public void DuelClockSpawnsOnlyAtIntervalBelowActiveEnemyLimit()
    {
        Assert.That(WaveManager.ShouldSpawnDuelClockEnemy(
            4, 5, 3, 3, 4), Is.False);
        Assert.That(WaveManager.ShouldSpawnDuelClockEnemy(
            5, 5, 3, 3, 4), Is.True);
        Assert.That(WaveManager.ShouldSpawnDuelClockEnemy(
            5, 5, 3, 4, 4), Is.False);
        Assert.That(WaveManager.ShouldSpawnDuelClockEnemy(
            10, 5, 0, 1, 2), Is.False);
    }

    private static EnemyWave CreateWave(params int[] counts)
    {
        EnemyWaveEntry[] entries = new EnemyWaveEntry[counts.Length];
        FieldInfo countField = typeof(EnemyWaveEntry).GetField(
            "count",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo enemiesField = typeof(EnemyWave).GetField(
            "enemies",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(countField, Is.Not.Null);
        Assert.That(enemiesField, Is.Not.Null);

        for (int index = 0; index < counts.Length; index++)
        {
            entries[index] = new EnemyWaveEntry();
            countField.SetValue(entries[index], counts[index]);
        }

        EnemyWave wave = new EnemyWave();
        enemiesField.SetValue(wave, entries);
        return wave;
    }
}

public sealed class DuelClockEnemySpawnPoolTests
{
    private readonly List<EnemyData> createdEnemies =
        new List<EnemyData>();

    [TearDown]
    public void TearDown()
    {
        foreach (EnemyData enemy in createdEnemies)
        {
            Object.DestroyImmediate(enemy);
        }

        createdEnemies.Clear();
    }

    [Test]
    public void FreshPoolPreservesDuplicateAuthoredEntries()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();

        bool configured = pool.ConfigureFresh(
            new[] { first, first, second });
        bool read = pool.TryGet(1, out EnemyData selected);
        bool consumed = pool.TryConsumeAt(1, first);

        Assert.That(configured, Is.True);
        Assert.That(read, Is.True);
        Assert.That(selected, Is.SameAs(first));
        Assert.That(consumed, Is.True);
        Assert.That(pool.InitialCount, Is.EqualTo(3));
        Assert.That(pool.RemainingCount, Is.EqualTo(2));
    }

    [Test]
    public void RemainingPoolRoundTripsInExactOrder()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool source = new DuelClockEnemySpawnPool();
        source.ConfigureFresh(new[] { first, second, first });
        source.TryConsumeAt(1, second);
        List<string> capturedNames = new List<string>();
        source.Capture(capturedNames);
        DuelClockEnemySpawnPool restored = new DuelClockEnemySpawnPool();

        bool restoredSuccessfully = restored.Restore(
            new[] { first, second, first },
            capturedNames,
            assetName => assetName == first.name ? first : second);

        Assert.That(restoredSuccessfully, Is.True);
        Assert.That(restored.InitialCount, Is.EqualTo(3));
        Assert.That(restored.RemainingCount, Is.EqualTo(2));
        Assert.That(restored.TryGet(0, out EnemyData restoredFirst), Is.True);
        Assert.That(restored.TryGet(1, out EnemyData restoredSecond), Is.True);
        Assert.That(restoredFirst, Is.SameAs(first));
        Assert.That(restoredSecond, Is.SameAs(first));
    }

    private EnemyData CreateEnemy(string enemyName)
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.name = enemyName;
        createdEnemies.Add(enemy);
        return enemy;
    }
}

public sealed class DuelClockHudAssetTests
{
    private const string CanvasPrefabPath =
        "Assets/Prefabs/UI/Canvas.prefab";
    private const string BattleScenePath =
        "Assets/Scenes/Battle.unity";

    [Test]
    public void CanvasPrefabContainsRecommendedDuelClockHierarchy()
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            CanvasPrefabPath);
        Assert.That(canvas, Is.Not.Null);
        Transform floating = FindDescendant(
            canvas.transform,
            "Panel | Floating");
        Transform hudRoot = FindDirectChild(
            floating,
            "Layout | Duel Clock");

        Assert.That(floating, Is.Not.Null);
        Assert.That(hudRoot, Is.Not.Null);
        Assert.That(hudRoot.GetComponent<DuelClockHUD>(), Is.Not.Null);
        Assert.That(hudRoot.GetComponent<CanvasGroup>(), Is.Not.Null);
        Assert.That(hudRoot.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);

        Transform header = FindDirectChild(hudRoot, "Layout | Header");
        Transform meter = FindDirectChild(hudRoot, "Layout | Meter");
        Transform footer = FindDirectChild(hudRoot, "Layout | Footer");
        Assert.That(header, Is.Not.Null);
        Assert.That(meter, Is.Not.Null);
        Assert.That(footer, Is.Not.Null);
        Assert.That(FindDirectChild(header, "Text | Title")
            ?.GetComponent<TMP_Text>(), Is.Not.Null);
        TMP_Text nextWaveText = FindDirectChild(
            header,
            "Text | Enemy Count")?.GetComponent<TMP_Text>();
        Assert.That(nextWaveText, Is.Not.Null);
        Assert.That(nextWaveText.text, Is.EqualTo("적 스폰까지 (0/5)"));
        Assert.That(FindDirectChild(meter, "Image | Track")
            ?.GetComponent<Image>(), Is.Not.Null);
        Image fill = FindDirectChild(meter, "Image | Progress Fill")
            ?.GetComponent<Image>();
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(FindDirectChild(meter, "Image | Beat Marker")
            ?.GetComponent<Image>(), Is.Not.Null);
        Assert.That(FindDirectChild(footer, "Text | Progress")
            ?.GetComponent<TMP_Text>(), Is.Not.Null);
        TMP_Text remainingEnemyText = FindDirectChild(
            footer,
            "Text | Action Preview")?.GetComponent<TMP_Text>();
        Assert.That(remainingEnemyText, Is.Not.Null);
        Assert.That(remainingEnemyText.text, Is.EqualTo("남은 적 수: 5"));

        SerializedObject serializedHud = new SerializedObject(
            hudRoot.GetComponent<DuelClockHUD>());
        Assert.That(serializedHud.FindProperty("canvasGroup")
            .objectReferenceValue, Is.Not.Null);
        Assert.That(serializedHud.FindProperty("progressFill")
            .objectReferenceValue, Is.Not.Null);
        Assert.That(serializedHud.FindProperty("titleText")
            .objectReferenceValue, Is.Not.Null);
        Assert.That(serializedHud.FindProperty("enemyCountText")
            .objectReferenceValue, Is.Not.Null);
        Assert.That(serializedHud.FindProperty("fillLerpSpeed").floatValue,
            Is.GreaterThan(0f));
        Assert.That(serializedHud.FindProperty("beatFillLerpSpeed")
            .floatValue, Is.GreaterThan(0f));
        Assert.That(serializedHud.FindProperty("beatFullHoldDuration")
            .floatValue, Is.GreaterThanOrEqualTo(0f));
        Assert.That(serializedHud.FindProperty("beatPulseDuration")
            .floatValue, Is.GreaterThan(0f));
        Assert.That(serializedHud.FindProperty("beatPulseScale")
            .floatValue, Is.GreaterThanOrEqualTo(1.1f));
        Assert.That(serializedHud.FindProperty("progressText")
            .objectReferenceValue, Is.Not.Null);
        Assert.That(serializedHud.FindProperty("actionPreviewText")
            .objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void BattleSceneContainsOneConfiguredDuelClockHud()
    {
        Scene scene = EditorSceneManager.OpenScene(
            BattleScenePath,
            OpenSceneMode.Additive);

        try
        {
            int namedRootCount = 0;
            int configuredRootCount = 0;

            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                Transform floating = FindDescendant(
                    sceneRoot.transform,
                    "Panel | Floating");

                if (floating == null)
                {
                    continue;
                }

                for (int index = 0;
                     index < floating.childCount;
                     index++)
                {
                    Transform child = floating.GetChild(index);

                    if (child.name != "Layout | Duel Clock")
                    {
                        continue;
                    }

                    namedRootCount++;

                    if (child.GetComponent<DuelClockHUD>() != null)
                    {
                        configuredRootCount++;
                    }
                }
            }

            Assert.That(namedRootCount, Is.EqualTo(1));
            Assert.That(configuredRootCount, Is.EqualTo(1));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(
                root.GetChild(index),
                objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDirectChild(
        Transform parent,
        string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }
}
