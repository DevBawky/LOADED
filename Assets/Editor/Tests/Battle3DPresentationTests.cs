using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class Battle3DPresentationTests
{
    private const string BattleScenePath = "Assets/Scenes/Battle.unity";
    private const string ProjectileProfilePath =
        "Assets/Resources/ProjectileVisuals/DefaultProjectileVisual.asset";
    private const string EnvironmentProfilePath =
        "Assets/Resources/Battle/DefaultBattleEnvironment.asset";

    [Test]
    public void EveryBulletUsesTheCommonSphereProjectileProfile()
    {
        ProjectileVisualProfile expected =
            AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                ProjectileProfilePath);

        Assert.That(expected, Is.Not.Null);
        Assert.That(expected.ProjectilePrefab, Is.Not.Null);
        Assert.That(expected.ArcHeight, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(expected.Scale, Is.EqualTo(0.17f).Within(0.0001f));
        Assert.That(
            expected.ProjectilePrefab.GetComponent<MeshFilter>(),
            Is.Not.Null);
        Assert.That(
            expected.ProjectilePrefab.GetComponent<SphereCollider>(),
            Is.Null);

        string[] bulletGuids = AssetDatabase.FindAssets("t:BulletData");
        Assert.That(bulletGuids, Is.Not.Empty);

        foreach (string guid in bulletGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(path);
            Assert.That(
                bullet.ProjectileVisual,
                Is.SameAs(expected),
                $"{path} does not use the common projectile profile.");
        }
    }

    [TestCase(0, 0.06f)]
    [TestCase(1, 0.06f)]
    [TestCase(2, 0.08f)]
    [TestCase(3, 0.10f)]
    [TestCase(4, 0.12f)]
    [TestCase(5, 0.14f)]
    [TestCase(6, 0.14f)]
    public void PlayerProjectileTravelScalesWithTileDistance(
        int tileDistance,
        float expectedDuration)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);
        PlayerShoot playerShoot = playerPrefab.GetComponent<PlayerShoot>();
        Assert.That(playerShoot, Is.Not.Null);
        SerializedObject serializedPlayerShoot =
            new SerializedObject(playerShoot);
        float authoredInterval = serializedPlayerShoot
            .FindProperty("shotInterval")
            .floatValue;

        Assert.That(authoredInterval, Is.EqualTo(0.15f).Within(0.0001f));
        Assert.That(
            PlayerShoot.ResolveProjectileTravelDuration(tileDistance),
            Is.EqualTo(expectedDuration).Within(0.0001f));

        Vector3 start = new Vector3(-2f, 1f, 0f);
        Vector3 nearTarget = new Vector3(-1f, 2f, 0.5f);
        Vector3 farTarget = new Vector3(12f, 4f, 7f);

        Assert.That(
            BulletProjectileView.EvaluateTravelPosition(
                start,
                nearTarget,
                0.2f,
                1f),
            Is.EqualTo(nearTarget));
        Assert.That(
            BulletProjectileView.EvaluateTravelPosition(
                start,
                farTarget,
                0.2f,
                1f),
            Is.EqualTo(farTarget));
    }

    [Test]
    public void PlayerProjectileTravelUsesMaximumDelayWhenDistanceIsUnknown()
    {
        Assert.That(
            PlayerShoot.ResolveProjectileTravelDuration(-1),
            Is.EqualTo(0.14f).Within(0.0001f));
    }

    [Test]
    public void ShotTimerUsesUnscaledTimeButStopsForExplicitPause()
    {
        Assert.That(
            PlayerShoot.AdvanceShotTimer(0.04f, 0.11f, false),
            Is.EqualTo(0.15f).Within(0.0001f));
        Assert.That(
            PlayerShoot.AdvanceShotTimer(0.04f, 0.11f, true),
            Is.EqualTo(0.04f).Within(0.0001f));
    }

    [Test]
    public void EveryBattleUsesTheCommon3DEnvironmentProfile()
    {
        BattleEnvironmentProfile expected =
            AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                EnvironmentProfilePath);

        Assert.That(expected, Is.Not.Null);

        string[] battleGuids = AssetDatabase.FindAssets("t:BattleData");
        Assert.That(battleGuids, Is.Not.Empty);

        foreach (string guid in battleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleData battle = AssetDatabase.LoadAssetAtPath<BattleData>(path);
            Assert.That(
                battle.EnvironmentProfile,
                Is.SameAs(expected),
                $"{path} does not use the common environment profile.");
        }
    }

    [Test]
    public void RotatedBoardKeepsEveryCellOnTheTerrainPlane()
    {
        GameObject managerObject = new GameObject("Board Manager");
        GameObject tileParentObject = new GameObject("Tile Parent");
        GameObject tileTemplateObject = new GameObject("Tile Template");

        try
        {
            tileParentObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0.03f, 0f),
                Quaternion.Euler(90f, 0f, 0f));

            BoardManager manager = managerObject.AddComponent<BoardManager>();
            BoardTile tileTemplate =
                tileTemplateObject.AddComponent<BoardTile>();
            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("tileParent").objectReferenceValue =
                tileParentObject.transform;
            serializedManager.FindProperty("boardDistance").floatValue = 2f;
            serializedManager.FindProperty("laneDistance").floatValue = 0.92f;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(manager.ConfigureBoard(3, 2, tileTemplate), Is.True);

            HashSet<Vector3> cells = new HashSet<Vector3>();
            for (int laneIndex = 0; laneIndex < 2; laneIndex++)
            {
                for (int tileIndex = 0; tileIndex < 3; tileIndex++)
                {
                    Assert.That(
                        manager.TryGetTilePosition(
                            tileIndex,
                            laneIndex,
                            out Vector3 position),
                        Is.True);
                    Assert.That(position.y, Is.EqualTo(0.03f).Within(0.0001f));
                    Assert.That(cells.Add(position), Is.True);
                    Assert.That(
                        manager.TryGetTileIndex(
                            position,
                            laneIndex,
                            out int resolvedIndex),
                        Is.True);
                    Assert.That(resolvedIndex, Is.EqualTo(tileIndex));
                }
            }

            Assert.That(cells, Has.Count.EqualTo(6));
            Assert.That(
                manager.TryGetTilePosition(1, 0, out Vector3 lowerLane),
                Is.True);
            Assert.That(
                manager.TryGetTilePosition(1, 1, out Vector3 upperLane),
                Is.True);
            Assert.That(
                upperLane.z,
                Is.GreaterThan(lowerLane.z),
                "The up input lane must appear above the lower lane.");
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tileParentObject);
            Object.DestroyImmediate(tileTemplateObject);
        }
    }

    [Test]
    public void CombatCameraShakePreservesTheAuthoredBattleCameraRotation()
    {
        GameObject cameraObject = new GameObject("Battle Camera");

        try
        {
            Quaternion expected = Quaternion.Euler(35f, 0f, 0f);
            cameraObject.transform.localRotation = expected;
            CombatCameraShake shake =
                cameraObject.AddComponent<CombatCameraShake>();
            MethodInfo awake = typeof(CombatCameraShake).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo lateUpdate = typeof(CombatCameraShake).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(awake, Is.Not.Null);
            Assert.That(lateUpdate, Is.Not.Null);
            awake.Invoke(shake, null);
            lateUpdate.Invoke(shake, null);
            Assert.That(
                Quaternion.Angle(
                    cameraObject.transform.localRotation,
                    expected),
                Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void BillboardPreservesTheSpritesHorizontalFacingDirection()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject spriteObject = new GameObject("Enemy Avatar");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            BattleSpriteBillboard billboard =
                spriteObject.AddComponent<BattleSpriteBillboard>();

            billboard.SetTargetCamera(camera);

            Assert.That(
                Vector3.Dot(
                    spriteObject.transform.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
            Assert.That(
                Vector3.Dot(
                    spriteObject.transform.up,
                    cameraObject.transform.up),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(spriteObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void DamageNumberFacesThePitchedBattleCamera()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject numberObject = new GameObject("Damage Number");

        try
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            DamageNumbersPro.DamageNumber number =
                numberObject.AddComponent<DamageNumbersPro.DamageNumberMesh>();

            EnemyDamageNumberDisplay.ConfigureForBattleCamera(number, camera);

            Assert.That(number.enable3DGame, Is.True);
            Assert.That(number.faceCameraView, Is.True);
            Assert.That(number.cameraOverride, Is.SameAs(camera.transform));
            Assert.That(
                Vector3.Dot(
                    numberObject.transform.right,
                    cameraObject.transform.right),
                Is.GreaterThan(0.999f));
            Assert.That(
                Vector3.Dot(
                    numberObject.transform.up,
                    cameraObject.transform.up),
                Is.GreaterThan(0.999f));
        }
        finally
        {
            Object.DestroyImmediate(numberObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void FollowOffsetKeepsThePlayerAtScreenCenter()
    {
        BattleEnvironmentProfile profile =
            AssetDatabase.LoadAssetAtPath<BattleEnvironmentProfile>(
                EnvironmentProfilePath);
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        GameObject cameraObject = null;

        try
        {
            Assert.That(profile, Is.Not.Null);
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            GameObject player = FindSceneObject(scene, "Player");
            Assert.That(player, Is.Not.Null);
            Transform avatar = player.transform.Find("Avatar");
            Assert.That(avatar, Is.Not.Null);
            cameraObject = new GameObject("Battle Camera Test");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = profile.OrthographicSize;
            cameraObject.transform.SetPositionAndRotation(
                player.transform.position + profile.CinemachineFollowOffset,
                profile.CameraLocalRotation);

            Vector3 viewportPoint = camera.WorldToViewportPoint(avatar.position);
            Assert.That(viewportPoint.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewportPoint.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
        }
        finally
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    [Test]
    public void WorldCanvasDepthOffsetPreservesItsScreenPosition()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            canvasRect.anchoredPosition3D = new Vector3(1f, 0.5f, 2f);
            Vector3 originalViewportPosition = camera.WorldToViewportPoint(
                canvasObject.transform.position);

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            Vector3 offsetViewportPosition = camera.WorldToViewportPoint(
                canvasObject.transform.position);
            Assert.That(offsetViewportPosition.x,
                Is.EqualTo(originalViewportPosition.x).Within(0.0001f));
            Assert.That(offsetViewportPosition.y,
                Is.EqualTo(originalViewportPosition.y).Within(0.0001f));
            Assert.That(offsetViewportPosition.z,
                Is.EqualTo(originalViewportPosition.z
                    - depthOffset.CameraDepthOffset).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void WorldCanvasCompensatesForCameraPitchWithoutChangingLayout()
    {
        GameObject cameraObject = new GameObject("Battle Camera");
        GameObject ownerObject = new GameObject("Enemy");
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform));

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 1f;
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 9.3f, -12f),
                Quaternion.Euler(35f, 0f, 0f));
            canvasObject.transform.SetParent(ownerObject.transform, false);
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            canvasRect.localScale = Vector3.one;

            BattleWorldCanvasDepthOffset depthOffset =
                canvasObject.AddComponent<BattleWorldCanvasDepthOffset>();
            depthOffset.SetTargetCamera(camera);

            Vector3 center = camera.WorldToViewportPoint(
                canvasRect.position);
            Vector3 right = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.right));
            Vector3 up = camera.WorldToViewportPoint(
                canvasRect.TransformPoint(Vector3.up));
            float projectedWidth = Vector2.Distance(center, right);
            float projectedHeight = Vector2.Distance(center, up);

            Assert.That(
                projectedWidth,
                Is.EqualTo(projectedHeight).Within(0.0001f));
            Assert.That(
                canvasRect.localScale.x,
                Is.EqualTo(Mathf.Cos(35f * Mathf.Deg2Rad))
                    .Within(0.0001f));
            Assert.That(canvasRect.localScale.y, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void BattleSceneContainsConfigured3DWorldAndPreservesBackgroundState()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Single);
            GameObject background = FindSceneObject(
                scene,
                "##--BACKGROUNDS--##");
            GameObject environment = FindSceneObject(
                scene,
                "##--ENVIRONMENT--##");
            GameObject board = FindSceneObject(scene, "##--BOARDS--##");
            GameObject player = FindSceneObject(scene, "Player");
            Camera battleCamera = Object.FindFirstObjectByType<Camera>(
                FindObjectsInactive.Include);

            Assert.That(background, Is.Not.Null);
            Assert.That(background.activeSelf, Is.False);
            Assert.That(environment, Is.Not.Null);
            Assert.That(
                environment.GetComponent<BattleWorld3DController>(),
                Is.Not.Null);

            Terrain terrain = Object.FindFirstObjectByType<Terrain>(
                FindObjectsInactive.Include);
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.gameObject.scene.path,
                Is.EqualTo(BattleScenePath));
            Assert.That(AssetDatabase.Contains(terrain.terrainData), Is.True);
            Assert.That(terrain.terrainData.size, Is.EqualTo(
                new Vector3(36f, 2f, 14f)));
            Assert.That(
                Object.FindFirstObjectByType<Light>(
                    FindObjectsInactive.Include),
                Is.Not.Null);

            Assert.That(board, Is.Not.Null);
            Assert.That(
                Quaternion.Angle(
                    board.transform.localRotation,
                    Quaternion.Euler(90f, 0f, 0f)),
                Is.LessThan(0.01f));

            Assert.That(battleCamera, Is.Not.Null);
            Assert.That(battleCamera.orthographic, Is.True);
            Assert.That(battleCamera.orthographicSize, Is.EqualTo(5f));
            Assert.That(
                Quaternion.Angle(
                    battleCamera.transform.localRotation,
                    Quaternion.Euler(35f, 0f, 0f)),
                Is.LessThan(0.01f));
            UniversalAdditionalCameraData cameraData =
                battleCamera.GetUniversalAdditionalCameraData();
            SerializedObject serializedCameraData =
                new SerializedObject(cameraData);
            Assert.That(
                serializedCameraData.FindProperty("m_RendererIndex").intValue,
                Is.EqualTo(1));

            GameObject gameplayCanvas = FindRootObject(scene, "Canvas");
            GameObject gameStartCanvas = FindRootObject(
                scene,
                "Canvas | Game Start");
            Assert.That(gameplayCanvas, Is.Not.Null);
            Assert.That(gameStartCanvas, Is.Not.Null);
            Assert.That(
                gameplayCanvas.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(
                gameStartCanvas.GetComponent<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            Transform avatar = player.transform.Find("Avatar");
            Assert.That(avatar, Is.Not.Null);
            CinemachineCamera cinemachineCamera =
                battleCamera.GetComponent<CinemachineCamera>();
            Assert.That(cinemachineCamera, Is.Not.Null);
            Assert.That(cinemachineCamera.Follow, Is.SameAs(player.transform));
            Assert.That(
                avatar.GetComponent<BattleSpriteBillboard>(),
                Is.Not.Null);
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = FindDescendant(root.transform, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject FindDescendant(
        Transform current,
        string objectName)
    {
        if (current.name == objectName)
        {
            return current.gameObject;
        }

        foreach (Transform child in current)
        {
            GameObject result = FindDescendant(child, objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
