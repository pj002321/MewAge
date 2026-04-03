#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponPoint 인스펙터: weaponType에 따라 관련 필드만 표시합니다.
/// 공통 스탯(기본공격력·쿨타임·탐지범위·주변범위)은 DB_무기에서 런타임 로드 → 표시 안 함.
/// </summary>
[CustomEditor(typeof(WeaponPoint), true)]
public class WeaponInfoEditor : Editor
{
    // 무기 타입
    SerializedProperty p_weaponType;

    // 무기 고유 스탯 (프리팹별 설정)
    SerializedProperty p_criticalRate;
    SerializedProperty p_criticalMultiplier;
    SerializedProperty p_dotDamage;
    SerializedProperty p_lifeSteal;

    // 범위 타격
    SerializedProperty p_areaHitDamageRatio;

    // 근거리
    SerializedProperty p_strikeSpeed;
    SerializedProperty p_returnSpeed;
    SerializedProperty p_pullbackDist;
    SerializedProperty p_anticipationTime;
    SerializedProperty p_hitDistance;
    SerializedProperty p_overshootDistance;
    SerializedProperty p_overshootSpeed;

    // 원거리 / 부메랑 공통
    SerializedProperty p_projectilePrefab;
    SerializedProperty p_projectileSpeed;

    // 발사 스케일 펄스
    SerializedProperty p_shootScalePulse;
    SerializedProperty p_scalePulseMax;
    SerializedProperty p_scalePulseDuration;

    // 부메랑 전용
    SerializedProperty p_boomerangMaxBounces;
    SerializedProperty p_boomerangBounceRange;

    // 스핀 전용
    SerializedProperty p_orbitRadius;
    SerializedProperty p_orbitSpeed;
    SerializedProperty p_spinHitRadius;
    SerializedProperty p_spinHitCooldown;
    SerializedProperty p_spinKnockback;

