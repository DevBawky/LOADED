using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Deliberately limited to the tabular OOXML used by the authoring tools. Formulas
// are rejected on import: applying a stale cached result would silently alter data.
public sealed class LoadedWorkbook
{
    public sealed class Sheet
    {
        public string Name;
        public readonly List<string[]> Rows = new List<string[]>();
        public readonly Dictionary<int, byte[]> Images = new Dictionary<int, byte[]>();
        public Sheet(string name, params string[] headers) { Name = name; Rows.Add(headers); }
        public void Add(params object[] values) => Rows.Add(values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").ToArray());
        public int Column(string name)
        {
            int index = Array.IndexOf(Rows[0], name);
            if (index < 0) throw new InvalidDataException(Name + ": missing column " + name);
            return index;
        }
        public string Get(string[] row, string name) { int index = Column(name); return index < row.Length ? row[index] : ""; }
    }
    public readonly List<Sheet> Sheets = new List<Sheet>();
    public Sheet Add(string name, params string[] headers) { var sheet = new Sheet(name, headers); Sheets.Add(sheet); return sheet; }
    public Sheet Require(string name) => Sheets.SingleOrDefault(s => s.Name == name) ?? throw new InvalidDataException("Missing sheet: " + name);
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace Draw = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    static readonly XNamespace Art = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static XDocument Xml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Missing XLSX part: " + path);
        if (entry.Length > 32 * 1024 * 1024) throw new InvalidDataException("XLSX part is too large: " + path);
        using (var stream = entry.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    static string Resolve(string part, string target)
    {
        var uri = new Uri(new Uri("https://workbook.invalid/" + part), target);
        if (uri.Host != "workbook.invalid") throw new InvalidDataException("External workbook relationship is not supported.");
        return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
    }
    static Dictionary<string, string> Relationships(ZipArchive zip, string part)
    {
        string path = Path.GetDirectoryName(part).Replace('\\', '/') + "/_rels/" + Path.GetFileName(part) + ".rels";
        if (zip.GetEntry(path) == null) return new Dictionary<string, string>();
        return Xml(zip, path).Root.Elements(Pkg + "Relationship").ToDictionary(e => (string)e.Attribute("Id"), e =>
        {
            if ((string)e.Attribute("TargetMode") == "External") throw new InvalidDataException("External image/link is unsupported. Embed the image in the workbook.");
            return Resolve(part, (string)e.Attribute("Target"));
        });
    }
    public static LoadedWorkbook Read(string path)
    {
        var result = new LoadedWorkbook();
        // Sharing only for reads prevents consuming Excel's partially written file.
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
        {
            if (zip.Entries.Sum(e => e.Length) > 128L * 1024 * 1024) throw new InvalidDataException("Workbook exceeds 128 MB uncompressed.");
            var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? new string[0] : Xml(zip, "xl/sharedStrings.xml").Root.Elements(Main + "si").Select(e => string.Concat(e.Descendants(Main + "t").Select(t => t.Value))).ToArray();
            var rels = Relationships(zip, "xl/workbook.xml");
            foreach (var definition in Xml(zip, "xl/workbook.xml").Root.Element(Main + "sheets").Elements())
            {
                string name = (string)definition.Attribute("name");
                string part = rels[(string)definition.Attribute(Rel + "id")];
                var xml = Xml(zip, part);
                var sheet = new Sheet(name); sheet.Rows.Clear();
                foreach (var row in xml.Descendants(Main + "sheetData").Elements(Main + "row"))
                {
                    int rowIndex = (int)row.Attribute("r") - 1;
                    if (rowIndex > 100000) throw new InvalidDataException("Too many rows.");
                    while (sheet.Rows.Count <= rowIndex) sheet.Rows.Add(Array.Empty<string>());
                    var cells = new Dictionary<int, string>();
                    foreach (var cell in row.Elements(Main + "c"))
                    {
                        if (cell.Element(Main + "f") != null) throw new InvalidDataException(name + "!" + (string)cell.Attribute("r") + ": formulas are not importable; paste values.");
                        string reference = (string)cell.Attribute("r");
                        int col = 0;
                        foreach (char c in reference.TakeWhile(char.IsLetter)) col = col * 26 + c - 'A' + 1;
                        if (col > 1000) throw new InvalidDataException("Too many columns.");
                        string type = (string)cell.Attribute("t");
                        string value = (string)cell.Element(Main + "v") ?? "";
                        if (type == "s") value = shared[int.Parse(value, CultureInfo.InvariantCulture)];
                        if (type == "inlineStr") value = string.Concat(cell.Descendants(Main + "t").Select(e => e.Value));
                        if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                        cells.Add(col - 1, value);
                    }
                    var values = Enumerable.Repeat("", cells.Count == 0 ? 0 : cells.Keys.Max() + 1).ToArray();
                    foreach (var pair in cells) values[pair.Key] = pair.Value;
                    sheet.Rows[rowIndex] = values;
                }
                var drawing = xml.Root.Element(Main + "drawing");
                if (drawing != null)
                {
                    string drawingPart = Relationships(zip, part)[(string)drawing.Attribute(Rel + "id")];
                    var imageRels = Relationships(zip, drawingPart);
                    foreach (var anchor in Xml(zip, drawingPart).Root.Elements())
                    {
                        var pic = anchor.Element(Draw + "pic");
                        if (pic == null) continue;
                        var from = anchor.Element(Draw + "from");
                        if (from == null) throw new InvalidDataException(name + ": image must be anchored to a cell.");
                        int row = int.Parse(from.Element(Draw + "row").Value, CultureInfo.InvariantCulture);
                        int col = int.Parse(from.Element(Draw + "col").Value, CultureInfo.InvariantCulture);
                        if (col != 0) throw new InvalidDataException("Place icons in column A, one image per row. Edit bullet icons only on 아이콘.");
                        if (sheet.Images.ContainsKey(row)) throw new InvalidDataException("Multiple icons at row " + (row + 1));
                        var blip = pic.Descendants(Art + "blip").Single();
                        var image = zip.GetEntry(imageRels[(string)blip.Attribute(Rel + "embed")]);
                        using (var stream = image.Open()) using (var memory = new MemoryStream()) { stream.CopyTo(memory); sheet.Images.Add(row, memory.ToArray()); }
                    }
                }
                if (sheet.Rows.Count == 0) sheet.Rows.Add(Array.Empty<string>());
                result.Sheets.Add(sheet);
            }
        }
        return result;
    }
    public void Write(string path)
    {
        string temporary = path + ".writing";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
                var types = new XElement(content + "Types",
                    new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(content + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")));
                Action<string, string> addType = (p, t) => types.Add(new XElement(content + "Override", new XAttribute("PartName", "/" + p), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml." + t + "+xml")));
                addType("xl/workbook.xml", "sheet.main"); addType("xl/styles.xml", "styles");
                var definitions = new XElement(Main + "sheets"); var relationships = new XElement(Pkg + "Relationships");
                for (int index = 0; index < Sheets.Count; index++)
                {
                    var sheet = Sheets[index]; string number = (index + 1).ToString(); string part = "xl/worksheets/sheet" + number + ".xml";
                    definitions.Add(new XElement(Main + "sheet", new XAttribute("name", sheet.Name), new XAttribute("sheetId", number), new XAttribute(Rel + "id", "rId" + number)));
                    relationships.Add(Relationship("rId" + number, "worksheet", "worksheets/sheet" + number + ".xml")); addType(part, "worksheet");
                    var rows = new XElement(Main + "sheetData");
                    for (int r = 0; r < sheet.Rows.Count; r++)
                    {
                        int height = r == 0 ? 40 : sheet.Images.ContainsKey(r) ? 64 : sheet.Name == "현지화" || sheet.Name == "레벨" ? 90 : 30;
                        if (r > 0 && BulletWorkbookLayout.IsTypeSheet(sheet.Name)) height = Math.Max(height, 10 + 16 * sheet.Get(sheet.Rows[r], "설명").Split('\n').Sum(line => Math.Max(1, (line.Length + 35) / 36)));
                        var row = new XElement(Main + "row", new XAttribute("r", r + 1), new XAttribute("ht", height), new XAttribute("customHeight", 1));
                        for (int c = 0; c < sheet.Rows[r].Length; c++)
                        {
                            string value = sheet.Rows[r][c] ?? "";
                            var cell = new XElement(Main + "c", new XAttribute("r", ColumnName(c) + (r + 1)), new XAttribute("s", r == 0 ? 1 : 0));
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) && value.Length < 16 && !value.StartsWith("0") && !double.IsNaN(numeric) && !double.IsInfinity(numeric)) cell.Add(new XElement(Main + "v", value));
                            else { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(Main + "is", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))); }
                            row.Add(cell);
                        }
                        rows.Add(row);
                    }
                    var root = new XElement(Main + "worksheet", new XAttribute(XNamespace.Xmlns + "r", Rel),
                        new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", 0), new XElement(Main + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                        new XElement(Main + "cols", sheet.Rows[0].Select((h, c) => new XElement(Main + "col", new XAttribute("min", c + 1), new XAttribute("max", c + 1), new XAttribute("width", h.Contains("설명") || h == "ko" || h == "en" ? 65 : h.Contains("GUID") || h == "key" || h == "value" ? 38 : 20), new XAttribute("customWidth", 1)))), rows);
                    if (sheet.Rows.Count > 1) root.Add(new XElement(Main + "autoFilter", new XAttribute("ref", "A1:" + ColumnName(sheet.Rows[0].Length - 1) + sheet.Rows.Count)));
                    if (sheet.Images.Count > 0)
                    {
                        string drawingPart = "xl/drawings/drawing" + number + ".xml";
                        types.Add(new XElement(content + "Override", new XAttribute("PartName", "/" + drawingPart), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.drawing+xml")));
                        root.Add(new XElement(Main + "drawing", new XAttribute(Rel + "id", "drawing")));
                        Put(zip, "xl/worksheets/_rels/sheet" + number + ".xml.rels", new XElement(Pkg + "Relationships", Relationship("drawing", "drawing", "../drawings/drawing" + number + ".xml")));
                        var drawing = new XElement(Draw + "wsDr", new XAttribute(XNamespace.Xmlns + "a", Art), new XAttribute(XNamespace.Xmlns + "r", Rel)); var imageRelations = new XElement(Pkg + "Relationships");
                        int imageIndex = 0;
                        foreach (var image in sheet.Images)
                        {
                            string id = "image" + (++imageIndex); string filename = "icon" + number + "_" + imageIndex + ".png";
                            using (var stream = zip.CreateEntry("xl/media/" + filename).Open()) stream.Write(image.Value, 0, image.Value.Length);
                            imageRelations.Add(Relationship(id, "image", "../media/" + filename));
                            drawing.Add(new XElement(Draw + "oneCellAnchor", new XElement(Draw + "from", new XElement(Draw + "col", 0), new XElement(Draw + "colOff", 38100), new XElement(Draw + "row", image.Key), new XElement(Draw + "rowOff", 38100)),
                                new XElement(Draw + "ext", new XAttribute("cx", 609600), new XAttribute("cy", 609600)),
                                new XElement(Draw + "pic", new XElement(Draw + "nvPicPr", new XElement(Draw + "cNvPr", new XAttribute("id", imageIndex), new XAttribute("name", id)), new XElement(Draw + "cNvPicPr")),
                                    new XElement(Draw + "blipFill", new XElement(Art + "blip", new XAttribute(Rel + "embed", id)), new XElement(Art + "stretch", new XElement(Art + "fillRect"))),
                                    new XElement(Draw + "spPr", new XElement(Art + "prstGeom", new XAttribute("prst", "rect"), new XElement(Art + "avLst")))), new XElement(Draw + "clientData")));
                        }
                        Put(zip, drawingPart, drawing); Put(zip, "xl/drawings/_rels/drawing" + number + ".xml.rels", imageRelations);
                    }
                    Put(zip, part, root);
                }
                relationships.Add(Relationship("styles", "styles", "styles.xml"));
                Put(zip, "xl/workbook.xml", new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel), definitions));
                Put(zip, "xl/_rels/workbook.xml.rels", relationships);
                Put(zip, "_rels/.rels", new XElement(Pkg + "Relationships", Relationship("rId1", "officeDocument", "xl/workbook.xml")));
                Put(zip, "[Content_Types].xml", types);
                Put(zip, "xl/styles.xml", XElement.Parse("<styleSheet xmlns='" + Main + "'><fonts count='2'><font><sz val='11'/><name val='Malgun Gothic'/></font><font><b/><color rgb='FFFFFFFF'/><sz val='11'/><name val='Malgun Gothic'/></font></fonts><fills count='3'><fill><patternFill patternType='none'/></fill><fill><patternFill patternType='gray125'/></fill><fill><patternFill patternType='solid'><fgColor rgb='FF28364B'/><bgColor indexed='64'/></patternFill></fill></fills><borders count='1'><border/></borders><cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs><cellXfs count='2'><xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0' applyAlignment='1'><alignment vertical='top' wrapText='1'/></xf><xf numFmtId='0' fontId='1' fillId='2' borderId='0' xfId='0' applyAlignment='1'><alignment vertical='center' wrapText='1'/></xf></cellXfs></styleSheet>"));
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            if (Sheets.Any(s => s.Name == "아이콘")) BulletWorkbookLayout.ProtectViews(path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    static XElement Relationship(string id, string type, string target) => new XElement(Pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", Rel.NamespaceName + "/" + type), new XAttribute("Target", target));
    static void Put(ZipArchive zip, string path, XElement xml) { using (var stream = zip.CreateEntry(path).Open()) new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(stream); }
    public static string ColumnName(int index) { string result = ""; for (index++; index > 0; index = (index - 1) / 26) result = (char)('A' + (index - 1) % 26) + result; return result; }
}
