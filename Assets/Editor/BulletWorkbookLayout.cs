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
    public const string Schema = "LOADED.Bullets.3";
    public const string LegacySchema = "LOADED.Bullets.2";
    public static readonly string[] TypeNames =
    {
        "표준형", "유령형", "저격형", "폭풍형", "산탄형", "관통형",
        "상태이상형", "기동형", "연계형", "경제형", "성장형", "혈투형"
    };
    public static readonly string[] Types =
    {
        "Normal", "Ghost", "Sniper", "Storm", "Shotgun", "Piercing",
        "Debuff", "Kinetic", "Combo", "Economy", "Growth", "Blood"
    };
    static readonly string[] TypeDescriptions =
    {
        "기본 전투 성능과 범용 효과를 갖춘 탄환 유형입니다.",
        "턴 소모 완화, 귀환, 무덤 상호작용을 활용하는 탄환 유형입니다.",
        "장거리 조준과 특정 대상 집중 공격에 특화된 탄환 유형입니다.",
        "여러 적이나 레인을 동시에 공격하는 광역 탄환 유형입니다.",
        "여러 팰릿을 근거리에서 분산 또는 집중 발사하는 탄환 유형입니다.",
        "같은 레인의 여러 대상을 꿰뚫거나 왕복 경로를 공격하는 탄환 유형입니다.",
        "독·표식·약화·기절 등 상태이상을 부여하고 증폭하는 탄환 유형입니다.",
        "회전·이동·밀치기 등 위치와 방향을 바꾸는 탄환 유형입니다.",
        "이전·다음 탄환과 장전 순서를 활용해 효과를 이어가는 탄환 유형입니다.",
        "골드 획득과 보유 자원을 전투 성능으로 전환하는 탄환 유형입니다.",
        "보유 탄환 구성과 전투 진행에 따라 성능이 커지는 탄환 유형입니다.",
        "체력을 비용·회복·피해 증폭에 활용하는 고위험 탄환 유형입니다."
    };
    static readonly string[] LegacyTypeNames =
    {
        "일반형", "유령형", "저격형", "폭풍형", "샷건형", "관통형",
        "디버프형"
    };
    static readonly string[] LegacyTypes =
    {
        "Normal", "Ghost", "Sniper", "Storm", "Shotgun", "Piercing",
        "Debuff"
    };
    static readonly string[] Grades = { "Normal", "Rare", "Ace", "Legendary" };
    const string MotifBorderColor = "FF7B8490";
    static readonly string[] Headers = { "아이콘", "이름", "등급", "설명", "레벨", "피해", "사거리", "치명타 확률 (%)", "치명타 배율", "발수 (0=기본값)", "턴 소모 없음", "반동", "다음 강화 비용", "가격", "GUID" };
    public static bool IsTypeSheet(string name) =>
        TypeNames.Contains(name) || LegacyTypeNames.Contains(name);
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
        string schema = source.Require("메타데이터").Rows
            .Single(r => r[0] == "schema")[1];
        if (schema != Schema && schema != LegacySchema) return source;
        string[] typeNames = schema == Schema ? TypeNames : LegacyTypeNames;
        string[] types = schema == Schema ? Types : LegacyTypes;
        var result = new LoadedWorkbook();
        var bullets = result.Add("탄환", "아이콘", "이름", "등급", "발사 유형", "가격", "GUID");
        var levels = result.Add("레벨", new[] { "이름", "레벨" }.Concat(BulletBalanceWorkbook.LevelHeaders).Concat(new[] { "GUID" }).ToArray());
        var meta = source.Require("메타데이터").Rows.Skip(1).ToDictionary(r => r[0], r => r[1]);
        var icons = source.Require("아이콘");
        var iconRows = icons.Rows.Skip(1).Select((row, index) => new { Row = row, Index = index + 1 }).Where(r => r.Row.Any(v => !string.IsNullOrEmpty(v))).ToDictionary(r => icons.Get(r.Row, "GUID"));
        if (icons.Images.Keys.Except(iconRows.Values.Select(r => r.Index)).Any()) throw new InvalidDataException("아이콘 탭의 그림은 탄환 행에 배치하세요.");
        var seen = new HashSet<string>();
        foreach (int type in Enumerable.Range(0, types.Length))
        {
            var sheet = NormalizeTypeSheet(source.Require(typeNames[type]));
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
                    bullets.Add("", sheet.Get(row, "이름"), sheet.Get(row, "등급"), types[type], sheet.Get(row, "가격"), guid);
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

    static LoadedWorkbook.Sheet NormalizeTypeSheet(LoadedWorkbook.Sheet source)
    {
        int headerIndex = source.Rows.FindIndex(row => Headers.All(header => row.Contains(header)));
        if (headerIndex <= 0)
            return source;

        var normalized = new LoadedWorkbook.Sheet(
            source.Name,
            (string[])source.Rows[headerIndex].Clone());
        for (int index = headerIndex + 1; index < source.Rows.Count; index++)
            normalized.Rows.Add((string[])source.Rows[index].Clone());
        foreach (var image in source.Images.Where(pair => pair.Key > headerIndex))
            normalized.Images.Add(image.Key - headerIndex, image.Value);
        return normalized;
    }

    // The artifact renderer has no worksheet-protection API. Patch the type-sheet
    // presentation, protection, editable styles, and column visibility without changing data.
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
            var borders = styles.Root.Element(ns + "borders");
            var fonts = styles.Root.Element(ns + "fonts");
            int titleFont = fonts.Elements().Count();
            fonts.Add(new XElement(ns + "font",
                new XElement(ns + "b"),
                new XElement(ns + "color", new XAttribute("rgb", "FF243447")),
                new XElement(ns + "sz", new XAttribute("val", 11)),
                new XElement(ns + "name", new XAttribute("val", "Malgun Gothic"))));
            fonts.SetAttributeValue("count", fonts.Elements().Count());
            var fills = styles.Root.Element(ns + "fills");
            Func<string, int> addFill = color =>
            {
                int id = fills.Elements().Count();
                fills.Add(new XElement(ns + "fill",
                    new XElement(ns + "patternFill",
                        new XAttribute("patternType", "solid"),
                        new XElement(ns + "fgColor", new XAttribute("rgb", color)),
                        new XElement(ns + "bgColor", new XAttribute("indexed", 64)))));
                return id;
            };
            int descriptionFill = addFill("FFEAF0F7");
            var gradeFills = new Dictionary<string, int>
            {
                { "Normal", addFill("FFF2F4F7") },
                { "Rare", addFill("FFE8F2FF") },
                { "Ace", addFill("FFFFF3D6") },
                { "Legendary", addFill("FFF3E8FF") }
            };
            fills.SetAttributeValue("count", fills.Elements().Count());
            Func<int, int, int, int> copyStyle = (source, font, fill) =>
            {
                var clone = new XElement(xfs.Elements().ElementAt(source));
                clone.SetAttributeValue("fontId", font);
                clone.SetAttributeValue("fillId", fill);
                clone.SetAttributeValue("applyFont", 1);
                clone.SetAttributeValue("applyFill", 1);
                int id = xfs.Elements().Count();
                xfs.Add(clone);
                return id;
            };
            int descriptionStyle = copyStyle(0, titleFont, descriptionFill);
            xfs.Elements().ElementAt(descriptionStyle).Element(ns + "alignment")
                ?.SetAttributeValue("vertical", "center");
            var unlocked = new Dictionary<int, int>();
            Func<int, int> unlock = id =>
            {
                if (unlocked.TryGetValue(id, out int value)) return value;
                var clone = new XElement(xfs.Elements().ElementAt(id)); clone.Element(ns + "protection")?.Remove();
                clone.SetAttributeValue("applyProtection", 1); clone.Add(new XElement(ns + "protection", new XAttribute("locked", 0)));
                value = xfs.Elements().Count(); xfs.Add(clone); unlocked.Add(id, value); return value;
            };
            var tinted = new Dictionary<string, int>();
            Func<int, int, int> tint = (style, fill) =>
            {
                string key = style + ":" + fill;
                if (tinted.TryGetValue(key, out int value)) return value;
                value = copyStyle(style, (int)xfs.Elements().ElementAt(style).Attribute("fontId"), fill);
                tinted.Add(key, value);
                return value;
            };
            var sectionStyles = gradeFills.ToDictionary(
                pair => pair.Key,
                pair => copyStyle(0, titleFont, pair.Value));
            var clearedBorders = new Dictionary<int, int>();
            Func<int, int> clearBorder = style =>
            {
                var sourceStyle = xfs.Elements().ElementAt(style);
                if ((int?)sourceStyle.Attribute("borderId") == 0) return style;
                if (clearedBorders.TryGetValue(style, out int value)) return value;
                var clone = new XElement(sourceStyle);
                clone.SetAttributeValue("borderId", 0);
                clone.SetAttributeValue("applyBorder", 1);
                value = xfs.Elements().Count();
                xfs.Add(clone);
                clearedBorders.Add(style, value);
                return value;
            };
            var decoratedBorders = new Dictionary<string, int>();
            Func<int, string, bool, bool, bool, bool, bool, bool, int> decorate =
                (style, lineStyle, top, bottom, left, right, diagonalUp, diagonalDown) =>
            {
                if (!top && !bottom && !left && !right && !diagonalUp && !diagonalDown) return style;
                string key = string.Join(":", style, lineStyle, top, bottom, left, right, diagonalUp, diagonalDown);
                if (decoratedBorders.TryGetValue(key, out int value)) return value;
                Func<string, bool, XElement> edge = (name, enabled) => enabled
                    ? new XElement(ns + name,
                        new XAttribute("style", lineStyle),
                        new XElement(ns + "color", new XAttribute("rgb", MotifBorderColor)))
                    : new XElement(ns + name);
                int borderId = borders.Elements().Count();
                var border = new XElement(ns + "border",
                    edge("left", left), edge("right", right), edge("top", top),
                    edge("bottom", bottom),
                    diagonalUp || diagonalDown
                        ? new XElement(ns + "diagonal",
                            new XAttribute("style", lineStyle),
                            new XElement(ns + "color", new XAttribute("rgb", MotifBorderColor)))
                        : new XElement(ns + "diagonal"));
                if (diagonalUp) border.SetAttributeValue("diagonalUp", 1);
                if (diagonalDown) border.SetAttributeValue("diagonalDown", 1);
                borders.Add(border);
                var clone = new XElement(xfs.Elements().ElementAt(style));
                clone.SetAttributeValue("borderId", borderId);
                clone.SetAttributeValue("applyBorder", 1);
                value = xfs.Elements().Count();
                xfs.Add(clone);
                decoratedBorders.Add(key, value);
                return value;
            };
            var workbook = read("xl/workbook.xml");
            var sharedStrings = zip.GetEntry("xl/sharedStrings.xml") == null
                ? Array.Empty<string>()
                : read("xl/sharedStrings.xml").Root.Elements(ns + "si")
                    .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
                    .ToArray();
            var relationships = read("xl/_rels/workbook.xml.rels").Root.Elements(pkg + "Relationship").ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
            foreach (var def in workbook.Descendants(ns + "sheet"))
            {
                string name = (string)def.Attribute("name");
                if (name == "메타데이터") { def.SetAttributeValue("state", "hidden"); continue; }
                if (!IsTypeSheet(name)) continue;
                string target = relationships[(string)def.Attribute(rel + "id")];
                string part = new Uri(new Uri("https://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                var doc = read(part); var data = doc.Root.Element(ns + "sheetData");
                Func<XElement, string> cellValue = cell =>
                {
                    if (cell == null) return "";
                    string value = cell.Element(ns + "v")?.Value;
                    if ((string)cell.Attribute("t") == "s" &&
                        int.TryParse(value, out int sharedIndex) &&
                        sharedIndex >= 0 && sharedIndex < sharedStrings.Length)
                        return sharedStrings[sharedIndex];
                    return value ?? string.Concat(cell.Descendants(ns + "t").Select(text => text.Value));
                };
                string descriptionPrefix = name + " — ";
                var initialRows = data.Elements(ns + "row").Take(2).ToArray();
                bool removedDuplicateDescriptionRow = initialRows.Length == 2 &&
                    cellValue(initialRows[0].Elements(ns + "c").FirstOrDefault()).StartsWith(descriptionPrefix, StringComparison.Ordinal) &&
                    cellValue(initialRows[1].Elements(ns + "c").FirstOrDefault()).StartsWith(descriptionPrefix, StringComparison.Ordinal);
                if (removedDuplicateDescriptionRow)
                {
                    initialRows[1].Remove();
                    foreach (var row in data.Elements(ns + "row").Where(row => (int)row.Attribute("r") >= 3).ToArray())
                    {
                        row.SetAttributeValue("r", (int)row.Attribute("r") - 1);
                        foreach (var cell in row.Elements(ns + "c"))
                        {
                            string reference = (string)cell.Attribute("r");
                            int split = 0;
                            while (split < reference.Length && char.IsLetter(reference[split])) split++;
                            cell.SetAttributeValue("r", reference.Substring(0, split) +
                                (int.Parse(reference.Substring(split), System.Globalization.CultureInfo.InvariantCulture) - 1));
                        }
                    }
                }
                bool hasDescriptionRow = cellValue(data.Elements(ns + "row").FirstOrDefault()?.Elements(ns + "c").FirstOrDefault())
                    .StartsWith(descriptionPrefix, StringComparison.Ordinal);
                if (!hasDescriptionRow) foreach (var row in data.Elements(ns + "row").ToArray())
                {
                    row.SetAttributeValue("r", (int)row.Attribute("r") + 1);
                    foreach (var cell in row.Elements(ns + "c"))
                    {
                        string reference = (string)cell.Attribute("r");
                        int split = 0;
                        while (split < reference.Length && char.IsLetter(reference[split])) split++;
                        cell.SetAttributeValue("r", reference.Substring(0, split) +
                            (int.Parse(reference.Substring(split), System.Globalization.CultureInfo.InvariantCulture) + 1));
                    }
                }
                int typeIndex = Array.IndexOf(TypeNames, name);
                string description = typeIndex >= 0 ? TypeDescriptions[typeIndex] : "탄환 유형별 능력치와 레벨 정보를 확인합니다.";
                if (!hasDescriptionRow)
                    data.AddFirst(new XElement(ns + "row",
                        new XAttribute("r", 1),
                        new XAttribute("ht", 38),
                        new XAttribute("customHeight", 1),
                        new XElement(ns + "c",
                            new XAttribute("r", "A1"),
                            new XAttribute("s", descriptionStyle),
                            new XAttribute("t", "inlineStr"),
                            new XElement(ns + "is",
                                new XElement(ns + "t",
                                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                                    name + " — " + description)))));
                bool usesMotifBorder = name == "저격형" || name == "경제형" || name == "혈투형";
                if (usesMotifBorder)
                {
                    var titleCell = data.Elements(ns + "row").First().Elements(ns + "c").First();
                    int titleStyle = (int?)titleCell.Attribute("s") ?? 0;
                    titleCell.SetAttributeValue("s", clearBorder(titleStyle));
                    foreach (var headerCell in data.Elements(ns + "row").Skip(1).First().Elements(ns + "c"))
                    {
                        int headerStyle = (int?)headerCell.Attribute("s") ?? 0;
                        headerCell.SetAttributeValue("s", clearBorder(headerStyle));
                    }
                }
                var mergeCells = doc.Root.Element(ns + "mergeCells");
                if (mergeCells == null)
                {
                    mergeCells = new XElement(ns + "mergeCells");
                    data.AddAfterSelf(mergeCells);
                }
                if (!mergeCells.Elements(ns + "mergeCell").Any(cell => (string)cell.Attribute("ref") == "A1:O1"))
                    mergeCells.Add(new XElement(ns + "mergeCell", new XAttribute("ref", "A1:O1")));
                mergeCells.SetAttributeValue("count", mergeCells.Elements().Count());
                var filter = doc.Root.Element(ns + "autoFilter");
                if (filter != null && (!hasDescriptionRow || removedDuplicateDescriptionRow))
                {
                    int rowDelta = hasDescriptionRow ? -1 : 1;
                    string[] cells = ((string)filter.Attribute("ref")).Split(':');
                    filter.SetAttributeValue("ref", string.Join(":", cells.Select(cell =>
                    {
                        int split = 0;
                        while (split < cell.Length && char.IsLetter(cell[split])) split++;
                        return cell.Substring(0, split) +
                            (int.Parse(cell.Substring(split), System.Globalization.CultureInfo.InvariantCulture) + rowDelta);
                    })));
                }
                var pane = doc.Descendants(ns + "pane").FirstOrDefault();
                if (pane != null) { pane.SetAttributeValue("xSplit", 4); pane.SetAttributeValue("ySplit", 2); pane.SetAttributeValue("topLeftCell", "E3"); pane.SetAttributeValue("activePane", "bottomRight"); }
                string currentGrade = "";
                foreach (var row in data.Elements(ns + "row").Skip(2))
                {
                    var cells = row.Elements(ns + "c").ToArray();
                    string section = cellValue(cells.FirstOrDefault(c => ((string)c.Attribute("r")).StartsWith("B")));
                    if (section.EndsWith(" 등급", StringComparison.Ordinal))
                    {
                        currentGrade = section.Substring(0, section.Length - " 등급".Length);
                        if (sectionStyles.TryGetValue(currentGrade, out int sectionStyle))
                            foreach (var cell in cells) cell.SetAttributeValue("s", sectionStyle);
                        continue;
                    }
                    string value = cellValue(cells.FirstOrDefault(c => ((string)c.Attribute("r")).StartsWith("E")));
                    // Inline strings are used by the native exporter, shared strings by Excel.
                    bool basic = value == "0" || cells.Any(c => ((string)c.Attribute("r")).StartsWith("E") && c.Descendants(ns + "t").Any(t => t.Value == "0"));
                    bool finalLevel = value == "3" || cells.Any(c => ((string)c.Attribute("r")).StartsWith("E") && c.Descendants(ns + "t").Any(t => t.Value == "3"));
                    bool hasGuid = cells.Any(c => ((string)c.Attribute("r")).StartsWith("O") && (c.Element(ns + "v") != null || c.Descendants(ns + "t").Any(t => t.Value != "")));
                    if (!hasGuid) continue;
                    string rowGrade = cellValue(cells.FirstOrDefault(c => ((string)c.Attribute("r")).StartsWith("C")));
                    if (rowGrade != "") currentGrade = rowGrade;
                    foreach (var cell in cells)
                    {
                        string col = new string(((string)cell.Attribute("r")).TakeWhile(char.IsLetter).ToArray());
                        int style = (int?)cell.Attribute("s") ?? 0;
                        if (new[] { "D", "F", "G", "H", "I", "J", "K", "L", "M" }.Contains(col) || basic && new[] { "B", "C", "N" }.Contains(col))
                            style = unlock(style);
                        if (gradeFills.TryGetValue(currentGrade, out int gradeFill))
                            style = tint(style, gradeFill);
                        if (usesMotifBorder)
                        {
                            style = clearBorder(style);
                            if (name == "저격형")
                            {
                                bool tickColumn = new[] { "A", "B", "H", "I", "M", "N" }.Contains(col);
                                style = decorate(style, "thin", basic && tickColumn, finalLevel && tickColumn,
                                    col == "A", col == "N", false, false);
                            }
                            else if (name == "경제형")
                            {
                                style = decorate(style, "double", basic, finalLevel,
                                    col == "A", col == "N", false, false);
                            }
                            else
                            {
                                bool topLeft = basic && col == "A";
                                bool topRight = basic && col == "N";
                                bool bottomLeft = finalLevel && col == "A";
                                bool bottomRight = finalLevel && col == "N";
                                style = decorate(style, "thin", basic, finalLevel,
                                    col == "A", col == "N",
                                    topRight || bottomLeft, topLeft || bottomRight);
                            }
                        }
                        cell.SetAttributeValue("s", style);
                    }
                }
                var drawingReference = doc.Root.Element(ns + "drawing");
                if (drawingReference != null && (!hasDescriptionRow || removedDuplicateDescriptionRow))
                {
                    int rowDelta = hasDescriptionRow ? -1 : 1;
                    string relsPath = Path.GetDirectoryName(part).Replace('\\', '/') + "/_rels/" + Path.GetFileName(part) + ".rels";
                    var sheetRels = read(relsPath);
                    string drawingTarget = (string)sheetRels.Root.Elements(pkg + "Relationship")
                        .Single(item => (string)item.Attribute("Id") == (string)drawingReference.Attribute(rel + "id"))
                        .Attribute("Target");
                    string drawingPart = new Uri(new Uri("https://local/" + part), drawingTarget).AbsolutePath.TrimStart('/');
                    var drawing = read(drawingPart);
                    foreach (var anchorRow in drawing.Descendants(XName.Get("row", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing")))
                        anchorRow.Value = (int.Parse(anchorRow.Value, System.Globalization.CultureInfo.InvariantCulture) + rowDelta).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    write(drawingPart, drawing);
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
            xfs.SetAttributeValue("count", xfs.Elements().Count());
            borders.SetAttributeValue("count", borders.Elements().Count());
            write("xl/styles.xml", styles); write("xl/workbook.xml", workbook);
        }
    }
}
