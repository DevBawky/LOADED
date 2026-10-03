using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyDamagePreviewBulletIconTests
{
    private const string EnemyPrefabPath =
        "Assets/Prefabs/Enemy/Enemy.prefab";
    private const string BulletAssetPath =
        "Assets/Scripts/Bullet/SO/Rare/Sniping.asset";
    private const string PreviewIconPath =
        "Canvas/Image | Damage Preview Bullet";

    [Test]
    public void DamagePreviewShowsHoveredBulletIconAtEnemyCenter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EnemyPrefabPath);
        BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(
            BulletAssetPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(bullet, Is.Not.Null);
        Assert.That(bullet.CylinderIcon, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            EnemyController enemy = instance.GetComponent<EnemyController>();
            Assert.That(enemy, Is.Not.Null);

            enemy.ShowDamagePreview(
                new[]
                {
                    new EnemyHealthBarFeedback.DamagePreviewSegment(
                        1,
                        Color.white,
                        true)
                },
                bullet.CylinderIcon);

            Transform previewTransform = instance.transform.Find(
                PreviewIconPath);
            Assert.That(previewTransform, Is.Not.Null);
            Assert.That(previewTransform.gameObject.activeSelf, Is.True);

            UnityEngine.UI.Image image =
                previewTransform.GetComponent<UnityEngine.UI.Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.SameAs(bullet.CylinderIcon));
            Assert.That(image.color.a, Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.raycastTarget, Is.False);

            RectTransform rect = (RectTransform)previewTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rect.sizeDelta.x, Is.GreaterThan(0f));
            Assert.That(rect.sizeDelta.y, Is.GreaterThan(0f));

            enemy.ClearDamagePreview();
            Assert.That(previewTransform.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void NonBulletDamagePreviewDoesNotShowTargetIcon()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            EnemyPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            EnemyController enemy = instance.GetComponent<EnemyController>();
            Assert.That(enemy, Is.Not.Null);

            enemy.ShowDamagePreview(
                new[]
                {
                    new EnemyHealthBarFeedback.DamagePreviewSegment(
                        1,
                        Color.white,
                        false)
                });

            Assert.That(instance.transform.Find(PreviewIconPath), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
