using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public sealed class BattleTestGui : MonoBehaviour
{
    [SerializeField] private BattleTestController controller;
    [SerializeField] private BattleTestConsole console;
    [SerializeField] private GameObject listsPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject[] forms;
    [SerializeField] private UnityEngine.UI.Button[] tabs;
    [SerializeField] private TMP_Text leftTitle;
    [SerializeField] private TMP_Text rightTitle;
    [SerializeField] private TMP_Text details;
    [SerializeField] private TMP_InputField search;
    [SerializeField] private RectTransform ownedContent;
    [SerializeField] private RectTransform catalogContent;
    [SerializeField] private BattleTestListRow rowTemplate;
    [SerializeField] private TMP_InputField bulletLevel;
    [SerializeField] private UnityEngine.UI.Button addSelected;
    [SerializeField] private TMP_InputField enemyTile;
    [SerializeField] private TMP_InputField enemyLane;
    [SerializeField] private TMP_InputField enemyValue;
    [SerializeField] private TMP_InputField statusStacks;
    [SerializeField] private UnityEngine.UI.Button statusType;
    [SerializeField] private UnityEngine.UI.Button applyStatus;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_InputField itemSlot;
    [SerializeField] private BattleTestInteractions interactions;
    [SerializeField] private GameObject filterPanel;
    [SerializeField] private TMP_Dropdown gradeFilter;
    [SerializeField] private TMP_Dropdown typeFilter;
    [SerializeField] private BattleTestBulletAuthoring authoring;
    [SerializeField] private GameObject enemyRefillPanel;
    [SerializeField] private TMP_InputField enemyRefillPercent;
    [SerializeField] private TMP_Text enemyRefillLabel;
    [SerializeField] private TMP_Text enemyRefillSummary;
    [SerializeField] private UnityEngine.UI.Slider enemyRefillLimit;
    [SerializeField] private TMP_Text enemyRefillLimitLabel;
    private int displayedRefillPercent = -1;
    private int displayedRefillLimit = -1;
    private readonly int[] gradeSelections = new int[2];
    private readonly int[] typeSelections = new int[2];
    internal string AddedBulletLevel => bulletLevel.text;

    private readonly List<BattleTestListRow> ownedRows = new List<BattleTestListRow>();
    private readonly List<BattleTestListRow> catalogRows = new List<BattleTestListRow>();
    private readonly List<UnityAction> tabActions = new List<UnityAction>();
    private readonly int[] selectedCatalog = new int[4];
    private int page;
    private EnemyController inspectedEnemy;
    private StatusEffectType selectedStatus = StatusEffectType.Poison;
    private bool dirty = true;
    private bool resetScroll;
    private bool initialized;
    private float nextRefresh;

    private void OnEnable()
    {
        if (console == null) return;
        console.CommandExecuted += HandleCommand;
        controller.TestWaves.StateChanged += HandleEnemyStateChanged;
        search.onValueChanged.AddListener(HandleSearch);
        bulletLevel.onValueChanged.AddListener(HandleSearch);
        itemSlot.onValueChanged.AddListener(HandleSearch);
        gradeFilter.onValueChanged.AddListener(HandleFilter);
        typeFilter.onValueChanged.AddListener(HandleFilter);
        addSelected.onClick.AddListener(AddSelectedBullet);
        statusType.onClick.AddListener(CycleStatus);
        applyStatus.onClick.AddListener(ApplyStatus);
        enemyRefillLimit.onValueChanged.AddListener(HandleEnemyRefillLimitChanged);
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            UnityAction action = () => SelectPage(index);
            tabActions.Add(action);
            tabs[i].onClick.AddListener(action);
        }
    }

    private void OnDisable()
    {
        if (console == null) return;
        console.CommandExecuted -= HandleCommand;
        if (controller.TestWaves != null) controller.TestWaves.StateChanged -= HandleEnemyStateChanged;
        search.onValueChanged.RemoveListener(HandleSearch);
        bulletLevel.onValueChanged.RemoveListener(HandleSearch);
        itemSlot.onValueChanged.RemoveListener(HandleSearch);
        gradeFilter.onValueChanged.RemoveListener(HandleFilter);
        typeFilter.onValueChanged.RemoveListener(HandleFilter);
        addSelected.onClick.RemoveListener(AddSelectedBullet);
        statusType.onClick.RemoveListener(CycleStatus);
        applyStatus.onClick.RemoveListener(ApplyStatus);
        enemyRefillLimit.onValueChanged.RemoveListener(HandleEnemyRefillLimitChanged);
        for (int i = 0; i < tabActions.Count; i++) tabs[i].onClick.RemoveListener(tabActions[i]);
        tabActions.Clear();
    }

    private void Update()
    {
        if (!controller.IsReady) return;
        enemyRefillLabel.text = "전멸 시 자동 보충: " + (controller.EnemyAutoRefill ? "켜짐" : "꺼짐");
        enemyRefillSummary.text = controller.EnemyRefillSummary;
        enemyRefillLimitLabel.text = controller.EnemyRefillLimit == 0
            ? "최대 횟수: 무한"
            : $"최대 횟수: {controller.EnemyRefillLimit}회 · 남음 {controller.EnemyRefillsRemaining}회";
        if (displayedRefillPercent != controller.EnemyRefillPercent)
        {
            displayedRefillPercent = controller.EnemyRefillPercent;
            enemyRefillPercent.SetTextWithoutNotify(displayedRefillPercent.ToString());
        }
        if (displayedRefillLimit != controller.EnemyRefillLimit)
        {
            displayedRefillLimit = controller.EnemyRefillLimit;
            enemyRefillLimit.SetValueWithoutNotify(displayedRefillLimit);
        }
        if (!initialized) { initialized = true; SelectPage(0); }
        if (console.IsOpen && dirty && !interactions.IsDragging && Time.unscaledTime >= nextRefresh)
        {
            dirty = false;
            RefreshLists();
        }
    }

    internal void RequestRefresh() => dirty = true;
    private void HandleCommand(string _) { dirty = true; nextRefresh = 0; }
    private void HandleEnemyStateChanged() { if (page == 2) { dirty = true; nextRefresh = 0; } }
    private void HandleSearch(string _) { dirty = true; resetScroll = true; nextRefresh = Time.unscaledTime + 0.15f; }
    private void HandleFilter(int _)
    {
        if (page > 1) return;
        gradeSelections[page] = gradeFilter.value; typeSelections[page] = typeFilter.value;
        interactions.Cancel(); dirty = resetScroll = true; nextRefresh = 0;
    }

    private void HandleEnemyRefillLimitChanged(float value) =>
        Run($"refill limit {Mathf.RoundToInt(value)}");

    public void SelectPage(int index)
    {
        interactions.Cancel();
        authoring.Hide();
        page = Mathf.Clamp(index, 0, 5);
        enemyRefillPanel.SetActive(page == 2);
        RectTransform ownedRect = ownedContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).GetComponent<RectTransform>();
        ownedRect.anchorMin = new Vector2(.015f, page == 2 ? .35f : .16f);
        filterPanel.SetActive(page < 2);
        if (page < 2)
        {
            gradeFilter.ClearOptions(); typeFilter.ClearOptions();
            gradeFilter.AddOptions(new List<string>(page == 0
                ? new[] { "등급: 전체", "일반", "희귀", "에이스", "전설" }
                : new[] { "지속: 전체", "지속형", "소모형" }));
            typeFilter.AddOptions(page == 0
                ? new[] { "유형: 전체" }.Concat(Enum.GetValues(typeof(BulletType)).Cast<BulletType>().Select(BulletData.GetBulletTypeDisplayName)).ToList()
                : new List<string>(BattleTestCatalogFilters.RelicCategories));
            gradeFilter.SetValueWithoutNotify(gradeSelections[page]);
            typeFilter.SetValueWithoutNotify(typeSelections[page]);
            gradeFilter.RefreshShownValue(); typeFilter.RefreshShownValue();
        }
        console.ShowCommands(page == 5);
        listsPanel.SetActive(page < 4);
        settingsPanel.SetActive(page == 4);
        for (int i = 0; i < forms.Length; i++) forms[i].SetActive(page == i);
        RectTransform catalogRect = catalogContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).GetComponent<RectTransform>();
        catalogRect.anchorMin = new Vector2(.015f, page == 2 ? .57f : .15f);
        catalogRect.anchorMax = new Vector2(.985f, page < 2 ? .755f : .827f);
        RectTransform detailRect = details.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(.025f, page == 2 ? .34f : .13f);
        detailRect.anchorMax = new Vector2(.975f, page == 2 ? .555f : .425f);
        detailRect.gameObject.SetActive(page == 2);
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].GetComponent<UnityEngine.UI.Image>().color = i == page
                ? new Color(0.14f, 0.43f, 0.37f) : new Color(0.10f, 0.15f, 0.21f);
        search.SetTextWithoutNotify(string.Empty);
        statusLabel.text = BattleTestCommandRouter.StatusName(selectedStatus) + " 전환";
        dirty = true;
        resetScroll = true;
        nextRefresh = 0;
    }

    private void RefreshLists()
    {
        if (page >= 4) return;
        Clear(ownedRows);
        Clear(catalogRows);
        switch (page)
        {
            case 0: BuildBullets(); break;
            case 1: BuildRelics(); break;
            case 2: BuildEnemies(); break;
            case 3: BuildItems(); break;
        }
        if (ownedRows.Count == 0) Row(ownedContent, ownedRows, "목록이 비어 있습니다", "오른쪽 도감에서 추가해 주세요.");
        if (catalogRows.Count == 0) Row(catalogContent, catalogRows, "검색 결과가 없습니다", "이름·유형·효과를 다른 단어로 검색해 주세요.");
        Canvas.ForceUpdateCanvases();
        if (resetScroll)
        {
            ownedContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).verticalNormalizedPosition = 1f;
            catalogContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).verticalNormalizedPosition = 1f;
            resetScroll = false;
        }
        RefreshDetails();
    }

    private void BuildBullets()
    {
        DeckManager deck = controller.TestDeck;
        leftTitle.text = $"내 덱  {deck.TotalBulletCount} / {DeckManager.MaximumOwnedBulletCount}발";
        rightTitle.text = "탄환 도감 · 끌어서 추가";
        var owned = new List<BulletInstance>();
        deck.GetOwnedBullets(owned);
        foreach (BulletInstance bullet in owned.OrderBy(value => value.AcquisitionOrder))
        {
            int id = bullet.AcquisitionOrder;
            bool loaded = deck.LoadedBullets.Contains(bullet);
            string location = loaded ? "장전됨" : deck.Graveyard.Contains(bullet) ? "사용 후 대기" : "덱";
            BattleTestListRow row = Row(ownedContent, ownedRows, bullet.Data.GetDisplayName(bullet.Level),
                $"#{id} · {location} · {bullet.Data.BulletTypeDisplayName}", bullet.Data.CylinderIcon, bullet.Data);
            row.Action(0, "− 강화", () => Run($"bullet level {id} {bullet.Level - 1}"), bullet.Level > 0);
            row.Action(1, "+ 강화", () => Run($"bullet level {id} {bullet.Level + 1}"), bullet.Level < BulletData.MaximumUpgradeLevel);
            row.Action(2, "수치 편집", () => EditBullet(bullet.Data, bullet.Level));
            row.Action(3, "제거", () => Run($"bullet remove {id}"), deck.CanRemoveOwnedBullet);
            row.ConfigureInteraction(interactions, BattleTestDragKind.Bullet, id, false, 2,
                () => bullet.Data.GetDetailedDescription(bullet.Level));
            row.OnSelect(() => EditBullet(bullet.Data, bullet.Level));
        }
        for (int i = 0; i < controller.Bullets.Count; i++)
        {
            int index = i;
            BulletData bullet = controller.Bullets[i];
            if (!BattleTestCatalogFilters.Matches(bullet, gradeFilter.value, typeFilter.value)) continue;
            if (!Matches(bullet.GetDisplayName(0), bullet.BulletTypeDisplayName, bullet.GetDescription(0))) continue;
            BattleTestListRow row = Row(catalogContent, catalogRows, bullet.GetDisplayName(0),
                $"{GradeName(bullet.Grade)} · {bullet.BulletTypeDisplayName}", bullet.CylinderIcon, bullet);
            row.Action(2, "수치 편집", () => EditBullet(bullet, 0));
            row.Action(3, "추가", () => AddBullet(index), deck.TotalBulletCount < DeckManager.MaximumOwnedBulletCount);
            row.ConfigureInteraction(interactions, BattleTestDragKind.BulletCatalog, index, true, 3,
                () => bullet.GetDetailedDescription(int.TryParse(bulletLevel.text, out int level) ? Mathf.Clamp(level, 0, 3) : 0));
            row.OnSelect(() => EditBullet(bullet, 0));
        }
    }

    private void EditBullet(BulletData bullet, int level)
    {
        interactions.Cancel();
        for (int i = 0; i < controller.Bullets.Count; i++)
            if (controller.Bullets[i] == bullet) { selectedCatalog[0] = i; break; }
        authoring.Show(bullet, level);
    }

    private void BuildRelics()
    {
        leftTitle.text = $"보유 유물  {controller.TestRelics.Count}개";
        rightTitle.text = "유물 도감";
        for (int i = 0; i < controller.TestRelics.Count; i++)
        {
            int index = i;
            RelicData data = controller.TestRelics.OwnedRelics[i].Data;
            BattleTestListRow row = Row(ownedContent, ownedRows, data.DisplayName, "보유 중", data.Icon);
            row.Action(2, "설명", () => interactions.Inspect(data.DisplayName + "\n" + data.Description));
            row.Action(3, "제거", () => Run($"relic remove {index}"));
            row.ConfigureInteraction(interactions, BattleTestDragKind.Relic, index, false, 2, () => data.Description);
        }
        for (int i = 0; i < controller.Relics.Count; i++)
        {
            int index = i;
            RelicData data = controller.Relics[i];
            if (!BattleTestCatalogFilters.Matches(data, gradeFilter.value, typeFilter.value)) continue;
            if (!Matches(data.DisplayName, data.Description)) continue;
            BattleTestListRow row = Row(catalogContent, catalogRows, data.DisplayName, BattleTestCatalogFilters.RelicLabel(data), data.Icon);
            row.Action(2, "설명", () => SelectCatalog(index));
            row.Action(3, "추가", () => Run($"relic add {index}"));
            row.ConfigureInteraction(interactions, BattleTestDragKind.RelicCatalog, index, true, 3, () => data.Description);
        }
    }

    private void BuildEnemies()
    {
        leftTitle.text = $"배치된 적  {controller.TestWaves.ActiveEnemies.Count}마리";
        rightTitle.text = "적 도감 · 전투판으로 끌기";
        foreach (EnemyController enemy in controller.TestWaves.ActiveEnemies)
        {
            RunEnemySaveData saved = enemy.CaptureRunState(controller.TestWaves.ActiveEnemies);
            int tile = saved.tileIndex, lane = saved.laneIndex;
            BattleTestListRow row = Row(ownedContent, ownedRows, enemy.Data.DisplayName,
                $"{tile}번 칸 / {lane}번 레인 · 체력 {enemy.CurrentHealth} · 보호막 {enemy.CurrentShield}", EnemyIcon(enemy.Data));
            row.Action(1, "조절", () => SelectEnemy(enemy, tile, lane));
            row.Action(2, "처치", () => Run($"enemy kill {tile} {lane}"));
            row.Action(3, "제거", () => Run($"enemy remove {tile} {lane}"));
            row.ConfigureInteraction(interactions, BattleTestDragKind.Enemy, enemy.GetInstanceID(), false, 1,
                () => $"체력 {enemy.CurrentHealth}/{enemy.MaxHealth} · 보호막 {enemy.CurrentShield}\n{enemy.Data.Description}\n조절: 더블클릭 또는 전투판에서 선택");
        }
        for (int i = 0; i < controller.Enemies.Count; i++)
        {
            int index = i;
            EnemyData data = controller.Enemies[i];
            if (!Matches(data.DisplayName, data.Description)) continue;
            bool allowed = controller.IsEnemySpawnAllowed(data);
            BattleTestListRow row = Row(catalogContent, catalogRows, data.DisplayName,
                $"최대 체력 {data.MaxHealth} · 자동 스폰 {(allowed ? "켜짐" : "꺼짐")}", EnemyIcon(data));
            row.Action(1, allowed ? "자동 켜짐" : "자동 꺼짐", () => Run($"spawnpool {index} {(allowed ? "off" : "on")}"));
            row.Action(2, "설명", () => SelectCatalog(index));
            row.Action(3, "생성", () => Run($"spawn {index} {enemyTile.text} {enemyLane.text}"));
            row.ConfigureInteraction(interactions, BattleTestDragKind.EnemyCatalog, index, true, 3,
                () => data.Description + "\n\n자동 스폰: " + (controller.IsEnemySpawnAllowed(data) ? "켜짐" : "꺼짐")
                    + "\n행의 자동 켜짐/꺼짐 버튼으로 자동 보충·무작위 생성 대상을 바꿉니다. 직접 배치는 항상 가능합니다.");
        }
    }

    private void SelectEnemy(EnemyController enemy, int tile, int lane)
    {
        if (enemy == null) { dirty = true; return; }
        inspectedEnemy = enemy;
        enemyTile.SetTextWithoutNotify(tile.ToString());
        enemyLane.SetTextWithoutNotify(lane.ToString());
        enemyValue.SetTextWithoutNotify(enemy.CurrentHealth.ToString());
        ShowEnemyDetails(enemy);
    }

    private void ShowEnemyDetails(EnemyController enemy)
    {
        RunEnemySaveData state = enemy.CaptureRunState(controller.TestWaves.ActiveEnemies);
        int tile = state.tileIndex, lane = state.laneIndex;
        ShowText($"{enemy.Data.DisplayName} · {tile}번 칸 / {lane}번 레인\n"
            + $"체력 {enemy.CurrentHealth}/{enemy.MaxHealth} · 보호막 {enemy.CurrentShield}\n"
            + $"표식 {state.statusEffects.markStacks} · 독 {state.statusEffects.poisonStacks} · 기절 {state.statusEffects.stunStacks}\n"
            + $"약화 {state.statusEffects.weaknessStacks} · 무방비 {(state.statusEffects.isExposed ? "적용" : "없음")}\n"
            + "아래 수치와 상태이상 중첩을 입력하여 적용하세요.");
    }

    private void BuildItems()
    {
        leftTitle.text = "보유 아이템 · 슬롯 0~2";
        rightTitle.text = "아이템 도감";
        for (int i = 0; i < controller.TestInventory.SlotCount; i++)
        {
            int slot = i;
            ItemData data = controller.TestInventory.GetItem(i);
            BattleTestListRow row = Row(ownedContent, ownedRows, $"슬롯 {i} · {data?.DisplayName ?? "비어 있음"}",
                "오른쪽 도감에서 교체할 수 있습니다.", data?.Icon);
            row.Action(2, "선택", () => itemSlot.SetTextWithoutNotify(slot.ToString()));
            row.Action(3, "비우기", () => Run($"item remove {slot}"), data != null);
            row.ConfigureInteraction(interactions, BattleTestDragKind.Item, slot, false, 2,
                () => data == null ? "도감의 아이템을 이 슬롯에 끌어놓으세요." : data.Description);
        }
        for (int i = 0; i < controller.Items.Count; i++)
        {
            int index = i;
            ItemData data = controller.Items[i];
            if (!Matches(data.DisplayName, data.Description)) continue;
            BattleTestListRow row = Row(catalogContent, catalogRows, data.DisplayName, "선택한 슬롯에 넣기", data.Icon);
            row.Action(2, "설명", () => SelectCatalog(index));
            row.Action(3, "넣기", () => Run($"item set {itemSlot.text} {index}"));
            row.ConfigureInteraction(interactions, BattleTestDragKind.ItemCatalog, index, true, 3, () => data.Description);
        }
    }

    private void SelectCatalog(int index)
    {
        selectedCatalog[page] = index; inspectedEnemy = null; RefreshDetails();
        interactions.Inspect(details.text);
    }

    internal void CancelPointer() => interactions.Cancel();
    internal void SelectWorldCell(int tile, int lane)
    {
        SelectPage(2);
        enemyTile.SetTextWithoutNotify(tile.ToString());
        enemyLane.SetTextWithoutNotify(lane.ToString());
        if (controller.TestWaves.TryGetEnemyAtTile(tile, lane, out EnemyController enemy)) SelectEnemy(enemy, tile, lane);
        else { inspectedEnemy = null; ShowText($"선택한 위치: {tile}번 칸 · {lane}번 레인\n도감의 적을 이 칸에 끌어놓으세요."); }
    }

    internal string EnemyRemovalCommand(int instanceId)
    {
        foreach (EnemyController enemy in controller.TestWaves.ActiveEnemies)
        {
            if (enemy.GetInstanceID() != instanceId) continue;
            RunEnemySaveData saved = enemy.CaptureRunState(controller.TestWaves.ActiveEnemies);
            return $"enemy remove {saved.tileIndex} {saved.laneIndex}";
        }
        return null;
    }
    private void RefreshDetails()
    {
        int selected = selectedCatalog[page];
        switch (page)
        {
            case 0:
                BulletData bullet = controller.Bullets[selected];
                int level = int.TryParse(bulletLevel.text, out int value) ? Mathf.Clamp(value, 0, 3) : 0;
                ShowText(bullet.GetDisplayName(level) + " · " + bullet.BulletTypeDisplayName + "\n" + bullet.GetDetailedDescription(level));
                addSelected.interactable = controller.TestDeck.TotalBulletCount < DeckManager.MaximumOwnedBulletCount;
                break;
            case 1: ShowText(controller.Relics[selected].DisplayName + "\n" + controller.Relics[selected].Description); break;
            case 2:
                if (inspectedEnemy != null && controller.TestWaves.ActiveEnemies.Contains(inspectedEnemy))
                    ShowEnemyDetails(inspectedEnemy);
                else ShowText(controller.Enemies[selected].DisplayName + "\n" + controller.Enemies[selected].Description);
                break;
            case 3: ShowText(controller.Items[selected].DisplayName + "\n" + controller.Items[selected].Description); break;
        }
    }

    private void ShowText(string text)
    {
        details.text = text.Replace("DUEL CLOCK", "행동 기반 전투");
        details.GetComponentInParent<UnityEngine.UI.ScrollRect>(true).verticalNormalizedPosition = 1;
    }
    private void AddSelectedBullet() => AddBullet(selectedCatalog[0]);
    private void AddBullet(int index) => Run($"bullet add {index} {bulletLevel.text}");
    private void CycleStatus()
    {
        selectedStatus = (StatusEffectType)(((int)selectedStatus + 1) % 5);
        statusLabel.text = BattleTestCommandRouter.StatusName(selectedStatus) + " 전환";
    }
    private void ApplyStatus() => Run($"status {enemyTile.text} {enemyLane.text} {selectedStatus} {statusStacks.text}");
    private void Run(string command) => console.RunCommand(command);
    private bool Matches(params string[] values) => string.IsNullOrWhiteSpace(search.text)
        || values.Any(value => value != null && value.IndexOf(search.text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
    private static string GradeName(BulletGrade grade) => grade switch
    {
        BulletGrade.Rare => "희귀", BulletGrade.Ace => "에이스", BulletGrade.Legendary => "전설", _ => "일반"
    };
    private static Sprite EnemyIcon(EnemyData data) => data.Avatar == null
        ? null : data.Avatar.GetComponentInChildren<SpriteRenderer>(true)?.sprite;

    private BattleTestListRow Row(RectTransform parent, List<BattleTestListRow> rows,
        string title, string subtitle, Sprite icon = null, BulletData bullet = null)
    {
        BattleTestListRow row = Instantiate(rowTemplate, parent);
        row.name = "항목 | " + title;
        row.Bind(title, subtitle, icon, bullet);
        row.gameObject.SetActive(true);
        rows.Add(row);
        return row;
    }
    private static void Clear(List<BattleTestListRow> rows)
    {
        foreach (BattleTestListRow row in rows)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        rows.Clear();
    }
}
