#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NewBulletExpansionTests
{
    [Test]
    public void AuthoredRoster_ContainsSeventyOneBulletsAndNewSet()
    {
        BulletData[] bullets = BulletPoolSyncBuilder.LoadAllBulletData();
        Assert.That(bullets.Length, Is.EqualTo(71));
        string[] required =
        {
            "집속탄", "승부탄", "주사위탄", "선봉탄", "종결탄",
            "무음탄", "망령탄", "강령탄", "사냥탄", "고정탄",
            "처형탄", "점멸탄"
        };
        foreach (string name in required)
            Assert.That(bullets.Any(b => b.DisplayName == name), Is.True, name);
    }

    [Test]
    public void SilentBullet_PreservesRenameAndNormalGrade()
    {
        BulletData silent = AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Normal/Silent.asset");
        Assert.That(silent, Is.Not.Null);
        Assert.That(silent.DisplayName, Is.EqualTo("무음탄"));
        Assert.That(silent.Grade, Is.EqualTo(BulletGrade.Normal));
        Assert.That(silent.BulletType, Is.EqualTo(BulletType.Ghost));
        Assert.That(silent.DoesNotConsumeTurn, Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<BulletData>(
            "Assets/Scripts/Bullet/SO/Ace/Ghost.asset"), Is.Null);
    }

    [Test]
    public void ShotgunSet_UsesIndependentShotCountsAndCriticalRules()
    {
        BulletData focused = Find("집속탄");
        BulletData gambit = Find("승부탄");
        BulletData dice = Find("주사위탄");
        Assert.That(focused.ShotCount, Is.EqualTo(8));
        Assert.That(gambit.ShotCount, Is.EqualTo(12));
        Assert.That(gambit.CriticalChance, Is.EqualTo(8f));
        Assert.That(gambit.CriticalDamageMultiplier, Is.EqualTo(5f));
        Assert.That(dice.ShotCount, Is.EqualTo(6));
        Assert.That(BulletEffectUtility.Find(
            new BulletInstance(dice, 0),
            BulletEffectType.RandomPelletDamage), Is.Not.Null);
    }

    [TestCase(BulletEffectType.Vanguard, true, false, 30f, 1.3f)]
    [TestCase(BulletEffectType.Finisher, false, true, 40f, 1.4f)]
    public void PositionDamageMultiplier_UsesPhysicalCylinderPosition(
        BulletEffectType type,
        bool first,
        bool last,
        float amount,
        float expected)
    {
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        try
        {
            SerializedObject serialized = new SerializedObject(data);
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").enumValueIndex = (int)type;
            effect.FindPropertyRelative("amount").floatValue = amount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BulletInstance bullet = new BulletInstance(data, 0);
            Assert.That(
                BulletEffectUtility.GetPositionDamageMultiplier(
                    bullet, first, last),
                Is.EqualTo(expected).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void CartoonArtwork_AndRawWorkbookIcon_AreAvailable()
    {
        Assert.That(Shader.Find("LOADED/UI/Bullet Image Frame"), Is.Not.Null);
        BulletData focused = Find("집속탄");
        string path = AssetDatabase.GetAssetPath(focused.CylinderIcon);
        Assert.That(path, Is.EqualTo(
            "Assets/Sprites/Bullet_LineArt/Rare/Bullet_focused_volley.png"));
        byte[] icon = BulletBalanceWorkbook.CaptureIcon(focused);
        Assert.That(icon, Is.Not.Null.And.Length.GreaterThan(128));
        Assert.That(icon, Is.EqualTo(File.ReadAllBytes(path)),
            "The workbook must embed raw artwork without a runtime border.");
    }

    [Test]
    public void LineArtArtwork_IsOrganizedByGrade_AndSparklesAreRemoved()
    {
        string[] paths =
        {
            "Normal/Bullet_silent.png", "Normal/Bullet_hunt.png",
            "Rare/Bullet_focused_volley.png", "Rare/Bullet_vanguard.png",
            "Rare/Bullet_finisher.png", "Rare/Bullet_wraith.png",
            "Rare/Bullet_lock_on.png", "Rare/Bullet_blink.png",
            "Ace/Bullet_gambit.png", "Ace/Bullet_dice_shot.png",
            "Ace/Bullet_necromancy.png", "Legendary/Bullet_execution.png"
        };
        foreach (string relative in paths)
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/Bullet_LineArt/" + relative), Is.Not.Null,
                relative);

        string shaderSource = File.ReadAllText(
            "Assets/Resources/Shaders/BulletIconImageFrame.shader");
        Assert.That(shaderSource, Does.Contain("_SpriteUvRect"));
        Assert.That(shaderSource, Does.Contain("_FrameTex"));
        Assert.That(shaderSource, Does.Not.Contain("float sparkle"));
        Assert.That(shaderSource, Does.Not.Contain("float glint"));
    }

    [Test]
    public void AuthoredFrameMasks_ExistForEveryBulletType()
    {
        string shaderSource = File.ReadAllText(
            "Assets/Resources/Shaders/BulletIconImageFrame.shader");
        Assert.That(shaderSource, Does.Contain("sampler2D _FrameTex"));
        Assert.That(shaderSource, Does.Not.Contain("RingMask("));
        Assert.That(shaderSource, Does.Not.Contain("TriangleMask("));

        foreach (BulletType bulletType in System.Enum.GetValues(
                     typeof(BulletType)))
        {
            string path = $"Assets/Resources/BulletFrames/{bulletType}.png";
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(mask, Is.Not.Null, path);
            Assert.That(mask.width, Is.EqualTo(512), path);
            Assert.That(mask.height, Is.EqualTo(512), path);

            TextureImporter importer = AssetImporter.GetAtPath(path)
                as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp),
                path);
            Assert.That(importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed), path);
        }
    }

    [Test]
    public void Presenter_PassesEveryBulletTypeToRuntimeFrameMaterial()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64),
            new Vector2(0.5f, 0.5f));
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        GameObject owner = new GameObject("BulletTypeFrameTest");
        UnityEngine.UI.Image image = owner.AddComponent<UnityEngine.UI.Image>();
        try
        {
            foreach (BulletType bulletType in System.Enum.GetValues(
                         typeof(BulletType)))
            {
                SerializedObject serialized = new SerializedObject(data);
                serialized.FindProperty("cylinderIcon").objectReferenceValue = sprite;
                serialized.FindProperty("bulletType").enumValueIndex =
                    (int)bulletType;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                BulletIconPresenter.Apply(image, data, true);

                Assert.That(image.material, Is.Not.Null, bulletType.ToString());
                Assert.That(image.material.GetFloat("_TypeMode"),
                    Is.EqualTo((float)bulletType), bulletType.ToString());
                Assert.That(image.material.GetTexture("_FrameTex"),
                    Is.SameAs(Resources.Load<Texture2D>(
                        $"BulletFrames/{bulletType}")), bulletType.ToString());
            }
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void EveryBullet_UsesGradeOrganizedBorderlessCartoonArtwork()
    {
        BulletData[] bullets = BulletPoolSyncBuilder.LoadAllBulletData();
        Assert.That(bullets.Length, Is.EqualTo(71));
        foreach (BulletData bullet in bullets)
        {
            string path = AssetDatabase.GetAssetPath(bullet.CylinderIcon)
                .Replace('\\', '/');
            Assert.That(path, Does.StartWith(
                $"Assets/Sprites/Bullet_LineArt/{bullet.Grade}/"),
                bullet.DisplayName);
        }
    }

    [Test]
    public void EveryArtwork_VisualBoundsAreCentered()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Sprite", new[] { "Assets/Sprites/Bullet_LineArt" });
        Assert.That(guids.Length, Is.EqualTo(71));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                Color32[] pixels = texture.GetPixels32();
                int minX = texture.width;
                int minY = texture.height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a == 0)
                        continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }

                Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), path);
                float boundsCenterX = (minX + maxX) * 0.5f;
                float boundsCenterY = (minY + maxY) * 0.5f;
                Assert.That(boundsCenterX,
                    Is.EqualTo((texture.width - 1) * 0.5f).Within(0.51f), path);
                Assert.That(boundsCenterY,
                    Is.EqualTo((texture.height - 1) * 0.5f).Within(0.51f), path);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    [Test]
    public void Presenter_NormalizesTypeFrameAgainstSpriteSubRect()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Sprite sprite = Sprite.Create(texture, new Rect(16, 8, 32, 40),
            new Vector2(0.5f, 0.5f));
        BulletData data = ScriptableObject.CreateInstance<BulletData>();
        GameObject owner = new GameObject("BulletIconTest");
        UnityEngine.UI.Image image = owner.AddComponent<UnityEngine.UI.Image>();
        try
        {
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("cylinderIcon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BulletIconPresenter.Apply(image, data, true);
            Assert.That(image.material, Is.Not.Null);
            Vector4 rect = image.material.GetVector("_SpriteUvRect");
            Assert.That(rect.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(rect.z, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rect.w, Is.EqualTo(0.625f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    private static BulletData Find(string displayName)
    {
        return BulletPoolSyncBuilder.LoadAllBulletData()
            .Single(b => b.DisplayName == displayName);
    }
}
#endif
