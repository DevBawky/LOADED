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
