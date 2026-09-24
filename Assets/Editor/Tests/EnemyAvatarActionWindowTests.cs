using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyAvatarActionWindowTests
{
    private const string AvatarRoot =
        "Assets/Prefabs/Enemy/Enemy_Avatar";

    [Test]
    public void EveryEnemyAvatarAuthorsActionWindowOnAnimator()
    {
        string[] prefabPaths = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { AvatarRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(prefabPaths, Is.Not.Empty);

        foreach (string prefabPath in prefabPaths)
        {
            GameObject avatar = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(avatar, Is.Not.Null, prefabPath);

            Animator animator = avatar.GetComponentInChildren<Animator>(true);
            Assert.That(
                animator,
                Is.Not.Null,
                $"{prefabPath} has no Animator.");
            Assert.That(
                animator.GetComponent<EnemyAttackAnimationEvents>(),
                Is.Not.Null,
                $"{prefabPath} must author its Action Window on the "
                + "Animator GameObject.");
        }
    }
}

public sealed class CombatFeedbackDodgeTests
{
    [TestCase(10, 10, 0, 0f)]
    [TestCase(15, 10, 0, 0.5f)]
    [TestCase(20, 10, 0, 1f)]
    [TestCase(1000, 1, 0, 1f)]
    [TestCase(15, 10, 5, 0f)]
    [TestCase(20, 10, 5, 0.5f)]
    [TestCase(100, -1, 0, 0f)]
    [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, 0f)]
    public void OverkillUsesRemainingHealthExcludesShieldsAndCapsStrength(
        int damage, int health, int shield, float expected)
    {
        Assert.That(CombatPresentation.CalculateOverkillStrength(damage, health, shield),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase(0.5f)]
    [TestCase(1f)]
    [TestCase(0f)]
    public void FinalDefeatKeepsExactSpeedAndInversionUntilRecovery(float intensity)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var settings = typeof(CombatAccessibilitySettings);
        var staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var intensityField = settings.GetField("presentationIntensity", staticFlags);
        var loadedField = settings.GetField("hasLoadedPresentationIntensity", staticFlags);
        object previousIntensity = intensityField.GetValue(null);
        object previousLoaded = loadedField.GetValue(null);
        float previousScale = Time.timeScale;
        float previousInversion = Shader.GetGlobalFloat("_FinalDefeatInversion");
        var root = new GameObject("Final Defeat Feedback Test");
        try
        {
            intensityField.SetValue(null, intensity);
            loadedField.SetValue(null, true);
            Time.timeScale = 0.8f;
            Shader.SetGlobalFloat("_FinalDefeatInversion", 0f);
            var feedback = root.AddComponent<CombatFeedbackController>();
            var type = typeof(CombatFeedbackController);
            object Read(string name) => type.GetField(name, flags).GetValue(feedback);
            void Write(string name, object value) => type.GetField(name, flags).SetValue(feedback, value);
            void Invoke(string name, params object[] args) => type.GetMethod(name, flags).Invoke(feedback, args);

            Invoke("HandleFinalEnemyDefeated", new object[] { null });
            feedback.StopAllCoroutines();
            if (intensity == 0f)
            {
                Assert.That(feedback.IsFinalDefeatPresentationActive, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.8f));
                Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.Zero);
                return;
            }

            Assert.That(feedback.IsFinalDefeatPresentationActive, Is.True);
            Assert.That((float)Read("slowMotionTargetScale"), Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.EqualTo(0.3f).Within(0.0001f));
            Invoke("StartSlowMotion", 1f, 0.4f, 0.09f, 0.18f, 1f);
            Invoke("StartDodgeSlowMotion");
            Write("hitStopRemaining", 0f);
            feedback.RequestHitStop(1f);
            Assert.That((float)Read("hitStopRemaining"), Is.Zero);

            var routine = (System.Collections.IEnumerator)type.GetMethod("TimeEffectRoutine", flags).Invoke(feedback, null);
            Assert.That(routine.MoveNext(), Is.True);
            foreach (float elapsed in new[] { 0.25f, 0.499f, 0.56f })
            {
                Write("slowMotionElapsed", elapsed - Time.unscaledDeltaTime);
                Assert.That(routine.MoveNext(), Is.True);
                if (elapsed < 0.5f)
                    Assert.That(Time.timeScale, Is.EqualTo(0.05f).Within(0.0001f));
                else
                    Assert.That(Time.timeScale, Is.InRange(0.65f, 0.8f));
                Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.InRange(0.0001f, 0.3f));
            }
            Write("slowMotionElapsed", 0.63f);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(feedback.IsFinalDefeatPresentationActive, Is.False);
            Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.Zero);

            Invoke("HandleFinalEnemyDefeated", new object[] { null });
            feedback.CancelPresentationForPause();
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.Zero);
            Invoke("HandleFinalEnemyDefeated", new object[] { null });
            // EditMode does not dispatch this MonoBehaviour's runtime lifecycle.
            Invoke("OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(Shader.GetGlobalFloat("_FinalDefeatInversion"), Is.Zero);
        }
        finally
        {
            root.GetComponent<CombatFeedbackController>()?.CancelPresentationForPause();
            UnityEngine.Object.DestroyImmediate(root);
            Time.timeScale = previousScale;
            Shader.SetGlobalFloat("_FinalDefeatInversion", previousInversion);
            intensityField.SetValue(null, previousIntensity);
            loadedField.SetValue(null, previousLoaded);
        }
    }

    private const string SoundLibraryPath =
        "Assets/Resources/Sound/SoundClipLibrary.asset";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/Player.prefab";

    [TestCase(3, 1f, 3)]
    [TestCase(3, 0.5f, 2)]
    [TestCase(3, 0.2f, 1)]
    [TestCase(3, 0f, 0)]
    [TestCase(0, 1f, 0)]
    public void AfterimageCountRespectsPresentationDensity(
        int authoredCount,
        float densityMultiplier,
        int expectedCount)
    {
        Assert.That(
            CombatFeedbackController.CalculateDodgeAfterimageCount(
                authoredCount,
                densityMultiplier),
            Is.EqualTo(expectedCount));
    }

    [TestCase(0.5f, 0.18f, 0.22f, 0.5f)]
    [TestCase(0.1f, 0.7f, 0.2f, 0.7f)]
    [TestCase(0f, 0f, 0f, 0.05f)]
    public void DodgePresentationUsesLongestAuthoredDuration(
        float sustainedDuration,
        float fullscreenDuration,
        float volumeDuration,
        float expectedDuration)
    {
        Assert.That(
            CombatFeedbackController.CalculateDodgePresentationDuration(
                sustainedDuration,
                fullscreenDuration,
                volumeDuration),
            Is.EqualTo(expectedDuration).Within(0.0001f));
    }

    [TestCase(1f, 0.05f, 1f, 1f, 0.05f)]
    [TestCase(1f, 0.05f, 1f, 0.5f, 0.525f)]
    [TestCase(1f, 0.1f, 1f, 0f, 1f)]
    public void SlowMotionScaleRespectsPresentationIntensity(
        float intensity,
        float strongestScale,
        float strengthMultiplier,
        float timeEffectMultiplier,
        float expectedScale)
    {
        Assert.That(
            CombatFeedbackController.CalculateSlowMotionScale(
                intensity,
                strongestScale,
                strengthMultiplier,
                timeEffectMultiplier),
            Is.EqualTo(expectedScale).Within(0.0001f));
    }

    [Test]
    public void PlayerPrefabReceivesCinematicDodgeDefaults()
    {
        GameObject playerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(playerPrefab, Is.Not.Null, PlayerPrefabPath);

        CombatFeedbackController feedback =
            playerPrefab.GetComponent<CombatFeedbackController>();
        Assert.That(feedback, Is.Not.Null, PlayerPrefabPath);

        SerializedObject serializedFeedback = new SerializedObject(feedback);
        Assert.That(
            serializedFeedback.FindProperty("dodgeSustainedEffectDuration")
                .floatValue,
            Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(
            serializedFeedback.FindProperty("dodgeInitialSlowMotionDuration")
                .floatValue,
            Is.GreaterThan(0f));
        Assert.That(
            serializedFeedback.FindProperty("dodgeAfterimageInterval")
                .floatValue,
            Is.GreaterThan(0f));
        Assert.That(
            serializedFeedback.FindProperty("dodgeOriginGhostDuration")
                .floatValue,
            Is.GreaterThan(0f));
    }

    [Test]
    public void SoundLibraryAuthorsEvadeSfxWithClip()
    {
        SoundClipLibrary library =
            AssetDatabase.LoadAssetAtPath<SoundClipLibrary>(SoundLibraryPath);
        Assert.That(library, Is.Not.Null, SoundLibraryPath);

        SerializedProperty entries = new SerializedObject(library)
            .FindProperty("sfx");
        Assert.That(entries, Is.Not.Null, "Missing serialized SFX list.");

        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            string id = entry.FindPropertyRelative("id").stringValue;

            if (id != "SFX_Evade")
            {
                continue;
            }

            Assert.That(
                entry.FindPropertyRelative("clip").objectReferenceValue,
                Is.Not.Null,
                "SFX_Evade must reference an AudioClip.");
            Assert.That(
                entry.FindPropertyRelative("volume").floatValue,
                Is.GreaterThan(0f));
            return;
        }

        Assert.Fail("SoundClipLibrary must contain SFX_Evade.");
    }
}
