using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
{
    private SerializedProperty enemyId;
    private SerializedProperty displayName;
    private SerializedProperty description;
    private SerializedProperty sprite;
    private SerializedProperty maxHealth;
    private SerializedProperty dropChance;
    private SerializedProperty dropItems;
    private SerializedProperty behaviorType;
    private SerializedProperty preferredDistance;
    private SerializedProperty maxQueuedAttacks;
    private SerializedProperty queuedActionInterval;
    private SerializedProperty recoveryTurns;
    private SerializedProperty maxSupportCharges;
    private SerializedProperty supportHealAmount;
    private SerializedProperty supportShieldAmount;
    private SerializedProperty supportHealThreshold;
    private SerializedProperty thrownProjectilePrefab;
    private SerializedProperty thrownProjectileDuration;
    private SerializedProperty thrownProjectileArcHeight;
    private SerializedProperty gunnerTelegraphMaterial;
    private SerializedProperty throwerTelegraphMaterial;
    private SerializedProperty supportTelegraphMaterial;
    private SerializedProperty supportHealColor;
    private SerializedProperty supportShieldColor;
    private SerializedProperty telegraphLineWidth;
    private SerializedProperty telegraphVerticalOffset;
    private SerializedProperty throwerTelegraphSegments;
    private SerializedProperty telegraphSortingOrder;
    private SerializedProperty actions;

    private void OnEnable()
    {
        enemyId = Find("enemyId");
        displayName = Find("displayName");
        description = Find("description");
        sprite = Find("sprite");
        maxHealth = Find("maxHealth");
        dropChance = Find("dropChance");
        dropItems = Find("dropItems");
        behaviorType = Find("behaviorType");
        preferredDistance = Find("preferredDistance");
        maxQueuedAttacks = Find("maxQueuedAttacks");
        queuedActionInterval = Find("queuedActionInterval");
        recoveryTurns = Find("recoveryTurns");
        maxSupportCharges = Find("maxSupportCharges");
        supportHealAmount = Find("supportHealAmount");
        supportShieldAmount = Find("supportShieldAmount");
        supportHealThreshold = Find("supportHealThreshold");
        thrownProjectilePrefab = Find("thrownProjectilePrefab");
        thrownProjectileDuration = Find("thrownProjectileDuration");
        thrownProjectileArcHeight = Find("thrownProjectileArcHeight");
        gunnerTelegraphMaterial = Find("gunnerTelegraphMaterial");
        throwerTelegraphMaterial = Find("throwerTelegraphMaterial");
        supportTelegraphMaterial = Find("supportTelegraphMaterial");
        supportHealColor = Find("supportHealColor");
        supportShieldColor = Find("supportShieldColor");
        telegraphLineWidth = Find("telegraphLineWidth");
        telegraphVerticalOffset = Find("telegraphVerticalOffset");
        throwerTelegraphSegments = Find("throwerTelegraphSegments");
        telegraphSortingOrder = Find("telegraphSortingOrder");
        actions = Find("actions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(Find("m_Script"));
        }

        DrawSection("기본 정보", enemyId, displayName, description, sprite);
        DrawSection(
            "전투 능력치",
            maxHealth,
            maxQueuedAttacks,
            queuedActionInterval);

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("AI 행동", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(behaviorType);

        EnemyBehaviorType selectedType =
            (EnemyBehaviorType)behaviorType.enumValueIndex;

        switch (selectedType)
        {
            case EnemyBehaviorType.Melee:
                EditorGUILayout.HelpBox(
                    "플레이어에게 접근해 공격을 예고하고, 공격 뒤 선호 거리까지 물러납니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(preferredDistance);
                break;

            case EnemyBehaviorType.Gunner:
                EditorGUILayout.HelpBox(
                    "사거리는 보드 전체입니다. 플레이어와 같은 쪽의 전열에 있을 때만 행동하며, 사선 앞에 다른 적이 있으면 준비하거나 사격하지 않습니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(recoveryTurns);
                EditorGUILayout.PropertyField(gunnerTelegraphMaterial);
                DrawTelegraphSettings();
                break;

            case EnemyBehaviorType.Thrower:
                EditorGUILayout.HelpBox(
                    "사거리는 보드 전체입니다. 준비 순간의 플레이어 타일을 고정한 뒤 투척합니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(recoveryTurns);
                EditorGUILayout.PropertyField(thrownProjectilePrefab);
                EditorGUILayout.PropertyField(thrownProjectileDuration);
                EditorGUILayout.PropertyField(thrownProjectileArcHeight);
                EditorGUILayout.PropertyField(throwerTelegraphMaterial);
                EditorGUILayout.PropertyField(throwerTelegraphSegments);
                DrawTelegraphSettings();
                break;

            case EnemyBehaviorType.Porter:
                EditorGUILayout.HelpBox(
                    "거리 제한 없이 부상당한 아군을 회복하고, 회복 대상이 없으면 최전선 아군에게 보호막을 줍니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(preferredDistance);
                EditorGUILayout.PropertyField(maxSupportCharges);
                EditorGUILayout.PropertyField(supportHealAmount);
                EditorGUILayout.PropertyField(supportShieldAmount);
                EditorGUILayout.PropertyField(supportHealThreshold);
                EditorGUILayout.PropertyField(supportTelegraphMaterial);
                EditorGUILayout.PropertyField(supportHealColor);
                EditorGUILayout.PropertyField(supportShieldColor);
                DrawTelegraphSettings();
                break;
        }

        EditorGUILayout.EndVertical();
        DrawSection("행동 목록", actions);
        DrawSection("처치 보상", dropChance, dropItems);

        serializedObject.ApplyModifiedProperties();
        DrawValidationMessages((EnemyData)target);
    }

    private SerializedProperty Find(string propertyName)
    {
        return serializedObject.FindProperty(propertyName);
    }

    private void DrawTelegraphSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("예고선 공통 설정", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(telegraphLineWidth);
        EditorGUILayout.PropertyField(telegraphVerticalOffset);
        EditorGUILayout.PropertyField(telegraphSortingOrder);
    }

    private static void DrawSection(
        string title,
        params SerializedProperty[] properties)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        foreach (SerializedProperty property in properties)
        {
            EditorGUILayout.PropertyField(property, true);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawValidationMessages(EnemyData data)
    {
        if (string.IsNullOrWhiteSpace(data.EnemyId))
        {
            EditorGUILayout.HelpBox(
                "Enemy ID를 입력하세요.",
                MessageType.Warning);
        }

        EnemyActionType requiredAction = data.BehaviorType switch
        {
            EnemyBehaviorType.Melee => EnemyActionType.MeleeAttack,
            EnemyBehaviorType.Porter => EnemyActionType.Support,
            _ => EnemyActionType.RangedAttack
        };
        bool requiresAttackData =
            requiredAction != EnemyActionType.Support;

        if (!HasAction(data, requiredAction, requiresAttackData))
        {
            EditorGUILayout.HelpBox(
                $"{requiredAction} 행동을 최소 하나 연결해야 합니다.",
                MessageType.Error);
        }

        Material requiredTelegraph = data.BehaviorType switch
        {
            EnemyBehaviorType.Gunner => data.GunnerTelegraphMaterial,
            EnemyBehaviorType.Thrower => data.ThrowerTelegraphMaterial,
            EnemyBehaviorType.Porter => data.SupportTelegraphMaterial,
            _ => null
        };

        if (data.BehaviorType != EnemyBehaviorType.Melee
            && requiredTelegraph == null)
        {
            EditorGUILayout.HelpBox(
                "행동 예고선 머티리얼이 연결되지 않았습니다.",
                MessageType.Warning);
        }
    }

    private static bool HasAction(
        EnemyData data,
        EnemyActionType actionType,
        bool requiresAttackData)
    {
        foreach (EnemyActionData action in data.Actions)
        {
            if (action != null
                && action.ActionType == actionType
                && (!requiresAttackData || action.AttackData != null))
            {
                return true;
            }
        }

        return false;
    }
}
