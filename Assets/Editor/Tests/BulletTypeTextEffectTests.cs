#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class BulletTypeTextEffectTests
{
    private const string ShaderPath =
        "Assets/Resources/Shaders/BulletTypeText.shader";
    private const string KoreanFontPath =
        "Assets/Package/Galmuri9 SDF.asset";

    [TestCase("Assets/Prefabs/UI/Canvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Shop/ShopCanvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Treasure/TreasureCanvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Event/EventCanvas.prefab")]
    public void BulletTooltipOwnersContainTypeDescriptionPanel(
        string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        Assert.That(prefab, Is.Not.Null, prefabPath);
        Assert.That(
            prefab.GetComponentInChildren<InventoryTooltipUI>(true),
            Is.Not.Null,
            prefabPath);

        Transform panel = FindDescendant(
            prefab.transform,
            "Panel | Bullet Type Desciption");
        Assert.That(panel, Is.Not.Null, prefabPath);
        Assert.That(
            FindDescendant(panel, "Text | Bullet Name"),
            Is.Not.Null,
            prefabPath);
        Assert.That(
            FindDescendant(panel, "Text | Bullet Description"),
            Is.Not.Null,
            prefabPath);
    }

    [Test]
    public void BulletTypeTextShaderExposesRequiredRuntimeProperties()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.name, Is.EqualTo("LOADED/UI/Bullet Type Text"));
        Assert.That(shader.FindPropertyIndex("_MainTex"), Is.GreaterThanOrEqualTo(0));
        Assert.That(
            shader.FindPropertyIndex("_EffectMode"),
            Is.GreaterThanOrEqualTo(0));
        Assert.That(
            shader.FindPropertyIndex("_MotionIntensity"),
            Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void BulletTypeTextShaderKeepsEffectsInsideGlyphCoverage()
    {
        string shaderSource = File.ReadAllText(ShaderPath);

        Assert.That(
            shaderSource,
            Does.Contain(
                "color.rgb += effectColor * color.a * innerLightStrength;"));
        Assert.That(shaderSource, Does.Not.Contain("halo *"));
    }

    [Test]
    public void ApplyAssignsAndRestoresTheBulletTypeRuntimeMaterial()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            KoreanFontPath);
        GameObject textObject = new GameObject(
            "Bullet Type Text Test",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        try
        {
            Assert.That(font, Is.Not.Null);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = "폭풍";

            BulletTypeTextEffect.Apply(text, BulletType.Storm);

            Material appliedMaterial = text.fontSharedMaterial;
            Assert.That(appliedMaterial, Is.Not.Null);
            Assert.That(
                appliedMaterial.shader.name,
                Is.EqualTo("LOADED/UI/Bullet Type Text"));
            Assert.That(
                appliedMaterial.GetFloat("_EffectMode"),
                Is.EqualTo((float)BulletType.Storm));

            text.fontSharedMaterial = font.material;
            BulletTypeTextEffect effect =
                text.GetComponent<BulletTypeTextEffect>();
            MethodInfo lateUpdate = typeof(BulletTypeTextEffect).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(effect, null);

            Assert.That(text.fontSharedMaterial, Is.SameAs(appliedMaterial));
        }
        finally
        {
            Object.DestroyImmediate(textObject);
        }
    }

    [TestCase(BulletType.Normal, false)]
    [TestCase(BulletType.Debuff, false)]
    [TestCase(BulletType.Ghost, true)]
    [TestCase(BulletType.Sniper, true)]
    [TestCase(BulletType.Storm, true)]
    [TestCase(BulletType.Shotgun, true)]
    [TestCase(BulletType.Piercing, true)]
    public void OnlySpecialNonDebuffTypesShowTypeDescription(
        BulletType bulletType,
        bool expected)
    {
        Assert.That(
            InventoryTooltipUI.ShouldShowBulletTypeDescription(bulletType),
            Is.EqualTo(expected));
    }

    [TestCase(BulletType.Debuff, false, true)]
    [TestCase(BulletType.Debuff, true, false)]
    [TestCase(BulletType.Normal, false, false)]
    [TestCase(BulletType.Shotgun, false, false)]
    public void GenericDebuffHelpIsOnlyFallbackForDebuffType(
        BulletType bulletType,
        bool hasSpecificDebuff,
        bool expected)
    {
        Assert.That(
            InventoryTooltipUI.ShouldShowGenericDebuffDescription(
                bulletType,
                hasSpecificDebuff),
            Is.EqualTo(expected));
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform candidate in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }
}
#endif
