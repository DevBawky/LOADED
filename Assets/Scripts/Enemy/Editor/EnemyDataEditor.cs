using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
{
    private SerializedProperty enemyId;
    private SerializedProperty displayName;
    private SerializedProperty description;
    private SerializedProperty sprite;
    private SerializedProperty prefab;
    private SerializedProperty maxHealth;
    private SerializedProperty dropChance;
    private SerializedProperty dropItems;
    private SerializedProperty behaviorType;
    private SerializedProperty preferredDistance;
    private SerializedProperty maxQueuedAttacks;
    private SerializedProperty queuedActionInterval;
    private SerializedProperty meleeAdditionalAttackChance;
    private SerializedProperty firingRange;
    private SerializedProperty thrownProjectilePrefab;
    private SerializedProperty thrownProjectileDuration;
    private SerializedProperty thrownProjectileArcHeight;
    private SerializedProperty gunnerTelegraphMaterial;
    private SerializedProperty throwerTelegraphMaterial;
    private SerializedProperty telegraphLineWidth;
    private SerializedProperty telegraphVerticalOffset;
    private SerializedProperty throwerTelegraphSegments;
    private SerializedProperty telegraphSortingOrder;
    private SerializedProperty actions;
    private SerializedProperty randomizeStartingActionIndex;

    private void OnEnable()
    {
        enemyId = serializedObject.FindProperty("enemyId");
        displayName = serializedObject.FindProperty("displayName");
        description = serializedObject.FindProperty("description");
        sprite = serializedObject.FindProperty("sprite");
        prefab = serializedObject.FindProperty("prefab");
        maxHealth = serializedObject.FindProperty("maxHealth");
        dropChance = serializedObject.FindProperty("dropChance");
        dropItems = serializedObject.FindProperty("dropItems");
        behaviorType = serializedObject.FindProperty("behaviorType");
        preferredDistance = serializedObject.FindProperty("preferredDistance");
        maxQueuedAttacks = serializedObject.FindProperty("maxQueuedAttacks");
        queuedActionInterval = serializedObject.FindProperty("queuedActionInterval");
        meleeAdditionalAttackChance = serializedObject.FindProperty(
            "meleeAdditionalAttackChance");
        firingRange = serializedObject.FindProperty("firingRange");
        thrownProjectilePrefab = serializedObject.FindProperty(
            "thrownProjectilePrefab");
        thrownProjectileDuration = serializedObject.FindProperty(
            "thrownProjectileDuration");
        thrownProjectileArcHeight = serializedObject.FindProperty(
            "thrownProjectileArcHeight");
        gunnerTelegraphMaterial = serializedObject.FindProperty(
            "gunnerTelegraphMaterial");
        throwerTelegraphMaterial = serializedObject.FindProperty(
            "throwerTelegraphMaterial");
        telegraphLineWidth = serializedObject.FindProperty(
            "telegraphLineWidth");
        telegraphVerticalOffset = serializedObject.FindProperty(
            "telegraphVerticalOffset");
        throwerTelegraphSegments = serializedObject.FindProperty(
            "throwerTelegraphSegments");
        telegraphSortingOrder = serializedObject.FindProperty(
            "telegraphSortingOrder");
        actions = serializedObject.FindProperty("actions");
        randomizeStartingActionIndex = serializedObject.FindProperty(
            "randomizeStartingActionIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptReference();
        DrawIdentitySection();
        DrawCombatSection();
        DrawBehaviorSection();
        DrawActionSection();
        DrawDropSection();

        serializedObject.ApplyModifiedProperties();
        DrawValidationMessages((EnemyData)target);
    }

    private void DrawScriptReference()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_Script"));
        }
    }

    private void DrawIdentitySection()
    {
        BeginSection("기본 정보");
        DrawProperty(enemyId, "Enemy ID", "저장에 사용하는 고유 ID입니다.");
        DrawProperty(displayName, "표시 이름", "게임 UI에 표시할 이름입니다.");
        DrawProperty(description, "설명", "적의 특징과 전투 방식을 적어주세요.");
        DrawProperty(sprite, "스프라이트", "월드에 표시할 적 이미지입니다.");
        DrawProperty(prefab, "생성 프리팹", "이 데이터를 사용하는 EnemyController 프리팹입니다.");
        EndSection();
    }

    private void DrawCombatSection()
    {
        BeginSection("전투 능력치");
        DrawProperty(maxHealth, "최대 체력", "적의 최대 체력입니다.");
        DrawProperty(
            maxQueuedAttacks,
            "최대 공격 슬롯",
            "머리 위 공격 예약 UI에 동시에 저장할 수 있는 최대 공격 개수입니다.");
        DrawProperty(
            queuedActionInterval,
            "연속 공격 간격",
            "예약된 공격을 여러 번 실행할 때 공격 사이의 시간입니다.");
        EndSection();
    }

    private void DrawBehaviorSection()
    {
        BeginSection("AI 행동 설정");
        DrawProperty(
            behaviorType,
            "AI 타입",
            "근접 추격형, 직선 사격형 또는 고정 타일 투척형을 선택합니다.");

        EnemyBehaviorType selectedType =
            (EnemyBehaviorType)behaviorType.enumValueIndex;

        switch (selectedType)
        {
            case EnemyBehaviorType.Melee:
                EditorGUILayout.HelpBox(
                    "플레이어에게 접근해 공격을 예약하고, 공격 후 선호 거리까지 후퇴합니다.",
                    MessageType.Info);
                DrawProperty(
                    preferredDistance,
                    "선호 거리",
                    "근접 공격 후 플레이어와 유지하려는 타일 거리입니다.");
                DrawProperty(
                    meleeAdditionalAttackChance,
                    "추가 공격 확률",
                    "선호 거리 안에서 슬롯에 공격을 한 번 더 추가할 확률입니다.");
                break;

            case EnemyBehaviorType.Gunner:
                EditorGUILayout.HelpBox(
                    "스폰 즉시 사격을 하나 예약합니다. 플레이어가 사격 범위 안에 있고 사선에 다른 적이 없을 때 공격을 준비하며, 조건이 맞지 않으면 접근합니다.",
                    MessageType.Info);
                DrawProperty(
                    firingRange,
                    "사격 가능 범위",
                    "총잡이가 직선 사격을 준비하고 명중시킬 수 있는 최대 타일 거리입니다.");
                DrawProperty(
                    gunnerTelegraphMaterial,
                    "사격 범위 머티리얼",
                    "공격 준비 턴에 직선 사격 범위를 표시할 LineRenderer 머티리얼입니다.");
                DrawTelegraphCommonSettings();
                break;

            case EnemyBehaviorType.Thrower:
                EditorGUILayout.HelpBox(
                    "이동하거나 회전하지 않습니다. 공격 추가 → 현재 플레이어 타일 조준 → 포물선 투척 → 공격 추가를 반복합니다.",
                    MessageType.Info);
                DrawProperty(
                    firingRange,
                    "투척 가능 범위",
                    "투척병이 플레이어 타일을 조준할 수 있는 최대 타일 거리입니다.");
                DrawProperty(
                    thrownProjectilePrefab,
                    "투사체 프리팹",
                    "적 위치에서 목표 타일까지 포물선으로 이동할 폭탄 프리팹입니다.");
                DrawProperty(
                    thrownProjectileDuration,
                    "비행 시간",
                    "폭탄이 목표 타일까지 도달하는 데 걸리는 시간입니다.");
                DrawProperty(
                    thrownProjectileArcHeight,
                    "포물선 높이",
                    "폭탄 궤적 정점에 더할 높이입니다.");
                DrawProperty(
                    throwerTelegraphMaterial,
                    "투척 궤적 머티리얼",
                    "공격 준비 턴에 포물선 궤적을 표시할 LineRenderer 머티리얼입니다.");
                DrawProperty(
                    throwerTelegraphSegments,
                    "포물선 정밀도",
                    "포물선 예고선을 구성하는 점 개수입니다.");
                DrawTelegraphCommonSettings();
                break;
        }

        EndSection();
    }

    private void DrawTelegraphCommonSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("공격 예고선", EditorStyles.miniBoldLabel);
        DrawProperty(
            telegraphLineWidth,
            "선 굵기",
            "LineRenderer로 표시할 공격 예고선의 굵기입니다.");
        DrawProperty(
            telegraphVerticalOffset,
            "높이 오프셋",
            "캐릭터와 타일 중심에서 예고선을 위로 올릴 거리입니다.");
        DrawProperty(
            telegraphSortingOrder,
            "렌더 순서",
            "적 스프라이트와 같은 Sorting Layer 안에서 예고선이 사용할 Sorting Order입니다.");
    }

    private void DrawActionSection()
    {
        BeginSection("행동 및 공격 목록");
        DrawProperty(
            actions,
            "사용 가능한 행동",
            "AI가 사용할 이동 및 공격 EnemyActionData 목록입니다.",
            true);
        DrawProperty(
            randomizeStartingActionIndex,
            "첫 원거리 공격 무작위 선택",
            "원거리 공격이 여러 개라면 스폰 시 처음 예약할 공격을 무작위로 선택합니다.");
        EndSection();
    }

    private void DrawDropSection()
    {
        BeginSection("처치 보상");
        DrawProperty(dropChance, "보상 등장 확률", "처치 보상이 등장할 확률입니다.");
        DrawProperty(
            dropItems,
            "보상 후보",
            "보상 종류, 수량 및 선택 가중치를 설정합니다.",
            true);
        EndSection();
    }

    private static void DrawValidationMessages(EnemyData data)
    {
        if (string.IsNullOrWhiteSpace(data.EnemyId))
        {
            EditorGUILayout.HelpBox(
                "Enemy ID가 비어 있습니다. 저장 데이터를 구분할 고유 ID를 입력하세요.",
                MessageType.Warning);
        }

        if (data.Prefab == null)
        {
            EditorGUILayout.HelpBox(
                "생성 프리팹이 연결되지 않았습니다.",
                MessageType.Warning);
        }
        else if (data.Prefab.GetComponent<EnemyController>() == null)
        {
            EditorGUILayout.HelpBox(
                "생성 프리팹 루트에 EnemyController가 없습니다.",
                MessageType.Error);
        }
        else if (data.Prefab.GetComponent<EnemyController>().Data != data)
        {
            EditorGUILayout.HelpBox(
                "생성 프리팹의 EnemyController가 현재 EnemyData를 참조하고 있지 않습니다.",
                MessageType.Warning);
        }

        EnemyActionType requiredAttack = data.BehaviorType
            == EnemyBehaviorType.Melee
                ? EnemyActionType.MeleeAttack
                : EnemyActionType.RangedAttack;

        if (!HasAction(data, requiredAttack, true))
        {
            EditorGUILayout.HelpBox(
                $"{requiredAttack} 행동과 EnemyAttackData를 최소 하나 연결해야 합니다.",
                MessageType.Error);
        }

        if (data.BehaviorType == EnemyBehaviorType.Gunner
            && !HasAction(data, EnemyActionType.Approach, false))
        {
            EditorGUILayout.HelpBox(
                "총잡이가 사격 범위 밖에서 접근하려면 Approach 행동이 필요합니다.",
                MessageType.Warning);
        }

        if (data.BehaviorType == EnemyBehaviorType.Thrower
            && data.ThrownProjectilePrefab == null)
        {
            EditorGUILayout.HelpBox(
                "투사체 프리팹이 비어 있어 포물선 비행은 보이지 않지만, 조준과 착탄 피해는 정상 작동합니다.",
                MessageType.Info);
        }

        if (data.BehaviorType == EnemyBehaviorType.Gunner
            && data.GunnerTelegraphMaterial == null)
        {
            EditorGUILayout.HelpBox(
                "사격 범위 머티리얼이 비어 있어 공격 준비 예고선이 표시되지 않습니다.",
                MessageType.Warning);
        }

        if (data.BehaviorType == EnemyBehaviorType.Thrower
            && data.ThrowerTelegraphMaterial == null)
        {
            EditorGUILayout.HelpBox(
                "투척 궤적 머티리얼이 비어 있어 포물선 예고선이 표시되지 않습니다.",
                MessageType.Warning);
        }
    }

    private static bool HasAction(
        EnemyData data,
        EnemyActionType actionType,
        bool requiresAttackData)
    {
        if (data.Actions == null)
        {
            return false;
        }

        foreach (EnemyActionData action in data.Actions)
        {
            if (action != null && action.ActionType == actionType
                && (!requiresAttackData || action.AttackData != null))
            {
                return true;
            }
        }

        return false;
    }

    private static void BeginSection(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    private static void DrawProperty(
        SerializedProperty property,
        string label,
        string tooltip,
        bool includeChildren = false)
    {
        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label, tooltip),
            includeChildren);
    }
}
