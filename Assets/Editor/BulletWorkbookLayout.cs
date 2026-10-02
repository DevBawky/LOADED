using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

// Presentation rows are converted to the existing validated authoring model.
// Icon ownership stays in one sheet; repeated pictures never become input data.
public static class BulletWorkbookLayout
{
    public const string Schema = "LOADED.Bullets.2";
    public static readonly string[] TypeNames = { "일반형", "유령형", "저격형", "폭풍형", "샷건형", "관통형", "디버프형" };
    public static readonly string[] Types = { "Normal", "Ghost", "Sniper", "Storm", "Shotgun", "Piercing", "Debuff" };
    static readonly string[] Grades = { "Normal", "Rare", "Ace", "Legendary" };
    static readonly string[] Headers = { "아이콘", "이름", "등급", "레벨", "피해", "사거리", "치명타 확률 (%)", "치명타 배율", "발수 (0=기본값)", "턴 소모 없음", "반동", "다음 강화 비용", "가격", "설명", "GUID" };
    public static bool IsTypeSheet(string name) => TypeNames.Contains(name);
    public static LoadedWorkbook Group(LoadedWorkbook source)
    {
        var result = new LoadedWorkbook();
        var guide = result.Add("사용 안내", "항목", "설명");
        guide.Add("스탯 편집", "유형별 탭에서 Normal → Rare → Ace → Legendary 순서로 찾으세요. 탄환 하나의 기본·+1·+2·+3 스탯을 네 줄로 비교합니다.");
        guide.Add("입력 셀", "노란 셀을 편집합니다. 이름·등급·구매 가격은 기본(0) 행에서 한 번만 입력합니다. 피해부터 설명까지는 각 레벨별 값입니다.");
        guide.Add("아이콘 변경", "아이콘 탭 A열의 기존 그림을 그림 변경으로 교체하세요. 유형별 탭의 그림은 잠긴 미리보기이며 가져오기 대상이 아닙니다.");
        guide.Add("미리보기 갱신", "유형 탭 그림은 내보내기 시점의 이미지입니다. 아이콘 적용 후 새로 내보내면 모든 유형 탭에 교체한 이미지가 표시됩니다.");
        guide.Add("게임 반영", "저장 후 Unity의 Tools > LOADED > Bullet Balance에서 검증·선택 적용합니다. Excel 저장만으로 자동 반영되지 않습니다.");
        guide.Add("공통 규칙", "발수 0은 강화 레벨에서 기본 발수를 상속합니다. 치명타 확률은 0~100입니다. GUID·레벨·회색 셀은 수정하지 마세요.");
        guide.Add("고급 데이터", "효과·조건·관통 탭은 기존 능력 설정입니다. 초월 준비 탭은 설계값이며 현재 게임에 반영되지 않습니다.");
        var bullets = source.Require("탄환"); var levels = source.Require("레벨");
        var rows = bullets.Rows.Skip(1).Select((row, index) => new { Row = row, Index = index + 1 }).ToArray();
        foreach (int type in Enumerable.Range(0, Types.Length))
        {
            var sheet = result.Add(TypeNames[type], Headers);
            foreach (string grade in Grades)
            {
                var group = rows.Where(b => bullets.Get(b.Row, "발사 유형") == Types[type] && bullets.Get(b.Row, "등급") == grade).OrderBy(b => bullets.Get(b.Row, "이름"), StringComparer.Ordinal).ToArray();
                if (group.Length == 0) continue;
                var section = new string[Headers.Length]; section[1] = grade + " 등급"; sheet.Add(section);
                foreach (var bullet in group)
                {
                    string guid = bullets.Get(bullet.Row, "GUID");
                    foreach (var level in levels.Rows.Skip(1).Where(r => levels.Get(r, "GUID") == guid).OrderBy(r => int.Parse(levels.Get(r, "레벨"))))
                    {
                        bool basic = levels.Get(level, "레벨") == "0";
                        var values = Headers.Select(h => h == "아이콘" ? "" : h == "이름" || h == "등급" || h == "가격" ? basic ? bullets.Get(bullet.Row, h) : "" : levels.Get(level, h)).ToArray();
                        sheet.Add(values);
                        if (basic && bullets.Images.TryGetValue(bullet.Index, out byte[] image)) sheet.Images.Add(sheet.Rows.Count - 1, image);
                    }
                }
            }
            if (sheet.Rows.Count == 1) { var empty = new string[Headers.Length]; empty[1] = "등록된 탄환 없음"; sheet.Add(empty); }
        }
        var icons = result.Add("아이콘", "아이콘", "이름", "발사 유형", "등급", "GUID");
        foreach (var bullet in rows.OrderBy(b => Array.IndexOf(Types, bullets.Get(b.Row, "발사 유형"))).ThenBy(b => Array.IndexOf(Grades, bullets.Get(b.Row, "등급"))).ThenBy(b => bullets.Get(b.Row, "이름"), StringComparer.Ordinal))
        {
            icons.Add("", bullets.Get(bullet.Row, "이름"), bullets.Get(bullet.Row, "발사 유형"), bullets.Get(bullet.Row, "등급"), bullets.Get(bullet.Row, "GUID"));
            if (bullets.Images.TryGetValue(bullet.Index, out byte[] image)) icons.Images.Add(icons.Rows.Count - 1, image);
        }
        foreach (string name in new[] { "초월 준비", "효과", "조건", "관통", "메타데이터" }) result.Sheets.Add(source.Require(name));
        result.Require("메타데이터").Rows.Single(r => r[0] == "schema")[1] = Schema;
        return result;
    }
    public static LoadedWorkbook Flatten(LoadedWorkbook source)
    {
        if (source.Require("메타데이터").Rows.Single(r => r[0] == "schema")[1] != Schema) return source;
        var result = new LoadedWorkbook();
        var bullets = result.Add("탄환", "아이콘", "이름", "등급", "발사 유형", "가격", "GUID");
        var levels = result.Add("레벨", new[] { "이름", "레벨" }.Concat(BulletBalanceWorkbook.LevelHeaders).Concat(new[] { "GUID" }).ToArray());
        var meta = source.Require("메타데이터").Rows.Skip(1).ToDictionary(r => r[0], r => r[1]);
        var icons = source.Require("아이콘");
        var iconRows = icons.Rows.Skip(1).Select((row, index) => new { Row = row, Index = index + 1 }).Where(r => r.Row.Any(v => !string.IsNullOrEmpty(v))).ToDictionary(r => icons.Get(r.Row, "GUID"));
        if (icons.Images.Keys.Except(iconRows.Values.Select(r => r.Index)).Any()) throw new InvalidDataException("아이콘 탭의 그림은 탄환 행에 배치하세요.");
        var seen = new HashSet<string>();
        foreach (int type in Enumerable.Range(0, Types.Length))
        {
            var sheet = source.Require(TypeNames[type]);
            for (int index = 1; index < sheet.Rows.Count; index++)
            {
                var row = sheet.Rows[index]; string guid = sheet.Get(row, "GUID");
                if (guid == "")
                {
                    if (row.Where((v, c) => c != 1).Any(v => !string.IsNullOrEmpty(v)) || sheet.Images.ContainsKey(index)) throw new InvalidDataException(sheet.Name + ": missing GUID at row " + (index + 1));
                    continue;
                }
                string level = sheet.Get(row, "레벨");
                if (level == "0")
                {
                    if (!seen.Add(guid)) throw new InvalidDataException("Duplicate bullet: " + guid);
                    bullets.Add("", sheet.Get(row, "이름"), sheet.Get(row, "등급"), Types[type], sheet.Get(row, "가격"), guid);
                    if (!iconRows.TryGetValue(guid, out var iconRow)) throw new InvalidDataException("아이콘 탭에 없는 탄환: " + guid);
                    if (icons.Images.TryGetValue(iconRow.Index, out byte[] image)) bullets.Images.Add(bullets.Rows.Count - 1, image);
                    string baseline = meta["icon." + guid];
                    string preview = sheet.Images.TryGetValue(index, out byte[] view) ? BulletBalanceWorkbook.Hash(view) : "";
                    if (preview != baseline) throw new InvalidDataException(sheet.Name + ": 미리보기 그림이 변경되었습니다. 아이콘은 아이콘 탭에서만 변경하세요.");
                }
                else if (sheet.Images.ContainsKey(index) || new[] { "이름", "등급", "가격" }.Any(h => sheet.Get(row, h) != "")) throw new InvalidDataException(sheet.Name + ": 이름·등급·가격·그림은 기본(0) 행에만 둡니다.");
                levels.Add(new[] { sheet.Get(row, "이름"), level }.Concat(BulletBalanceWorkbook.LevelHeaders.Select(h => sheet.Get(row, h))).Concat(new[] { guid }).ToArray());
            }
        }
        if (!seen.SetEquals(iconRows.Keys)) throw new InvalidDataException("아이콘 탭과 유형별 탭의 탄환 목록이 다릅니다.");
        foreach (string name in new[] { "효과", "조건", "관통", "초월 준비" }) result.Sheets.Add(source.Require(name));
        var metadata = result.Add("메타데이터", "key", "value");
        foreach (var pair in meta) metadata.Add(pair.Key, pair.Key == "schema" ? BulletBalanceWorkbook.Schema : pair.Value);
        return result;
    }

