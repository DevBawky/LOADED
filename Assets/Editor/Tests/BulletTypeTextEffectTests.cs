#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class BulletTypeTextEffectTests
{
    private const string SharedTooltipPath =
        "Assets/Prefabs/UI/Shared/Panel_Tooltips.prefab";
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

    [TestCase(
        "Panel | Item Tooltip",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_ItemTooltip.prefab")]
    [TestCase(
        "Panel | Bullet Tooltip",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_BulletTooltip.prefab")]
    [TestCase(
        "Panel | Cylinder Bullet Tooltip",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_CylinderBulletTooltip.prefab")]
    [TestCase(
        "Panel | Action Tooltip",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_ActionTooltip.prefab")]
    [TestCase(
        "Panel | Relic Tooltip",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_RelicTooltip.prefab")]
    [TestCase(
        "Panel | Debuff Desciption",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_DebuffDescription.prefab")]
    [TestCase(
        "Panel | Bullet Type Desciption",
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_BulletTypeDescription.prefab")]
    public void SharedContainerUsesIndividualTooltipPrefab(
        string objectName,
        string sourcePrefabPath)
    {
        GameObject sharedTooltips = AssetDatabase.LoadAssetAtPath<GameObject>(
            SharedTooltipPath);
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            sourcePrefabPath);

        Assert.That(sharedTooltips, Is.Not.Null);
        Assert.That(sourcePrefab, Is.Not.Null, sourcePrefabPath);
        Transform[] matches = FindDescendants(
            sharedTooltips.transform,
            objectName);
        Assert.That(matches, Has.Length.EqualTo(1), objectName);
        AssertOriginalPrefabSource(matches[0], sourcePrefabPath);
    }

    [TestCase("Assets/Prefabs/UI/Canvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Shop/ShopCanvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Treasure/TreasureCanvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Event/EventCanvas.prefab")]
    public void GameplayCanvasUsesSingleSharedTooltipPrefab(string prefabPath)
    {
        GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        Assert.That(canvas, Is.Not.Null, prefabPath);
        Transform[] tooltipRoots = FindDescendants(
            canvas.transform,
            "Panel | Tooltips");
        Assert.That(tooltipRoots, Has.Length.EqualTo(1), prefabPath);
        AssertOriginalPrefabSource(tooltipRoots[0], SharedTooltipPath);
    }

    [TestCase(
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_BulletTooltip.prefab")]
    [TestCase(
        "Assets/Prefabs/UI/Shared/Tooltips/Panel_CylinderBulletTooltip.prefab")]
    public void BulletTooltipContainsSeparateTypeAndGradeFields(
        string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        Assert.That(prefab, Is.Not.Null, prefabPath);
        Transform typeBackground = FindDescendant(
            prefab.transform,
            "BG | Bullet Type");
        Transform gradeBackground = FindDescendant(
            prefab.transform,
            "BG | Bullet Grade");
        Assert.That(typeBackground, Is.Not.Null, prefabPath);
        Assert.That(gradeBackground, Is.Not.Null, prefabPath);
        Assert.That(
            typeBackground.GetComponentInChildren<TextMeshProUGUI>(true),
            Is.Not.Null,
            prefabPath);
        Assert.That(
            gradeBackground.GetComponentInChildren<TextMeshProUGUI>(true),
            Is.Not.Null,
            prefabPath);
    }

    [TestCase("Assets/Prefabs/UI/Canvas.prefab")]
    [TestCase("Assets/Prefabs/UI/Shop/Panel_Shop.prefab")]
    [TestCase("Assets/Prefabs/UI/Event/EventCanvas.prefab")]
    public void BulletManagementUsesSharedUpgradeTooltipPrefab(string prefabPath)
    {
        GameObject target = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        Assert.That(target, Is.Not.Null, prefabPath);
        Transform[] targetTooltips = FindDescendants(
            target.transform,
            "Panel | Upgrade Tooltip");

        Assert.That(targetTooltips, Has.Length.EqualTo(1), prefabPath);
        Assert.That(
            targetTooltips[0].parent.name,
            Is.EqualTo("Layout | Bullet Manage"),
            prefabPath);
        AssertOriginalPrefabSource(
            targetTooltips[0],
            "Assets/Prefabs/UI/Shared/Tooltips/Panel_UpgradeTooltip.prefab");
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

    [Test]
    public void BulletTooltipMetadataRendersTypeAndGradeSeparately()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            KoreanFontPath);
        GameObject typeObject = new GameObject(
            "Bullet Type",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        GameObject gradeObject = new GameObject(
            "Bullet Grade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        try
        {
            Assert.That(font, Is.Not.Null);
            TextMeshProUGUI typeText =
                typeObject.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI gradeText =
                gradeObject.GetComponent<TextMeshProUGUI>();
            typeText.font = font;
            gradeText.font = font;
            Color gradeColor = new Color(0.3f, 0.6f, 0.9f, 1f);

            InventoryTooltipUI.ApplyBulletMetadata(
                typeText,
                gradeText,
                BulletType.Storm,
                BulletGrade.Ace,
                gradeColor);

            Assert.That(
                typeText.text,
                Is.EqualTo(BulletData.GetBulletTypeDisplayName(
                    BulletType.Storm)));
            Assert.That(gradeText.text, Is.EqualTo("Ace"));
            Assert.That(gradeText.color, Is.EqualTo(gradeColor));
            Assert.That(
                typeText.GetComponent<BulletTypeTextEffect>(),
                Is.Not.Null);
            Assert.That(
                gradeText.GetComponent<BulletTypeTextEffect>(),
                Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(typeObject);
            Object.DestroyImmediate(gradeObject);
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

    private static Transform[] FindDescendants(
        Transform root,
        string objectName)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate.name == objectName)
            .ToArray();
    }

    private static void AssertOriginalPrefabSource(
        Transform instance,
        string expectedAssetPath)
    {
        GameObject originalSource =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                instance.gameObject);
        Assert.That(originalSource, Is.Not.Null, instance.name);
        Assert.That(
            AssetDatabase.GetAssetPath(originalSource),
            Is.EqualTo(expectedAssetPath),
            instance.name);
    }
}
#endif
