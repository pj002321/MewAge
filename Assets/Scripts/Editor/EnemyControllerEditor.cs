#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// EnemyController 인스펙터: enemyType에 따라 관련 필드만 표시합니다.
/// Melee  → 공통 + 접촉 데미지 섹션
/// Ranged → 공통 + 원거리 섹션 (투사체 설정)
/// </summary>
[CustomEditor(typeof(EnemyController))]
public class EnemyControllerEditor : Editor
{
    // 타입
    SerializedProperty p_enemyType;

    // 공통
    SerializedProperty p_moveSpeed;
    SerializedProperty p_maxHP;
    SerializedProperty p_contactDamage;
    SerializedProperty p_scoreValue;
    SerializedProperty p_damageCooldown;

    // 드롭
    SerializedProperty p_goldDrop;
    SerializedProperty p_expDrop;

    // 원거리 전용
    SerializedProperty p_projectilePrefab;
    SerializedProperty p_projectileDamage;
    SerializedProperty p_projectileSpeed;
    SerializedProperty p_preferredRange;
    SerializedProperty p_fireCooldown;

    private void OnEnable()
    {
        p_enemyType      = serializedObject.FindProperty("unitType");
        p_moveSpeed      = serializedObject.FindProperty("moveSpeed");
        p_maxHP          = serializedObject.FindProperty("maxHP");
        p_contactDamage  = serializedObject.FindProperty("contactDamage");
        p_scoreValue     = serializedObject.FindProperty("scoreValue");
        p_damageCooldown = serializedObject.FindProperty("damageCooldown");
        p_goldDrop       = serializedObject.FindProperty("goldDrop");
        p_expDrop        = serializedObject.FindProperty("expDrop");

        p_projectilePrefab = serializedObject.FindProperty("projectilePrefab");
        p_projectileDamage = serializedObject.FindProperty("projectileDamage");
        p_projectileSpeed  = serializedObject.FindProperty("projectileSpeed");
        p_preferredRange   = serializedObject.FindProperty("preferredRange");
        p_fireCooldown     = serializedObject.FindProperty("fireCooldown");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 타입
        EditorGUILayout.PropertyField(p_enemyType, new GUIContent("적 타입"));
        EditorGUILayout.Space(4);

        // 공통 스탯
        EditorGUILayout.LabelField("공통 스탯", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_moveSpeed,      new GUIContent("이동 속도"));
        EditorGUILayout.PropertyField(p_maxHP,          new GUIContent("최대 HP"));
        EditorGUILayout.PropertyField(p_scoreValue,     new GUIContent("점수"));
        EditorGUILayout.PropertyField(p_contactDamage,  new GUIContent("접촉 데미지"));
        EditorGUILayout.PropertyField(p_damageCooldown, new GUIContent("접촉 데미지 쿨타임"));
        EditorGUILayout.Space(4);

        // 드롭
        EditorGUILayout.LabelField("드롭", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_goldDrop, new GUIContent("골드"));
        EditorGUILayout.PropertyField(p_expDrop,  new GUIContent("경험치"));

        UnitType type = (UnitType)p_enemyType.enumValueIndex;

        if (type == UnitType.원거리)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("원거리 전용 (Ranged)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_projectilePrefab, new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileDamage, new GUIContent("투사체 데미지"));
            EditorGUILayout.PropertyField(p_projectileSpeed,  new GUIContent("투사체 속도"));
            EditorGUILayout.PropertyField(p_preferredRange,   new GUIContent("선호 교전 거리"));
            EditorGUILayout.PropertyField(p_fireCooldown,     new GUIContent("발사 간격(초)"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
