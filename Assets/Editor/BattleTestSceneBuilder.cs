using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleTestSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/BattleTest.unity";
    private const string SourcePath = "Assets/Scenes/Battle.unity";

    [MenuItem("Tools/LOADED/전투 테스트/Battle에서 생성 또는 갱신")]
    public static void Build()
    {
        EnsureCanChangeScenes();
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene source = EditorSceneManager.OpenScene(SourcePath);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                throw new InvalidOperationException("Battle 씬을 복제하지 못했습니다.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            StateManager state = Find<StateManager>(scene);
            var stateData = new SerializedObject(state);
            var stage = (StageData)stateData.FindProperty("stages").GetArrayElementAtIndex(0).objectReferenceValue;
            BattleData environment = stage.Battles[0];
            state.enabled = false;

            var root = new GameObject("Battle Test Tools");
            var controller = root.AddComponent<BattleTestController>();
            var console = root.AddComponent<BattleTestConsole>();
            var data = new SerializedObject(controller);
            Reference(data, "board", Find<BoardManager>(scene));
            Reference(data, "waves", Find<WaveManager>(scene));
            Reference(data, "deck", Find<DeckManager>(scene));
            Reference(data, "relics", Find<RelicManager>(scene));
            Reference(data, "player", Find<PlayerMove>(scene));
            Reference(data, "shooting", Find<PlayerShoot>(scene));
            Reference(data, "health", Find<PlayerHealth>(scene));
            Reference(data, "inventory", Find<PlayerInventory>(scene));
            Reference(data, "currency", Find<CurrencyManager>(scene));
            Reference(data, "rewards", Find<RewardManager>(scene));
            Reference(data, "feedback", Find<CombatFeedbackController>(scene));
            Reference(data, "world", Find<BattleWorld3DController>(scene));
            Reference(data, "state", state);
            Reference(data, "mainGamePanel", stateData.FindProperty("mainGamePanel").objectReferenceValue);
            Reference(data, "environment", environment);
            data.FindProperty("playerOffset").vector3Value = stateData.FindProperty("playerSpawnOffset").vector3Value;
            Catalog<BulletData>(data, "bulletCatalog");
            Catalog<EnemyData>(data, "enemyCatalog");
            Catalog<RelicData>(data, "relicCatalog");
            Catalog<ItemData>(data, "itemCatalog");
            data.ApplyModifiedPropertiesWithoutUndo();

            // The normal guide installer detects this disabled component and leaves test input alone.
            FirstRunGuideController guide = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<FirstRunGuideController>(true)).FirstOrDefault();
            if (guide == null)
            {
                var suppressedGuide = new GameObject("Suppressed Test Guide", typeof(RectTransform), typeof(Canvas));
                suppressedGuide.transform.SetParent(root.transform, false);
                suppressedGuide.SetActive(false);
                guide = suppressedGuide.AddComponent<FirstRunGuideController>();
            }
            guide.enabled = false;

            TMP_FontAsset font = Find<TMP_Text>(scene).font;
            CreateConsole(root.transform, controller, console, font);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("전투 테스트 씬을 저장하지 못했습니다.");
            Debug.Log("전투 테스트 씬을 생성했습니다. Tools > LOADED > 전투 테스트 > 열기에서 실행하세요.");
        }
        finally
        {
            if (previous.Any(value => value.isLoaded && value.isActive && !string.IsNullOrEmpty(value.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [MenuItem("Tools/LOADED/전투 테스트/열기")]
    public static void Open()
    {
        EnsureCanChangeScenes();
        EditorSceneManager.OpenScene(ScenePath);
    }

    public static void InstallEnemyRefillControls()
    {
        EnsureCanChangeScenes();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("BattleTest 씬에서 실행해 주세요.");
        BattleTestGuiBuilder.InstallEnemyRefillControls(Find<BattleTestGui>(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("자동 보충 UI를 저장하지 못했습니다.");
    }

    private static void EnsureCanChangeScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play를 종료한 뒤 테스트 씬을 열거나 갱신해 주세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("열려 있는 씬의 변경 사항을 먼저 저장해 주세요.");
    }

    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(value => value.GetComponentsInChildren<T>(true)).FirstOrDefault()
        ?? throw new InvalidOperationException("Battle 씬에 필요한 구성 요소가 없습니다: " + typeof(T).Name);

    private static void Reference(SerializedObject data, string name, UnityEngine.Object value) =>
        data.FindProperty(name).objectReferenceValue = value;

    private static void Catalog<T>(SerializedObject data, string property) where T : UnityEngine.Object
    {
        T[] values = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(value => value != null).OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
        if (values.Length == 0) throw new InvalidOperationException("도감이 비어 있습니다: " + typeof(T).Name);
        SerializedProperty array = data.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void CreateConsole(Transform parent, BattleTestController controller,
        BattleTestConsole console, TMP_FontAsset font)
    {
        BattleTestGuiBuilder.Create(parent, controller, console, font);
    }
}
