#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponPoint 커스텀 인스펙터.
/// 공격력·탐지범위·공격속도·주변범위·튕김횟수·넉백·치명타·출혈·흡혈은
/// DB_무기에서 자동 로드되므로 표시하지 않습니다.
/// 프리팹별로 직접 설정이 필요한 값만 노출합니다.
/// </summary>
[CustomEditor(typeof(WeaponPoint), true)]
public class WeaponInfoEditor : Editor
{
    SerializedProperty p_weaponType;
    SerializedProperty p_projectileSpeed;
    SerializedProperty p_areaHitDamageRatio;

    // 스핀 전용
    SerializedProperty p_orbitRadius;
    SerializedProperty p_orbitSpeed;
    SerializedProperty p_spinHitRadius;
    SerializedProperty p_spinHitCooldown;

    private void OnEnable()
    {
        p_weaponType         = serializedObject.FindProperty("weaponType");
        p_projectileSpeed    = serializedObject.FindProperty("projectileSpeed");
        p_areaHitDamageRatio = serializedObject.FindProperty("areaHitDamageRatio");
        p_orbitRadius        = serializedObject.FindProperty("orbitRadius");
        p_orbitSpeed         = serializedObject.FindProperty("orbitSpeed");
        p_spinHitRadius      = serializedObject.FindProperty("spinHitRadius");
        p_spinHitCooldown    = serializedObject.FindProperty("spinHitCooldown");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(p_weaponType, new GUIContent("무기 타입"));

        EditorGUILayout.HelpBox(
            "공격력 · 탐지범위 · 공격속도 · 주변범위 · 튕김횟수\n넉백 · 치명타 · 출혈 · 흡혈  →  DB_무기에서 자동 로드",
            MessageType.Info);

        EditorGUILayout.Space(6);

        WeaponType type = (WeaponType)p_weaponType.enumValueIndex;

        // 투사체 속도 — 원거리·마법·부메랑·번개
        if (type != WeaponType.Melee && type != WeaponType.Spin)
            EditorGUILayout.PropertyField(p_projectileSpeed, new GUIContent("투사체 속도"));

        // 주변 데미지 배율 — 번개 제외 전 타입
        if (type != WeaponType.Lightning)
            EditorGUILayout.PropertyField(p_areaHitDamageRatio, new GUIContent("주변 데미지 배율 (0~1)"));

        // 스핀 전용 공전 파라미터
        if (type == WeaponType.Spin)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("스핀 전용", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_orbitRadius,     new GUIContent("공전 반경"));
            EditorGUILayout.PropertyField(p_orbitSpeed,      new GUIContent("공전 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_spinHitRadius,   new GUIContent("타격 판정 반경"));
            EditorGUILayout.PropertyField(p_spinHitCooldown, new GUIContent("재타격 쿨타임"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
