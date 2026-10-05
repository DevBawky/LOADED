using System;
using TMPro;
using UnityEditor;
using UnityEngine;

internal static class BattleTestGuiBuilder
{
    private static readonly Color Surface = new Color(0.075f, 0.10f, 0.14f);
    private static readonly Color ButtonColor = new Color(0.14f, 0.22f, 0.29f);

    internal static void Create(Transform parent, BattleTestController controller, BattleTestConsole console, TMP_FontAsset fallbackFont)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Package/Bold_Ko SDF.asset") ?? fallbackFont;
        var gui = parent.gameObject.AddComponent<BattleTestGui>();
        var interactions = parent.gameObject.AddComponent<BattleTestInteractions>();
        RectTransform canvasRect = Rect("테스트 설정 화면", parent, 0, 0, 1, 1);
        Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // TMP dropdown lists use 30000 and their input blockers use 29999.
        // Keep the developer panels below both so options remain visible and clickable.
        canvas.sortingOrder = 29000;
        var scaler = canvasRect.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        Box("단축키 배경", canvasRect, 0, .965f, 1, 1, new Color(.025f, .04f, .055f, .97f), false);
        TMP_Text hint = Label("단축키", canvasRect, font, 19, .02f, .965f, .98f, 1);
        RectTransform panel = Rect("설정 패널", canvasRect, 0, 0, 1, .965f);
        RectTransform worldInput = Box("전투판 직접 조작", panel, 0, 0, 1, 1, Color.clear);
        DropTarget(worldInput, interactions, BattleTestDropKind.World);
        Box("도구막대 배경", panel, 0, .795f, 1, 1, new Color(.025f, .04f, .055f, .98f));
        Label("제목", panel, font, 32, .025f, .936f, .23f, .987f).text = "전투 테스트";

        string[] toolbarLabels = { "자동 / 수동", "한 사이클", "무적 전환", "상태 저장", "상태 복원", "전투로 돌아가기" };
        string[] toolbarCommands = { "ai toggle", "step", "god toggle", "checkpoint", "reset", "close" };
        for (int i = 0; i < toolbarLabels.Length; i++)
            CommandButton(toolbarLabels[i], panel, font, console, controller, toolbarCommands[i],
                .24f + i * .124f, .938f, .36f + i * .124f, .982f, i == 1);
        TMP_Text summary = Label("현재 전투 상태", panel, font, 20, .025f, .861f, .975f, .927f);
        summary.color = new Color(.62f, .91f, .80f);
        string[] tabNames = { "탄환 · 내 덱", "유물", "적 배치 · 상태", "아이템", "플레이어 · 보드", "명령 · 도움말" };
        var tabs = new UnityEngine.UI.Button[tabNames.Length];
        for (int i = 0; i < tabs.Length; i++)
            tabs[i] = Button(tabNames[i], panel, font, .025f + i * .16f, .803f, .177f + i * .16f, .85f);

        RectTransform body = Rect("목록 화면", panel, .012f, .095f, .988f, .78f);
        RectTransform left = Box("보유 목록", body, 0, 0, .29f, 1, Surface);
        RectTransform right = Box("도감", body, .71f, 0, 1, 1, Surface);
        TMP_Text leftTitle = Label("보유 제목", left, font, 24, .025f, .92f, .98f, .98f);
        RectTransform owned = Scroll("보유 스크롤", left, .015f, .16f, .985f, .90f);
        DropTarget(owned.GetComponentInParent<UnityEngine.UI.ScrollRect>().GetComponent<RectTransform>(), interactions, BattleTestDropKind.Collection);
        TMP_Text dragTip = Label("보유 조작 안내", left, font, 16, .025f, .105f, .975f, .15f);
        dragTip.text = "우클릭 조작 · 제목을 끌어 패널 이동 / 두 번 클릭 복귀";
        RectTransform trash = Box("제거 드롭", left, .025f, .02f, .975f, .09f, new Color(.35f, .15f, .17f));
        Label("제거 안내", trash, font, 18, .02f, .05f, .98f, .95f).text = "여기에 놓아 제거";
        DropTarget(trash, interactions, BattleTestDropKind.Trash);
        TMP_Text rightTitle = Label("도감 제목", right, font, 24, .025f, .92f, .98f, .98f);
        TMP_InputField search = Input("도감 검색", right, font, "한글 이름 · 유형 · 효과 검색", "", .025f, .84f, .975f, .913f, false);
        RectTransform filters = Rect("도감 분류", right, .025f, .772f, .975f, .827f);
        TMP_Dropdown gradeFilter = Dropdown("등급 또는 지속 분류", filters, font, 0, .49f);
        TMP_Dropdown typeFilter = Dropdown("유형 또는 효과 분류", filters, font, .51f, 1);
        RectTransform catalog = Scroll("도감 스크롤", right, .015f, .15f, .985f, .827f);
        RectTransform detailsContent = Scroll("선택 항목 설명", right, .025f, .13f, .975f, .425f);
        TMP_Text details = Label("선택 설명", detailsContent, font, 21, 0, 0, 1, 1);
        details.margin = new Vector4(10, 8, 10, 8);
        var detailSize = details.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        detailSize.minHeight = 80;
        details.text = "도감에서 항목을 선택하면 설명을 볼 수 있습니다.";

