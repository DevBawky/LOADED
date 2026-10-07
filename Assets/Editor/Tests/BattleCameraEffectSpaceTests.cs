using NUnit.Framework;
using UnityEngine;

public sealed class BattleCameraEffectSpaceTests
{
    [Test]
    public void CameraPlaneDeltaUsesTheCurrentCameraAxes()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(31f, 24f, 0f);
            Vector2 authoredDelta = new Vector2(0.7f, -0.35f);

            Vector3 resolved = BattleCameraEffectSpace.CameraPlaneDelta(
                authoredDelta,
                camera);
            Vector3 expected = camera.transform.right * authoredDelta.x
                + camera.transform.up * authoredDelta.y;

            Assert.That(Vector3.Distance(resolved, expected),
                Is.LessThan(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void CameraPlaneComponentsRecoverCameraRelativeMovement()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(38f, -17f, 0f);
            Vector2 authoredDelta = new Vector2(-0.8f, 0.45f);
            Vector3 worldDelta = BattleCameraEffectSpace.CameraPlaneDelta(
                authoredDelta,
                camera);

            Vector2 resolved = BattleCameraEffectSpace.CameraPlaneComponents(
                worldDelta,
                camera);

            Assert.That(Vector2.Distance(resolved, authoredDelta),
                Is.LessThan(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void HorizontalViewportCorrectionAlignsToProjectedGridCenter()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-2f, 5f, -10f),
                Quaternion.Euler(29f, 13f, 0f));
            Vector3 visualCenter = new Vector3(1.3f, 1.1f, 0.6f);
            Vector3 targetCenterLine = new Vector3(1f, 1.1f, 0.2f);

            Vector3 correction = BattleCameraEffectSpace
                .ResolveHorizontalViewportCorrection(
                    visualCenter,
                    targetCenterLine,
                    camera);
            Vector3 resolvedViewport = camera.WorldToViewportPoint(
                visualCenter + correction);
            Vector3 targetViewport = camera.WorldToViewportPoint(
                targetCenterLine);

            Assert.That(resolvedViewport.x,
                Is.EqualTo(targetViewport.x).Within(0.0001f));
            Assert.That(Vector3.Dot(correction, camera.transform.up),
                Is.Zero.Within(0.0001f));
            Assert.That(Vector3.Dot(correction, camera.transform.forward),
                Is.Zero.Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void ProjectedGroundPointKeepsTheGridCenterOnScreen()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 3.5f, -12f),
                Quaternion.Euler(9f, 0f, 0f));
            Vector3 gridCenter = new Vector3(-1f, 0.03f, -0.46f);

            bool resolved = BattleContactShadow
                .TryResolveProjectedGroundPoint(
                    camera,
                    gridCenter,
                    -0.12f,
                    out Vector3 groundPoint);
            Vector3 gridViewport = camera.WorldToViewportPoint(gridCenter);
            Vector3 groundViewport = camera.WorldToViewportPoint(groundPoint);

            Assert.That(resolved, Is.True);
            Assert.That(groundPoint.y,
                Is.EqualTo(-0.12f).Within(0.0001f));
            Assert.That(groundViewport.x,
                Is.EqualTo(gridViewport.x).Within(0.0001f));
            Assert.That(groundViewport.y,
                Is.EqualTo(gridViewport.y).Within(0.0001f));
            Assert.That(groundPoint.z, Is.GreaterThan(gridCenter.z));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void VisualCenterFollowsTheBillboardAfterCameraRotation()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject actorObject = new GameObject("Enemy");
        GameObject visualObject = new GameObject("Avatar");
        GameObject spriteObject = new GameObject("Body");
        Texture2D texture = new Texture2D(4, 4);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.15f, 0.35f),
            4f);

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(1.2f, 6.5f, -9f),
                Quaternion.Euler(31f, -8f, 0f));
            actorObject.transform.position = new Vector3(0.7f, 0.2f, 1.1f);
            visualObject.transform.SetParent(actorObject.transform, false);
            visualObject.transform.localPosition = new Vector3(0.12f, 0.18f, -0.08f);
            spriteObject.transform.SetParent(visualObject.transform, false);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            BattleSpriteBillboard billboard =
                visualObject.AddComponent<BattleSpriteBillboard>();
            billboard.SetTargetCamera(camera);

            AssertProjectedCenterMatchesRenderer(
                actorObject.transform,
                renderer,
                camera);

            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-2.4f, 8f, -7f),
                Quaternion.Euler(42f, 14f, 0f));
            billboard.SetTargetCamera(camera);

            AssertProjectedCenterMatchesRenderer(
                actorObject.transform,
                renderer,
                camera);
        }
        finally
        {
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void VisualOriginUsesTheCameraAlignedAvatarRoot()
    {
        GameObject actorObject = new GameObject("Enemy");
        GameObject visualObject = new GameObject("Avatar");

        try
        {
            actorObject.transform.position = new Vector3(1f, 0.2f, 3f);
            visualObject.transform.SetParent(actorObject.transform, false);
            visualObject.transform.localPosition = new Vector3(0.3f, 0.4f, -0.2f);
            visualObject.AddComponent<BattleSpriteBillboard>();

            Vector3 resolved = BattleCameraEffectSpace
                .ResolveActorVisualOrigin(
                    actorObject.transform,
                    actorObject.transform.position);

            Assert.That(Vector3.Distance(
                    resolved,
                    visualObject.transform.position),
                Is.LessThan(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static void AssertProjectedCenterMatchesRenderer(
        Transform actor,
        SpriteRenderer renderer,
        Camera camera)
    {
        Vector3 resolved = BattleCameraEffectSpace.ResolveActorVisualCenter(
            actor,
            camera,
            actor.position);
        Vector3 expectedViewport = camera.WorldToViewportPoint(
            renderer.transform.TransformPoint(renderer.localBounds.center));
        Vector3 resolvedViewport = camera.WorldToViewportPoint(resolved);

        Assert.That(resolvedViewport.x,
            Is.EqualTo(expectedViewport.x).Within(0.0001f));
        Assert.That(resolvedViewport.y,
            Is.EqualTo(expectedViewport.y).Within(0.0001f));
    }
}
