#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponPoint), true)]
public class WeaponInfoEditor : Editor
{
    SerializedProperty p_weaponType;
    SerializedProperty p_magicElement;
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

    // 번개 뮤즐 전용
    SerializedProperty p_muzzlePoint;
    SerializedProperty p_muzzleVfxPrefab;
    SerializedProperty p_zapBoltVfxPrefab;

    // Melee 당기기
    SerializedProperty p_pullOnReturn;
    SerializedProperty p_pullRadius;
    SerializedProperty p_pullStrength;
    SerializedProperty p_pullDuration;

    // Hammer 전용
    SerializedProperty p_hammerRiseHeight;
    SerializedProperty p_hammerRiseTime;
    SerializedProperty p_hammerSpinSpeed;
    SerializedProperty p_hammerSlamSpeed;
    SerializedProperty p_hammerImpactWorldPos;
    SerializedProperty p_hammerImpactRadius;
    SerializedProperty p_hammerImpactVfxPrefab;
    SerializedProperty p_hammerHighlightScale;
    SerializedProperty p_hammerHighlightDur;

    // 슬래시 전용
    SerializedProperty p_slashRadius;
    SerializedProperty p_slashArcDegrees;
    SerializedProperty p_slashSweepSpeed;
    SerializedProperty p_slashHitRadius;
    SerializedProperty p_slashVfxPrefab;
    SerializedProperty p_slashVfxDuration;
    SerializedProperty p_slashTeleportMaxDist;
    SerializedProperty p_slashFadeTime;

    // VFX 미리보기
    private GameObject _vfxPreview;
    private bool       _isPreviewing;

    private void OnEnable()
    {
        p_weaponType             = serializedObject.FindProperty("weaponType");
        p_magicElement           = serializedObject.FindProperty("magicElement");
        p_projectilePrefab       = serializedObject.FindProperty("projectilePrefab");
        p_projectileSpeed        = serializedObject.FindProperty("projectileSpeed");
        p_useParabolicShot       = serializedObject.FindProperty("useParabolicShot");
        p_parabolicArcHeight     = serializedObject.FindProperty("parabolicArcHeight");
        p_parabolicSpawnLocalPos = serializedObject.FindProperty("parabolicSpawnLocalPos");
        p_areaHitDamageRatio     = serializedObject.FindProperty("areaHitDamageRatio");
        p_orbitRadius            = serializedObject.FindProperty("orbitRadius");
        p_orbitSpeed             = serializedObject.FindProperty("orbitSpeed");
        p_spinHitRadius          = serializedObject.FindProperty("spinHitRadius");
        p_spinHitCooldown        = serializedObject.FindProperty("spinHitCooldown");
        p_muzzlePoint            = serializedObject.FindProperty("muzzlePoint");
        p_muzzleVfxPrefab        = serializedObject.FindProperty("muzzleVfxPrefab");
        p_zapBoltVfxPrefab       = serializedObject.FindProperty("zapBoltVfxPrefab");
        p_pullOnReturn           = serializedObject.FindProperty("pullOnReturn");
        p_pullRadius             = serializedObject.FindProperty("pullRadius");
        p_pullStrength           = serializedObject.FindProperty("pullStrength");
        p_pullDuration           = serializedObject.FindProperty("pullDuration");
        p_hammerRiseHeight       = serializedObject.FindProperty("hammerRiseHeight");
        p_hammerRiseTime         = serializedObject.FindProperty("hammerRiseTime");
        p_hammerSpinSpeed        = serializedObject.FindProperty("hammerSpinSpeed");
        p_hammerSlamSpeed        = serializedObject.FindProperty("hammerSlamSpeed");
        p_hammerImpactWorldPos   = serializedObject.FindProperty("hammerImpactWorldPos");
        p_hammerImpactRadius     = serializedObject.FindProperty("hammerImpactRadius");
        p_hammerImpactVfxPrefab  = serializedObject.FindProperty("hammerImpactVfxPrefab");
        p_hammerHighlightScale   = serializedObject.FindProperty("hammerHighlightScale");
        p_hammerHighlightDur     = serializedObject.FindProperty("hammerHighlightDur");
        p_slashRadius            = serializedObject.FindProperty("slashRadius");
        p_slashArcDegrees        = serializedObject.FindProperty("slashArcDegrees");
        p_slashSweepSpeed        = serializedObject.FindProperty("slashSweepSpeed");
        p_slashHitRadius         = serializedObject.FindProperty("slashHitRadius");
        p_slashVfxPrefab         = serializedObject.FindProperty("slashVfxPrefab");
        p_slashVfxDuration       = serializedObject.FindProperty("slashVfxDuration");
        p_slashTeleportMaxDist   = serializedObject.FindProperty("slashTeleportMaxDist");
        p_slashFadeTime          = serializedObject.FindProperty("slashFadeTime");
    }