        var forms = new GameObject[4];
        RectTransform bulletForm = Rect("탄환 추가 설정", right, .025f, .015f, .975f, .115f);
        forms[0] = bulletForm.gameObject;
        TMP_InputField level = Field("추가 강화", bulletForm, font, "0", 0, 0, .20f, 1);
        UnityEngine.UI.Button addSelected = Button("선택 탄환 추가", bulletForm, font, .25f, .08f, 1, .73f);
        RectTransform relicForm = Rect("유물 안내", right, .025f, .015f, .975f, .115f);
        forms[1] = relicForm.gameObject;
        Label("유물 조작 안내", relicForm, font, 18, 0, 0, 1, 1).text = "왼쪽으로 끌어 추가 · 더블클릭으로 바로 획득\n올려두면 설명 · 우클릭하면 조작 메뉴";

        // Enemy controls use a taller form; the view adjusts only these two scroll areas on that tab.
        RectTransform enemyForm = Rect("적 조절", right, .025f, .015f, .975f, .325f);
        forms[2] = enemyForm.gameObject;
        TMP_InputField tile = Field("칸 번호", enemyForm, font, "0", 0, .74f, .23f, 1);
        TMP_InputField lane = Field("레인 번호", enemyForm, font, "0", .25f, .74f, .48f, 1);
        TMP_InputField randomCount = Field("무작위 수", enemyForm, font, "1", .5f, .74f, .70f, 1);
        CommandButton("무작위 생성", enemyForm, font, console, controller, "random {0}", .72f, .76f, 1, .94f, false, randomCount);
        TMP_InputField enemyValue = Field("변경 수치", enemyForm, font, "10", 0, .49f, .23f, .74f);
        CommandButton("체력 적용", enemyForm, font, console, controller, "enemy hp {0} {1} {2}", .25f, .51f, .48f, .69f, false, tile, lane, enemyValue);
        CommandButton("보호막 적용", enemyForm, font, console, controller, "enemy shield {0} {1} {2}", .5f, .51f, .74f, .69f, false, tile, lane, enemyValue);
        CommandButton("피해 주기", enemyForm, font, console, controller, "enemy damage {0} {1} {2}", .76f, .51f, 1, .69f, false, tile, lane, enemyValue);
        UnityEngine.UI.Button statusType = Button("독 전환", enemyForm, font, 0, .27f, .23f, .45f);
        TMP_InputField stacks = Field("중첩 수", enemyForm, font, "1", .25f, .24f, .48f, .49f);
        UnityEngine.UI.Button applyStatus = Button("상태이상 적용", enemyForm, font, .5f, .27f, .78f, .45f);
        CommandButton("모두 정리", enemyForm, font, console, controller, "clear", .80f, .27f, 1, .45f);
        Label("좌표 안내", enemyForm, font, 16, 0, 0, 1, .22f).text = "전투판에 끌어 배치 · 칸 클릭으로 대상 선택\n숫자에 휠 ±1 / Shift ±10 / Ctrl ±100 · 중첩 0은 해제";

