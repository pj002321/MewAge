#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponPoint), true)]
public class WeaponInfoEditor : Editor
{
    SerializedProperty p_weaponType;
    SerializedProperty p_projectilePrefab;
    SerializedProperty p_projectileSpeed;
    SerializedProperty p_useParabolicShot;
    SerializedProperty p_parabolicArcHeight;
    SerializedProperty p_parabolicSpawnLocalPos;
    SerializedProperty p_areaHitDamageRatio;

    // 스핀 전용
    SerializedProperty p_orbitRadius;
    SerializedProperty p_orbitSpeed;
    SerializedProperty p_spinHitRadius;
    SerializedProperty p_spinHitCooldown;

    // 슬래시 전용
    SerializedProperty p_slashRadius;
    SerializedProperty p_slashArcDegrees;
    SerializedProperty p_slashSweepSpeed;
    SerializedProperty p_slashHitRadius;
    SerializedProperty p_slashVfxPrefab;
    SerializedProperty p_slashVfxDuration;

    // VFX 미리보기
    private GameObject _vfxPreview;
    private bool       _isPreviewing;

    private void OnEnable()
    {
        p_weaponType          = serializedObject.FindProperty("weaponType");
        p_projectilePrefab    = serializedObject.FindProperty("projectilePrefab");
        p_projectileSpeed     = serializedObject.FindProperty("projectileSpeed");
        p_useParabolicShot       = serializedObject.FindProperty("useParabolicShot");
        p_parabolicArcHeight     = serializedObject.FindProperty("parabolicArcHeight");
        p_parabolicSpawnLocalPos = serializedObject.FindProperty("parabolicSpawnLocalPos");
        p_areaHitDamageRatio  = serializedObject.FindProperty("areaHitDamageRatio");
        p_orbitRadius         = serializedObject.FindProperty("orbitRadius");
        p_orbitSpeed          = serializedObject.FindProperty("orbitSpeed");
        p_spinHitRadius       = serializedObject.FindProperty("spinHitRadius");
        p_spinHitCooldown     = serializedObject.FindProperty("spinHitCooldown");
        p_slashRadius         = serializedObject.FindProperty("slashRadius");
        p_slashArcDegrees     = serializedObject.FindProperty("slashArcDegrees");
        p_slashSweepSpeed     = serializedObject.FindProperty("slashSweepSpeed");
        p_slashHitRadius      = serializedObject.FindProperty("slashHitRadius");
        p_slashVfxPrefab      = serializedObject.FindProperty("slashVfxPrefab");
        p_slashVfxDuration    = serializedObject.FindProperty("slashVfxDuration");
    }

    private void OnDisable() => StopPreview();

    private void StopPreview()
    {
        if (_vfxPreview != null) { DestroyImmediate(_vfxPreview); _vfxPreview = null; }
        _isPreviewing = false;
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

        // ── 투사체 공통 ──────────────────────────────────────────────
        if (type != WeaponType.Melee && type != WeaponType.Spin && type != WeaponType.Slash)
        {
            EditorGUILayout.PropertyField(p_projectilePrefab, new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileSpeed,  new GUIContent("투사체 속도"));
        }

        if (type != WeaponType.Lightning && type != WeaponType.Slash)
            EditorGUILayout.PropertyField(p_areaHitDamageRatio, new GUIContent("주변 데미지 배율 (0~1)"));

        // ── RANGED 포물선 옵션 ────────────────────────────────────────
        if (type == WeaponType.Ranged)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("포물선 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_useParabolicShot, new GUIContent("포물선 발사"));
            if (p_useParabolicShot.boolValue)
            {
                EditorGUILayout.PropertyField(p_parabolicArcHeight,     new GUIContent("호 높이"));
                EditorGUILayout.PropertyField(p_parabolicSpawnLocalPos, new GUIContent("발사 위치 (로컬)"));
                EditorGUILayout.HelpBox("씬 뷰에서 녹색 구체로 발사 위치를 확인할 수 있습니다.", MessageType.None);
            }
        }

        // ── SPIN 전용 ────────────────────────────────────────────────
        if (type == WeaponType.Spin)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("스핀 전용", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_orbitRadius,     new GUIContent("공전 반경"));
            EditorGUILayout.PropertyField(p_orbitSpeed,      new GUIContent("공전 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_spinHitRadius,   new GUIContent("타격 판정 반경"));
            EditorGUILayout.PropertyField(p_spinHitCooldown, new GUIContent("재타격 쿨타임"));
        }

        // ── SLASH 전용 ───────────────────────────────────────────────
        if (type == WeaponType.Slash)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("슬래시 전용 — 반원 베기", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_slashVfxPrefab,   new GUIContent("슬래시 VFX 프리팹"));
            EditorGUILayout.PropertyField(p_slashVfxDuration, new GUIContent("VFX 지속 시간 (초)"));
            EditorGUILayout.PropertyField(p_slashRadius,      new GUIContent("최소 베기 반경 (폴백)"));
            EditorGUILayout.PropertyField(p_slashArcDegrees,  new GUIContent("호 각도 (기본 180)"));
            EditorGUILayout.PropertyField(p_slashSweepSpeed,  new GUIContent("휩쓸기 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_slashHitRadius,   new GUIContent("타격 판정 반경"));

            // VFX 미리보기 (아크 중간지점에 스폰)
            var prefabObj = p_slashVfxPrefab.objectReferenceValue as GameObject;
            if (prefabObj != null)
            {
                EditorGUILayout.Space(4);
                if (!_isPreviewing)
                {
                    if (GUILayout.Button("VFX 미리보기"))
                    {
                        var weapon = (WeaponPoint)target;
                        // 아크 중간지점 = 플레이어 위치에서 무기 방향으로 slashRadius 거리
                        Vector3 center    = weapon.transform.parent != null ? weapon.transform.parent.position : weapon.transform.position;
                        Vector2 weaponDir = (weapon.transform.position - center).sqrMagnitude > 0.001f
                            ? ((Vector2)(weapon.transform.position - center)).normalized : Vector2.up;
                        float radius      = Mathf.Max(p_slashRadius.floatValue, (weapon.transform.position - center).magnitude);
                        Vector3 spawnPos  = center + (Vector3)(weaponDir * radius);
                        float angle       = Mathf.Atan2(weaponDir.y, weaponDir.x) * Mathf.Rad2Deg - 90f;

                        _vfxPreview = (GameObject)PrefabUtility.InstantiatePrefab(prefabObj);
                        _vfxPreview.transform.position   = spawnPos;
                        _vfxPreview.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
                        _vfxPreview.transform.localScale  = Vector3.one;
                        _vfxPreview.hideFlags             = HideFlags.DontSaveInBuild;
                        _isPreviewing = true;
                    }
                }
                else
                {
                    if (GUILayout.Button("미리보기 종료"))
                        StopPreview();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
