using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BattleTestInteractionTests
{
    [UnityTest]
    public IEnumerator DropdownOptionsStayAbovePanelsAndAcceptRealPointerSelection()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        var previous = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("BattleTestDropdownPointer");
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
            BattleTestController controller = Object.FindFirstObjectByType<BattleTestController>();
            for (int page = 0; page < 2; page++)
            {
                gui.SelectPage(page);
                yield return null;
                yield return ClickDropdownOption(mouse, Field<TMP_Dropdown>(gui, "gradeFilter"), 1);
                // The last bullet type is below the fold and must also be scrollable/clickable.
                TMP_Dropdown type = Field<TMP_Dropdown>(gui, "typeFilter");
                int typeIndex = page == 0 ? type.options.Count - 1 : 1;
                yield return ClickDropdownOption(mouse, type, typeIndex);
                foreach (BattleTestListRow row in Field<RectTransform>(gui, "catalogContent").GetComponentsInChildren<BattleTestListRow>())
                {
                    if (row.Kind == BattleTestDragKind.None) continue;
                    Assert.That(page == 0 ? BattleTestCatalogFilters.Matches(controller.Bullets[row.Id], 1, typeIndex)
                        : BattleTestCatalogFilters.Matches(controller.Relics[row.Id], 1, typeIndex), Is.True);
                }
            }
            gui.SelectPage(0);
            yield return null;
            BattleTestListRow owned = Field<RectTransform>(gui, "ownedContent").GetComponentInChildren<BattleTestListRow>();
            owned.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 });
            var authoring = Object.FindFirstObjectByType<BattleTestBulletAuthoring>();
            TMP_Dropdown level = Field<TMP_Dropdown>(authoring, "level");
            yield return ClickDropdownOption(mouse, level, 2);
            Assert.That(Field<TMP_Text>(authoring, "title").text, Does.Contain(authoring.Selected.GetDisplayName(2)));
            yield return ClickMouse(mouse, level.GetComponent<RectTransform>().TransformPoint(level.GetComponent<RectTransform>().rect.center));
            Assert.That(level.IsExpanded, Is.True);
            yield return ClickMouse(mouse, new Vector2(10, Screen.height - 10));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(level.IsExpanded, Is.False, "목록 바깥 클릭은 아래 패널 대신 드롭다운을 닫아야 합니다.");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = previous;
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator ClickDropdownOption(UnityEngine.InputSystem.Mouse mouse, TMP_Dropdown dropdown, int index)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = dropdown.GetComponent<RectTransform>();
        yield return ClickMouse(mouse, rect.TransformPoint(rect.rect.center));
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(dropdown.IsExpanded, Is.True);
        Transform popup = dropdown.transform.Find("Dropdown List");
        var scroll = popup.GetComponent<UnityEngine.UI.ScrollRect>();
        if (index == dropdown.options.Count - 1) scroll.verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases();
        var option = popup.GetComponentsInChildren<UnityEngine.UI.Toggle>()[index];
        RectTransform optionRect = option.GetComponent<RectTransform>();
        Vector2 point = optionRect.TransformPoint(optionRect.rect.center);
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Toggle>(), Is.SameAs(option),
            "드롭다운 항목이 개발자 패널보다 앞에 표시되고 클릭되어야 합니다.");
        yield return ClickMouse(mouse, point);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(dropdown.value, Is.EqualTo(index));
        Assert.That(dropdown.IsExpanded, Is.False);
    }

    private static IEnumerator ClickMouse(UnityEngine.InputSystem.Mouse mouse, Vector2 point)
    {
        QueueMouse(mouse, point, false);
        yield return null;
        QueueMouse(mouse, point, true);
        yield return null;
        yield return null;
        QueueMouse(mouse, point, false);
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator RealPointerCanHoverCardWhitespaceAndDragFromCatalogToDeck()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        var previous = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("BattleTestPointerTest");
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
            BattleTestInteractions pointer = Object.FindFirstObjectByType<BattleTestInteractions>();
            DeckManager deck = Object.FindFirstObjectByType<DeckManager>();
            RectTransform catalog = Field<RectTransform>(gui, "catalogContent");
            RectTransform owned = Field<RectTransform>(gui, "ownedContent");
            Canvas.ForceUpdateCanvases();
            RectTransform card = catalog.GetComponentInChildren<BattleTestListRow>().GetComponent<RectTransform>();
            Vector2 source = card.TransformPoint(new Vector3(card.rect.xMax - 8, 0));
            QueueMouse(mouse, source, false);
            float deadline = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(pointer.TooltipVisible, Is.True, "이름이 없는 칸 오른쪽 여백에서도 실제 마우스 호버가 작동해야 합니다.");
            Assert.That(Field<TMP_Text>(pointer, "tooltipText").text, Does.Contain(card.GetComponent<BattleTestListRow>().Title));
            int before = deck.TotalBulletCount;
            QueueMouse(mouse, source, true);
            yield return null;
            yield return null;
            RectTransform target = owned.GetComponentInChildren<BattleTestListRow>().GetComponent<RectTransform>();
            Vector2 destination = target.TransformPoint(target.rect.center);
            QueueMouse(mouse, destination, true);
            yield return null;
            yield return null;
            Assert.That(pointer.IsDragging, Is.True);
            QueueMouse(mouse, destination, false);
            yield return null;
            yield return null;
            Assert.That(deck.TotalBulletCount, Is.EqualTo(before + 1));
            Assert.That(pointer.IsDragging, Is.False);
            gui.SelectPage(1);
            yield return null;
            yield return null;
            card = catalog.GetComponentInChildren<BattleTestListRow>().GetComponent<RectTransform>();
            source = card.TransformPoint(new Vector3(card.rect.xMax - 8, 0));
            QueueMouse(mouse, source, false);
            deadline = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(pointer.TooltipVisible, Is.True, "유물 칸의 여백도 호버 영역입니다.");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = previous;
        }
        yield return new ExitPlayMode();
    }

    private static void QueueMouse(UnityEngine.InputSystem.Mouse mouse, Vector2 position, bool pressed)
    {
        var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = position };
        if (pressed) state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, state);
    }

    [UnityTest]
    public IEnumerator UpgradeAndCheckpointKeepMixedBulletIdentityAndCatalogFiltersCompose()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestController controller = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestConsole console = Object.FindFirstObjectByType<BattleTestConsole>();
        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        DeckManager deck = controller.TestDeck;
        var before = new System.Collections.Generic.List<RunBulletSaveData>();
        var after = new System.Collections.Generic.List<RunBulletSaveData>();
        var cycle = new System.Collections.Generic.List<int>();
        deck.CaptureRunState(before, cycle);
        Assert.That(before.Count, Is.GreaterThan(1));
        console.RunCommand("bullet level 0 1");
        deck.CaptureRunState(after, cycle);
        Assert.That(after.Count, Is.EqualTo(before.Count));
        for (int i = 0; i < before.Count; i++)
        {
            if (before[i].acquisitionOrder == 0) before[i].level = 1;
            Assert.That(JsonUtility.ToJson(after[i]), Is.EqualTo(JsonUtility.ToJson(before[i])), "강화로 탄환 종류·위치·상태가 바뀌면 안 됩니다.");
        }
        console.RunCommand("checkpoint");
        console.RunCommand("deck set Gold 0");
        console.RunCommand("reset");
        deck.CaptureRunState(after, cycle);
        for (int i = 0; i < before.Count; i++)
            Assert.That(JsonUtility.ToJson(after[i]), Is.EqualTo(JsonUtility.ToJson(before[i])), "복원도 정확한 에셋을 유지해야 합니다.");

        TMP_Dropdown grade = Field<TMP_Dropdown>(gui, "gradeFilter");
        TMP_Dropdown type = Field<TMP_Dropdown>(gui, "typeFilter");
        RectTransform catalog = Field<RectTransform>(gui, "catalogContent");
        BulletData chosen = controller.Bullets[0];
        grade.value = (int)chosen.Grade + 1;
        type.value = (int)chosen.BulletType + 1;
        yield return null;
        yield return null;
        int expected = 0;
        foreach (BulletData data in controller.Bullets)
            if (data.Grade == chosen.Grade && data.BulletType == chosen.BulletType) expected++;
        Assert.That(catalog.GetComponentsInChildren<BattleTestListRow>().Length, Is.EqualTo(expected));
        foreach (BattleTestListRow row in catalog.GetComponentsInChildren<BattleTestListRow>())
        {
            Assert.That(controller.Bullets[row.Id].Grade, Is.EqualTo(chosen.Grade));
            Assert.That(controller.Bullets[row.Id].BulletType, Is.EqualTo(chosen.BulletType));
        }
        Field<TMP_InputField>(gui, "search").text = chosen.GetDisplayName(0);
        float deadline = Time.realtimeSinceStartup + .3f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(catalog.GetComponentInChildren<BattleTestListRow>().Id, Is.Zero);
        gui.SelectPage(1);
        yield return null;
        Assert.That(grade.options[1].text, Is.EqualTo("지속형"));
        grade.value = 1;
        type.value = 1;
        yield return null;
        yield return null;
        foreach (BattleTestListRow row in catalog.GetComponentsInChildren<BattleTestListRow>())
            if (row.Kind != BattleTestDragKind.None)
                Assert.That(BattleTestCatalogFilters.Matches(controller.Relics[row.Id], 1, 1), Is.True);
        gui.SelectPage(0);
        Assert.That(grade.value, Is.EqualTo((int)chosen.Grade + 1));
        Assert.That(type.value, Is.EqualTo((int)chosen.BulletType + 1));
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DragDropContextMenuAndWorldPickingPreserveCombatOwnership()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(BattleTestSceneBuilder.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        BattleTestController controller = Object.FindFirstObjectByType<BattleTestController>();
        BattleTestConsole console = Object.FindFirstObjectByType<BattleTestConsole>();
        BattleTestGui gui = Object.FindFirstObjectByType<BattleTestGui>();
        BattleTestInteractions pointer = Object.FindFirstObjectByType<BattleTestInteractions>();
        RectTransform owned = Field<RectTransform>(gui, "ownedContent");
        RectTransform catalog = Field<RectTransform>(gui, "catalogContent");
        var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = new Vector2(960, 500) };
        console.RunCommand("deck set 0 0");
        yield return null;
        yield return null;
        Field<TMP_InputField>(gui, "bulletLevel").SetTextWithoutNotify("2");
        BattleTestListRow source = catalog.GetComponentInChildren<BattleTestListRow>();
        source.OnBeginDrag(eventData);
        Assert.That(pointer.IsDragging, Is.True);
        DropTarget(BattleTestDropKind.Trash).OnDrop(eventData);
        Assert.That(controller.TestDeck.TotalBulletCount, Is.EqualTo(1), "카탈로그를 제거 영역에 놓아도 보유 탄환을 지우면 안 됩니다.");
        source.OnBeginDrag(eventData);
        DropTarget(BattleTestDropKind.Collection).OnDrop(eventData);
        source.OnEndDrag(eventData);
        yield return null;
        yield return null;
        Assert.That(controller.TestDeck.TotalBulletCount, Is.EqualTo(2));
        Assert.That(controller.TestDeck.FindByAcquisitionOrder(1).Level, Is.EqualTo(2));
        Assert.That(pointer.IsDragging, Is.False);

        BattleTestListRow row = owned.GetComponentsInChildren<BattleTestListRow>()[1];
        eventData.button = PointerEventData.InputButton.Right;
        row.OnPointerClick(eventData);
        RectTransform menu = Field<RectTransform>(pointer, "menu");
        Assert.That(menu.gameObject.activeSelf, Is.True);
        var buttons = menu.GetComponentsInChildren<UnityEngine.UI.Button>();
        buttons[1].onClick.Invoke();
        yield return null;
        yield return null;
        Assert.That(controller.TestDeck.FindByAcquisitionOrder(1).Level, Is.EqualTo(3));
        Assert.That(menu.gameObject.activeSelf, Is.False);
        eventData.button = PointerEventData.InputButton.Left;
        row = owned.GetComponentsInChildren<BattleTestListRow>()[1];
        row.OnBeginDrag(eventData);
        DropTarget(BattleTestDropKind.Trash).OnDrop(eventData);
        yield return null;
        yield return null;
        Assert.That(controller.TestDeck.FindByAcquisitionOrder(1), Is.Null);
        owned.GetComponentInChildren<BattleTestListRow>().OnBeginDrag(eventData);
        DropTarget(BattleTestDropKind.Trash).OnDrop(eventData);
        Assert.That(controller.TestDeck.TotalBulletCount, Is.EqualTo(1));
        Assert.That(Field<TMP_Text>(console, "resultText").text, Does.StartWith(BattleTestCommandRouter.RejectedPrefix));

        gui.SelectPage(3);
        yield return null;
        source = catalog.GetComponentInChildren<BattleTestListRow>();
        source.OnBeginDrag(eventData);
        owned.GetComponentsInChildren<BattleTestListRow>()[2].OnDrop(eventData);
        Assert.That(controller.TestInventory.GetItem(2), Is.SameAs(controller.Items[0]));
        Assert.That(controller.TestInventory.GetItem(0), Is.Null);

        gui.SelectPage(2);
        yield return null;
        Camera camera = Field<Camera>(pointer, "worldCamera");
        for (int lane = 0; lane < controller.TestBoard.LaneCount; lane++)
        for (int tile = 0; tile < controller.TestBoard.BoardCount; tile++)
        {
            controller.TestBoard.TryGetTilePosition(tile, lane, out Vector3 position);
            Assert.That(pointer.TryCell(camera.WorldToScreenPoint(position), out int foundTile, out int foundLane), Is.True);
            Assert.That(foundTile, Is.EqualTo(tile)); Assert.That(foundLane, Is.EqualTo(lane));
        }
        controller.TestBoard.TryGetTilePosition(4, 0, out Vector3 targetPosition);
        eventData.position = camera.WorldToScreenPoint(targetPosition);
        source = catalog.GetComponentInChildren<BattleTestListRow>();
        source.OnBeginDrag(eventData);
        DropTarget(BattleTestDropKind.World).OnDrop(eventData);
        yield return null;
        yield return null;
        Assert.That(controller.TestWaves.ActiveEnemies.Count, Is.EqualTo(1));
        pointer.SelectWorld(eventData.position);
        Assert.That(Field<TMP_InputField>(gui, "enemyTile").text, Is.EqualTo("4"));
        Assert.That(Field<TMP_InputField>(gui, "enemyLane").text, Is.EqualTo("0"));
        Assert.That(Field<TMP_Text>(gui, "details").text, Does.Contain("체력"));
        yield return null;
        yield return null;
        source = catalog.GetComponentInChildren<BattleTestListRow>();
        source.OnBeginDrag(eventData);
        DropTarget(BattleTestDropKind.World).OnDrop(eventData);
        Assert.That(controller.TestWaves.ActiveEnemies.Count, Is.EqualTo(1), "이미 점유된 칸에는 적을 중복 배치할 수 없습니다.");

        gui.SelectPage(0);
        yield return null;
        source = catalog.GetComponentInChildren<BattleTestListRow>();
        source.OnPointerEnter(eventData);
        float deadline = Time.realtimeSinceStartup + .5f;
        while (Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(pointer.TooltipVisible, Is.True);
        Assert.That(Field<TMP_Text>(pointer, "tooltipText").text, Does.Contain(source.Title));
        source.OnBeginDrag(eventData);
        Assert.That(pointer.TooltipVisible, Is.False);
        console.SetOpen(false);
        Assert.That(pointer.IsDragging, Is.False);
        console.SetOpen(true);
        yield return null;
        yield return null;
        Assert.That(controller.TestDeck.TotalBulletCount, Is.EqualTo(1));

        var handle = Field<TMP_Text>(gui, "leftTitle").GetComponent<BattleTestWindowHandle>();
        RectTransform window = Field<RectTransform>(handle, "window");
        Vector2 home = window.anchoredPosition;
        eventData.delta = new Vector2(50, 0);
        handle.OnBeginDrag(eventData); handle.OnDrag(eventData);
        Assert.That(window.anchoredPosition, Is.Not.EqualTo(home));
        eventData.clickCount = 2; handle.OnPointerClick(eventData);
        Assert.That(window.anchoredPosition, Is.EqualTo(home));
        Field<UnityEngine.UI.Button>(handle, "collapseButton").onClick.Invoke();
        Assert.That(Field<GameObject>(handle, "content").activeSelf, Is.False);
        gui.SelectPage(1);
        yield return null;
        Field<UnityEngine.UI.Button>(handle, "collapseButton").onClick.Invoke();
        Assert.That(Field<GameObject>(handle, "content").activeSelf, Is.True);

        gui.SelectPage(4);
        yield return null;
        TMP_InputField hp = GameObject.Find("현재 체력").GetComponent<TMP_InputField>();
        hp.text = "20";
        eventData.scrollDelta = Vector2.up;
        hp.GetComponent<BattleTestNumberField>().OnScroll(eventData);
        Assert.That(hp.text, Is.EqualTo("21"));
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    private static T Field<T>(Object owner, string name) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static BattleTestDropTarget DropTarget(BattleTestDropKind kind)
    {
        foreach (BattleTestDropTarget target in Object.FindObjectsByType<BattleTestDropTarget>(FindObjectsSortMode.None))
            if (target.Kind == kind) return target;
        Assert.Fail("드롭 영역 없음: " + kind);
        return null;
    }
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
