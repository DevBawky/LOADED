#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BulletTypeClassificationBuilder
{
    private const string BulletRoot = "Assets/Scripts/Bullet/SO";

    internal static readonly IReadOnlyDictionary<string, BulletType>
        ExpectedTypes = new Dictionary<string, BulletType>(
            StringComparer.Ordinal)
        {
            ["Assets/Scripts/Bullet/SO/Normal/Normal.asset"] = BulletType.Normal,
            ["Assets/Scripts/Bullet/SO/Ace/Power.asset"] = BulletType.Normal,

            ["Assets/Scripts/Bullet/SO/Normal/Silent.asset"] = BulletType.Ghost,
            ["Assets/Scripts/Bullet/SO/Rare/Emergency.asset"] = BulletType.Ghost,
            ["Assets/Scripts/Bullet/SO/Rare/Wraith.asset"] = BulletType.Ghost,
            ["Assets/Scripts/Bullet/SO/Ace/Necromancy.asset"] = BulletType.Ghost,

            ["Assets/Scripts/Bullet/SO/Rare/Sniping.asset"] = BulletType.Sniper,
            ["Assets/Scripts/Bullet/SO/Ace/Assassination.asset"] = BulletType.Sniper,
            ["Assets/Scripts/Bullet/SO/Legendary/Mastery.asset"] = BulletType.Sniper,
            ["Assets/Scripts/Bullet/SO/Normal/Hunt.asset"] = BulletType.Sniper,
            ["Assets/Scripts/Bullet/SO/Rare/Lock On.asset"] = BulletType.Sniper,
            ["Assets/Scripts/Bullet/SO/Legendary/Execution.asset"] = BulletType.Sniper,

            ["Assets/Scripts/Bullet/SO/Ace/Tracking.asset"] = BulletType.Storm,
            ["Assets/Scripts/Bullet/SO/Ace/Typhoon.asset"] = BulletType.Storm,
            ["Assets/Scripts/Bullet/SO/Legendary/Cataclysm.asset"] = BulletType.Storm,

            ["Assets/Scripts/Bullet/SO/Normal/Shot.asset"] = BulletType.Shotgun,
            ["Assets/Scripts/Bullet/SO/Rare/Focused Volley.asset"] = BulletType.Shotgun,
            ["Assets/Scripts/Bullet/SO/Ace/Gambit.asset"] = BulletType.Shotgun,
            ["Assets/Scripts/Bullet/SO/Ace/Dice Shot.asset"] = BulletType.Shotgun,
            ["Assets/Scripts/Bullet/SO/Legendary/Pierce.asset"] = BulletType.Piercing,
            ["Assets/Scripts/Bullet/SO/Ace/Return.asset"] = BulletType.Piercing,

            ["Assets/Scripts/Bullet/SO/Rare/Mark.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Rare/Stun.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Rare/Venom.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Rare/Weakness.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Ace/Amplifier.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Ace/Judgment.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Ace/Venom Burst.asset"] = BulletType.Debuff,

            ["Assets/Scripts/Bullet/SO/Normal/Rotation Shot.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Evasion.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/KnockBack.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Position Swap.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Reverse Shot.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Seismometer.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Wall Impact.asset"] = BulletType.Kinetic,
            ["Assets/Scripts/Bullet/SO/Rare/Blink.asset"] = BulletType.Kinetic,

            ["Assets/Scripts/Bullet/SO/Rare/Charge.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Focus.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Immersion.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Vanguard.asset"] = BulletType.Normal,
            ["Assets/Scripts/Bullet/SO/Rare/Finisher.asset"] = BulletType.Normal,
            ["Assets/Scripts/Bullet/SO/Rare/Powder Pouch.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Resonance.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Shell Collector.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Rare/Stack.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Ace/Accumulator.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Ace/Chain Fire.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Ace/Clone.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Ace/Distributor.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Ace/Spread.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Legendary/Finale.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Legendary/Repeat_Mark.asset"] = BulletType.Combo,
            ["Assets/Scripts/Bullet/SO/Legendary/Harvest.asset"] = BulletType.Combo,

            ["Assets/Scripts/Bullet/SO/Normal/Gold.asset"] = BulletType.Economy,
            ["Assets/Scripts/Bullet/SO/Rare/Gilded.asset"] = BulletType.Economy,
            ["Assets/Scripts/Bullet/SO/Rare/Rebate.asset"] = BulletType.Economy,
            ["Assets/Scripts/Bullet/SO/Rare/Saver.asset"] = BulletType.Economy,
            ["Assets/Scripts/Bullet/SO/Rare/Jackpot.asset"] = BulletType.Economy,

            ["Assets/Scripts/Bullet/SO/Rare/Crescendo.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Rare/Mass Produced.asset"] = BulletType.Growth,
            ["Assets/Scripts/Bullet/SO/Rare/Mixed Grade.asset"] = BulletType.Debuff,
            ["Assets/Scripts/Bullet/SO/Ace/Health.asset"] = BulletType.Growth,
            ["Assets/Scripts/Bullet/SO/Ace/Legacy.asset"] = BulletType.Growth,
            ["Assets/Scripts/Bullet/SO/Ace/Masterpiece.asset"] = BulletType.Growth,
            ["Assets/Scripts/Bullet/SO/Legendary/Devourer.asset"] = BulletType.Growth,

            ["Assets/Scripts/Bullet/SO/Rare/Coagulation.asset"] = BulletType.Blood,
            ["Assets/Scripts/Bullet/SO/Ace/High Roller.asset"] = BulletType.Blood,
            ["Assets/Scripts/Bullet/SO/Ace/Ritual.asset"] = BulletType.Blood,
            ["Assets/Scripts/Bullet/SO/Legendary/Flesh For Bone.asset"] = BulletType.Blood,
            ["Assets/Scripts/Bullet/SO/Legendary/Heart.asset"] = BulletType.Blood,
            ["Assets/Scripts/Bullet/SO/Legendary/Lifesteal.asset"] = BulletType.Blood
        };

    [MenuItem("Tools/LOADED/Classify All Bullet Types")]
    public static void Apply()
    {
        string[] authoredPaths = AssetDatabase.FindAssets(
                "t:BulletData", new[] { BulletRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] missingMappings = authoredPaths
            .Where(path => !ExpectedTypes.ContainsKey(path))
            .ToArray();
        string[] missingAssets = ExpectedTypes.Keys
            .Where(path => !authoredPaths.Contains(path, StringComparer.Ordinal))
            .ToArray();
        if (missingMappings.Length > 0 || missingAssets.Length > 0)
        {
            throw new InvalidOperationException(
                "Bullet type classification is incomplete. "
                + $"Unmapped assets: {string.Join(", ", missingMappings)}. "
                + $"Missing assets: {string.Join(", ", missingAssets)}.");
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Classify All Bullet Types");
        int changed = 0;
        foreach (KeyValuePair<string, BulletType> pair in ExpectedTypes)
        {
            BulletData bullet = AssetDatabase.LoadAssetAtPath<BulletData>(
                pair.Key);
            if (bullet == null || bullet.BulletType == pair.Value)
            {
                continue;
            }

            Undo.RecordObject(bullet, "Classify Bullet Type");
            SerializedObject serialized = new(bullet);
            serialized.FindProperty("bulletType").enumValueIndex =
                (int)pair.Value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bullet);
            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Bullet type classification complete. Authored bullets: "
            + $"{authoredPaths.Length}, changed: {changed}.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }
}
#endif