        RectTransform itemForm = Rect("아이템 설정", right, .025f, .015f, .975f, .115f);
        forms[3] = itemForm.gameObject;
        TMP_InputField slot = Field("넣을 슬롯", itemForm, font, "0", 0, 0, .22f, 1);
        CommandButton("모든 슬롯 무작위", itemForm, font, console, controller, "item random", .25f, .08f, .66f, .73f);
        Label("슬롯 안내", itemForm, font, 18, .69f, .05f, 1, .9f).text = "슬롯 번호: 0~2\n기존 아이템을 교체합니다.";

        RectTransform settings = Box("플레이어와 보드", panel, .12f, .13f, .88f, .78f, Surface);
        CreateSettings(settings, font, console, controller);
        RectTransform commands = Box("명령 기록", panel, .025f, .105f, .975f, .78f, Surface);
        TMP_Text output = Label("기록", commands, font, 21, .02f, .17f, .98f, .98f);
        output.richText = false;
        output.textWrappingMode = TextWrappingModes.NoWrap;
        output.overflowMode = TextOverflowModes.Truncate;
        Label("명령 안내", commands, font, 18, .02f, .09f, .78f, .15f).text = "Enter 실행 · 위/아래 입력 기록 · Page Up/Down 출력 기록 · 닫기: 전투 재개";
        CommandButton("도움말 보기", commands, font, console, controller, "help", .80f, .09f, .98f, .15f);
        TMP_InputField input = Input("명령 입력", commands, font, "도움말 또는 명령을 입력하세요", "", .02f, .01f, .98f, .085f, false);
        Box("결과 배경", panel, 0, 0, 1, .08f, new Color(.025f, .04f, .055f, .97f));
        TMP_Text result = Label("조작 결과", panel, font, 20, .02f, .012f, .98f, .07f);

