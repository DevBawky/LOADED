using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BattleTestBulletAuthoringTests
{
    private const string ProbePath = "Assets/Editor/Tests/BattleTestAuthoringProbe.asset";

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void SaveChangesOnlySelectedLevelAndExistingInstancesReadItImmediately(int level)
    {
        BulletData data = CreateProbe();
        try
        {
            string original = EditorJsonUtility.ToJson(data);
            var instances = new BulletInstance[4];
            var before = new BattleTestBulletAuthoring.Values[4];
            for (int i = 0; i < 4; i++)
            {
                instances[i] = new BulletInstance(data, i);
                for (int step = 0; step < i; step++) instances[i].TryUpgrade();
                before[i] = new BattleTestBulletAuthoring.Values(data.GetDamage(i), data.GetCriticalChance(i),
                    data.GetCriticalDamageMultiplier(i), data.GetMaxRange(i));
            }
            var values = new BattleTestBulletAuthoring.Values(123, 37.5f, 2.25f, 8);
            BattleTestBulletAssetWriter.WriteValues(data, level, values);
            for (int i = 0; i < 4; i++)
            {
                var expected = i == level ? values : before[i];
                Assert.That(instances[i].Damage, Is.EqualTo(expected.Damage));
                Assert.That(instances[i].CriticalChance, Is.EqualTo(expected.CriticalChance));
                Assert.That(instances[i].CriticalDamageMultiplier, Is.EqualTo(expected.CriticalMultiplier));
                Assert.That(instances[i].MaxRange, Is.EqualTo(expected.Range));
                Assert.That(instances[i].Data, Is.SameAs(data));
            }
            string disk = File.ReadAllText(ProbePath);
            Assert.That(disk, Does.Contain("criticalChance: 37.5"));
            Assert.That(disk, Does.Contain("criticalDamageMultiplier: 2.25"));
            Assert.That(EditorUtility.IsDirty(data), Is.False, "수치는 메모리가 아닌 에셋 파일에도 저장되어야 합니다.");
            BattleTestBulletAssetWriter.WriteValues(data, level, before[level]);
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(original), "Effects, identity and other authored fields must stay intact.");
        }
        finally { AssetDatabase.DeleteAsset(ProbePath); }
    }

    [Test]
    public void InvalidInputNeverPartiallyChangesAnAsset()
    {
        BulletData data = CreateProbe();
        try
        {
            string original = EditorJsonUtility.ToJson(data);
            byte[] disk = File.ReadAllBytes(ProbePath);
            var invalid = new[]
            {
                new BattleTestBulletAuthoring.Values(-1, 50, 2, 3),
                new BattleTestBulletAuthoring.Values(1, 101, 2, 3),
                new BattleTestBulletAuthoring.Values(1, float.NaN, 2, 3),
                new BattleTestBulletAuthoring.Values(1, 50, float.PositiveInfinity, 3),
                new BattleTestBulletAuthoring.Values(1, 50, .5f, 3),
                new BattleTestBulletAuthoring.Values(1, 50, 2, 11)
            };
            foreach (var values in invalid)
                Assert.Throws<ArgumentException>(() => BattleTestBulletAssetWriter.WriteValues(data, 0, values));
            Assert.Throws<ArgumentException>(() => BattleTestBulletAssetWriter.WriteValues(data, 4,
                new BattleTestBulletAuthoring.Values(1, 50, 2, 3)));
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(ProbePath), Is.EqualTo(disk));
        }
        finally { AssetDatabase.DeleteAsset(ProbePath); }
    }

    [UnityTest]
    public IEnumerator CardSelectionOpensAuthoringAndFunctionTooltipsWithoutChangingTheDeck()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        BattleTestController controller = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestBulletAuthoring authoring = Object.FindFirstObjectByType<BattleTestBulletAuthoring>();
        BattleTestInteractions interactions = Object.FindFirstObjectByType<BattleTestInteractions>();
        BattleTestListRow row = Field<RectTransform>(gui, "ownedContent").GetComponentInChildren<BattleTestListRow>();
        int count = controller.TestDeck.TotalBulletCount;
        row.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 });
        Assert.That(Field<GameObject>(authoring, "panel").activeSelf, Is.True);
        Assert.That(authoring.Selected, Is.Not.Null);
        Assert.That(BattleTestBulletAuthoring.SaveAsset, Is.Not.Null);
        string original = EditorJsonUtility.ToJson(authoring.Selected);
        TMP_InputField damage = Field<TMP_InputField>(authoring, "damage");
        damage.text = "321";
        Assert.That(Field<TMP_Text>(authoring, "status").text, Does.Contain("미저장"));
        Assert.That(EditorJsonUtility.ToJson(authoring.Selected), Is.EqualTo(original));
        Field<UnityEngine.UI.Button>(authoring, "revert").onClick.Invoke();
        Assert.That(damage.text, Is.EqualTo(authoring.Selected.GetDamage(0).ToString()));
        Field<TMP_Dropdown>(authoring, "level").value = 2;
        Assert.That(damage.text, Is.EqualTo(authoring.Selected.GetDamage(2).ToString()));
        Assert.That(Field<TMP_InputField>(authoring, "chance").contentType, Is.EqualTo(TMP_InputField.ContentType.DecimalNumber));
        damage.text = "-1";
        Field<UnityEngine.UI.Button>(authoring, "save").onClick.Invoke();
        Assert.That(EditorJsonUtility.ToJson(authoring.Selected), Is.EqualTo(original));
        Assert.That(Field<TMP_Text>(authoring, "status").text, Does.Contain("0 이상"));
        Field<UnityEngine.UI.Button>(authoring, "revert").onClick.Invoke();
        // A no-op save verifies the real Editor bridge without changing game balance.
        Field<UnityEngine.UI.Button>(authoring, "save").onClick.Invoke();
        Assert.That(Field<TMP_Text>(authoring, "status").text, Does.Contain("저장 완료"));
        Assert.That(EditorJsonUtility.ToJson(authoring.Selected), Is.EqualTo(original));
        Assert.That(controller.TestDeck.TotalBulletCount, Is.EqualTo(count));
        BattleTestControlTooltip help = Field<TMP_InputField>(authoring, "multiplier").GetComponent<BattleTestControlTooltip>();
        help.OnPointerEnter(new PointerEventData(EventSystem.current) { position = Vector2.one * 300 });
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(interactions.TooltipVisible, Is.True);
        Assert.That(Field<TMP_Text>(interactions, "tooltipText").text, Does.Contain("250%"));
        help.OnPointerExit(new PointerEventData(EventSystem.current));
        Assert.That(interactions.TooltipVisible, Is.False);
        gui.SelectPage(1);
        Assert.That(Field<GameObject>(authoring, "panel").activeSelf, Is.False);
        yield return new ExitPlayMode();
    }

    private static BulletData CreateProbe()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<BulletData>(ProbePath), Is.Null);
        var data = Object.Instantiate(BulletPoolSyncBuilder.LoadAllBulletData()[0]);
        data.name = "BattleTestAuthoringProbe";
        AssetDatabase.CreateAsset(data, ProbePath);
        AssetDatabase.SaveAssetIfDirty(data);
        return data;
    }
    private static T Field<T>(Object target, string name) where T : Object =>
        (T)new SerializedObject(target).FindProperty(name).objectReferenceValue;
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
