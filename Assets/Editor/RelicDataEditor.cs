using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RelicData))]
public sealed class RelicDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Script",
                MonoScript.FromScriptableObject((RelicData)target),
                typeof(RelicData),
                false);
        }

        EditorGUILayout.HelpBox(
            "유물은 상점 가격이나 개별 등장 가중치를 갖지 않습니다. "
            + "이벤트·보물상자·보스 보상 풀에서 획득 가능한 모든 유물이 "
            + "동일한 확률로 선택됩니다.",
            MessageType.Info);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("relicId"),
            new GUIContent(
                "고유 ID",
                "저장 데이터에서 사용하는 변경 불가능한 ID입니다."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("displayName"),
            new GUIContent("표시 이름"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("description"),
            new GUIContent("설명"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("icon"),
            new GUIContent("아이콘"));

        SerializedProperty lifetime =
            serializedObject.FindProperty("lifetimeType");
        EditorGUILayout.PropertyField(
            lifetime,
            new GUIContent(
                "수명 유형",
                "런 지속형은 게임 종료까지 유지되고, 소모형은 충전을 모두 사용하면 파괴됩니다."));

        if ((RelicLifetimeType)lifetime.enumValueIndex
            == RelicLifetimeType.Consumable)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("initialCharges"),
                new GUIContent("초기 발동 횟수"));
        }

        SerializedProperty canStack =
            serializedObject.FindProperty("canStack");
        EditorGUILayout.PropertyField(
            canStack,
            new GUIContent("중복 획득 가능"));

        if (canStack.boolValue)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("maxStack"),
                new GUIContent("최대 중복 수"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("effects"),
            new GUIContent("보유 효과"),
            true);

        serializedObject.ApplyModifiedProperties();

        string summary = ((RelicData)target).BuildEffectSummary();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "런타임 능력 요약\n" + summary,
                MessageType.Info);
        }
    }
}