        RectTransform templates = Rect("행 템플릿", parent, 0, 0, 1, 1);
        templates.gameObject.SetActive(false);
        BattleTestListRow row = CreateRow(templates, font);
        var guiData = new SerializedObject(gui);
        Set(guiData, "controller", controller); Set(guiData, "console", console);
        Set(guiData, "listsPanel", body.gameObject); Set(guiData, "settingsPanel", settings.gameObject);
        Set(guiData, "leftTitle", leftTitle); Set(guiData, "rightTitle", rightTitle);
        Set(guiData, "details", details); Set(guiData, "search", search);
        Set(guiData, "ownedContent", owned); Set(guiData, "catalogContent", catalog);
        Set(guiData, "rowTemplate", row); Set(guiData, "bulletLevel", level); Set(guiData, "addSelected", addSelected);
        Set(guiData, "enemyTile", tile); Set(guiData, "enemyLane", lane); Set(guiData, "enemyValue", enemyValue);
        Set(guiData, "statusStacks", stacks); Set(guiData, "statusType", statusType); Set(guiData, "applyStatus", applyStatus);
        Set(guiData, "statusLabel", statusType.GetComponentInChildren<TMP_Text>()); Set(guiData, "itemSlot", slot);
        Set(guiData, "interactions", interactions);
        Set(guiData, "filterPanel", filters.gameObject); Set(guiData, "gradeFilter", gradeFilter); Set(guiData, "typeFilter", typeFilter);
        CreateEnemyRefillControls(left, font, console, controller, guiData);
        Array(guiData, "forms", forms); Array(guiData, "tabs", tabs);
        guiData.ApplyModifiedPropertiesWithoutUndo();
        var consoleData = new SerializedObject(console);
        Set(consoleData, "controller", controller); Set(consoleData, "panel", panel.gameObject);
        Set(consoleData, "input", input); Set(consoleData, "output", output); Set(consoleData, "summary", summary);
        Set(consoleData, "hint", hint); Set(consoleData, "resultText", result); Set(consoleData, "commandPanel", commands.gameObject);
        Set(consoleData, "gui", gui);
        consoleData.ApplyModifiedPropertiesWithoutUndo();
        settings.gameObject.SetActive(false);
        commands.gameObject.SetActive(false);
        for (int i = 1; i < forms.Length; i++) forms[i].SetActive(false);
        WindowHandle(leftTitle, left, font);
        WindowHandle(rightTitle, right, font);
        BattleTestBulletAuthoring authoring = CreateAuthoring(panel, font, controller, gui);
        guiData.Update(); Set(guiData, "authoring", authoring); guiData.ApplyModifiedPropertiesWithoutUndo();
        CreatePointerViews(canvasRect, font, interactions, controller, console, gui);
        AddControlTooltips(panel, interactions);
        foreach (BattleTestWindowHandle handle in panel.GetComponentsInChildren<BattleTestWindowHandle>(true))
            handle.gameObject.AddComponent<BattleTestControlTooltip>().Configure(interactions,
                "제목을 끌어 패널을 이동합니다. 제목을 두 번 클릭하면 원래 위치로 돌아갑니다.");
        trash.gameObject.AddComponent<BattleTestControlTooltip>().Configure(interactions,
            "보유 탄환·유물·아이템·적을 이곳에 끌어 놓으면 제거합니다. 덱의 마지막 한 발은 유지됩니다.");
    }

    internal static void InstallEnemyRefillControls(BattleTestGui gui)
    {
        var data = new SerializedObject(gui);
        var interactions = (BattleTestInteractions)data.FindProperty("interactions").objectReferenceValue;
        foreach (BattleTestControlTooltip tooltip in gui.GetComponentsInChildren<BattleTestControlTooltip>(true))
        {
            if (tooltip.name == "모두 정리") tooltip.Configure(interactions, BattleTestHelpText.For("모두 정리"));
        }
        if (data.FindProperty("enemyRefillPanel").objectReferenceValue != null) return;
        var title = (TMP_Text)data.FindProperty("leftTitle").objectReferenceValue;
        var handle = new SerializedObject(title.GetComponent<BattleTestWindowHandle>());
        var content = (GameObject)handle.FindProperty("content").objectReferenceValue;
        RectTransform panel = CreateEnemyRefillControls(content.transform, title.font,
            (BattleTestConsole)data.FindProperty("console").objectReferenceValue,
            (BattleTestController)data.FindProperty("controller").objectReferenceValue, data);
        AddControlTooltips(panel, interactions);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform CreateEnemyRefillControls(Transform parent, TMP_FontAsset font,
        BattleTestConsole console, BattleTestController controller, SerializedObject guiData)
    {
        RectTransform panel = Rect("전멸 자동 보충 설정", parent, .025f, .165f, .975f, .33f);
        UnityEngine.UI.Button toggle = CommandButton("전멸 시 자동 보충", panel, font, console, controller,
            "refill toggle", 0, .44f, .53f, .90f);
        TMP_InputField percent = Field("점유율 (%)", panel, font, "50", .56f, .38f, .77f, 1);
        CommandButton("점유율 적용", panel, font, console, controller,
            "refill percent {0}", .80f, .44f, 1, .90f, false, percent);
        TMP_Text summary = Label("자동 보충 목표", panel, font, 17, 0, 0, 1, .33f);
        summary.color = new Color(.62f, .91f, .80f);
        Set(guiData, "enemyRefillPanel", panel.gameObject); Set(guiData, "enemyRefillPercent", percent);
        Set(guiData, "enemyRefillLabel", toggle.GetComponentInChildren<TMP_Text>()); Set(guiData, "enemyRefillSummary", summary);
        panel.gameObject.SetActive(false);
        return panel;
    }

    private static void AddControlTooltips(Transform parent, BattleTestInteractions interactions)
    {
        foreach (UnityEngine.UI.Selectable control in parent.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
        {
            if (control is UnityEngine.UI.Toggle) continue;
            var help = control.gameObject.AddComponent<BattleTestControlTooltip>();
            help.Configure(interactions, BattleTestHelpText.For(control.name)
                + (control.GetComponent<BattleTestNumberField>() != null ? "\n입력란 위에서 휠 ±1 / Shift ±10 / Ctrl ±100" : ""));
        }
    }

    private static BattleTestBulletAuthoring CreateAuthoring(Transform parent, TMP_FontAsset font,
        BattleTestController controller, BattleTestGui gui)
    {
        RectTransform panel = Box("탄환 원본 편집", parent, .31f, .115f, .69f, .515f, Surface);
        TMP_Text title = Label("편집 제목", panel, font, 24, .025f, .915f, .975f, .995f);
        Label("단계 안내", panel, font, 18, .035f, .80f, .34f, .90f).text = "편집할 강화 단계";
        RectTransform levelRect = Rect("편집 단계 영역", panel, .36f, .80f, .965f, .895f);
        TMP_Dropdown level = Dropdown("편집 강화 단계", levelRect, font, 0, 1);
        level.ClearOptions(); level.AddOptions(new System.Collections.Generic.List<string> { "기본 (0)", "강화 +1", "강화 +2", "강화 +3" });
        TMP_InputField damage = Field("대미지", panel, font, "0", .035f, .57f, .48f, .78f);
        TMP_InputField range = Field("사거리 (칸)", panel, font, "1", .52f, .57f, .965f, .78f);
        TMP_InputField chance = DecimalField("치명타 확률 (%)", panel, font, .035f, .48f);
        TMP_InputField multiplier = DecimalField("치명타 대미지 (배율)", panel, font, .52f, .965f);
        UnityEngine.UI.Button save = Button("원본에 저장", panel, font, .035f, .20f, .48f, .31f);
        save.GetComponent<UnityEngine.UI.Image>().color = new Color(.10f, .40f, .31f);
        UnityEngine.UI.Button revert = Button("입력 되돌리기", panel, font, .50f, .20f, .78f, .31f);
        UnityEngine.UI.Button close = Button("편집 닫기", panel, font, .80f, .20f, .965f, .31f);
        TMP_Text status = Label("편집 결과", panel, font, 18, .035f, .02f, .965f, .18f);
        status.color = new Color(.68f, .90f, .80f);
        var authoring = parent.gameObject.AddComponent<BattleTestBulletAuthoring>();
        var data = new SerializedObject(authoring);
        Set(data, "controller", controller); Set(data, "gui", gui); Set(data, "panel", panel.gameObject);
        Set(data, "title", title); Set(data, "status", status); Set(data, "level", level);
        Set(data, "damage", damage); Set(data, "range", range); Set(data, "chance", chance); Set(data, "multiplier", multiplier);
        Set(data, "save", save); Set(data, "revert", revert); Set(data, "close", close);
        data.ApplyModifiedPropertiesWithoutUndo();
        WindowHandle(title, panel, font);
        panel.gameObject.SetActive(false);
        return authoring;
    }

    private static TMP_InputField DecimalField(string name, Transform parent, TMP_FontAsset font, float x0, float x1)
    {
        RectTransform root = Rect(name + " 입력", parent, x0, .34f, x1, .55f);
        Label(name + " 안내", root, font, 18, 0, .66f, 1, 1).text = name;
        TMP_InputField input = Input(name, root, font, "소수 가능", "1", 0, .05f, 1, .63f, false);
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        return input;
    }

    private static void CreateSettings(RectTransform parent, TMP_FontAsset font, BattleTestConsole console, BattleTestController controller)
    {
        Label("체력 제목", parent, font, 28, .025f, .88f, .45f, .96f).text = "플레이어 · 재화";
        TMP_InputField hp = Field("현재 체력", parent, font, "100", .025f, .68f, .17f, .85f);
        TMP_InputField max = Field("최대 체력", parent, font, "100", .19f, .68f, .335f, .85f);
        CommandButton("체력 적용", parent, font, console, controller, "hp {0} {1}", .355f, .70f, .47f, .79f, false, hp, max);
        TMP_InputField gold = Field("골드", parent, font, "1000", .025f, .45f, .20f, .62f);
        CommandButton("골드 적용", parent, font, console, controller, "gold {0}", .22f, .47f, .335f, .56f, false, gold);
        CommandButton("골드 무작위", parent, font, console, controller, "gold random", .355f, .47f, .47f, .56f);
        TMP_InputField playerTile = Field("이동할 칸", parent, font, "0", .025f, .22f, .17f, .39f);
        TMP_InputField playerLane = Field("이동할 레인", parent, font, "0", .19f, .22f, .335f, .39f);
        CommandButton("위치 이동", parent, font, console, controller, "player {0} {1}", .355f, .24f, .47f, .33f, false, playerTile, playerLane);
        Label("보드 제목", parent, font, 28, .53f, .88f, .975f, .96f).text = "보드 · 반복 테스트";
        TMP_InputField columns = Field("레인당 칸 수 (2~30)", parent, font, "7", .53f, .68f, .69f, .85f);
        TMP_InputField lanes = Field("레인 수 (1~6)", parent, font, "2", .71f, .68f, .84f, .85f);
        CommandButton("보드 재생성", parent, font, console, controller, "board {0} {1}", .86f, .70f, .975f, .79f, false, columns, lanes);
        Label("보드 주의", parent, font, 19, .53f, .50f, .975f, .65f).text = "보드를 다시 만들면 적·폭탄·드롭을 지우고 플레이어를 중앙으로 옮깁니다. 덱과 소지품은 유지됩니다.";
        TMP_InputField seed = Field("무작위 수 초기값", parent, font, "1234", .53f, .24f, .76f, .41f);
        CommandButton("초기값 적용", parent, font, console, controller, "seed {0}", .78f, .26f, .975f, .35f, false, seed);
        Label("저장 안내", parent, font, 20, .025f, .025f, .975f, .17f).text = "상단의 상태 저장 → 전투 시험 → 상태 복원으로 같은 구성을 반복할 수 있습니다.\n테스트 구성은 재생 종료 시 사라집니다. ‘원본에 저장’한 탄환 수치는 유지됩니다.";
    }

    private static BattleTestListRow CreateRow(Transform parent, TMP_FontAsset font)
    {
        RectTransform rect = Box("목록 행", parent, 0, 0, 1, 1, new Color(.11f, .15f, .20f));
        rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 90;
        var icon = Rect("아이콘", rect, .015f, .20f, .14f, .88f).gameObject.AddComponent<UnityEngine.UI.Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        TMP_Text title = Label("이름", rect, font, 22, .16f, .55f, .98f, .94f);
        TMP_Text subtitle = Label("상태", rect, font, 16, .16f, .28f, .98f, .56f);
        title.enableAutoSizing = true; title.fontSizeMin = 17; title.fontSizeMax = 22;
        title.overflowMode = TextOverflowModes.Ellipsis;
        subtitle.color = new Color(.65f, .75f, .84f);
        var buttons = new UnityEngine.UI.Button[4];
        var labels = new TMP_Text[4];
        RectTransform actionBar = Rect("빠른 조작", rect, .16f, .015f, .99f, .32f);
        actionBar.gameObject.AddComponent<CanvasGroup>();
        for (int i = 0; i < 4; i++)
        {
            buttons[i] = Button("조작 " + i, actionBar, font, i * .25f, 0, i * .25f + .24f, 1);
            labels[i] = buttons[i].GetComponentInChildren<TMP_Text>();
            labels[i].fontSize = 18;
        }
        var row = rect.gameObject.AddComponent<BattleTestListRow>();
        var data = new SerializedObject(row);
        Set(data, "icon", icon); Set(data, "title", title); Set(data, "subtitle", subtitle);
        Array(data, "actions", buttons); Array(data, "actionLabels", labels);
        data.ApplyModifiedPropertiesWithoutUndo();
        rect.gameObject.SetActive(false);
        return row;
    }

    private static RectTransform Scroll(string name, Transform parent, float x0, float y0, float x1, float y1)
    {
        RectTransform root = Rect(name, parent, x0, y0, x1, y1);
        var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        RectTransform viewport = Box("표시 영역", root, 0, 0, 1, 1, new Color(.045f, .065f, .09f));
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
        RectTransform content = Rect("내용", viewport, 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        return content;
    }

    private static TMP_InputField Field(string label, Transform parent, TMP_FontAsset font, string value, float x0, float y0, float x1, float y1)
    {
        RectTransform root = Rect(label + " 입력", parent, x0, y0, x1, y1);
        Label(label + " 안내", root, font, 18, 0, .66f, 1, 1).text = label;
        return Input(label, root, font, "정수", value, 0, .05f, 1, .63f, true);
    }

    private static TMP_Dropdown Dropdown(string name, Transform parent, TMP_FontAsset font, float x0, float x1)
    {
        RectTransform root = Box(name, parent, x0, 0, x1, 1, ButtonColor);
        var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
        dropdown.captionText = Label("현재 분류", root, font, 19, .04f, .04f, .87f, .96f);
        dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        Label("분류 열기", root, font, 18, .88f, .05f, .98f, .95f).text = "+";
        RectTransform template = Box("분류 선택 목록", root, 0, 0, 1, 0, Surface);
        template.pivot = new Vector2(.5f, 1); template.sizeDelta = new Vector2(0, 264);
        var scroll = template.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        RectTransform viewport = Box("분류 표시 영역", template, 0, 0, 1, 1, Surface);
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
        RectTransform content = Rect("분류 내용", viewport, 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 36);
        RectTransform item = Box("분류 항목", content, 0, .5f, 1, .5f, ButtonColor);
        item.sizeDelta = new Vector2(0, 36);
        var toggle = item.gameObject.AddComponent<UnityEngine.UI.Toggle>();
        toggle.targetGraphic = item.GetComponent<UnityEngine.UI.Image>();
        RectTransform selected = Box("선택 표시", item, .015f, .2f, .04f, .8f, new Color(.4f, .95f, .72f));
        selected.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        toggle.graphic = selected.GetComponent<UnityEngine.UI.Image>();
        TMP_Text itemText = Label("분류 이름", item, font, 19, .07f, .05f, .98f, .95f);
        itemText.alignment = TextAlignmentOptions.MidlineLeft;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
        dropdown.template = template; dropdown.itemText = itemText;
        dropdown.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("전체") };
        dropdown.RefreshShownValue(); template.gameObject.SetActive(false);
        return dropdown;
    }

    private static TMP_InputField Input(string name, Transform parent, TMP_FontAsset font, string placeholder, string value,
        float x0, float y0, float x1, float y1, bool numeric)
    {
        RectTransform rect = Box(name, parent, x0, y0, x1, y1, new Color(.17f, .22f, .29f));
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("입력 영역", rect, 0, 0, 1, 1);
        viewport.offsetMin = new Vector2(10, 2); viewport.offsetMax = new Vector2(-10, -2);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        TMP_Text text = Label("입력값", viewport, font, 23, 0, 0, 1, 1);
        text.alignment = TextAlignmentOptions.MidlineLeft; text.richText = false;
        TMP_Text hint = Label("자리 표시", viewport, font, 20, 0, 0, 1, 1);
        hint.text = placeholder; hint.color = new Color(.65f, .72f, .8f); hint.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport; input.textComponent = text; input.placeholder = hint;
        input.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = numeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
        input.characterLimit = numeric ? 12 : 256;
        input.text = value;
        if (numeric) rect.gameObject.AddComponent<BattleTestNumberField>();
        return input;
    }

    private static UnityEngine.UI.Button CommandButton(string name, Transform parent, TMP_FontAsset font,
        BattleTestConsole console, BattleTestController controller, string command,
        float x0, float y0, float x1, float y1, bool close = false, params TMP_InputField[] args)
    {
        UnityEngine.UI.Button button = Button(name, parent, font, x0, y0, x1, y1);
        var binding = button.gameObject.AddComponent<BattleTestCommandButton>();
        var data = new SerializedObject(binding);
        Set(data, "console", console); Set(data, "controller", controller); Set(data, "button", button);
        data.FindProperty("command").stringValue = command;
        data.FindProperty("closeAfterSuccess").boolValue = close;
        Array(data, "arguments", args);
        data.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font,
        float x0, float y0, float x1, float y1)
    {
        RectTransform rect = Box(name, parent, x0, y0, x1, y1, ButtonColor);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        TMP_Text label = Label("버튼 글자", rect, font, 21, .02f, .02f, .98f, .98f);
        label.text = name; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 15; label.fontSizeMax = 21;
        return button;
    }
    private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool raycast = true)
    {
        RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color; image.raycastTarget = raycast;
        return rect;
    }
    private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, int size, float x0, float y0, float x1, float y1)
    {
        var text = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = new Color(.91f, .94f, .97f); text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
    private static void Set(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
    private static void WindowHandle(TMP_Text title, RectTransform window, TMP_FontAsset font)
    {
        RectTransform content = Rect("접을 내용", window, 0, 0, 1, 1);
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in window) if (child != content && child != title.transform) children.Add(child);
        foreach (Transform child in children) child.SetParent(content, false);
        RectTransform header = Box("패널 제목 막대", window, 0, .915f, 1, 1, new Color(.10f, .15f, .21f));
        title.transform.SetParent(header, false);
        title.rectTransform.anchorMin = new Vector2(.025f, .1f);
        title.rectTransform.anchorMax = new Vector2(.80f, .9f);
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
        title.enableAutoSizing = true; title.fontSizeMin = 17; title.fontSizeMax = 23;
        UnityEngine.UI.Button collapse = Button("접기", header, font, .82f, .12f, .98f, .88f);
        title.raycastTarget = true;
        var handle = title.gameObject.AddComponent<BattleTestWindowHandle>();
        var data = new SerializedObject(handle);
        Set(data, "window", window); Set(data, "content", content.gameObject);
        Set(data, "collapseButton", collapse); Set(data, "collapseLabel", collapse.GetComponentInChildren<TMP_Text>());
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void DropTarget(RectTransform rect, BattleTestInteractions interactions, BattleTestDropKind kind)
    {
        var target = rect.gameObject.AddComponent<BattleTestDropTarget>();
        var data = new SerializedObject(target);
        Set(data, "interactions", interactions); data.FindProperty("kind").enumValueIndex = (int)kind;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static RectTransform Popup(string name, RectTransform canvas, Vector2 size, Color color, bool raycast)
    {
        RectTransform rect = Box(name, canvas, .5f, .5f, .5f, .5f, color, raycast);
        rect.pivot = new Vector2(0, 1); rect.sizeDelta = size;
        rect.gameObject.SetActive(false);
        return rect;
    }
    private static void CreatePointerViews(RectTransform canvas, TMP_FontAsset font, BattleTestInteractions interactions,
        BattleTestController controller, BattleTestConsole console, BattleTestGui gui)
    {
        RectTransform tip = Popup("마우스 설명", canvas, new Vector2(440, 300), new Color(.025f, .035f, .055f, .99f), false);
        TMP_Text tipText = Label("설명", tip, font, 21, 0, 0, 1, 1);
        tipText.margin = new Vector4(14, 14, 14, 14);
        RectTransform ghost = Popup("드래그 미리보기", canvas, new Vector2(280, 76), ButtonColor, false);
        var icon = Rect("드래그 아이콘", ghost, .025f, .12f, .22f, .88f).gameObject.AddComponent<UnityEngine.UI.Image>();
        icon.raycastTarget = false; icon.preserveAspect = true;
        TMP_Text ghostText = Label("드래그 안내", ghost, font, 18, .25f, .08f, .98f, .92f);
        RectTransform marker = Popup("배치 칸 표시", canvas, new Vector2(140, 58), new Color(.08f, .32f, .25f, .9f), false);
        TMP_Text markerText = Label("배치 안내", marker, font, 18, .05f, .05f, .95f, .95f);
        RectTransform menu = Popup("우클릭 메뉴", canvas, new Vector2(220, 180), Surface, true);
        var layout = menu.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6); layout.childControlHeight = true;
        layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.spacing = 2;
        var buttons = new UnityEngine.UI.Button[4]; var labels = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            buttons[i] = Button("메뉴 " + i, menu, font, 0, 0, 1, 1);
            buttons[i].gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40;
            labels[i] = buttons[i].GetComponentInChildren<TMP_Text>();
        }
        var data = new SerializedObject(interactions);
        Set(data, "controller", controller); Set(data, "console", console); Set(data, "gui", gui);
        Set(data, "canvasRect", canvas); Set(data, "tooltip", tip); Set(data, "tooltipText", tipText);
        Set(data, "ghost", ghost); Set(data, "ghostIcon", icon); Set(data, "ghostText", ghostText);
        Set(data, "marker", marker); Set(data, "markerText", markerText); Set(data, "menu", menu);
        var owner = new SerializedObject(controller);
        var world = new SerializedObject(owner.FindProperty("world").objectReferenceValue);
        var board = new SerializedObject(owner.FindProperty("board").objectReferenceValue);
        Set(data, "worldCamera", world.FindProperty("battleCamera").objectReferenceValue);
        Set(data, "boardPlane", board.FindProperty("tileParent").objectReferenceValue);
        Array(data, "menuButtons", buttons); Array(data, "menuLabels", labels);
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Array(SerializedObject data, string field, UnityEngine.Object[] values)
    {
        SerializedProperty array = data.FindProperty(field); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