    private void OnDisable() => StopPreview();

    private void StopPreview()
    {
        if (_vfxPreview != null) { DestroyImmediate(_vfxPreview); _vfxPreview = null; }
        _isPreviewing = false;
    }

    private void OnSceneGUI()
    {
        var weapon = (WeaponPoint)target;
        WeaponType type = (WeaponType)p_weaponType.enumValueIndex;

        // ── Hammer 착지 위치 핸들 (세계 좌표) ───────────────────────
        if (type == WeaponType.Hammer)
        {
            Vector2 impact   = p_hammerImpactWorldPos.vector2Value;
            Vector3 worldPos = new Vector3(impact.x, impact.y, 0f);
            float   radius   = p_hammerImpactRadius.floatValue;
            float   peakH    = p_hammerRiseHeight.floatValue;

            // 충격 범위 디스크
            Handles.color = new Color(1f, 0.4f, 0f, 0.18f);
            Handles.DrawSolidDisc(worldPos, Vector3.forward, radius);
            Handles.color = new Color(1f, 0.4f, 0f, 1f);
            Handles.DrawWireDisc(worldPos, Vector3.forward, radius);

            // 정점(상공) 표시
            Vector3 peakPos = worldPos + Vector3.up * peakH;
            Handles.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Handles.DrawDottedLine(worldPos, peakPos, 4f);
            Handles.DrawWireDisc(peakPos, Vector3.forward, 0.15f);

            // 드래그 가능한 점
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(worldPos, 0.12f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Hammer Impact World Pos");
                p_hammerImpactWorldPos.vector2Value = new Vector2(moved.x, moved.y);
                serializedObject.ApplyModifiedProperties();
            }

            Handles.Label(worldPos + Vector3.up * (radius + 0.15f), "착지");
            Handles.Label(peakPos + Vector3.up * 0.2f, "정점");
        }

        // ── Ranged 포물선 발사 위치 (기존) ────────────────────────────
        if (type == WeaponType.Ranged && p_useParabolicShot != null && p_useParabolicShot.boolValue)
        {
            Vector3 spawnWorld = weapon.transform.TransformPoint(p_parabolicSpawnLocalPos.vector3Value);
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, spawnWorld, Quaternion.identity, 0.1f, EventType.Repaint);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(p_weaponType, new GUIContent("무기 타입"));

        WeaponType type = (WeaponType)p_weaponType.enumValueIndex;

        // ── MAGIC ─────────────────────────────────────────────────────
        if (type == WeaponType.Magic)
        {
            EditorGUILayout.PropertyField(p_magicElement, new GUIContent("마법 속성"));

            var element = (MagicElement)p_magicElement.enumValueIndex;
            if (element == MagicElement.Fire)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("불꽃 호밍 투사체", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(p_projectilePrefab, new GUIContent("호밍 투사체 프리팹"));
                EditorGUILayout.PropertyField(p_projectileSpeed,  new GUIContent("투사체 속도"));
                EditorGUILayout.HelpBox("FireHomingProjectile 컴포넌트가 있는 프리팹을 연결하세요.\n적을 자동 추적하며, 공격력·출혈·흡혈이 자동 적용됩니다.", MessageType.None);
            }

            if (element == MagicElement.Lightning)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("번개 Muzzle", EditorStyles.boldLabel);
                if (p_muzzlePoint      != null) EditorGUILayout.PropertyField(p_muzzlePoint,      new GUIContent("뮤즐 Transform"));
                if (p_muzzleVfxPrefab  != null) EditorGUILayout.PropertyField(p_muzzleVfxPrefab,  new GUIContent("뮤즐 VFX 프리팹"));
                if (p_zapBoltVfxPrefab != null) EditorGUILayout.PropertyField(p_zapBoltVfxPrefab, new GUIContent("번개 볼트 VFX 프리팹"));
                else EditorGUILayout.HelpBox("WeaponPoint에 muzzlePoint / muzzleVfxPrefab / zapBoltVfxPrefab 필드가 없습니다.", MessageType.Warning);
                EditorGUILayout.HelpBox("뮤즐 Transform: 무기 프리팹 자식 오브젝트로 배치 후 연결\n뮤즐 VFX: 발사 시 1초 후 자동 파괴됩니다.\n번개 볼트 VFX: 뮤즐→타겟 방향으로 스폰됩니다.", MessageType.None);
            }
        }

