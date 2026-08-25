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
    public void ProgressColorTransitionsFromYellowToRed()
    {
        Color yellow = new Color32(247, 191, 62, 255);
        Color red = new Color32(231, 77, 42, 255);

        Assert.That(
            DuelClockHUD.EvaluateProgressColor(0f, yellow, red),
            Is.EqualTo(yellow));
        Assert.That(
            DuelClockHUD.EvaluateProgressColor(0.5f, yellow, red),
            Is.EqualTo(Color.Lerp(yellow, red, 0.5f)));
        Assert.That(
            DuelClockHUD.EvaluateProgressColor(1f, yellow, red),
            Is.EqualTo(red));
        Assert.That(
            DuelClockHUD.EvaluateProgressColor(float.NaN, yellow, red),
            Is.EqualTo(yellow));
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

    [Test]
    public void CompletedShootBeatStaysFullUntilFiringEnds()
    {
        DuelClockFillAnimation animation = new DuelClockFillAnimation();
        animation.Reset(0.8f, 0L);
        animation.Observe(0f, 1L);
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
                1f / 60f,
                true);
            currentFill = frame.FillAmount;
            beatReached = frame.BeatReached;
        }

        DuelClockFillFrame heldFrame = animation.Advance(
            currentFill,
            12f,
            28f,
            0.08f,
            1f,
            true);
        DuelClockFillFrame releasedFrame = animation.Advance(
            heldFrame.FillAmount,
            12f,
            28f,
            0.08f,
            1f / 60f,
            false);

        Assert.That(beatReached, Is.True);
        Assert.That(heldFrame.FillAmount, Is.EqualTo(1f));
        Assert.That(releasedFrame.FillAmount, Is.Zero);
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

    [Test]
    public void EmptyDuelClockBattleImmediatelyRequestsRemainingEnemy()
    {
        Assert.That(WaveManager.ShouldImmediatelySpawnDuelClockEnemy(
            2, 0, 3), Is.True);
        Assert.That(WaveManager.ShouldImmediatelySpawnDuelClockEnemy(
            0, 0, 3), Is.False);
        Assert.That(WaveManager.ShouldImmediatelySpawnDuelClockEnemy(
            2, 1, 3), Is.False);
        Assert.That(WaveManager.ShouldImmediatelySpawnDuelClockEnemy(
            2, 0, 0), Is.False);
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
    public void MinimumCountIsForcedBeforeSpawnBudgetExpires()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();
        DuelClockEnemySpawnEntry[] entries =
        {
            CreateEntry(first, 100f),
            CreateEntry(second, 1f, 1)
        };

        Assert.That(pool.ConfigureFresh(entries, 3), Is.True);
        Assert.That(pool.TrySelect(0f, out int firstIndex,
            out EnemyData firstSelected), Is.True);
        Assert.That(firstSelected, Is.SameAs(first));
        Assert.That(pool.TryCommitSpawn(firstIndex, firstSelected), Is.True);
        Assert.That(pool.TrySelect(0f, out int secondIndex,
            out EnemyData secondSelected), Is.True);
        Assert.That(secondSelected, Is.SameAs(first));
        Assert.That(pool.TryCommitSpawn(secondIndex, secondSelected), Is.True);

        Assert.That(pool.TrySelect(0f, out _, out EnemyData forced), Is.True);
        Assert.That(forced, Is.SameAs(second));
        Assert.That(pool.InitialCount, Is.EqualTo(3));
        Assert.That(pool.RemainingCount, Is.EqualTo(1));
    }

    [Test]
    public void PreviousSpawnPenaltyChangesTheNextWeightedSelection()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();
        DuelClockEnemySpawnEntry[] entries =
        {
            CreateEntry(first, 10f, 0, 0.1f),
            CreateEntry(second, 10f)
        };

        Assert.That(pool.ConfigureFresh(entries, 3), Is.True);
        Assert.That(pool.TrySelect(0.2f, out int selectedIndex,
            out EnemyData selected), Is.True);
        Assert.That(selected, Is.SameAs(first));
        Assert.That(pool.TryCommitSpawn(selectedIndex, selected), Is.True);

        Assert.That(pool.TrySelect(0.2f, out _, out EnemyData next), Is.True);
        Assert.That(next, Is.SameAs(second));
    }

    [Test]
    public void WeightedStateRoundTripsWithoutChangingNextSelection()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnEntry[] entries =
        {
            CreateEntry(first, 10f, 1, 0.1f),
            CreateEntry(second, 10f)
        };
        DuelClockEnemySpawnPool source = new DuelClockEnemySpawnPool();
        Assert.That(source.ConfigureFresh(entries, 4), Is.True);
        Assert.That(source.TrySelect(0.2f, out int selectedIndex,
            out EnemyData selected), Is.True);
        Assert.That(source.TryCommitSpawn(selectedIndex, selected), Is.True);
        List<int> capturedCounts = new List<int>();
        List<int> capturedMissedCounts = new List<int>();
        source.Capture(
            capturedCounts,
            capturedMissedCounts,
            out int remainingCount,
            out string lastSpawnedEnemyName);
        DuelClockEnemySpawnPool restored = new DuelClockEnemySpawnPool();

        bool restoredSuccessfully = restored.Restore(
            entries,
            4,
            remainingCount,
            capturedCounts,
            capturedMissedCounts,
            lastSpawnedEnemyName,
            assetName => assetName == first.name ? first : second);

        Assert.That(restoredSuccessfully, Is.True);
        Assert.That(restored.InitialCount, Is.EqualTo(4));
        Assert.That(restored.RemainingCount, Is.EqualTo(3));
        Assert.That(source.TrySelect(0.2f, out _, out EnemyData sourceNext),
            Is.True);
        Assert.That(restored.TrySelect(0.2f, out _,
            out EnemyData restoredNext), Is.True);
        Assert.That(restoredNext, Is.SameAs(sourceNext));
    }

    [Test]
    public void MinimumCountsCannotExceedTotalSpawnCount()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();

        bool configured = pool.ConfigureFresh(
            new[]
            {
                CreateEntry(first, 1f, 2),
                CreateEntry(second, 1f, 2)
            },
            3);

        Assert.That(configured, Is.False);
        Assert.That(pool.InitialCount, Is.Zero);
        Assert.That(pool.RemainingCount, Is.Zero);
    }

    [Test]
    public void MissedEnemyGainsVariationWeightUntilSelected()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();
        DuelClockEnemySpawnEntry[] entries =
        {
            CreateEntry(first, 1f, 0, 1f, 0.25f),
            CreateEntry(second, 1f, 0, 1f, 0.25f)
        };

        Assert.That(pool.ConfigureFresh(entries, 3), Is.True);
        Assert.That(pool.TrySelect(0f, out int selectedIndex,
            out EnemyData selected), Is.True);
        Assert.That(pool.TryCommitSpawn(selectedIndex, selected), Is.True);

        Assert.That(pool.TrySelect(0.45f, out _, out EnemyData varied),
            Is.True);
        Assert.That(varied, Is.SameAs(second));
    }

    [Test]
    public void LegacyStateWithoutKnownLastEnemyCanRestore()
    {
        EnemyData first = CreateEnemy("First");
        EnemyData second = CreateEnemy("Second");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();

        bool restored = pool.Restore(
            new[]
            {
                CreateEntry(first, 1f),
                CreateEntry(second, 1f)
            },
            4,
            2,
            new[] { 1, 1 },
            new[] { 0, 0 },
            string.Empty,
            _ => null);

        Assert.That(restored, Is.True);
        Assert.That(pool.RemainingCount, Is.EqualTo(2));
    }

    [Test]
    public void SingleEnemyPoolFallsBackWhenRepeatWeightIsZero()
    {
        EnemyData enemy = CreateEnemy("Only");
        DuelClockEnemySpawnPool pool = new DuelClockEnemySpawnPool();
        Assert.That(pool.ConfigureFresh(
            new[] { CreateEntry(enemy, 1f, 0, 0f) }, 2), Is.True);
        Assert.That(pool.TrySelect(0.5f, out int selectedIndex,
            out EnemyData selected), Is.True);
        Assert.That(pool.TryCommitSpawn(selectedIndex, selected), Is.True);

        Assert.That(pool.TrySelect(0.5f, out _, out EnemyData repeated),
            Is.True);
        Assert.That(repeated, Is.SameAs(enemy));
    }

    private EnemyData CreateEnemy(string enemyName)
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.name = enemyName;
        createdEnemies.Add(enemy);
        return enemy;
    }

    private static DuelClockEnemySpawnEntry CreateEntry(
        EnemyData enemy,
        float weight,
        int minimumSpawnCount = 0,
        float previousSpawnWeightMultiplier = 0.35f,
        float missedSpawnWeightIncrease = 0.25f)
    {
        return new DuelClockEnemySpawnEntry(
            enemy,
            weight,
            minimumSpawnCount,
            previousSpawnWeightMultiplier,
            missedSpawnWeightIncrease);
    }
}

