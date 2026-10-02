using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LoadedWorkbookTests
{
    [Test]
    public void GroupedWorkbook_RoundTripsAllStatsWithOneIconOwner()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var book = BulletBalanceWorkbook.Export(); book.Write(path);
            var read = LoadedWorkbook.Read(path); var plan = BulletBalanceWorkbook.Validate(read);
            Assert.That(plan.Errors, Is.Empty); Assert.That(plan.Changes, Is.Empty);
            var flat = BulletWorkbookLayout.Flatten(read);
            Assert.That(flat.Require("레벨").Rows.Count, Is.EqualTo(BulletBalanceWorkbook.ExportData().Require("레벨").Rows.Count));
            Assert.That(read.Sheets.Where(s => BulletWorkbookLayout.IsTypeSheet(s.Name)).Sum(s => s.Images.Count), Is.EqualTo(read.Require("아이콘").Images.Count));
            using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
            {
                AssertPictureProtection(zip);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private static void AssertPictureProtection(System.IO.Compression.ZipArchive zip)
    {
        System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        // Guide is sheet1; the seven type sheets are protected, the icon sheet is not.
        for (int i = 2; i <= 9; i++)
        {
            using (var stream = zip.GetEntry("xl/worksheets/sheet" + i + ".xml").Open())
            {
                var xml = System.Xml.Linq.XDocument.Load(stream);
                Assert.That(xml.Root.Element(ns + "sheetProtection") != null, Is.EqualTo(i <= 8));
            }
        }
    }
    [Test]
    public void GroupedWorkbook_RejectsChangingPreviewPictures()
    {
        var book = BulletBalanceWorkbook.Export();
        var sheet = book.Sheets.First(s => BulletWorkbookLayout.IsTypeSheet(s.Name) && s.Images.Count > 0);
        sheet.Images[sheet.Images.Keys.First()] = new byte[] { 1, 2, 3 };
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors.Any(e => e.Contains("아이콘 탭")), Is.True);
    }
    [Test]
    public void GroupedWorkbook_RejectsDuplicateOrMissingLevel()
    {
        var book = BulletBalanceWorkbook.Export();
        var sheet = book.Sheets.First(s => BulletWorkbookLayout.IsTypeSheet(s.Name) && s.Images.Count > 0);
        var row = sheet.Rows.First(r => sheet.Get(r, "레벨") == "1");
        sheet.Rows.Add((string[])row.Clone());
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors, Is.Not.Empty);
        sheet.Rows.RemoveAt(sheet.Rows.Count - 1); sheet.Rows.Remove(row);
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors, Is.Not.Empty);
    }
    [Test]
    public void ExportReadValidate_PreservesAllBulletsAndIcons()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var exported = BulletBalanceWorkbook.ExportData(); exported.Write(path);
            var imported = LoadedWorkbook.Read(path);
            var plan = BulletBalanceWorkbook.Validate(imported);
            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.Changes, Is.Empty);
            Assert.That(plan.DraftChanged, Is.False);
            Assert.That(imported.Require("탄환").Images.Count, Is.EqualTo(exported.Require("탄환").Images.Count));
            Assert.That(imported.Require("레벨").Rows.Count, Is.EqualTo(exported.Require("레벨").Rows.Count));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    [TestCase("피해", "-1")]
    [TestCase("피해", "1.5")]
    [TestCase("사거리", "11")]
    [TestCase("치명타 확률 (%)", "101")]
    [TestCase("치명타 확률 (%)", "NaN")]
    [TestCase("치명타 배율", "0.5")]
    [TestCase("다음 강화 비용", "")]
    public void InvalidStats_AreRejectedWithoutAssetChanges(string column, string value)
    {
        var book = BulletBalanceWorkbook.ExportData(); var sheet = book.Require("레벨");
        sheet.Rows[1][sheet.Column(column)] = value;
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors, Is.Not.Empty);
    }
    [Test]
    public void DuplicateLevel_IsRejected()
    {
        var book = BulletBalanceWorkbook.ExportData(); var sheet = book.Require("레벨"); sheet.Rows.Add((string[])sheet.Rows[1].Clone());
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors, Is.Not.Empty);
    }
    [Test]
    public void MissingIcon_IsRejected()
    {
        var book = BulletBalanceWorkbook.ExportData(); book.Require("탄환").Images.Clear();
        Assert.That(BulletBalanceWorkbook.Validate(book).Errors, Is.Not.Empty);
    }
    [Test]
    public void RemovingEffectRows_ProducesExplicitRemovalDiff()
    {
        var book = BulletBalanceWorkbook.ExportData(); var effects = book.Require("효과");
        string guid = effects.Get(effects.Rows[1], "GUID"), level = effects.Get(effects.Rows[1], "레벨"), group = effects.Get(effects.Rows[1], "조건 번호 (-1=일반)");
        effects.Rows.RemoveAll(r => r != effects.Rows[0] && effects.Get(r, "GUID") == guid && effects.Get(r, "레벨") == level && effects.Get(r, "조건 번호 (-1=일반)") == group);
        var plan = BulletBalanceWorkbook.Validate(book);
        Assert.That(plan.Errors, Is.Empty); Assert.That(plan.Changes.Count, Is.EqualTo(1)); Assert.That(plan.Changes[0].Lines.Any(l => l.Contains("effects")), Is.True);
    }
    [Test]
    public void Localization_PreservesVariablesTagsAndNewlines()
    {
        Assert.That(LoadedLocalizationWorkbook.ValidateText("피해 {0}\n<color=red>독 {1}</color>", "Damage {0}\n<color=red>Poison {1}</color>"), Is.Empty);
        Assert.That(LoadedLocalizationWorkbook.ValidateText("피해 {0}", "Damage"), Is.Not.Empty);
        Assert.That(LoadedLocalizationWorkbook.ValidateText("<b>독</b>", "Poison"), Is.Not.Empty);
        Assert.That(LoadedLocalizationWorkbook.ValidateText("독\n기절", "Poison\\nStun"), Is.Not.Empty);
    }
    [Test]
    public void LocalizationExport_RoundTripsWithoutChanges()
    {
        var plan = LoadedLocalizationWorkbook.Validate(LoadedLocalizationWorkbook.Export());
        Assert.That(plan.Errors, Is.Empty); Assert.That(plan.Entries, Is.Empty);
    }
    [Test]
    public void ChangedStatsAndIcon_ApplyAndUndoOnIsolatedAsset()
    {
        const string folder = "Assets/Editor/WorkbookTestData";
        string path = folder + "/TestBullet.asset", iconPath = null;
        if (AssetDatabase.IsValidFolder(folder)) Assert.Fail("Test folder already exists; refusing to touch it.");
        AssetDatabase.CreateFolder("Assets/Editor", "WorkbookTestData");
        var data = ScriptableObject.CreateInstance<BulletData>(); data.EnsureUpgradeLevels();
        AssetDatabase.CreateAsset(data, path);
        var texture = new Texture2D(2, 2); texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white }); texture.Apply();
        try
        {
            string guid = AssetDatabase.AssetPathToGUID(path); string before = EditorJsonUtility.ToJson(data);
            var book = BulletBalanceWorkbook.Export(); var icons = book.Require("아이콘"); var levels = book.Require("일반형");
            var row = icons.Rows.Skip(1).Single(r => icons.Get(r, "GUID") == guid);
            icons.Images[icons.Rows.IndexOf(row)] = texture.EncodeToPNG();
            var stats = levels.Rows.Skip(1).Single(r => levels.Get(r, "GUID") == guid && levels.Get(r, "레벨") == "0");
            stats[levels.Column("이름")] = "Workbook test"; stats[levels.Column("피해")] = "123";
            stats[levels.Column("사거리")] = "7"; stats[levels.Column("치명타 확률 (%)")] = "31";
            stats[levels.Column("치명타 배율")] = "2.5"; stats[levels.Column("다음 강화 비용")] = "42";
            stats[levels.Column("가격")] = "29";
            var upgraded = levels.Rows.Skip(1).Single(r => levels.Get(r, "GUID") == guid && levels.Get(r, "레벨") == "1");
            upgraded[levels.Column("피해")] = "234"; upgraded[levels.Column("다음 강화 비용")] = "67";
            var plan = BulletBalanceWorkbook.Validate(book); Assert.That(plan.Errors, Is.Empty); Assert.That(plan.Changes.Count, Is.EqualTo(1));
            BulletBalanceWorkbook.Apply(plan); iconPath = AssetDatabase.GetAssetPath(data.CylinderIcon);
            Assert.That(data.Damage, Is.EqualTo(123)); Assert.That(data.CylinderIcon, Is.Not.Null); Assert.That(data.DisplayName, Is.EqualTo("Workbook test"));
            Assert.That(data.GetDamage(1), Is.EqualTo(234)); Assert.That(data.GetUpgradeCost(1), Is.EqualTo(67));
            Assert.That(data.MaxRange, Is.EqualTo(7)); Assert.That(data.CriticalChance, Is.EqualTo(31));
            Assert.That(data.CriticalDamageMultiplier, Is.EqualTo(2.5f)); Assert.That(data.UpgradeCost, Is.EqualTo(42)); Assert.That(data.Price, Is.EqualTo(29));
            Assert.Throws<InvalidOperationException>(() => BulletBalanceWorkbook.Apply(plan));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before)); AssetDatabase.SaveAssetIfDirty(data);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture); Undo.ClearUndo(data);
            AssetDatabase.DeleteAsset(path); AssetDatabase.DeleteAsset(folder);
            if (iconPath != null) AssetDatabase.DeleteAsset(iconPath);
            if (Directory.Exists("Assets/Sprites/BulletWorkbook") && Directory.GetFileSystemEntries("Assets/Sprites/BulletWorkbook").Length == 0) AssetDatabase.DeleteAsset("Assets/Sprites/BulletWorkbook");
        }
    }
}
