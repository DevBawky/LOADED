using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BulletBalanceWorkbook
{
    public const string Schema = "LOADED.Bullets.1";
    public const string DraftPath = "ProjectSettings/LOADEDTranscendenceDraft.json";
    public static readonly string[] LevelFields = { "description", "damage", "maxRange", "criticalChance", "criticalDamageMultiplier", "shotgunShotCount", "doesNotConsumeTurn", "recoilStrength", "upgradeCost" };
    public static readonly string[] LevelHeaders = { "설명", "피해", "사거리", "치명타 확률 (%)", "치명타 배율", "발수 (0=기본값)", "턴 소모 없음", "반동", "다음 강화 비용" };
    public static readonly string[] EffectFields = { "effectType", "target", "activationChance", "stackCount", "knockbackDistance", "amount", "secondTransferPercent", "thirdTransferPercent" };
    public static readonly string[] EffectHeaders = { "효과", "대상", "발동 확률 (%)", "스택", "이동 거리", "수치", "2차 전이 (%)", "3차 전이 (%)" };
    public sealed class Change
    {
        public string Guid, Path, Before, After;
        public byte[] Icon;
        public readonly List<string> Lines = new List<string>();
        public bool Selected = true;
    }
    public sealed class Plan
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<Change> Changes = new List<Change>();
        public string Draft, OriginalDraft;
        public bool DraftChanged => Draft != OriginalDraft;
    }
    public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static string Text(JToken token) => token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
    static JObject Body(string json) => (JObject)JObject.Parse(json)["MonoBehaviour"];
    static string Json(BulletData data) => EditorJsonUtility.ToJson(data);
    static string ProjectKey => AssetDatabase.AssetPathToGUID("Assets/Scripts/Bullet/BulletData.cs");
    public static LoadedWorkbook Export() => BulletWorkbookLayout.Group(ExportData());
    internal static LoadedWorkbook ExportData()
    {
        var workbook = new LoadedWorkbook();
        var guide = workbook.Add("사용 안내", "항목", "설명");
        guide.Add("편집", "탄환 목록은 공통 정보, 레벨은 Lv.0~+3 수치입니다. 필터로 탄환을 찾으세요. GUID는 수정하지 마세요.");
        guide.Add("아이콘 교체", "탄환 시트 A열의 그림을 우클릭해 그림 변경을 선택하세요. 셀 위에 배치된 PNG/JPEG 그림만 지원합니다. 셀 안 그림과 IMAGE 수식은 지원하지 않습니다.");
        guide.Add("아이콘 위치", "그림의 왼쪽 위를 해당 탄환 A열 셀 안에 두세요. 행마다 그림은 하나입니다. 정렬 후 그림이 해당 탄환 행에 있는지 확인하세요.");
        guide.Add("적용", "Tools > LOADED > Bullet Balance에서 검증하고 변경을 선택해 적용합니다. Unity에서 변경된 에셋은 다시 내보낸 뒤 편집하세요.");
        guide.Add("효과 편집", "인덱스는 0부터 연속으로 입력합니다. 효과 행을 삭제하면 해당 효과가 제거됩니다. 조건 번호 -1은 일반 효과입니다.");
        guide.Add("초월", "초월 단계는 탄환 등급과 별개입니다. 확률 미정은 빈칸으로 유지합니다. 준비 데이터만 저장하며 런타임 강화 규칙에는 적용되지 않습니다.");
        var bullets = workbook.Add("탄환", "아이콘", "이름", "등급", "발사 유형", "가격", "GUID");
        var levels = workbook.Add("레벨", new[] { "이름", "레벨" }.Concat(LevelHeaders).Concat(new[] { "GUID" }).ToArray());
        var effects = workbook.Add("효과", new[] { "이름", "레벨", "조건 번호 (-1=일반)", "효과 번호" }.Concat(EffectHeaders).Concat(new[] { "GUID" }).ToArray());
        var conditions = workbook.Add("조건", "이름", "레벨", "조건 번호", "발동 조건", "GUID");
        var penetration = workbook.Add("관통", "이름", "레벨", "관통 번호", "확률 (%)", "GUID");
        var draft = workbook.Add("초월 준비", "초월 단계", "성공 확률 (%)", "비용", "메모");
        if (File.Exists(DraftPath)) foreach (var row in JArray.Parse(File.ReadAllText(DraftPath))) draft.Add(row.Select(Text).Cast<object>().ToArray());
        else for (int stage = 1; stage <= 10; stage++) draft.Add(stage, "", "", "미정 · 런타임 미적용");
        var metadata = workbook.Add("메타데이터", "key", "value");
        metadata.Add("schema", Schema); metadata.Add("project", ProjectKey); metadata.Add("unity", Application.unityVersion);
        metadata.Add("transcendenceBaseline", File.Exists(DraftPath) ? File.ReadAllText(DraftPath) : "");
        foreach (string guid in AssetDatabase.FindAssets("t:BulletData", new[] { "Assets" }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
        {
            var data = AssetDatabase.LoadAssetAtPath<BulletData>(AssetDatabase.GUIDToAssetPath(guid));
            string json = Json(data); JObject body = Body(json);
            byte[] icon = CaptureIcon(data.CylinderIcon);
            bullets.Add("", data.DisplayName, data.Grade.ToString(), data.BulletType.ToString(), body["price"], guid);
            if (icon != null) bullets.Images.Add(bullets.Rows.Count - 1, icon);
            metadata.Add("baseline." + guid, json); metadata.Add("icon." + guid, icon == null ? "" : Hash(icon));
            for (int level = 0; level <= BulletData.MaximumUpgradeLevel; level++)
            {
                var state = Level(body, level);
                levels.Add(new object[] { data.DisplayName, level }.Concat(LevelFields.Select(f => (object)Text(state[f]))).Concat(new object[] { guid }).ToArray());
                AddEffects(effects, data.DisplayName, guid, level, -1, (JArray)state["effects"]);
                var groups = (JArray)state["conditionalEvents"];
                for (int g = 0; g < groups.Count; g++)
                {
                    conditions.Add(data.DisplayName, level, g, ((BulletConditionalTrigger)(int)groups[g]["trigger"]).ToString(), guid);
                    AddEffects(effects, data.DisplayName, guid, level, g, (JArray)groups[g]["events"]);
                }
                var chances = (JArray)state["penetrationChances"];
                for (int p = 0; p < chances.Count; p++) penetration.Add(data.DisplayName, level, p, Text(chances[p]["chance"]), guid);
            }
        }
        return workbook;
    }
    static void AddEffects(LoadedWorkbook.Sheet sheet, string name, string guid, int level, int group, JArray effects)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            var values = new List<object> { name, level, group, i };
            for (int f = 0; f < EffectFields.Length; f++) values.Add(f == 0 ? ((BulletEffectType)(int)effects[i][EffectFields[f]]).ToString() : f == 1 ? ((BulletEffectTarget)(int)effects[i][EffectFields[f]]).ToString() : Text(effects[i][EffectFields[f]]));
            values.Add(guid); sheet.Add(values.ToArray());
        }
    }
    static JObject Level(JObject body, int level) => level == 0 ? body : (JObject)body["upgradeLevels"][level - 1];
    static IEnumerable<string[]> Data(LoadedWorkbook.Sheet sheet) => sheet.Rows.Skip(1).Where(row => row.Any(cell => !string.IsNullOrEmpty(cell)));
    public static Plan Validate(LoadedWorkbook workbook)
    {
        var plan = new Plan();
        try { BuildPlan(BulletWorkbookLayout.Flatten(workbook), plan); }
        catch (Exception e) { plan.Errors.Add(e.Message); }
        return plan;
    }
    static void BuildPlan(LoadedWorkbook book, Plan plan)
    {
        var metaSheet = book.Require("메타데이터");
        var meta = Data(metaSheet).ToDictionary(r => metaSheet.Get(r, "key"), r => metaSheet.Get(r, "value"));
        if (meta["schema"] != Schema || meta["project"] != ProjectKey) throw new InvalidDataException("Workbook schema/project does not match LOADED.");
        var bodies = new Dictionary<string, JObject>(); var changes = new Dictionary<string, Change>();
        var bullets = book.Require("탄환");
        for (int rowIndex = 1; rowIndex < bullets.Rows.Count; rowIndex++)
        {
            var row = bullets.Rows[rowIndex]; if (!row.Any(c => !string.IsNullOrEmpty(c))) continue;
            string guid = bullets.Get(row, "GUID");
            if (!meta.TryGetValue("baseline." + guid, out string baseline)) throw new InvalidDataException("Unknown GUID: " + guid);
            var data = AssetDatabase.LoadAssetAtPath<BulletData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) throw new InvalidDataException("Bullet asset is missing: " + guid);
            var body = Body(baseline); bodies.Add(guid, body);
            var change = new Change { Guid = guid, Path = AssetDatabase.GetAssetPath(data), Before = baseline }; changes.Add(guid, change);
            body["displayName"] = Required(bullets.Get(row, "이름"), "이름");
            body["grade"] = EnumValue<BulletGrade>(bullets.Get(row, "등급"));
            body["bulletType"] = EnumValue<BulletType>(bullets.Get(row, "발사 유형"));
            body["price"] = Integer(bullets.Get(row, "가격"), 0, int.MaxValue);
            string oldIcon = meta["icon." + guid];
            if (!bullets.Images.TryGetValue(rowIndex, out byte[] image))
            {
                if (oldIcon != "") throw new InvalidDataException("Missing icon: " + body["displayName"] + ". Use an embedded image over cell A" + (rowIndex + 1));
            }
            else if (Hash(image) != oldIcon)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!ImageConversion.LoadImage(texture, image) || texture.width > 4096 || texture.height > 4096) throw new InvalidDataException("Icon must be a PNG/JPEG up to 4096 pixels.");
                    change.Icon = texture.EncodeToPNG();
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                change.Lines.Add("아이콘 이미지 교체");
            }
            for (int l = 0; l <= BulletData.MaximumUpgradeLevel; l++)
            {
                var state = Level(body, l); state["effects"] = new JArray(); state["conditionalEvents"] = new JArray(); state["penetrationChances"] = new JArray();
            }
        }
        var expected = meta.Keys.Where(k => k.StartsWith("baseline.")).Select(k => k.Substring(9)).OrderBy(k => k);
        if (!expected.SequenceEqual(bodies.Keys.OrderBy(k => k))) throw new InvalidDataException("Bullet rows were removed. Export a fresh workbook instead.");
        var levelSheet = book.Require("레벨"); var levelKeys = new HashSet<string>();
        foreach (var row in Data(levelSheet))
        {
            string guid = levelSheet.Get(row, "GUID"); int level = Integer(levelSheet.Get(row, "레벨"), 0, BulletData.MaximumUpgradeLevel);
            if (!levelKeys.Add(guid + ":" + level)) throw new InvalidDataException("Duplicate GUID/level: " + guid + ":" + level);
            var state = Level(bodies[guid], level);
            for (int f = 0; f < LevelFields.Length; f++)
            {
                string value = levelSheet.Get(row, LevelHeaders[f]); string field = LevelFields[f];
                switch (field)
                {
                    case "description": state[field] = value; break;
                    case "doesNotConsumeTurn": state[field] = Boolean(value); break;
                    case "damage": case "upgradeCost": state[field] = Integer(value, 0, int.MaxValue); break;
                    case "maxRange": state[field] = Integer(value, 1, 10); break;
                    case "shotgunShotCount":
                        int count = Integer(value, 0, int.MaxValue);
                        if ((int)bodies[guid]["bulletType"] == (int)BulletType.Shotgun && (level == 0 && count < 2 || level > 0 && count == 1)) throw new InvalidDataException("Shotgun count must be >=2; upgraded levels may use 0 to inherit.");
                        state[field] = count; break;
                    case "criticalChance": state[field] = Number(value, 0, 100); break;
                    case "criticalDamageMultiplier": state[field] = Number(value, 1, float.MaxValue); break;
                    default: state[field] = Number(value, 0, float.MaxValue); break;
                }
            }
        }
        if (levelKeys.Count != bodies.Count * (BulletData.MaximumUpgradeLevel + 1)) throw new InvalidDataException("Every bullet needs all level rows.");
        var conditions = book.Require("조건");
        foreach (var row in Data(conditions).OrderBy(r => conditions.Get(r, "GUID")).ThenBy(r => Integer(conditions.Get(r, "레벨"), 0, BulletData.MaximumUpgradeLevel)).ThenBy(r => Integer(conditions.Get(r, "조건 번호"), 0, 1000)))
        {
            var array = (JArray)Level(bodies[conditions.Get(row, "GUID")], Integer(conditions.Get(row, "레벨"), 0, BulletData.MaximumUpgradeLevel))["conditionalEvents"];
            if (Integer(conditions.Get(row, "조건 번호"), 0, 1000) != array.Count) throw new InvalidDataException("Condition indices must be unique and contiguous from 0.");
            array.Add(new JObject { ["trigger"] = EnumValue<BulletConditionalTrigger>(conditions.Get(row, "발동 조건")), ["events"] = new JArray() });
        }
        var effects = book.Require("효과");
        foreach (var row in Data(effects).OrderBy(r => effects.Get(r, "GUID")).ThenBy(r => Integer(effects.Get(r, "레벨"), 0, BulletData.MaximumUpgradeLevel)).ThenBy(r => Integer(effects.Get(r, "조건 번호 (-1=일반)"), -1, 1000)).ThenBy(r => Integer(effects.Get(r, "효과 번호"), 0, 1000)))
        {
            var state = Level(bodies[effects.Get(row, "GUID")], Integer(effects.Get(row, "레벨"), 0, BulletData.MaximumUpgradeLevel));
            int group = Integer(effects.Get(row, "조건 번호 (-1=일반)"), -1, 1000);
            var array = group == -1 ? (JArray)state["effects"] : (JArray)state["conditionalEvents"][group]["events"];
            if (Integer(effects.Get(row, "효과 번호"), 0, 1000) != array.Count) throw new InvalidDataException("Effect indices must be unique and contiguous from 0.");
            var item = new JObject();
            for (int f = 0; f < EffectFields.Length; f++)
            {
                string value = effects.Get(row, EffectHeaders[f]);
                if (f == 0) item[EffectFields[f]] = EnumValue<BulletEffectType>(value);
                else if (f == 1) item[EffectFields[f]] = EnumValue<BulletEffectTarget>(value);
                else if (f == 3 || f == 4) item[EffectFields[f]] = Integer(value, 0, int.MaxValue);
                else item[EffectFields[f]] = Number(value, 0, f == 5 ? float.MaxValue : 100);
            }
            array.Add(item);
        }
        var penetration = book.Require("관통");
        foreach (var row in Data(penetration).OrderBy(r => penetration.Get(r, "GUID")).ThenBy(r => Integer(penetration.Get(r, "레벨"), 0, BulletData.MaximumUpgradeLevel)).ThenBy(r => Integer(penetration.Get(r, "관통 번호"), 0, 1000)))
        {
            var array = (JArray)Level(bodies[penetration.Get(row, "GUID")], Integer(penetration.Get(row, "레벨"), 0, BulletData.MaximumUpgradeLevel))["penetrationChances"];
            if (Integer(penetration.Get(row, "관통 번호"), 0, 1000) != array.Count) throw new InvalidDataException("Penetration indices must be unique and contiguous from 0.");
            array.Add(new JObject { ["chance"] = Number(penetration.Get(row, "확률 (%)"), 0, 100) });
        }
        foreach (var pair in changes)
        {
            var before = Body(pair.Value.Before); var after = bodies[pair.Key];
            Diff(before, after, "", pair.Value.Lines);
            pair.Value.After = new JObject { ["MonoBehaviour"] = after }.ToString(Formatting.None);
            if (pair.Value.Lines.Count == 0) continue;
            var current = AssetDatabase.LoadAssetAtPath<BulletData>(pair.Value.Path);
            if (!Equivalent(Body(Json(current)), before)) throw new InvalidDataException(current.name + ": Unity asset changed since export. Re-export before applying.");
            plan.Changes.Add(pair.Value);
        }
        var draft = book.Require("초월 준비"); var draftRows = new JArray(); var stages = new HashSet<int>(); float previous = 100;
        foreach (var row in Data(draft).OrderBy(r => Integer(draft.Get(r, "초월 단계"), 1, int.MaxValue)))
        {
            int stage = Integer(draft.Get(row, "초월 단계"), 1, int.MaxValue);
            if (!stages.Add(stage)) throw new InvalidDataException("Duplicate transcendence stage.");
            string chance = draft.Get(row, "성공 확률 (%)"), cost = draft.Get(row, "비용");
            if (chance != "") { float number = Number(chance, 0, 100); if (number > previous) throw new InvalidDataException("Transcendence success chance must not increase with stage."); previous = number; }
            if (cost != "") Integer(cost, 0, int.MaxValue);
            draftRows.Add(new JArray(stage.ToString(), chance, cost, draft.Get(row, "메모")));
        }
        plan.OriginalDraft = File.Exists(DraftPath) ? File.ReadAllText(DraftPath) : "";
        plan.Draft = draftRows.ToString(Formatting.None);
        // Unconfigured starter rows are not a mutation on an untouched round trip.
        if (plan.OriginalDraft == "" && draftRows.All(r => (string)r[1] == "" && (string)r[2] == "")) plan.Draft = "";
        if (plan.DraftChanged && meta["transcendenceBaseline"] != plan.OriginalDraft) throw new InvalidDataException("Transcendence draft changed since export.");
    }
    public static void Apply(Plan plan)
    {
        if (plan.Errors.Count > 0) throw new InvalidOperationException("Fix workbook validation errors first.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Apply is available only in idle Edit Mode.");
        var selected = plan.Changes.Where(c => c.Selected).ToArray();
        foreach (var c in selected) if (!Equivalent(Body(Json(AssetDatabase.LoadAssetAtPath<BulletData>(c.Path))), Body(c.Before))) throw new InvalidOperationException("Asset changed after preview: " + c.Path);
        if (plan.DraftChanged && (File.Exists(DraftPath) ? File.ReadAllText(DraftPath) : "") != plan.OriginalDraft) throw new InvalidOperationException("Transcendence draft changed after preview.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Import LOADED bullet workbook");
        try
        {
            foreach (var c in selected)
            {
                var data = AssetDatabase.LoadAssetAtPath<BulletData>(c.Path); Undo.RegisterCompleteObjectUndo(data, "Import bullet");
                using (var serialized = new SerializedObject(data))
                {
                    var body = Body(c.After);
                    foreach (string field in new[] { "displayName", "grade", "bulletType", "price" }.Concat(LevelFields).Concat(new[] { "effects", "conditionalEvents", "penetrationChances", "upgradeLevels" })) Set(serialized.FindProperty(field), body[field]);
                    if (c.Icon != null) serialized.FindProperty("cylinderIcon").objectReferenceValue = ImportIcon(c.Guid, c.Icon, data.CylinderIcon);
                    serialized.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            }
            if (plan.DraftChanged)
            {
                string temporary = DraftPath + ".writing"; File.WriteAllText(temporary, plan.Draft, new UTF8Encoding(false));
                if (File.Exists(DraftPath)) File.Replace(temporary, DraftPath, null); else File.Move(temporary, DraftPath);
            }
            Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group); foreach (var c in selected) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<BulletData>(c.Path)); throw; }
    }
    static void Set(SerializedProperty property, JToken value)
    {
        if (property == null) throw new InvalidDataException("Unknown serialized property.");
        if (property.propertyType == SerializedPropertyType.ObjectReference) return;
        if (value is JArray array) { property.arraySize = array.Count; for (int i = 0; i < array.Count; i++) Set(property.GetArrayElementAtIndex(i), array[i]); }
        else if (value is JObject obj) { foreach (var field in obj.Properties()) Set(property.FindPropertyRelative(field.Name), field.Value); }
        else switch (property.propertyType)
        {
            case SerializedPropertyType.String: property.stringValue = (string)value; break;
            case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
            case SerializedPropertyType.Float: property.floatValue = (float)value; break;
            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: property.intValue = (int)value; break;
            default: throw new InvalidDataException("Unsupported property: " + property.propertyPath);
        }
    }
    static Sprite ImportIcon(string guid, byte[] bytes, Sprite original)
    {
        const string folder = "Assets/Sprites/BulletWorkbook";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Sprites", "BulletWorkbook");
        string path = folder + "/" + guid + "_" + Hash(bytes).Substring(0, 16) + ".png";
        if (!File.Exists(path)) { File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = original == null ? 100 : original.pixelsPerUnit;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = original == null ? new Vector2(.5f, .5f) : new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidDataException("Sprite import failed.");
    }
    public static byte[] CaptureIcon(Sprite sprite)
    {
        if (sprite == null) return null;
        var previous = RenderTexture.active; var texture = sprite.texture;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Texture2D copy = null;
        try { Graphics.Blit(texture, rt); RenderTexture.active = rt; var rect = sprite.rect; copy = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false); copy.ReadPixels(rect, 0, 0); copy.Apply(); return copy.EncodeToPNG(); }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); if (copy != null) UnityEngine.Object.DestroyImmediate(copy); }
    }
    public static bool Equivalent(JToken a, JToken b)
    {
        if (a == null || b == null) return a == b;
        if (a is JObject ao && b is JObject bo) return ao.Count == bo.Count && ao.Properties().All(p => Equivalent(p.Value, bo[p.Name]));
        if (a is JArray aa && b is JArray ba) return aa.Count == ba.Count && aa.Zip(ba, Equivalent).All(x => x);
        if ((a.Type == JTokenType.Float || a.Type == JTokenType.Integer) && (b.Type == JTokenType.Float || b.Type == JTokenType.Integer)) return a.Type == JTokenType.Integer && b.Type == JTokenType.Integer ? (long)a == (long)b : (float)a == (float)b;
        return JToken.DeepEquals(a, b);
    }
    static void Diff(JToken a, JToken b, string path, List<string> lines)
    {
        if (Equivalent(a, b)) return;
        if (a is JObject ao && b is JObject bo) { foreach (var p in bo.Properties()) Diff(ao[p.Name], p.Value, path == "" ? p.Name : path + "." + p.Name, lines); }
        else if (a is JArray aa && b is JArray ba && aa.Count == ba.Count) { for (int i = 0; i < aa.Count; i++) Diff(aa[i], ba[i], path + "[" + i + "]", lines); }
        else lines.Add(path + ": " + Text(a) + " → " + Text(b));
    }
    public static int Integer(string value, int min, int max) { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < min || n > max) throw new InvalidDataException("Integer out of range [" + min + ", " + max + "]: " + value); return n; }
    public static float Number(string value, float min, float max) { if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) || float.IsNaN(n) || float.IsInfinity(n) || n < min || n > max) throw new InvalidDataException("Number out of range [" + min + ", " + max + "]: " + value); return n; }
    static bool Boolean(string value) { if (!bool.TryParse(value, out bool result)) throw new InvalidDataException("Use TRUE or FALSE: " + value); return result; }
    static string Required(string value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("Required: " + field);
    static int EnumValue<T>(string value) where T : struct { if (!Enum.TryParse<T>(value, out var parsed) || !Enum.IsDefined(typeof(T), parsed)) throw new InvalidDataException("Unknown " + typeof(T).Name + ": " + value); return Convert.ToInt32(parsed); }
}