[CustomPropertyDrawer(typeof(RelicEffectData))]
public sealed class RelicEffectDataDrawer : PropertyDrawer
{
    private const float HelpHeight = 48f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        SerializedProperty typeProperty =
            property.FindPropertyRelative("effectType");
        RelicEffectType effectType =
            (RelicEffectType)typeProperty.intValue;
        int fieldCount = GetEffectFieldCount(effectType);
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return line + spacing + HelpHeight
            + fieldCount * (line + spacing);
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);
        SerializedProperty typeProperty =
            property.FindPropertyRelative("effectType");

        EditorGUI.PropertyField(
            row,
            typeProperty,
            new GUIContent("효과 유형"));

        RelicEffectType effectType =
            (RelicEffectType)typeProperty.intValue;
        row.y += line + spacing;
        row.height = HelpHeight;
        EditorGUI.HelpBox(row, GetEffectDescription(effectType), MessageType.None);
        row.y += HelpHeight + spacing;
        row.height = line;

        switch (effectType)
        {
            case RelicEffectType.PreventLethalDamage:
                DrawField(
                    ref row,
                    property,
                    "survivingHealth",
                    "생존 체력",
                    "죽음 방지 후 남길 체력입니다.");
                break;
            case RelicEffectType.MovementDamageMultiplier:
                DrawField(
                    ref row,
                    property,
                    "movementDamageMultiplierPerStack",
                    "타일당 최종 피해 배율",
                    "1.1이면 이동한 타일마다 최종 피해가 x1.1씩 누적됩니다.");
                DrawField(
                    ref row,
                    property,
                    "movementSources",
                    "인정할 이동 원인",
                    "일반 이동, 위치 교환 탄환, 강제 이동 중 집계할 원인을 선택합니다.");
                DrawField(
                    ref row,
                    property,
                    "movementStackReset",
                    "스택 소비 시점",
                    "사용한 이동 스택을 언제 제거할지 결정합니다.");
                break;
            case RelicEffectType.FirstShotFinalMultiplier:
            case RelicEffectType.LastShotFinalMultiplier:
            case RelicEffectType.LuckyChamber:
            case RelicEffectType.PredatorHolster:
            case RelicEffectType.InfectiousIncubator:
            case RelicEffectType.Carriage:
            case RelicEffectType.ExecutionersOath:
                DrawField(
                    ref row,
                    property,
                    "finalDamageMultiplier",
                    "최종 피해 배율",
                    "조건을 만족하면 다른 최종 배율과 곱합니다. 2는 x2입니다.");
                break;
            case RelicEffectType.ClosedCircuit:
                DrawField(ref row, property, "debuffTransferPercent", "후방 피해 전이율 (%)", "직접 피해를 받은 적 뒤의 가장 가까운 적에게 적용합니다.");
                break;
            case RelicEffectType.EyeOfTheStorm:
                DrawField(ref row, property, "stormDamagePercent", "최고 피해 복제 비율 (%)", "모든 적 피해 조건을 달성했을 때 적용합니다.");
                break;
            case RelicEffectType.EmptyBeat:
            case RelicEffectType.RunningSpur:
                DrawField(ref row, property, "primerBaseChance", "턴 미소모 확률 (%)", "실제 행동을 완료한 뒤 한 번 판정합니다.");
                break;
            case RelicEffectType.GoldPanner:
                DrawField(ref row, property, "goldNuggetChance", "7배 획득 확률 (%)", "적 처치 골드가 확정될 때 한 번 판정합니다.");
                DrawField(ref row, property, "nuggetsRequired", "골드 배율", "발동 시 적 처치 골드에 곱합니다.");
                break;
            case RelicEffectType.CrackedPrimer:
                DrawField(ref row, property, "primerBaseChance", "재사용 확률 (%)", "물리 탄환마다 한 번 판정하며 재사용 사격은 다시 판정하지 않습니다.");
                break;
            case RelicEffectType.Scale:
                DrawField(ref row, property, "scaleMaximumDamagePercent", "기본 피해 증가 (%)", "생존 적 수에 따른 감소 전 증가량입니다.");
                DrawField(ref row, property, "primerFailureChanceBonus", "적당 감소량 (%p)", "생존 적 한 명마다 기본 증가량에서 뺍니다.");
                break;
            case RelicEffectType.FamilyWill:
                DrawField(ref row, property, "memorialDamagePercentPerBullet", "보스당 최종 피해 증가 (%)", "보스 처치 카운터마다 모든 탄환에 더합니다.");
                break;
            case RelicEffectType.MutationCatalyst:
                DrawField(ref row, property, "mutationMaximumChance", "디버프 추가 확률 (%)", "이미 디버프가 있는 적에게 명중할 때 판정합니다.");
                break;
            case RelicEffectType.BrinkTrigger:
                DrawField(ref row, property, "brinkHealthThresholdPercent", "체력 조건 (%)", "현재 체력이 이 비율 이하일 때 활성화됩니다.");
                DrawField(ref row, property, "finalDamageMultiplier", "최종 피해 배율", "체력 조건을 만족하는 모든 탄환에 적용합니다.");
                break;
            case RelicEffectType.AdvancedScope:
                DrawField(ref row, property, "shotRangeBonus", "사거리 증가", "모든 탄환의 유효 사거리와 미리보기에 더합니다.");
                break;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawField(
        ref Rect row,
        SerializedProperty parent,
        string propertyName,
        string displayName,
        string tooltip)
    {
        EditorGUI.PropertyField(
            row,
            parent.FindPropertyRelative(propertyName),
            new GUIContent(displayName, tooltip));
        row.y += EditorGUIUtility.singleLineHeight
            + EditorGUIUtility.standardVerticalSpacing;
    }

    private static void DrawExecutionMultipliers(
        ref Rect row,
        SerializedProperty parent)
    {
        SerializedProperty multipliers = parent.FindPropertyRelative(
            "executionDamageMultipliers");
        multipliers.arraySize = 4;

        for (int index = 0; index < 4; index++)
        {
            SerializedProperty value = multipliers.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(
                row,
                value,
                new GUIContent($"{index + 1}단계 최종 피해 배율"));
            row.y += EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private static int GetEffectFieldCount(RelicEffectType effectType)
    {
        return effectType switch
        {
            RelicEffectType.PreventLethalDamage => 1,
            RelicEffectType.MovementDamageMultiplier => 3,
            RelicEffectType.FirstShotFinalMultiplier => 1,
            RelicEffectType.LastShotFinalMultiplier => 1,
            RelicEffectType.LuckyChamber => 1,
            RelicEffectType.PredatorHolster => 1,
            RelicEffectType.ClosedCircuit => 1,
            RelicEffectType.InfectiousIncubator => 1,
            RelicEffectType.EmptyBeat => 1,
            RelicEffectType.EyeOfTheStorm => 1,
            RelicEffectType.Carriage => 1,
            RelicEffectType.GoldPanner => 2,
            RelicEffectType.CrackedPrimer => 1,
            RelicEffectType.Scale => 2,
            RelicEffectType.FamilyWill => 1,
            RelicEffectType.ExecutionersOath => 1,
            RelicEffectType.MutationCatalyst => 1,
            RelicEffectType.BrinkTrigger => 2,
            RelicEffectType.AdvancedScope => 1,
            RelicEffectType.RunningSpur => 1,
            _ => 0
        };
    }

    private static string GetEffectDescription(RelicEffectType effectType)
    {
        return effectType switch
        {
            RelicEffectType.PreventLethalDamage =>
                "죽음에 이르는 플레이어 피해를 막습니다. 소모형 유물이라면 성공 후 발동 횟수를 1 차감합니다.",
            RelicEffectType.MovementDamageMultiplier =>
                "플레이어가 실제로 이동한 타일 수를 누적하고 다음 공격의 최종 피해를 곱합니다.",
            RelicEffectType.FirstShotFinalMultiplier =>
                "한 실린더에서 처음 성공한 사격의 최종 피해를 곱합니다.",
            RelicEffectType.LastShotFinalMultiplier =>
                "장전 탄환이 남지 않은 상태에서 발생하는 사격의 최종 피해를 곱합니다.",
            RelicEffectType.PredatorHolster =>
                "적 처치 후 다음에 장전되는 탄환 1발에 피해 증가 표식을 부여합니다.",
            RelicEffectType.ClosedCircuit =>
                "직접 피해를 받은 적 뒤의 가장 가까운 적에게 피해 일부를 전이합니다.",
            RelicEffectType.InfectiousIncubator =>
                "디버프를 보유한 적에게 주는 최종 피해를 증가시킵니다.",
            RelicEffectType.EmptyBeat =>
                "재장전할 때 일정 확률로 턴 소모를 면제합니다.",
            RelicEffectType.EyeOfTheStorm =>
                "한 실린더에서 모든 적을 공격하면 기록한 최고 단일 피해를 광역 피해로 변환합니다.",
            RelicEffectType.Carriage =>
                "발차기로 충돌 피해를 줄 때 피해를 증가시킵니다.",
            RelicEffectType.GoldPanner =>
                "적 처치 기본 골드가 확정될 때 일정 확률로 획득량을 곱합니다.",
            RelicEffectType.CrackedPrimer =>
                "물리 탄환 발사마다 일정 확률로 같은 탄환을 한 번 더 발사합니다.",
            RelicEffectType.Scale =>
                "필드의 생존 적 수가 적을수록 최종 피해를 증가시킵니다.",
            RelicEffectType.FamilyWill =>
                "보스 처치 횟수를 저장하고 모든 탄환의 최종 피해를 영구 증가시킵니다.",
            RelicEffectType.LuckyChamber =>
                "실린더의 무작위 약실 하나를 선택해 그 탄환을 폭증시킵니다.",
            RelicEffectType.ExecutionersOath =>
                "적을 처치한 다음 탄환을 강화하며 연속 처치하면 효과를 이어갑니다.",
            RelicEffectType.MutationCatalyst =>
                "디버프가 있는 적에게 명중할 때 무작위 디버프 1스택을 추가할 수 있습니다.",
            RelicEffectType.BrinkTrigger =>
                "낮은 체력에서 모든 탄환의 최종 피해를 증가시킵니다.",
            RelicEffectType.AdvancedScope =>
                "모든 탄환의 실제 사거리와 사거리 미리보기를 늘립니다.",
            RelicEffectType.RunningSpur =>
                "정상 이동을 완료한 뒤 일정 확률로 턴 소모를 면제합니다.",
            _ => "효과가 없습니다."
        };
    }
}
