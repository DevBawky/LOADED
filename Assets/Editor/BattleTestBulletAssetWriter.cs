using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Writes authored values without replacing any live BulletInstance.</summary>
[InitializeOnLoad]
public static class BattleTestBulletAssetWriter
{
    public const string WorkbookPath = "outputs/authoring-20261002/LOADED_BulletData.xlsx";

    static BattleTestBulletAssetWriter()
    {
        BattleTestBulletAuthoring.SaveAsset = Save;
    }

    private static string Save(BulletData data, int level, BattleTestBulletAuthoring.Values values)
    {
        WriteValues(data, level, values);
        try
        {
            ExportWorkbook();
            return "저장 완료 · 전투에 즉시 반영했습니다. 탄환 관리 엑셀도 갱신했습니다.";
        }
        catch (IOException)
        {
            return "탄환 저장 완료 · 엑셀을 교체할 수 없습니다.\n열려 있는 엑셀을 저장하고 닫은 뒤 다시 저장해 주세요.";
        }
        catch (Exception exception)
        {
            return "탄환 저장 완료 · 엑셀 갱신 실패: " + exception.Message + "\n파일을 닫고 다시 저장해 주세요.";
        }
    }

    internal static void WriteValues(BulletData data, int level, BattleTestBulletAuthoring.Values values)
    {
        if (data == null || !AssetDatabase.Contains(data)) throw new ArgumentException("저장할 탄환 원본이 없습니다.");
        if (level < 0 || level > BulletData.MaximumUpgradeLevel || !values.IsValid)
            throw new ArgumentException("강화 단계 또는 탄환 수치의 범위를 확인해 주세요.");
        var serialized = new SerializedObject(data);
        SerializedProperty upgrades = serialized.FindProperty("upgradeLevels");
        if (level > 0 && upgrades.arraySize < level) throw new InvalidOperationException("원본의 강화 단계 데이터가 없습니다.");
        SerializedProperty entry = level == 0 ? null : upgrades.GetArrayElementAtIndex(level - 1);
        SerializedProperty Field(string name) => entry == null ? serialized.FindProperty(name) : entry.FindPropertyRelative(name);
        string before = EditorJsonUtility.ToJson(data);
        Field("damage").intValue = values.Damage;
        Field("criticalChance").floatValue = values.CriticalChance;
        Field("criticalDamageMultiplier").floatValue = values.CriticalMultiplier;
        Field("maxRange").intValue = values.Range;
        if (!serialized.hasModifiedProperties) return;
        Undo.RecordObject(data, "테스트 씬 탄환 수치 저장");
        try
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(before, data);
            EditorUtility.SetDirty(data);
            throw;
        }
    }

    public static void ExportWorkbook()
    {
        string directory = Path.GetDirectoryName(WorkbookPath);
        Directory.CreateDirectory(directory);
        string temporary = WorkbookPath + ".pending.xlsx";
        try
        {
            BulletBalanceWorkbook.Export().Write(temporary);
            BulletBalanceWorkbook.Plan validation = BulletBalanceWorkbook.Validate(LoadedWorkbook.Read(temporary));
            if (validation.Errors.Count != 0 || validation.Changes.Count != 0 || validation.DraftChanged)
                throw new InvalidDataException("엑셀 검증 실패: " + string.Join("; ", validation.Errors));
            if (File.Exists(WorkbookPath))
            {
                string backup = "Logs/BattleSandbox/WorkbookBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".xlsx";
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(WorkbookPath, backup, false);
                File.Replace(temporary, WorkbookPath, null);
            }
            else File.Move(temporary, WorkbookPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
