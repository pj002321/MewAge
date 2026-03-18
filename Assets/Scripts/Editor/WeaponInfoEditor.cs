#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponInfo 인스펙터: weaponType에 따라 관련 필드만 표시합니다.
/// Melee  → 근거리 전용 섹션만 표시
/// Ranged / Magic → 원거리 전용 섹션만 표시
/// </summary>
[CustomEditor(typeof(WeaponInfo), true)] // true = 서브클래스(WeaponPoint)에도 적용
public class WeaponInfoEditor : Editor
{
    // 공통
    SerializedProperty p_weaponType;
    SerializedProperty p_attackDamage;
    SerializedProperty p_attackCooldown;
    SerializedProperty p_attackRange;

    // 근거리
    SerializedProperty p_pullbackSpeed;
    SerializedProperty p_strikeSpeed;
    SerializedProperty p_returnSpeed;
    SerializedProperty p_hitDistance;
    SerializedProperty p_pullbackDistance;

    // 원거리
    SerializedProperty p_projectilePrefab;
    SerializedProperty p_projectileSpeed;
    SerializedProperty p_projectileSize;
    SerializedProperty p_projectileBounce;
    SerializedProperty p_projectileMaxBounces;

    private void OnEnable()
    {
        p_weaponType       = serializedObject.FindProperty("weaponType");
        p_attackDamage     = serializedObject.FindProperty("attackDamage");
        p_attackCooldown   = serializedObject.FindProperty("attackCooldown");
        p_attackRange      = serializedObject.FindProperty("attackRange");

        p_pullbackSpeed    = serializedObject.FindProperty("pullbackSpeed");
        p_strikeSpeed      = serializedObject.FindProperty("strikeSpeed");
        p_returnSpeed      = serializedObject.FindProperty("returnSpeed");
        p_hitDistance      = serializedObject.FindProperty("hitDistance");
        p_pullbackDistance = serializedObject.FindProperty("pullbackDistance");

        p_projectilePrefab      = serializedObject.FindProperty("projectilePrefab");
        p_projectileSpeed       = serializedObject.FindProperty("projectileSpeed");
        p_projectileSize        = serializedObject.FindProperty("projectileSize");
        p_projectileBounce      = serializedObject.FindProperty("projectileBounce");
        p_projectileMaxBounces  = serializedObject.FindProperty("projectileMaxBounces");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 타입 선택
        EditorGUILayout.PropertyField(p_weaponType, new GUIContent("무기 타입"));
        EditorGUILayout.Space(4);

        // 공통
        EditorGUILayout.LabelField("공통 스탯", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_attackDamage,   new GUIContent("공격력"));
        EditorGUILayout.PropertyField(p_attackCooldown, new GUIContent("공격 쿨타임"));
        EditorGUILayout.PropertyField(p_attackRange,    new GUIContent("공격 범위"));

        WeaponType type = (WeaponType)p_weaponType.enumValueIndex;

        if (type == WeaponType.Melee)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("근거리 전용 (Melee)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_pullbackSpeed,    new GUIContent("풀백 속도"));
            EditorGUILayout.PropertyField(p_strikeSpeed,      new GUIContent("돌진 속도"));
            EditorGUILayout.PropertyField(p_returnSpeed,      new GUIContent("복귀 속도"));
            EditorGUILayout.PropertyField(p_hitDistance,      new GUIContent("타격 판정 거리"));
            EditorGUILayout.PropertyField(p_pullbackDistance, new GUIContent("풀백 거리"));
        }
        else // Ranged, Magic
        {
            EditorGUILayout.Space(6);
            string label = type == WeaponType.Magic ? "원거리 전용 (Magic)" : "원거리 전용 (Ranged)";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_projectilePrefab,     new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileSpeed,      new GUIContent("투사체 속도"));
            EditorGUILayout.PropertyField(p_projectileSize,       new GUIContent("투사체 크기"));
            EditorGUILayout.PropertyField(p_projectileBounce,     new GUIContent("경계 반사"));
            if (p_projectileBounce.boolValue)
                EditorGUILayout.PropertyField(p_projectileMaxBounces, new GUIContent("최대 반사 횟수"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