    private void OnEnable()
    {
        p_weaponType          = serializedObject.FindProperty("weaponType");

        p_criticalRate        = serializedObject.FindProperty("criticalRate");
        p_criticalMultiplier  = serializedObject.FindProperty("criticalMultiplier");
        p_dotDamage           = serializedObject.FindProperty("dotDamage");
        p_lifeSteal           = serializedObject.FindProperty("lifeSteal");

        p_areaHitDamageRatio  = serializedObject.FindProperty("areaHitDamageRatio");

        p_strikeSpeed         = serializedObject.FindProperty("strikeSpeed");
        p_returnSpeed         = serializedObject.FindProperty("returnSpeed");
        p_pullbackDist        = serializedObject.FindProperty("pullbackDist");
        p_anticipationTime    = serializedObject.FindProperty("anticipationTime");
        p_hitDistance         = serializedObject.FindProperty("hitDistance");
        p_overshootDistance   = serializedObject.FindProperty("overshootDistance");
        p_overshootSpeed      = serializedObject.FindProperty("overshootSpeed");

        p_projectilePrefab    = serializedObject.FindProperty("projectilePrefab");
        p_projectileSpeed     = serializedObject.FindProperty("projectileSpeed");

        p_shootScalePulse     = serializedObject.FindProperty("shootScalePulse");
        p_scalePulseMax       = serializedObject.FindProperty("scalePulseMax");
        p_scalePulseDuration  = serializedObject.FindProperty("scalePulseDuration");

        p_boomerangMaxBounces  = serializedObject.FindProperty("boomerangMaxBounces");
        p_boomerangBounceRange = serializedObject.FindProperty("boomerangBounceRange");

        p_orbitRadius     = serializedObject.FindProperty("orbitRadius");
        p_orbitSpeed      = serializedObject.FindProperty("orbitSpeed");
        p_spinHitRadius   = serializedObject.FindProperty("spinHitRadius");
        p_spinHitCooldown = serializedObject.FindProperty("spinHitCooldown");
        p_spinKnockback   = serializedObject.FindProperty("spinKnockback");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(p_weaponType, new GUIContent("무기 타입"));
        EditorGUILayout.Space(4);

        // 공통 스탯은 DB_무기에서 런타임 로드 (기본공격력·공격속도·탐지범위·주변범위)
        EditorGUILayout.HelpBox("공통 스탯 (기본공격력 / 공격속도 / 탐지범위 / 주변범위) 은 DB_무기에서 자동 로드됩니다.", MessageType.Info);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("무기 고유 스탯", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(p_criticalRate,       new GUIContent("크리티컬 확률 (0~1)"));
        EditorGUILayout.PropertyField(p_criticalMultiplier, new GUIContent("크리티컬 배율"));
        EditorGUILayout.PropertyField(p_dotDamage,          new GUIContent("출혈 데미지"));
        EditorGUILayout.PropertyField(p_lifeSteal,          new GUIContent("흡혈 (0~1)"));

        WeaponType type = (WeaponType)p_weaponType.enumValueIndex;

        if (type == WeaponType.Melee || type == WeaponType.Spin)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(p_areaHitDamageRatio, new GUIContent("주변 데미지 배율"));
        }

        if (type == WeaponType.Melee)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("근거리 전용 (Melee)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_strikeSpeed,      new GUIContent("돌진 속도"));
            EditorGUILayout.PropertyField(p_returnSpeed,      new GUIContent("복귀 속도"));
            EditorGUILayout.PropertyField(p_pullbackDist,     new GUIContent("백스윙 거리"));
            EditorGUILayout.PropertyField(p_anticipationTime, new GUIContent("백스윙 대기 시간"));
            EditorGUILayout.PropertyField(p_hitDistance,      new GUIContent("타격 판정 거리"));
            EditorGUILayout.PropertyField(p_overshootDistance, new GUIContent("오버슛 거리"));
            EditorGUILayout.PropertyField(p_overshootSpeed,   new GUIContent("오버슛 속도"));
        }
        else if (type == WeaponType.Boomerang)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("부메랑 전용 (Boomerang)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_projectilePrefab,     new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileSpeed,      new GUIContent("투사체 속도"));
            EditorGUILayout.PropertyField(p_boomerangMaxBounces,  new GUIContent("최대 튕김 횟수"));
            EditorGUILayout.PropertyField(p_boomerangBounceRange, new GUIContent("튕김 탐색 범위"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(p_shootScalePulse,    new GUIContent("발사 스케일 펄스"));
            if (p_shootScalePulse.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(p_scalePulseMax,      new GUIContent("최대 배율"));
                EditorGUILayout.PropertyField(p_scalePulseDuration, new GUIContent("지속 시간"));
                EditorGUI.indentLevel--;
            }
        }
        else if (type == WeaponType.Spin)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("돌리기 전용 (Spin)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_orbitRadius,     new GUIContent("공전 반경"));
            EditorGUILayout.PropertyField(p_orbitSpeed,      new GUIContent("공전 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_spinHitRadius,   new GUIContent("타격 판정 반경"));
            EditorGUILayout.PropertyField(p_spinHitCooldown, new GUIContent("재타격 쿨타임"));
            EditorGUILayout.PropertyField(p_spinKnockback,   new GUIContent("넉백 강도"));
        }
        else // Ranged, Magic
        {
            EditorGUILayout.Space(6);
            string label = type == WeaponType.Magic ? "원거리 전용 (Magic)" : "원거리 전용 (Ranged)";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_projectilePrefab, new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileSpeed,  new GUIContent("투사체 속도"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(p_shootScalePulse,    new GUIContent("발사 스케일 펄스"));
            if (p_shootScalePulse.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(p_scalePulseMax,      new GUIContent("최대 배율"));
                EditorGUILayout.PropertyField(p_scalePulseDuration, new GUIContent("지속 시간"));
                EditorGUI.indentLevel--;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
