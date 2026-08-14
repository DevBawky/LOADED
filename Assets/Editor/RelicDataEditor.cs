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
                DrawField(
                    ref row,
                    property,
                    "finalDamageMultiplier",
                    "최종 피해 배율",
                    "조건을 만족하면 다른 최종 배율과 곱합니다. 2는 x2입니다.");
                break;
            case RelicEffectType.ClosedCircuit:
                DrawField(ref row, property, "circuitShotThreshold", "필요 사격 횟수", "이 횟수마다 가장 오래된 사용 탄환을 장전합니다.");
                DrawField(ref row, property, "circuitMaxReloadsPerCylinder", "실린더당 최대 발동", "한 실린더의 무한 순환을 막는 발동 상한입니다.");
                break;
            case RelicEffectType.InfectiousIncubator:
                DrawField(ref row, property, "debuffTransferPercent", "디버프 이전 비율 (%)", "각 디버프 스택에 개별 적용하고 올림합니다.");
                break;
            case RelicEffectType.EyeOfTheStorm:
                DrawField(ref row, property, "stormDamagePercent", "최고 피해 복제 비율 (%)", "모든 적 피해 조건을 달성했을 때 적용합니다.");
                break;
            case RelicEffectType.Carriage:
                DrawField(ref row, property, "movementTilesPerFreeReload", "무료 재장전당 이동 칸", "실제 이동 거리를 누적합니다.");
                DrawField(ref row, property, "freeReloadStorageLimit", "무료 재장전 저장 상한", "동시에 보유할 수 있는 무료 재장전 횟수입니다.");
                DrawField(ref row, property, "movementSources", "인정할 이동 원인", "일반 이동, 위치 교환, 강제 이동 중 집계할 원인입니다.");
                break;
            case RelicEffectType.GoldPanner:
                DrawField(ref row, property, "goldNuggetChance", "골드당 금덩이 확률 (%)", "획득한 골드 1개마다 독립 판정합니다.");
                DrawField(ref row, property, "nuggetsRequired", "필요 금덩이", "다음 탄환 폭증 시 이 수만큼 소비합니다.");
                DrawField(ref row, property, "finalDamageMultiplier", "최종 피해 배율", "금덩이 조건을 만족한 다음 탄환에 적용합니다.");
                break;
            case RelicEffectType.CrackedPrimer:
                DrawField(ref row, property, "primerBaseChance", "초기 확률 (%)", "성공 후 돌아가는 확률입니다.");
                DrawField(ref row, property, "primerFailureChanceBonus", "실패 보정 (%p)", "실패할 때마다 다음 판정에 더합니다.");
                DrawField(ref row, property, "finalDamageMultiplier", "최종 피해 배율", "발동한 실제 사격에 적용합니다.");
                break;
            case RelicEffectType.Scale:
                DrawField(ref row, property, "scaleMaximumDamagePercent", "피해 증가 상한 (%)", "이전 실린더에서 잃은 최대 체력 비율을 제한합니다.");
                break;
            case RelicEffectType.FamilyWill:
                DrawField(ref row, property, "memorialDamagePercentPerBullet", "파괴 탄환당 추모 위력 (%)", "영구 파괴 기록 하나당 추가되는 위력입니다.");
                DrawField(ref row, property, "memorialMaximumDamagePercent", "추모 위력 상한 (%)", "첫 실린더 추가 사격의 최대 위력입니다.");
                break;
            case RelicEffectType.ExecutionersOath:
                DrawExecutionMultipliers(ref row, property);
                break;
            case RelicEffectType.MutationCatalyst:
                DrawField(ref row, property, "mutationChancePerDebuffType", "디버프 종류당 확률 (%)", "대상의 활성 디버프 종류 수에 곱합니다.");
                DrawField(ref row, property, "mutationMaximumChance", "최대 확률 (%)", "변이 발동 확률의 상한입니다.");
                DrawField(ref row, property, "finalDamageMultiplier", "최종 피해 배율", "변이가 발생한 대상 피해에 적용합니다.");
                break;
            case RelicEffectType.BrinkTrigger:
                DrawField(ref row, property, "brinkHealthThresholdPercent", "체력 조건 (%)", "현재 체력이 이 비율 이하일 때 활성화됩니다.");
                DrawField(ref row, property, "brinkBaseChance", "초기 확률 (%)", "실린더의 첫 판정 확률입니다.");
                DrawField(ref row, property, "brinkFailureChanceBonus", "실패 보정 (%p)", "같은 실린더에서 실패할 때마다 더합니다.");
                DrawField(ref row, property, "finalDamageMultiplier", "최종 피해 배율", "실린더당 첫 성공 탄환에 적용합니다.");
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
            RelicEffectType.ClosedCircuit => 2,
            RelicEffectType.InfectiousIncubator => 1,
            RelicEffectType.EyeOfTheStorm => 1,
            RelicEffectType.Carriage => 3,
            RelicEffectType.GoldPanner => 3,
            RelicEffectType.CrackedPrimer => 3,
            RelicEffectType.Scale => 1,
            RelicEffectType.FamilyWill => 2,
            RelicEffectType.ExecutionersOath => 4,
            RelicEffectType.MutationCatalyst => 3,
            RelicEffectType.BrinkTrigger => 4,
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
                "처치 탄환을 다음 장전 순서로 옮기고 그 재장전을 무료로 만듭니다.",
            RelicEffectType.ClosedCircuit =>
                "기본·연쇄·탄피 사격 횟수를 누적해 가장 오래전에 사용한 탄환을 빈 약실에 즉시 장전합니다.",
            RelicEffectType.InfectiousIncubator =>
                "죽은 적이 보유한 각 디버프를 가장 가까운 생존 적에게 이전합니다.",
            RelicEffectType.EmptyBeat =>
                "완전히 빈 실린더에 넣는 첫 탄환의 재장전 턴을 면제합니다.",
            RelicEffectType.EyeOfTheStorm =>
                "한 실린더에서 모든 적을 공격하면 기록한 최고 단일 피해를 광역 피해로 변환합니다.",
            RelicEffectType.Carriage =>
                "실제 플레이어 이동 거리를 무료 재장전 횟수로 변환합니다.",
            RelicEffectType.GoldPanner =>
                "전투 중 획득한 골드로 금덩이를 찾고 다음 탄환을 치명타·폭증시킵니다.",
            RelicEffectType.CrackedPrimer =>
                "모든 실제 사격에 폭증 확률을 부여하고 실패할수록 다음 확률을 올립니다.",
            RelicEffectType.Scale =>
                "실린더 사격 중 잃은 체력 비율을 다음 실린더 전체의 피해로 변환합니다.",
            RelicEffectType.FamilyWill =>
                "영구 파괴 횟수를 저장하고 이후 전투의 첫 실린더에 추모 추가 사격을 만듭니다.",
            RelicEffectType.LuckyChamber =>
                "실린더의 무작위 약실 하나를 선택해 그 탄환을 폭증시킵니다.",
            RelicEffectType.ExecutionersOath =>
                "연속 처치 성공 시 다음 사격의 피해 단계를 올리고 실패 시 초기화합니다.",
            RelicEffectType.MutationCatalyst =>
                "대상의 활성 디버프 종류 수에 비례한 확률로 해당 대상 피해를 폭증시킵니다.",
            RelicEffectType.BrinkTrigger =>
                "낮은 체력에서 탄환 폭증을 판정하며 실패할수록 같은 실린더의 확률이 상승합니다.",
            _ => "효과가 없습니다."
        };
    }
}
