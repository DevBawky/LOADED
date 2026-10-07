using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class EnemyIntentPresentationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(EnemyAttackIconType.Melee, "근접 공격")]
    [TestCase(EnemyAttackIconType.Ranged, "원거리 사격")]
    [TestCase(EnemyAttackIconType.Throw, "투척 공격")]
    [TestCase(EnemyAttackIconType.Shotgun, "샷건 사격")]
    [TestCase(EnemyAttackIconType.Bomb, "폭탄 투척")]
    public void AttackTypeReachesIconAndTooltipInBothPreparationStates(EnemyAttackIconType type, string title)
    {
        var actor = new GameObject("Typed Intent");
        try
        {
            var icon = new GameObject("Queue", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(actor.transform, false);
            var queue = actor.AddComponent<EnemyActionQueueUI>();
            typeof(EnemyActionQueueUI).GetField("queueImage", PrivateInstance).SetValue(queue, icon.GetComponent<Image>());
            foreach (var action in new[] { EnemyTurnActionType.PrepareAttack, EnemyTurnActionType.Fire })
            {
                queue.ShowIntent(action, Vector3.zero, false, type);
                var graphic = actor.GetComponentInChildren<EnemyIntentGraphic>();
                Assert.That(typeof(EnemyIntentGraphic).GetField("attackType", PrivateInstance).GetValue(graphic), Is.EqualTo(type));
                var tooltip = graphic.GetComponent<EnemyActionTooltipTrigger>();
                Assert.That(typeof(EnemyActionTooltipTrigger).GetField("intentName", PrivateInstance).GetValue(tooltip), Does.StartWith(title));
                Assert.That(typeof(EnemyActionTooltipTrigger).GetField("intentDescription", PrivateInstance).GetValue(tooltip), Is.Not.Empty);
                using var mesh = new VertexHelper();
                typeof(EnemyIntentGraphic).GetMethod("OnPopulateMesh", PrivateInstance, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                var vertex = new UIVertex();
                mesh.PopulateUIVertex(ref vertex, mesh.currentVertCount - 1);
                Assert.That(vertex.color, Is.EqualTo((Color32)Color.white), "Attack silhouettes stay white in both states");
            }
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void PreparationAndReadySwordHaveDistinctPersistentPresentation()
    {
        var actor = new GameObject("Ready Intent Actor");
        try
        {
            var queueObject = new GameObject("Queue", typeof(RectTransform), typeof(Image));
            queueObject.transform.SetParent(actor.transform, false);
            var queue = actor.AddComponent<EnemyActionQueueUI>();
            typeof(EnemyActionQueueUI).GetField("queueImage", PrivateInstance)
                .SetValue(queue, queueObject.GetComponent<Image>());
            queue.ShowIntent(EnemyTurnActionType.PrepareAttack, Vector3.zero, false);
            var graphic = actor.GetComponentInChildren<EnemyIntentGraphic>();
            Assert.That(graphic.color, Is.EqualTo(Color.white));
            queue.ShowIntent(EnemyTurnActionType.Fire, Vector3.zero, false);
            Assert.That(graphic.color, Is.EqualTo(Color.white));
            Assert.That(graphic.color.a, Is.EqualTo(1f), "The sword must remain readable between pulses");
            typeof(EnemyIntentGraphic).GetField("emberTime", PrivateInstance).SetValue(graphic, 0.4f);
            typeof(EnemyIntentGraphic).GetMethod("Update", PrivateInstance).Invoke(graphic, null);
            Assert.That(graphic.color, Is.EqualTo(Color.white));
            typeof(EnemyIntentGraphic).GetField("emberTime", PrivateInstance).SetValue(graphic, 0.4f);
            queue.ShowIntent(EnemyTurnActionType.Fire, Vector3.zero, false);
            Assert.That(typeof(EnemyIntentGraphic).GetField("emberTime", PrivateInstance).GetValue(graphic),
                Is.EqualTo(0.4f), "Repeated intent rendering must not restart the pulse");
            queue.ShowIntent(EnemyTurnActionType.Wait, Vector3.zero, false);
            Assert.That(graphic.color, Is.EqualTo(Color.white));
            Assert.That(typeof(EnemyIntentGraphic).GetField("attackReady", PrivateInstance).GetValue(graphic), Is.False);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void AttackWarningHasDistinctPreloadedClipAndFixedLookupPreservesRandom()
    {
        var library = Resources.Load<SoundClipLibrary>("Sound/SoundClipLibrary");
        Assert.That(library, Is.Not.Null);
        string before = JsonUtility.ToJson(UnityEngine.Random.state);
        Assert.That(library.TryGetFixedSfx("SFX_EnemyReady", out var ready, out _, out _), Is.True);
        Assert.That(library.TryGetFixedSfx("SFX_EnemyAttackWarning", out var warning, out float volume, out var group), Is.True);
        Assert.That(warning, Is.Not.EqualTo(ready));
        Assert.That(warning.length, Is.InRange(0.1f, 0.25f));
        Assert.That(volume, Is.GreaterThan(0f));
        Assert.That(group, Is.Not.Null);
        var importer = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(warning));
        Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True);
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(before));
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void MoveArrowFollowsScreenDestinationAfterCanvasTurns(float facing)
    {
        var actor = new GameObject("Intent Actor");
        var cameraObject = new GameObject("Intent Camera");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3f, -10f);
            camera.transform.rotation = Quaternion.Euler(12f, -7f, 0f);
            actor.transform.position = new Vector3(2f, 1f, 5f);
            actor.transform.localScale = new Vector3(facing, 1f, 1f);
            var canvas = new GameObject("Intent Canvas", typeof(RectTransform));
            canvas.transform.SetParent(actor.transform, false);
            canvas.transform.localPosition = new Vector3(0f, -1f, -2f);
            canvas.transform.localScale = Vector3.one * 0.1f;
            var queueObject = new GameObject("Queue", typeof(RectTransform), typeof(Image));
            queueObject.transform.SetParent(canvas.transform, false);
            var queue = actor.AddComponent<EnemyActionQueueUI>();
            typeof(EnemyActionQueueUI).GetField("queueImage", PrivateInstance)
                .SetValue(queue, queueObject.GetComponent<Image>());
            Vector3 delta = new Vector3(0.5f, 0f, 2f);
            queue.ShowIntent(EnemyTurnActionType.Move, delta, false);
            typeof(EnemyActionQueueUI).GetField("intentCamera", PrivateInstance).SetValue(queue, camera);
            canvas.transform.rotation = camera.transform.rotation;
            typeof(EnemyActionQueueUI).GetMethod("LateUpdate", PrivateInstance).Invoke(queue, null);
            var graphic = actor.GetComponentInChildren<EnemyIntentGraphic>();
            var direction = (Vector2)typeof(EnemyIntentGraphic).GetField("direction", PrivateInstance).GetValue(graphic);
            Vector3 origin = graphic.transform.position;
            Vector2 shown = camera.WorldToScreenPoint(origin + graphic.transform.TransformVector(direction))
                - camera.WorldToScreenPoint(origin);
            Vector2 expected = camera.WorldToScreenPoint(actor.transform.position + delta)
                - camera.WorldToScreenPoint(actor.transform.position);
            Assert.That(Vector2.Dot(shown.normalized, expected.normalized), Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void AnimatedShadowCanReplaceLargeTopologyWithSmallFrame()
    {
        var owner = new GameObject("Shadow Source", typeof(SpriteRenderer), typeof(MeshRenderer));
        var texture = new Texture2D(8, 8);
        var large = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 1f, 0, SpriteMeshType.FullRect);
        var small = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 1f, 0, SpriteMeshType.FullRect);
        var mesh = new Mesh();
        try
        {
            var renderer = owner.GetComponent<SpriteRenderer>();
            Type partType = typeof(BattleContactShadow).GetNestedType("ShadowPart", BindingFlags.NonPublic);
            object part = Activator.CreateInstance(partType, renderer, owner, owner.GetComponent<MeshRenderer>(), mesh);
            void Upload(Sprite sprite)
            {
                renderer.sprite = sprite;
                Assert.That(partType.GetMethod("EnsureSpriteData").Invoke(part, null), Is.True);
                mesh.vertices = (Vector3[])partType.GetProperty("ProjectedVertices").GetValue(part);
                mesh.uv = (Vector2[])partType.GetProperty("SpriteUvs").GetValue(part);
                mesh.triangles = (int[])partType.GetProperty("Triangles").GetValue(part);
            }
            Upload(large);
            // Model an earlier imported frame with more vertices and indices.
            mesh.Clear();
            mesh.vertices = new Vector3[6];
            mesh.uv = new Vector2[6];
            mesh.triangles = new[] { 0, 1, 5 };
            Upload(small);
            Assert.That(mesh.vertexCount, Is.EqualTo(small.vertices.Length));
            Assert.That(mesh.triangles, Is.All.LessThan(mesh.vertexCount));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(large);
            Object.DestroyImmediate(small);
            Object.DestroyImmediate(texture);
        }
    }
}
