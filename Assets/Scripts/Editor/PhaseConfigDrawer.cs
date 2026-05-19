using UnityEditor;
using UnityEngine;

/// <summary>
/// BossProjectileSkill.PhaseConfig 전용 PropertyDrawer.
/// 선택된 타입에 해당하는 필드만 표시하고 한글 라벨을 적용합니다.
/// </summary>
[CustomPropertyDrawer(typeof(BossProjectileSkill.PhaseConfig))]
public class PhaseConfigDrawer : PropertyDrawer
{
    private const float LineH = 20f;
    private const float Pad   = 2f;

    // ── 높이 계산 ─────────────────────────────────────────────────

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        => LineCount(prop) * (LineH + Pad);

    private static int LineCount(SerializedProperty prop)
    {
        var pType = GetType(prop);
        int n     = 4; // type, warningTime, count, interval(Fan 제외)

        switch (pType)
        {
            case BossProjectileSkill.ProjectileType.Aimed:
                n += 1; // accelTime
                if (prop.FindPropertyRelative("accelTime").floatValue > 0f) n++;
                break;
            case BossProjectileSkill.ProjectileType.Radial:
                n += 3; // dirCount, radialAngleOffset, accelTime
                if (prop.FindPropertyRelative("accelTime").floatValue > 0f) n++;
                break;
            case BossProjectileSkill.ProjectileType.Fan:
                n--; // interval 미표시
                n += 3; // dirCount, fanAngle, accelTime
                if (prop.FindPropertyRelative("accelTime").floatValue > 0f) n++;
                break;
            case BossProjectileSkill.ProjectileType.Homing:
                n += 2; // arcHeight, homingSpeed
                break;
            case BossProjectileSkill.ProjectileType.Volley:
                n += 12;
                break;
            case BossProjectileSkill.ProjectileType.Boulder:
                n += 5;
                break;
        }
        return n;
    }

    // ── 렌더링 ────────────────────────────────────────────────────

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var pType  = GetType(prop);
        pos.height = LineH;

        // 한 줄 그리고 Y를 내리는 헬퍼
        void Draw(string fieldName, string kor)
        {
            EditorGUI.PropertyField(pos, prop.FindPropertyRelative(fieldName), new GUIContent(kor));
            pos.y += LineH + Pad;
        }

        // ── 공통 ──────────────────────────────────────────────────
        Draw("type",        "타입");
        Draw("warningTime", "경고 시간 (초)");
        Draw("count",       "발사 횟수");
        if (pType != BossProjectileSkill.ProjectileType.Fan)
            Draw("interval", "발사 간격 (초)");

        // ── 방사형 · 부채꼴 공통 ──────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Radial ||
            pType == BossProjectileSkill.ProjectileType.Fan)
            Draw("dirCount", "방향 수");

        // ── 방사형 전용 ───────────────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Radial)
            Draw("radialAngleOffset", "각도 오프셋 (도)");

        // ── 부채꼴 전용 ───────────────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Fan)
            Draw("fanAngle", "부채꼴 각도 (도)");

        // ── 호밍 전용 ─────────────────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Homing)
        {
            Draw("arcHeight",   "포물선 높이");
            Draw("homingSpeed", "호밍 속도");
        }

        // ── 가속 (Aimed / Radial / Fan) ───────────────────────────
        if (pType != BossProjectileSkill.ProjectileType.Homing  &&
            pType != BossProjectileSkill.ProjectileType.Volley  &&
            pType != BossProjectileSkill.ProjectileType.Boulder)
        {
            Draw("accelTime", "가속 시간 (0=즉시)");
            if (prop.FindPropertyRelative("accelTime").floatValue > 0f)
                Draw("initialSpeedRatio", "초기 속도 비율");
        }

        // ── Volley 전용 ───────────────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Volley)
        {
            Draw("volleyPointCount",    "낙탄 수");
            Draw("volleySpreadRadius",  "산포 반경");
            Draw("volleyHangTime",      "체공 시간 (초)");
            Draw("volleyBurstInterval", "연속 충격 간격");
            Draw("volleyHitRadius",     "피해 반경");
            Draw("volleyHitDamage",     "피해량 (0=ContactDmg)");
            Draw("volleyShellArcHeight","포탄 포물선 높이");
            Draw("volleyShakeDuration", "카메라 흔들림 시간");
            Draw("volleyShakeMagnitude","카메라 흔들림 강도");
            Draw("volleyMarkerPrefab",  "마커 프리팹");
            Draw("volleyShellPrefab",   "포탄 프리팹");
            Draw("volleyImpactVfx",     "착탄 VFX");
        }

        // ── Boulder 전용 ──────────────────────────────────────────
        if (pType == BossProjectileSkill.ProjectileType.Boulder)
        {
            Draw("boulderSpeed",       "굴리기 속도");
            Draw("boulderLifetime",    "지속 시간 (초)");
            Draw("boulderMaxBounces",  "최대 반사 횟수");
            Draw("boulderRotateSpeed", "회전 속도 (도/초)");
            Draw("boulderRollInterval","연속 발사 간격");
        }

        EditorGUI.EndProperty();
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private static BossProjectileSkill.ProjectileType GetType(SerializedProperty prop)
        => (BossProjectileSkill.ProjectileType)prop.FindPropertyRelative("type").enumValueIndex;
}
