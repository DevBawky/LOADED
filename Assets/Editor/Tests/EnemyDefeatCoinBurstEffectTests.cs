using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyDefeatCoinBurstEffectTests
{
    [TestCase(0, 10)]
    [TestCase(1, 10)]
    [TestCase(2, 12)]
    [TestCase(3, 14)]
    [TestCase(4, 16)]
    [TestCase(5, 18)]
    public void CalculateCoinCount_GrowsByTwentyPercentOfBasePerKill(
        int comboKillCount,
        int expectedCount)
    {
        int count = EnemyDefeatCoinBurstEffect.CalculateCoinCount(
            10,
            0.2f,
            comboKillCount);

        Assert.That(count, Is.EqualTo(expectedCount));
    }

    [Test]
    public void CalculateCoinCount_ClampsInvalidConfiguration()
    {
        int count = EnemyDefeatCoinBurstEffect.CalculateCoinCount(
            0,
            -0.2f,
            7);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void PrefabUsesTunablePlaybackAndScaleAndIsWiredToPlayer()
    {
        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/VFX/VFX_EnemyDefeatCoinBurst.prefab");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        Assert.That(effectPrefab, Is.Not.Null);
        Assert.That(playerPrefab, Is.Not.Null);

        EnemyDefeatCoinBurstEffect effect = effectPrefab.GetComponent<
            EnemyDefeatCoinBurstEffect>();
        CombatFeedbackController feedback = playerPrefab.GetComponent<
            CombatFeedbackController>();
        Assert.That(effect, Is.Not.Null);
        Assert.That(feedback, Is.Not.Null);

        SerializedObject serializedEffect = new SerializedObject(effect);
        SerializedObject serializedFeedback = new SerializedObject(feedback);
        Assert.That(
            serializedEffect.FindProperty("playbackSpeed").floatValue,
            Is.EqualTo(1f).Within(0.0001f));
        Assert.That(
            serializedEffect.FindProperty("rootScale").floatValue,
            Is.EqualTo(1f).Within(0.0001f));
        Assert.That(
            serializedFeedback.FindProperty("defeatCoinBurstPrefab")
                .objectReferenceValue,
            Is.SameAs(effect));
    }

    [Test]
    public void SoundLibraryContainsComboKillClip()
    {
        SoundClipLibrary library = AssetDatabase.LoadAssetAtPath<
            SoundClipLibrary>(
            "Assets/Resources/Sound/SoundClipLibrary.asset");
        Assert.That(library, Is.Not.Null);

        SerializedProperty entries = new SerializedObject(library)
            .FindProperty("sfx");

        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);

            if (entry.FindPropertyRelative("id").stringValue
                != "SFX_Combo_Kill")
            {
                continue;
            }

            Assert.That(
                entry.FindPropertyRelative("clip").objectReferenceValue,
                Is.Not.Null);
            return;
        }

        Assert.Fail("SoundClipLibrary must contain SFX_Combo_Kill.");
    }
}