public sealed class DuelClockEnemySpawnSaveTests
{
    [Test]
    public void WeightedSpawnStateSurvivesJsonRoundTripAndNormalization()
    {
        RunSaveData source = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockWeightedSpawnStateInitialized = true,
            duelClockRemainingEnemySpawnCount = 3,
            duelClockEnemySpawnCounts = new List<int> { 2, 1 },
            duelClockEnemyMissedSpawnCounts = new List<int> { 0, 2 },
            duelClockLastSpawnedEnemyAssetName = "Enemy B"
        };

        RunSaveData restored = JsonUtility.FromJson<RunSaveData>(
            JsonUtility.ToJson(source));
        RunSaveSystem.NormalizeSaveData(restored);

        Assert.That(
            restored.duelClockWeightedSpawnStateInitialized,
            Is.True);
        Assert.That(restored.duelClockRemainingEnemySpawnCount,
            Is.EqualTo(3));
        Assert.That(restored.duelClockEnemySpawnCounts,
            Is.EqualTo(new[] { 2, 1 }));
        Assert.That(restored.duelClockEnemyMissedSpawnCounts,
            Is.EqualTo(new[] { 0, 2 }));
        Assert.That(restored.duelClockLastSpawnedEnemyAssetName,
            Is.EqualTo("Enemy B"));
    }

    [Test]
    public void InvalidWeightedSpawnFieldsAreNormalizedSafely()
    {
        RunSaveData saveData = new RunSaveData
        {
            combatPacingMode = (int)CombatPacingMode.DuelClock,
            duelClockRemainingEnemySpawnCount = -4,
            duelClockEnemySpawnCounts = null,
            duelClockEnemyMissedSpawnCounts = new List<int> { -2 },
            duelClockLastSpawnedEnemyAssetName = null
        };

        RunSaveSystem.NormalizeSaveData(saveData);

        Assert.That(saveData.duelClockRemainingEnemySpawnCount, Is.Zero);
        Assert.That(saveData.duelClockEnemySpawnCounts, Is.Empty);
        Assert.That(saveData.duelClockEnemyMissedSpawnCounts,
            Is.EqualTo(new[] { 0 }));
        Assert.That(saveData.duelClockLastSpawnedEnemyAssetName,
            Is.EqualTo(string.Empty));
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
        Assert.That(serializedHud.FindProperty("progressStartColor")
            .colorValue, Is.EqualTo(new Color32(247, 191, 62, 255)));
        Assert.That(serializedHud.FindProperty("progressEndColor")
            .colorValue, Is.EqualTo(new Color32(231, 77, 42, 255)));
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
