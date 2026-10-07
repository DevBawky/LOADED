using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BattleTestGuiTests
{
    [UnityTest]
    public IEnumerator KoreanGuiEditsIndividualBulletsAndUsesRealCombatOwners()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;

        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        BattleTestConsole console = Object.FindFirstObjectByType<BattleTestConsole>();
        BattleTestController controller = Object.FindFirstObjectByType<BattleTestController>();
        DeckManager deck = Object.FindFirstObjectByType<DeckManager>();
        RectTransform owned = Field<RectTransform>(gui, "ownedContent");
        RectTransform catalog = Field<RectTransform>(gui, "catalogContent");
        Assert.That(console.IsOpen && GamePauseController.IsPaused, Is.True);
        Assert.That(catalog.GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(controller.Bullets.Count));
        Assert.That(catalog.rect.height, Is.GreaterThan(catalog.GetComponentInParent<ScrollRect>().viewport.rect.height));
        Assert.That(Field<TMP_Text>(gui, "leftTitle").text, Does.StartWith("내 덱"));

        console.RunCommand("deck set 0 0");
        console.RunCommand("bullet add 0 3");
        yield return null;
        yield return null;
        BattleTestListRow[] rows = owned.GetComponentsInChildren<BattleTestListRow>();
        Assert.That(rows.Length, Is.EqualTo(2));
        Assert.That(Action(rows[0], 0).interactable, Is.False);
        Assert.That(Action(rows[1], 1).interactable, Is.False);
        AssertReachable(Action(rows[0], 3));
        Action(rows[0], 3).onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(deck.FindByAcquisitionOrder(0), Is.Null);
        Assert.That(deck.FindByAcquisitionOrder(1).Level, Is.EqualTo(3));
        rows = owned.GetComponentsInChildren<BattleTestListRow>();
        Assert.That(rows.Length, Is.EqualTo(1));
        Assert.That(Action(rows[0], 3).interactable, Is.False, "마지막 탄환은 제거할 수 없어야 합니다.");

        Field<TMP_InputField>(gui, "bulletLevel").SetTextWithoutNotify("2");
        Action(catalog.GetComponentsInChildren<BattleTestListRow>()[0], 3).onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(deck.TotalBulletCount, Is.EqualTo(2));
        Assert.That(deck.FindByAcquisitionOrder(2).Level, Is.EqualTo(2));
        rows = owned.GetComponentsInChildren<BattleTestListRow>();
        Action(rows[1], 1).onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(deck.FindByAcquisitionOrder(2).Level, Is.EqualTo(3));
        rows = owned.GetComponentsInChildren<BattleTestListRow>();
        Assert.That(Action(rows[1], 1).interactable, Is.False);
        Action(rows[1], 2).onClick.Invoke();
        Assert.That(deck.LoadedBullets, Is.Empty, "개발자 창의 설명 조작으로 장전하면 안 됩니다.");

        TMP_InputField search = Field<TMP_InputField>(gui, "search");
        search.text = "없는탄환검색검증";
        float deadline = Time.realtimeSinceStartup + .3f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(catalog.GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(1));
        Assert.That(Field<TMP_Text>(catalog.GetComponentInChildren<BattleTestListRow>(), "title").text,
            Is.EqualTo("검색 결과가 없습니다"));
        search.text = controller.Bullets[0].GetDisplayName(0);
        deadline = Time.realtimeSinceStartup + .3f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Field<TMP_Text>(catalog.GetComponentInChildren<BattleTestListRow>(), "title").text,
            Is.EqualTo(controller.Bullets[0].GetDisplayName(0)));

        for (int i = deck.TotalBulletCount; i < DeckManager.MaximumOwnedBulletCount; i++)
            console.RunCommand("bullet add 0");
        yield return null;
        yield return null;
        Assert.That(Field<Button>(gui, "addSelected").interactable, Is.False);
        Assert.That(Action(catalog.GetComponentInChildren<BattleTestListRow>(), 3).interactable, Is.False);

        Tab(gui, 1).onClick.Invoke();
        yield return null;
        Action(catalog.GetComponentInChildren<BattleTestListRow>(), 3).onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(controller.TestRelics.Count, Is.EqualTo(1));
        Action(owned.GetComponentInChildren<BattleTestListRow>(), 3).onClick.Invoke();
        Assert.That(controller.TestRelics.Count, Is.Zero);

        Tab(gui, 2).onClick.Invoke();
        yield return null;
        Action(catalog.GetComponentInChildren<BattleTestListRow>(), 3).onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(controller.TestWaves.ActiveEnemies.Count, Is.EqualTo(1));
        Action(owned.GetComponentInChildren<BattleTestListRow>(), 1).onClick.Invoke();
        Field<TMP_InputField>(gui, "statusStacks").text = "7";
        Field<Button>(gui, "applyStatus").onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(controller.TestWaves.ActiveEnemies[0].GetStatusStacks(StatusEffectType.Poison), Is.EqualTo(7));
        Assert.That(Field<TMP_Text>(gui, "details").text, Does.Contain("독 7"));

        Tab(gui, 3).onClick.Invoke();
        yield return null;
        Field<TMP_InputField>(gui, "itemSlot").SetTextWithoutNotify("2");
        Action(catalog.GetComponentInChildren<BattleTestListRow>(), 3).onClick.Invoke();
        Assert.That(controller.TestInventory.GetItem(2), Is.SameAs(controller.Items[0]));
        Tab(gui, 4).onClick.Invoke();
        yield return null;
        GameObject.Find("현재 체력").GetComponent<TMP_InputField>().text = "37";
        GameObject.Find("최대 체력").GetComponent<TMP_InputField>().text = "150";
        GameObject.Find("체력 적용").GetComponent<Button>().onClick.Invoke();
        Assert.That(controller.TestHealth.CurrentHealth, Is.EqualTo(37));
        Assert.That(controller.TestHealth.MaxHealth, Is.EqualTo(150));
        GameObject.Find("레인당 칸 수 (2~30)").GetComponent<TMP_InputField>().text = "0";
        GameObject.Find("보드 재생성").GetComponent<Button>().onClick.Invoke();
        Assert.That(Field<TMP_Text>(console, "resultText").text, Does.StartWith(BattleTestCommandRouter.RejectedPrefix));

        Tab(gui, 5).onClick.Invoke();
        yield return null;
        GameObject.Find("도움말 보기").GetComponent<Button>().onClick.Invoke();
        Assert.That(Field<TMP_Text>(console, "output").text, Does.Contain("번호와 좌표는 0부터"));
        Tab(gui, 0).onClick.Invoke();
        console.SetOpen(false);
        Assert.That(GamePauseController.IsPaused, Is.False);
        console.SetOpen(true);
        yield return null;
        yield return null;
        Assert.That(owned.GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(deck.TotalBulletCount));
        Assert.That(catalog.GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(controller.Bullets.Count));
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    private static T Field<T>(Object owner, string name) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

    private static Button Action(BattleTestListRow row, int index) =>
        (Button)new SerializedObject(row).FindProperty("actions").GetArrayElementAtIndex(index).objectReferenceValue;

    private static Button Tab(BattleTestGui gui, int index) =>
        (Button)new SerializedObject(gui).FindProperty("tabs").GetArrayElementAtIndex(index).objectReferenceValue;

    private static void AssertReachable(Button button)
    {
        button.GetComponentInParent<BattleTestListRow>()?.OnPointerEnter(new PointerEventData(EventSystem.current));
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