        EditorGUILayout.HelpBox(
            "공격력 · 탐지범위 · 공격속도 · 주변범위 · 튕김횟수\n넉백 · 치명타 · 출혈 · 흡혈  →  DB_무기에서 자동 로드",
            MessageType.Info);

        EditorGUILayout.Space(6);

        // ── 투사체 공통 (Magic은 속성별 섹션에서 별도 처리) ──────────
        if (type != WeaponType.Melee && type != WeaponType.Spin &&
            type != WeaponType.Slash && type != WeaponType.Magic)
        {
            EditorGUILayout.PropertyField(p_projectilePrefab, new GUIContent("투사체 프리팹"));
            EditorGUILayout.PropertyField(p_projectileSpeed,  new GUIContent("투사체 속도"));
        }

        if (type != WeaponType.Slash)
            EditorGUILayout.PropertyField(p_areaHitDamageRatio, new GUIContent("주변 데미지 배율 (0~1)"));

        // ── MELEE 당기기 ─────────────────────────────────────────────
        if (type == WeaponType.Melee)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("복귀 시 당기기", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_pullOnReturn, new GUIContent("당기기 활성화"));
            if (p_pullOnReturn != null && p_pullOnReturn.boolValue)
            {
                EditorGUILayout.PropertyField(p_pullRadius,   new GUIContent("당기기 범위"));
                EditorGUILayout.PropertyField(p_pullStrength, new GUIContent("당기기 힘"));
                EditorGUILayout.PropertyField(p_pullDuration, new GUIContent("당기기 지속시간 (초)"));
            }
        }

        // ── HAMMER 전용 ──────────────────────────────────────────────
        if (type == WeaponType.Hammer)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("해머 전용 — 상승 내려치기", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_hammerImpactVfxPrefab,    new GUIContent("착지 VFX 프리팹"));
            EditorGUILayout.PropertyField(p_hammerRiseHeight,         new GUIContent("상승 높이"));
            EditorGUILayout.PropertyField(p_hammerRiseTime,           new GUIContent("상승 시간 (초)"));
            EditorGUILayout.PropertyField(p_hammerSpinSpeed,          new GUIContent("자전 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_hammerSlamSpeed,          new GUIContent("하강 속도"));
            EditorGUILayout.PropertyField(p_hammerImpactWorldPos,     new GUIContent("착지 세계 좌표 (맵 정중앙)"));
            EditorGUILayout.PropertyField(p_hammerImpactRadius,       new GUIContent("충격 범위"));
            EditorGUILayout.PropertyField(p_hammerHighlightScale,  new GUIContent("충격 스케일 배율"));
            EditorGUILayout.PropertyField(p_hammerHighlightDur,    new GUIContent("하이라이트 지속시간 (초)"));
        }

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
            EditorGUILayout.PropertyField(p_slashVfxPrefab,         new GUIContent("슬래시 VFX 프리팹"));
            EditorGUILayout.PropertyField(p_slashVfxDuration,       new GUIContent("VFX 지속 시간 (초)"));
            EditorGUILayout.PropertyField(p_slashRadius,            new GUIContent("최소 베기 반경 (폴백)"));
            EditorGUILayout.PropertyField(p_slashArcDegrees,        new GUIContent("호 각도 (기본 180)"));
            EditorGUILayout.PropertyField(p_slashSweepSpeed,        new GUIContent("휩쓸기 속도 (deg/s)"));
            EditorGUILayout.PropertyField(p_slashHitRadius,         new GUIContent("타격 판정 반경"));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("텔레포트 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_slashTeleportMaxDist,   new GUIContent("최대 이동 거리"));
            EditorGUILayout.PropertyField(p_slashFadeTime,          new GUIContent("페이드 시간 (초)"));

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
                        Vector3 center    = weapon.transform.parent != null ? weapon.transform.parent.position : weapon.transform.position;
                        Vector2 weaponDir = (weapon.transform.position - center).sqrMagnitude > 0.001f
                            ? ((Vector2)(weapon.transform.position - center)).normalized : Vector2.up;
                        float radius      = Mathf.Max(p_slashRadius.floatValue, (weapon.transform.position - center).magnitude);
                        Vector3 spawnPos  = center + (Vector3)(weaponDir * radius);
                        float angle       = Mathf.Atan2(weaponDir.y, weaponDir.x) * Mathf.Rad2Deg - 90f;

                        _vfxPreview = (GameObject)PrefabUtility.InstantiatePrefab(prefabObj);
                        _vfxPreview.transform.position  = spawnPos;
                        _vfxPreview.transform.rotation  = Quaternion.Euler(0f, 0f, angle);
                        _vfxPreview.transform.localScale = Vector3.one;
                        _vfxPreview.hideFlags            = HideFlags.DontSaveInBuild;
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
