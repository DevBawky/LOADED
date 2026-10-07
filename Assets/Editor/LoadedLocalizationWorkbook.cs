using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Metadata;

public static class LoadedLocalizationWorkbook
{
    public const string SourcePath = "Tools/Authoring/localization.json";
    public const string CollectionName = "LOADED";
    public const string Schema = "LOADED.Localization.1";
    public sealed class Entry
    {
        public string Key, Korean, English, BeforeKorean, BeforeEnglish, Source, Note, Status, BeforeStatus;
        public bool Selected = true;
    }
    public sealed class Plan
    {
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }
    static JArray Sources() => JArray.Parse(File.ReadAllText(SourcePath));
    static StringTable Table(string locale) => LocalizationEditorSettings.GetStringTableCollection(CollectionName)?.GetTable(locale) as StringTable;
    static string ReviewStatus(string key) => LocalizationEditorSettings.GetStringTableCollection(CollectionName)?.SharedData.GetEntry(key)?.Metadata.GetMetadata<Comment>()?.CommentText ?? "Review";
    public static LoadedWorkbook Export()
    {
        var book = new LoadedWorkbook();
        var guide = book.Add("사용 안내", "항목", "설명");
        guide.Add("편집", "ko는 한국어, en은 영어입니다. key와 원본 위치는 유지하세요. 상태는 Draft, Review, Approved 중 선택합니다.");
        guide.Add("줄바꿈", "Alt+Enter로 줄바꿈합니다. 서로 다른 효과를 다른 줄에 두고 숫자와 단위를 분리하지 마세요. 앞뒤 공백은 연결 문구에 필요할 수 있어 보존됩니다.");
        guide.Add("변수와 서식", "{0}, {attempt}, <color=...> 등의 변수와 태그를 그대로 유지하세요. 문장 전체의 어순은 자연스럽게 바꿀 수 있습니다.");
        guide.Add("적용", "Tools > LOADED > Localization Workbook에서 검증 후 선택한 변경을 String Table에 적용합니다. 원본 장면과 C# 코드는 자동 변경하지 않습니다.");
        guide.Add("코드 문구", "Code 행은 런타임 연결할 때 필요한 원본 위치·변수 표현식입니다. 조각 문구는 문맥 열의 주변 코드와 함께 검토하세요.");
        guide.Add("장면 연결", "정적 UI 문구는 Unity의 Localize String Event로 key를 연결합니다. 동적 UI는 포맷 인자를 전달하는 코드 연결이 필요합니다.");
        var sheet = book.Add("현지화", "분류", "ko", "en", "상태", "문맥", "원본 위치", "필드", "key");
        var metadata = book.Add("메타데이터", "key", "value"); metadata.Add("schema", Schema);
        var ko = Table("ko"); var en = Table("en");
        foreach (var source in Sources())
        {
            string key = (string)source["key"];
            string korean = ko?.GetEntry(key)?.Value ?? (string)source["ko"];
            string english = en?.GetEntry(key)?.Value ?? (string)source["en"];
            sheet.Add(source["scope"], korean, english, ReviewStatus(key), source["note"], source["source"], source["field"], key);
            metadata.Add(key, new JArray(ko?.GetEntry(key)?.Value, en?.GetEntry(key)?.Value, ReviewStatus(key)).ToString(Newtonsoft.Json.Formatting.None));
        }
        return book;
    }
    public static Plan Validate(LoadedWorkbook book)
    {
        var plan = new Plan();
        try
        {
            var meta = book.Require("메타데이터").Rows.Skip(1).Where(r => r.Length >= 2).ToDictionary(r => r[0], r => r[1]);
            if (meta["schema"] != Schema) throw new InvalidDataException("Unknown localization schema.");
            var source = Sources().ToDictionary(s => (string)s["key"]);
            var sheet = book.Require("현지화"); var keys = new HashSet<string>(); var ko = Table("ko"); var en = Table("en");
            foreach (var row in sheet.Rows.Skip(1).Where(r => r.Any(v => !string.IsNullOrEmpty(v))))
            {
                string key = sheet.Get(row, "key");
                if (!keys.Add(key)) { plan.Errors.Add("Duplicate key: " + key); continue; }
                if (!source.ContainsKey(key)) { plan.Errors.Add("Unknown key: " + key); continue; }
                string korean = sheet.Get(row, "ko"), english = sheet.Get(row, "en");
                string state = sheet.Get(row, "상태");
                if (!new[] { "Draft", "Review", "Approved" }.Contains(state)) plan.Errors.Add(key + ": invalid review status.");
                var issues = ValidateText(korean, english); plan.Errors.AddRange(issues.Select(i => key + ": " + i));
                if (!Tokens((string)source[key]["ko"], @"(?<!\{)\{[^{}]+\}(?!\})").SequenceEqual(Tokens(korean, @"(?<!\{)\{[^{}]+\}(?!\})"))) plan.Errors.Add(key + ": source placeholders changed.");
                if (korean.Count(c => c == '\n') != english.Count(c => c == '\n')) plan.Warnings.Add(key + ": line count differs; review sentence boundaries.");
                if (korean.StartsWith(" ") != english.StartsWith(" ") || korean.EndsWith(" ") != english.EndsWith(" ")) plan.Warnings.Add(key + ": leading/trailing spaces differ; check concatenated text.");
                string beforeKo = ko?.GetEntry(key)?.Value, beforeEn = en?.GetEntry(key)?.Value;
                string beforeStatus = ReviewStatus(key);
                if (korean == beforeKo && english == beforeEn && state == beforeStatus) continue;
                if (!meta.TryGetValue(key, out string baseline) || !JToken.DeepEquals(JArray.Parse(baseline), new JArray(beforeKo, beforeEn, beforeStatus))) plan.Errors.Add(key + ": String Table changed since export. Re-export before applying.");
                plan.Entries.Add(new Entry { Key = key, Korean = korean, English = english, BeforeKorean = beforeKo, BeforeEnglish = beforeEn, Status = state, BeforeStatus = beforeStatus, Source = sheet.Get(row, "원본 위치"), Note = sheet.Get(row, "문맥") });
            }
            if (!keys.SetEquals(source.Keys)) plan.Errors.Add("Localization rows were removed or added. Refresh the source catalog first.");
        }
        catch (Exception e) { plan.Errors.Add(e.Message); }
        return plan;
    }
    public static List<string> ValidateText(string korean, string english)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(korean) || string.IsNullOrWhiteSpace(english)) errors.Add("Both ko and en must be nonempty.");
        if (!Tokens(korean, @"(?<!\{)\{[^{}]+\}(?!\})").SequenceEqual(Tokens(english, @"(?<!\{)\{[^{}]+\}(?!\})"))) errors.Add("Placeholder tokens differ.");
        if (!Tokens(korean, @"</?[A-Za-z][^>]*>").SequenceEqual(Tokens(english, @"</?[A-Za-z][^>]*>"))) errors.Add("Rich-text tags differ.");
        if (english.Contains("\\n")) errors.Add("Use an actual line break, not literal \\n.");
        return errors;
    }
    static string[] Tokens(string value, string pattern) => Regex.Matches(value ?? "", pattern).Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
    public static void Apply(Plan plan)
    {
        if (plan.Errors.Count > 0) throw new InvalidOperationException("Fix localization errors first.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Use idle Edit Mode.");
        var selected = plan.Entries.Where(e => e.Selected).ToArray();
        if (selected.Length == 0) return;
        var ko = Table("ko"); var en = Table("en");
        foreach (var entry in selected)
            if (ko?.GetEntry(entry.Key)?.Value != entry.BeforeKorean || en?.GetEntry(entry.Key)?.Value != entry.BeforeEnglish || ReviewStatus(entry.Key) != entry.BeforeStatus) throw new InvalidOperationException("Table changed after preview: " + entry.Key);
        EnsureSettings();
        var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName) ?? LocalizationEditorSettings.CreateStringTableCollection(CollectionName, "Assets/Localization/Tables");
        ko = collection.GetTable("ko") as StringTable; en = collection.GetTable("en") as StringTable;
        if (ko == null || en == null) throw new InvalidOperationException("Korean/English String Tables are missing.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { collection, collection.SharedData, ko, en }, "Import LOADED localization workbook");
        try
        {
            foreach (var entry in selected)
            {
                ko.AddEntry(entry.Key, entry.Korean); en.AddEntry(entry.Key, entry.English);
                var metadata = collection.SharedData.GetEntry(entry.Key).Metadata;
                var comment = metadata.GetMetadata<Comment>();
                if (comment == null) { comment = new Comment(); metadata.AddMetadata(comment); }
                comment.CommentText = entry.Status;
            }
            EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData); EditorUtility.SetDirty(ko); EditorUtility.SetDirty(en);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssetIfDirty(collection); AssetDatabase.SaveAssetIfDirty(collection.SharedData); AssetDatabase.SaveAssetIfDirty(ko); AssetDatabase.SaveAssetIfDirty(en);
            Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    static void EnsureSettings()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Localization")) AssetDatabase.CreateFolder("Assets", "Localization");
        if (!AssetDatabase.IsValidFolder("Assets/Localization/Tables")) AssetDatabase.CreateFolder("Assets/Localization", "Tables");
        if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
        {
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/Localization/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        foreach (string code in new[] { "ko", "en" })
        {
            if (LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code)) continue;
            var locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale, "Assets/Localization/" + code + ".asset"); LocalizationEditorSettings.AddLocale(locale);
        }
    }
}
