#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyController 커스텀 인스펙터.
/// </summary>
[CustomEditor(typeof(EnemyController))]
public class EnemyControllerEditor : Editor
{
    SerializedProperty p_moveSpeed;
    SerializedProperty p_maxHP;
    SerializedProperty p_contactDamage;
    SerializedProperty p_scoreValue;
    SerializedProperty p_damageCooldown;
    SerializedProperty p_contactMaxDist;
    SerializedProperty p_goldDrop;
    SerializedProperty p_expDrop;
    SerializedProperty p_knockbackImmune;
    SerializedProperty p_bleedDuration;
    SerializedProperty p_bleedInterval;
    SerializedProperty p_separationStrength;

    private void OnEnable()
    {
        p_moveSpeed          = serializedObject.FindProperty("moveSpeed");
        p_maxHP              = serializedObject.FindProperty("maxHP");
        p_contactDamage      = serializedObject.FindProperty("contactDamage");
        p_scoreValue         = serializedObject.FindProperty("scoreValue");
        p_damageCooldown     = serializedObject.FindProperty("damageCooldown");
        p_contactMaxDist     = serializedObject.FindProperty("contactMaxDist");
        p_goldDrop           = serializedObject.FindProperty("goldDrop");
        p_expDrop            = serializedObject.FindProperty("expDrop");
        p_separationStrength = serializedObject.FindProperty("separationStrength");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("기본 스탯", EditorStyles.boldLabel);
        if (p_moveSpeed != null) EditorGUILayout.PropertyField(p_moveSpeed, new GUIContent("이동 속도"));
        if (p_maxHP != null) EditorGUILayout.PropertyField(p_maxHP, new GUIContent("최대 HP"));
        if (p_contactDamage != null) EditorGUILayout.PropertyField(p_contactDamage, new GUIContent("접촉 데미지"));
        if (p_damageCooldown != null) EditorGUILayout.PropertyField(p_damageCooldown, new GUIContent("접촉 데미지 쿨타임"));
        if (p_contactMaxDist != null) EditorGUILayout.PropertyField(p_contactMaxDist, new GUIContent("접촉 판정 거리"));
        if (p_scoreValue != null) EditorGUILayout.PropertyField(p_scoreValue, new GUIContent("점수"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("드롭", EditorStyles.boldLabel);
        if (p_goldDrop != null) EditorGUILayout.PropertyField(p_goldDrop, new GUIContent("골드"));
        if (p_expDrop != null) EditorGUILayout.PropertyField(p_expDrop, new GUIContent("경험치"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("출혈", EditorStyles.boldLabel);
        if (p_bleedDuration != null) EditorGUILayout.PropertyField(p_bleedDuration, new GUIContent("출혈 지속 시간"));
        if (p_bleedInterval != null) EditorGUILayout.PropertyField(p_bleedInterval, new GUIContent("출혈 간격"));
        if (p_knockbackImmune != null) EditorGUILayout.PropertyField(p_knockbackImmune, new GUIContent("넉백 면역"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("분리 (간격 유지)", EditorStyles.boldLabel);
        if (p_separationStrength != null) EditorGUILayout.PropertyField(p_separationStrength, new GUIContent("밀어내는 힘"));
        EditorGUILayout.Space(4);



        serializedObject.ApplyModifiedProperties();
    }
}
#endif