    // The artifact renderer has no worksheet-protection API. Patch only protection,
    // locked-cell styles and column visibility; values and drawings remain intact.
    public static void ProtectViews(string path)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            Func<string, XDocument> read = name => { using (var stream = zip.GetEntry(name).Open()) return XDocument.Load(stream); };
            Action<string, XDocument> write = (name, doc) => { zip.GetEntry(name).Delete(); using (var stream = zip.CreateEntry(name).Open()) doc.Save(stream); };
            var styles = read("xl/styles.xml"); var xfs = styles.Root.Element(ns + "cellXfs");
            var unlocked = new Dictionary<int, int>();
            Func<int, int> unlock = id =>
            {
                if (unlocked.TryGetValue(id, out int value)) return value;
                var clone = new XElement(xfs.Elements().ElementAt(id)); clone.Element(ns + "protection")?.Remove();
                clone.SetAttributeValue("applyProtection", 1); clone.Add(new XElement(ns + "protection", new XAttribute("locked", 0)));
                value = xfs.Elements().Count(); xfs.Add(clone); unlocked.Add(id, value); return value;
            };
            var workbook = read("xl/workbook.xml");
            var relationships = read("xl/_rels/workbook.xml.rels").Root.Elements(pkg + "Relationship").ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
            foreach (var def in workbook.Descendants(ns + "sheet"))
            {
                string name = (string)def.Attribute("name");
                if (name == "메타데이터") { def.SetAttributeValue("state", "hidden"); continue; }
                if (!IsTypeSheet(name)) continue;
                string target = relationships[(string)def.Attribute(rel + "id")];
                string part = new Uri(new Uri("https://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                var doc = read(part); var data = doc.Root.Element(ns + "sheetData");
                var pane = doc.Descendants(ns + "pane").FirstOrDefault();
                if (pane != null) { pane.SetAttributeValue("xSplit", 4); pane.SetAttributeValue("ySplit", 1); pane.SetAttributeValue("topLeftCell", "E2"); pane.SetAttributeValue("activePane", "bottomRight"); }
                foreach (var row in data.Elements(ns + "row").Skip(1))
                {
                    var cells = row.Elements(ns + "c").ToArray();
                    string value = cells.FirstOrDefault(c => ((string)c.Attribute("r")).StartsWith("D"))?.Element(ns + "v")?.Value;
                    // Inline strings are used by the native exporter, shared strings by Excel.
                    bool basic = value == "0" || cells.Any(c => ((string)c.Attribute("r")).StartsWith("D") && c.Descendants(ns + "t").Any(t => t.Value == "0"));
                    bool hasGuid = cells.Any(c => ((string)c.Attribute("r")).StartsWith("O") && (c.Element(ns + "v") != null || c.Descendants(ns + "t").Any(t => t.Value != "")));
                    if (!hasGuid) continue;
                    foreach (var cell in cells)
                    {
                        string col = new string(((string)cell.Attribute("r")).TakeWhile(char.IsLetter).ToArray());
                        if (new[] { "E", "F", "G", "H", "I", "J", "K", "L", "N" }.Contains(col) || basic && new[] { "B", "C", "M" }.Contains(col)) cell.SetAttributeValue("s", unlock((int?)cell.Attribute("s") ?? 0));
                    }
                }
                doc.Root.Element(ns + "sheetProtection")?.Remove();
                data.AddAfterSelf(new XElement(ns + "sheetProtection", new XAttribute("sheet", 1), new XAttribute("objects", 1), new XAttribute("scenarios", 1), new XAttribute("formatColumns", 0), new XAttribute("formatRows", 0)));
                var cols = doc.Root.Element(ns + "cols");
                // Split any wide column span so only the technical GUID is hidden.
                if (cols != null) foreach (var column in cols.Elements().ToArray())
                {
                    int min = (int)column.Attribute("min"), max = (int)column.Attribute("max");
                    if (min > 15 || max < 15) continue;
                    if (min < 15) { var left = new XElement(column); left.SetAttributeValue("max", 14); column.AddBeforeSelf(left); }
                    if (max > 15) { var right = new XElement(column); right.SetAttributeValue("min", 16); column.AddAfterSelf(right); }
                    column.SetAttributeValue("min", 15); column.SetAttributeValue("max", 15); column.SetAttributeValue("hidden", 1);
                }
                write(part, doc);
            }
            xfs.SetAttributeValue("count", xfs.Elements().Count()); write("xl/styles.xml", styles); write("xl/workbook.xml", workbook);
        }
    }
}
