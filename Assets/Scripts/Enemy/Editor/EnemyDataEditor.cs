using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
{
    private SerializedProperty enemyId;
    private SerializedProperty displayName;
    private SerializedProperty description;
    private SerializedProperty avatar;
    private SerializedProperty maxHealth;
    private SerializedProperty dropChance;
    private SerializedProperty dropItems;
    private SerializedProperty behaviorType;
    private SerializedProperty preferredDistance;
    private SerializedProperty maxQueuedAttacks;
    private SerializedProperty queuedActionInterval;
    private SerializedProperty queueElementRevealDuration;
    private SerializedProperty recoveryTurns;
    private SerializedProperty maxSupportCharges;
    private SerializedProperty supportHealAmount;
    private SerializedProperty supportShieldAmount;
    private SerializedProperty supportHealThreshold;
    private SerializedProperty thrownProjectilePrefab;
    private SerializedProperty thrownProjectileSprite;
    private SerializedProperty thrownProjectileColor;
    private SerializedProperty thrownProjectileSize;
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
    private SerializedProperty bigBarrel;

    private void OnEnable()
    {
        enemyId = Find("enemyId");
        displayName = Find("displayName");
        description = Find("description");
        avatar = Find("avatar");
        maxHealth = Find("maxHealth");
        dropChance = Find("dropChance");
        dropItems = Find("dropItems");
        behaviorType = Find("behaviorType");
        preferredDistance = Find("preferredDistance");
        maxQueuedAttacks = Find("maxQueuedAttacks");
        queuedActionInterval = Find("queuedActionInterval");
        queueElementRevealDuration = Find("queueElementRevealDuration");
        recoveryTurns = Find("recoveryTurns");
        maxSupportCharges = Find("maxSupportCharges");
        supportHealAmount = Find("supportHealAmount");
        supportShieldAmount = Find("supportShieldAmount");
        supportHealThreshold = Find("supportHealThreshold");
        thrownProjectilePrefab = Find("thrownProjectilePrefab");
        thrownProjectileSprite = Find("thrownProjectileSprite");
        thrownProjectileColor = Find("thrownProjectileColor");
        thrownProjectileSize = Find("thrownProjectileSize");
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
        bigBarrel = Find("bigBarrel");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(Find("m_Script"));
        }

        DrawSection("기본 정보", enemyId, displayName, description, avatar);
        DrawSection(
            "전투 능력치",
            maxHealth,
            maxQueuedAttacks,
            queuedActionInterval,
            queueElementRevealDuration);

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
                EditorGUILayout.PropertyField(thrownProjectileSprite);
                EditorGUILayout.PropertyField(thrownProjectileColor);
                EditorGUILayout.PropertyField(thrownProjectileSize);
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

            case EnemyBehaviorType.BigBarrel:
                EditorGUILayout.HelpBox(
                    "고정 순서로 폭탄 투척, 거리 조정, 양옆 산탄 사격, 재장전을 반복합니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(preferredDistance);
                EditorGUILayout.PropertyField(recoveryTurns);
                EditorGUILayout.PropertyField(thrownProjectilePrefab);
                EditorGUILayout.PropertyField(thrownProjectileSprite);
                EditorGUILayout.PropertyField(thrownProjectileColor);
                EditorGUILayout.PropertyField(thrownProjectileSize);
                EditorGUILayout.PropertyField(thrownProjectileDuration);
                EditorGUILayout.PropertyField(bigBarrel, true);
                EditorGUILayout.PropertyField(throwerTelegraphSegments);
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

        if (data.Avatar == null)
        {
            EditorGUILayout.HelpBox(
                "적 Avatar 프리팹이 연결되지 않았습니다.",
                MessageType.Warning);
        }
        else
        {
            Animator animator =
                data.Avatar.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                EditorGUILayout.HelpBox(
                    "Avatar 프리팹에 Animator가 필요합니다.",
                    MessageType.Error);
            }
            else if (animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox(
                    "Avatar의 Animator Controller가 연결되지 않았습니다.",
                    MessageType.Error);
            }
        }

        if (data.BehaviorType == EnemyBehaviorType.BigBarrel)
        {
            DrawBigBarrelValidation(data);
            return;
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

    private static void DrawBigBarrelValidation(EnemyData data)
    {
        ValidateBossAction(data, EnemyActionType.ExplosiveThrow, "폭탄 투척");
        ValidateBossAction(data, EnemyActionType.ShotgunAttack, "산탄 사격");
        ValidateBossAction(data, EnemyActionType.Reload, "재장전");

        BigBarrelSettings settings = data.BigBarrel;

        if (settings.BossBombPrefab == null)
        {
            Warning("BossBomb 프리팹이 연결되지 않았습니다.");
        }
        else if (settings.BossBombPrefab.GetComponent<BossBomb>() == null)
        {
            Warning("BossBomb 프리팹 루트에 BossBomb 컴포넌트가 필요합니다.");
        }

        if (settings.BombTelegraphMaterial == null)
        {
            Warning("폭탄 Telegraph Material이 연결되지 않았습니다.");
        }

        if (settings.ShotgunTelegraphMaterial == null)
        {
            Warning("산탄 Telegraph Material이 연결되지 않았습니다.");
        }

        if (settings.ConfiguredBombDamage <= 0)
        {
            Warning("폭탄 피해량은 0보다 커야 합니다.");
        }

        if (settings.ConfiguredBombExplosionRadius <= 0)
        {
            Warning("폭발 범위는 0보다 커야 합니다.");
        }

        if (settings.ConfiguredBombFuseTurns < 1
            || settings.ConfiguredBombFuseTurns > 3
            || settings.ConfiguredPhaseTwoBombFuseTurns < 1
            || settings.ConfiguredPhaseTwoBombFuseTurns > 3)
        {
            Warning("모든 폭탄 퓨즈는 1에서 3 사이여야 합니다.");
        }

        if (settings.ConfiguredPhaseTwoHealthRatio <= 0f
            || settings.ConfiguredPhaseTwoHealthRatio >= 1f)
        {
            Warning("2페이즈 체력 비율은 0과 1 사이여야 합니다.");
        }
    }

    private static void ValidateBossAction(
        EnemyData data,
        EnemyActionType actionType,
        string label)
    {
        if (!HasAction(data, actionType, false))
        {
            Warning($"{label} Action이 연결되지 않았습니다.");
        }
    }

    private static void Warning(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Warning);
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
