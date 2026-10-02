using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class LoadedAuthoringWindow : EditorWindow
{
    [SerializeField] private bool localization;
    [SerializeField] private string workbookPath;
    private BulletBalanceWorkbook.Plan bulletPlan;
    private LoadedLocalizationWorkbook.Plan localizationPlan;
    private ScrollView results;
    private Label status;
    private Button apply;
    private string previewHash;
    [MenuItem("Tools/LOADED/Bullet Balance")]
    public static void OpenBullets() => Open(false);
    [MenuItem("Tools/LOADED/Localization Workbook")]
    public static void OpenLocalization() => Open(true);
    private static void Open(bool translated)
    {
        var window = CreateInstance<LoadedAuthoringWindow>(); window.localization = translated;
        window.titleContent = new GUIContent(translated ? "Localization Workbook" : "Bullet Balance"); window.minSize = new Vector2(760, 500); window.Show();
    }
    public void CreateGUI()
    {
        var root = rootVisualElement; root.Clear(); root.style.paddingLeft = 12; root.style.paddingRight = 12; root.style.paddingTop = 10;
        var heading = new Label(localization ? "한국어 · 영어 현지화" : "탄환 데이터와 아이콘"); heading.style.fontSize = 19; root.Add(heading);
        var note = new Label(localization ? "변수·서식 태그를 보존해 String Table에 적용합니다. 문구를 선택하면 번역 전후와 원본 위치를 비교할 수 있습니다." : "유형별 탭에서 등급별 스탯을 편집하고, 아이콘은 아이콘 탭에서만 교체하세요. 유형 탭 그림은 내보내기 시점의 미리보기입니다. 초월은 준비 데이터입니다."); note.style.whiteSpace = WhiteSpace.Normal; root.Add(note);
        var path = new TextField("워크북 경로") { value = workbookPath ?? "" }; path.RegisterValueChangedCallback(e => { workbookPath = e.newValue; Invalidate(); }); root.Add(path);
        var toolbar = new VisualElement(); toolbar.style.flexDirection = FlexDirection.Row; root.Add(toolbar);
        toolbar.Add(new Button(() => { string chosen = EditorUtility.OpenFilePanel("워크북 열기", "", "xlsx"); if (chosen != "") path.value = chosen; }) { text = "파일 선택" });
        toolbar.Add(new Button(() => Run(() =>
        {
            string chosen = EditorUtility.SaveFilePanel("현재 데이터 내보내기", Path.GetFullPath("outputs/authoring-20261002"), localization ? "LOADED_Localization" : "LOADED_BulletData", "xlsx");
            if (chosen == "") return;
            var book = localization ? LoadedLocalizationWorkbook.Export() : BulletBalanceWorkbook.Export(); book.Write(chosen); path.value = chosen; status.text = "내보내기 완료";
        })) { text = "현재 데이터 내보내기" });
        toolbar.Add(new Button(() => Run(Preview)) { text = "검증 · 변경 미리보기" });
        apply = new Button(() => Run(() =>
        {
            if (previewHash != BulletBalanceWorkbook.Hash(File.ReadAllBytes(workbookPath))) throw new InvalidOperationException("워크북이 변경되었습니다. 다시 검증하세요.");
            if (localization) LoadedLocalizationWorkbook.Apply(localizationPlan); else BulletBalanceWorkbook.Apply(bulletPlan);
            Invalidate(); results.Clear(); status.text = "선택한 변경 적용 완료. Ctrl+Z로 에셋 변경을 되돌릴 수 있습니다. 초월 준비 파일은 버전 관리로 복원하세요. 계속 편집하려면 새로 내보내세요.";
        })) { text = "선택한 변경 적용" }; apply.SetEnabled(false); toolbar.Add(apply);
        status = new Label("워크북을 선택하거나 현재 데이터를 내보내세요."); status.style.whiteSpace = WhiteSpace.Normal; root.Add(status);
        results = new ScrollView(); results.style.flexGrow = 1; root.Add(results);
    }
    private void Invalidate() { bulletPlan = null; localizationPlan = null; previewHash = null; apply?.SetEnabled(false); }
    private void Run(Action action) { try { action(); } catch (Exception e) { Invalidate(); status.text = e.Message; Debug.LogWarning("LOADED workbook: " + e.Message); } }
    private void Preview()
    {
        Invalidate(); results.Clear();
        var book = LoadedWorkbook.Read(workbookPath); previewHash = BulletBalanceWorkbook.Hash(File.ReadAllBytes(workbookPath));
        if (localization)
        {
            localizationPlan = LoadedLocalizationWorkbook.Validate(book);
            foreach (var error in localizationPlan.Errors) results.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (var warning in localizationPlan.Warnings) results.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
            foreach (var entry in localizationPlan.Entries)
            {
                var fold = new Foldout { text = entry.Korean.Replace('\n', ' ').Substring(0, Math.Min(80, entry.Korean.Length)) };
                var selected = new Toggle("적용") { value = true }; selected.RegisterValueChangedCallback(e => entry.Selected = e.newValue); fold.Add(selected);
                fold.Add(ReadonlyText("key", entry.Key)); fold.Add(ReadonlyText("원본", entry.Source)); fold.Add(ReadonlyText("문맥", entry.Note));
                fold.Add(ReadonlyText("한국어 이전", entry.BeforeKorean)); fold.Add(ReadonlyText("한국어 이후", entry.Korean)); fold.Add(ReadonlyText("영어 이전", entry.BeforeEnglish)); fold.Add(ReadonlyText("영어 이후", entry.English)); results.Add(fold);
            }
            status.text = $"변경 {localizationPlan.Entries.Count}개 · 오류 {localizationPlan.Errors.Count}개 · 줄바꿈/공백 검토 {localizationPlan.Warnings.Count}개";
            apply.SetEnabled(localizationPlan.Errors.Count == 0 && localizationPlan.Entries.Count > 0);
        }
        else
        {
            bulletPlan = BulletBalanceWorkbook.Validate(book);
            foreach (var error in bulletPlan.Errors) results.Add(new HelpBox(error, HelpBoxMessageType.Error));
            foreach (var change in bulletPlan.Changes)
            {
                var fold = new Foldout { text = Path.GetFileNameWithoutExtension(change.Path), value = true };
                var selected = new Toggle("적용") { value = true }; selected.RegisterValueChangedCallback(e => change.Selected = e.newValue); fold.Add(selected);
                foreach (var line in change.Lines) { var label = new Label(line); label.style.whiteSpace = WhiteSpace.Normal; fold.Add(label); }
                results.Add(fold);
            }
            if (bulletPlan.DraftChanged) results.Add(ReadonlyText("초월 준비 변경 (런타임 미적용)", bulletPlan.Draft));
            status.text = $"탄환 변경 {bulletPlan.Changes.Count}개 · 오류 {bulletPlan.Errors.Count}개";
            apply.SetEnabled(bulletPlan.Errors.Count == 0 && (bulletPlan.Changes.Count > 0 || bulletPlan.DraftChanged));
        }
    }
    private static TextField ReadonlyText(string label, string value) => new TextField(label) { value = value ?? "", multiline = true, isReadOnly = true };
}
