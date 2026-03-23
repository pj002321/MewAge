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
    SerializedProperty p_waveBreakpoints;
    SerializedProperty p_separationRadius;
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
        p_waveBreakpoints    = serializedObject.FindProperty("waveBreakpoints");
        p_separationRadius   = serializedObject.FindProperty("separationRadius");
        p_separationStrength = serializedObject.FindProperty("separationStrength");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("기본 스탯", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_moveSpeed,      new GUIContent("이동 속도"));
        EditorGUILayout.PropertyField(p_maxHP,          new GUIContent("최대 HP"));
        EditorGUILayout.PropertyField(p_contactDamage,  new GUIContent("접촉 데미지"));
        EditorGUILayout.PropertyField(p_damageCooldown, new GUIContent("접촉 데미지 쿨타임"));
        EditorGUILayout.PropertyField(p_contactMaxDist, new GUIContent("접촉 판정 거리"));
        EditorGUILayout.PropertyField(p_scoreValue,     new GUIContent("점수"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("드롭", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_goldDrop, new GUIContent("골드"));
        EditorGUILayout.PropertyField(p_expDrop,  new GUIContent("경험치"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("분리 (간격 유지)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_separationRadius,   new GUIContent("감지 반경"));
        EditorGUILayout.PropertyField(p_separationStrength, new GUIContent("밀어내는 힘"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("웨이브 구간별 스탯", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_waveBreakpoints, new GUIContent("Wave Breakpoints"), true);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
