using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainButtonShaderPrefabTests
{
    [Test]
    public void MainButtonShaderImportsWithoutCompilerErrors()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
            MainButtonShaderPrefabBuilder.ShaderPath);

        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
    }

    [Test]
    public void MainButtonUsesShaderFeedbackWithoutLegacyAnimatorLayers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            MainButtonShaderPrefabBuilder.PrefabPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<Animator>(), Is.Null);
        Assert.That(
            prefab.transform.Find("Image | Hover BG"),
            Is.Null);

        Transform legacyTint = prefab.transform.Find("Data | Legacy Tint");
        Assert.That(legacyTint, Is.Not.Null);
        Assert.That(legacyTint.gameObject.activeSelf, Is.False);
        Assert.That(legacyTint.GetComponent<Image>().enabled, Is.False);

        Button button = prefab.GetComponent<Button>();
        Image image = prefab.GetComponent<Image>();
        MainButtonShaderFeedback feedback =
            prefab.GetComponent<MainButtonShaderFeedback>();

        Assert.That(button, Is.Not.Null);
        Assert.That(image, Is.Not.Null);
        Assert.That(feedback, Is.Not.Null);
        Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
        Assert.That(button.targetGraphic, Is.SameAs(image));
        Assert.That(image.sprite, Is.Null);
        Assert.That(image.material, Is.Not.Null);
        Assert.That(
            image.material.shader.name,
            Is.EqualTo("Loaded/UI/Main Button"));
    }

    [Test]
    public void FeedbackDampingMovesTowardTargetWithoutOvershoot()
    {
        float value = MainButtonShaderFeedback.Damp(
            0f,
            1f,
            18f,
            1f / 60f);

        Assert.That(value, Is.GreaterThan(0f));
        Assert.That(value, Is.LessThan(1f));
        Assert.That(
            MainButtonShaderFeedback.Damp(value, 1f, 18f, 0f),
            Is.EqualTo(value));
    }
}
