using UnityEditor;
using UnityEngine;

public sealed class EnemyDefeatCoinBurstPreviewWindow : EditorWindow
{
    private const string PrefabPath =
        "Assets/Prefabs/VFX/VFX_EnemyDefeatCoinBurst.prefab";

    private PreviewRenderUtility previewUtility;
    private GameObject previewInstance;
    private EnemyDefeatCoinBurstEffect previewEffect;
    private int comboKillCount = 1;
    private double previousUpdateTime;
    private bool previewPlaying;

    [MenuItem("Tools/LOADED/Preview Enemy Defeat Coin Burst")]
    public static void Open()
    {
        EnemyDefeatCoinBurstPreviewWindow window =
            GetWindow<EnemyDefeatCoinBurstPreviewWindow>();
        window.titleContent = new GUIContent("Coin Burst Preview");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
        window.Focus();
        window.Replay();
    }

    private void OnEnable()
    {
        CreatePreviewUtility();
        EditorApplication.update += OnEditorUpdate;
        previousUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        DestroyPreviewInstance();
        previewUtility?.Cleanup();
        previewUtility = null;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        comboKillCount = EditorGUILayout.IntSlider(
            new GUIContent("Combo Kill"),
            comboKillCount,
            1,
            10);

        if (EditorGUI.EndChangeCheck())
        {
            Replay();
        }

        if (GUILayout.Button("Replay", EditorStyles.toolbarButton))
        {
            Replay();
        }

        EditorGUILayout.EndHorizontal();

        Rect previewRect = GUILayoutUtility.GetRect(
            10f,
            10000f,
            10f,
            10000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        DrawPreview(previewRect);

        int coinCount = EnemyDefeatCoinBurstEffect.CalculateCoinCount(
            10,
            0.2f,
            comboKillCount);
        EditorGUILayout.LabelField(
            $"{coinCount} GoldCoin instances · standalone preview",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void CreatePreviewUtility()
    {
        if (previewUtility != null)
        {
            return;
        }

        previewUtility = new PreviewRenderUtility(true);
        previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
        previewUtility.camera.backgroundColor = new Color(
            0.035f,
            0.025f,
            0.018f,
            1f);
        previewUtility.camera.fieldOfView = 34f;
        previewUtility.camera.nearClipPlane = 0.01f;
        previewUtility.camera.farClipPlane = 50f;
        previewUtility.camera.transform.position = new Vector3(0f, 0.2f, -4f);
        previewUtility.camera.transform.rotation = Quaternion.identity;
        previewUtility.ambientColor = new Color(0.42f, 0.32f, 0.18f, 1f);
        previewUtility.lights[0].intensity = 1.65f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(
            32f,
            28f,
            0f);
        previewUtility.lights[1].intensity = 0.75f;
        previewUtility.lights[1].transform.rotation = Quaternion.Euler(
            340f,
            210f,
            0f);
    }

    private void Replay()
    {
        CreatePreviewUtility();
        DestroyPreviewInstance();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            PrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"Missing coin burst prefab at {PrefabPath}.");
            return;
        }

        previewInstance = previewUtility.InstantiatePrefabInScene(prefab);
        previewEffect = previewInstance.GetComponent<
            EnemyDefeatCoinBurstEffect>();

        if (previewEffect == null)
        {
            Debug.LogError(
                "Coin burst preview prefab is missing its effect component.");
            DestroyPreviewInstance();
            return;
        }

        previewEffect.PlayForEditorPreview(
            new Vector3(0f, -0.55f, 0f),
            comboKillCount,
            previewUtility.camera);
        previewPlaying = true;
        previousUpdateTime = EditorApplication.timeSinceStartup;
        Repaint();
    }

    private void OnEditorUpdate()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min(
            0.05f,
            (float)(currentTime - previousUpdateTime));
        previousUpdateTime = currentTime;

        if (previewEffect != null && previewPlaying)
        {
            previewPlaying = previewEffect.AdvanceEditorPreview(deltaTime);
            Repaint();
        }
    }

    private void DrawPreview(Rect previewRect)
    {
        if (Event.current.type != EventType.Repaint
            || previewUtility == null)
        {
            return;
        }

        previewUtility.BeginPreview(previewRect, GUIStyle.none);
        previewUtility.camera.Render();
        Texture previewTexture = previewUtility.EndPreview();
        GUI.DrawTexture(
            previewRect,
            previewTexture,
            ScaleMode.StretchToFill,
            false);
    }

    private void DestroyPreviewInstance()
    {
        previewEffect = null;
        previewPlaying = false;

        if (previewInstance == null)
        {
            return;
        }

        DestroyImmediate(previewInstance);
        previewInstance = null;
    }
}
