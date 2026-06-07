using System;
using UnityEngine;

/// <summary>
/// 상태효과 VFX 및 무기별 특수효과 프리팹을 관리하는 ScriptableObject.
/// Resources/StatusEffectConfig 에 저장하면 Shared로 전역 접근됩니다.
/// MuAge/Weapon Special Effect Config Editor 창에서 편집하세요.
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Status Effect Config", fileName = "StatusEffectConfig")]
public class StatusEffectConfig : ScriptableObject
{
    // ── 열거형 ────────────────────────────────────────────────────

    /// <summary>HIT_STUN 통합 효과 종류. 무기별로 StatusEffectConfig에서 설정합니다.</summary>
    public enum HitStunType { Stun, Freeze, Fire }

    // ── 데이터 구조 ───────────────────────────────────────────────

    [Serializable]
    public struct ChapterEntry
    {
        public int        chapter;
        public GameObject freezeVfxPrefab;
        public GameObject stunVfxPrefab;
        public GameObject fireVfxPrefab;
    }

    [Serializable]
    public struct WeaponEntry
    {
        public string     weaponId;
        public string     traitId;
        public float      triggerChance;          // 0 = 기본값 사용

        // ── 스킬 오버라이드 ────────────────────────────────────
        [Tooltip("에디터에서 할당한 스킬 ID. 비워두면 DB f_특성 사용")]
        public string     skillOverride;

        // ── HIT_CLONE ──────────────────────────────────────────
        public GameObject cloneProjectilePrefab;
        public float      cloneOutTime;
        public float      clonePauseTime;
        public float      cloneReturnTime;
        public float      cloneRange;

        // ── HIT_SPIKE ──────────────────────────────────────────
        public GameObject spikePrefab;

        // ── HIT_STUN 통합 ──────────────────────────────────────
        [Tooltip("HIT_STUN / HIT_ICE / HIT_FIRE 공용. Stun·Freeze·Fire 중 선택")]
        public HitStunType hitStunType;
        [Tooltip("Freeze 시 스프라이트 색상. 기본값(000000 alpha=0)이면 하늘색 자동 적용")]
        public Color       hitStunColor;
        [Tooltip("무기별 상태이상 VFX. null이면 챕터별 기본 VFX 사용")]
        public GameObject  hitEffectVfxPrefab;

        // ── HIT_SPIN ───────────────────────────────────────────
        [Tooltip("true = 무기 해제 전까지 고스트 영구 공전. false = 4초 후 자동 소멸")]
        public bool hitSpinPermanent;

        // ── SHOOT_SUB ──────────────────────────────────────────
        [Tooltip("SHOOT_SUB 서브 투사체 프리팹. null이면 주 투사체 프리팹 사용")]
        public GameObject subProjectilePrefab;

        // ── BUFF_* 패시브 ──────────────────────────────────────
        [Tooltip("BUFF_* 패시브 버프 수치 (%). 0이면 기본값 2% 사용")]
        public float      passiveBuffPct;
        [Tooltip("장착 시 플레이어 머리 위에 떠오르다 사라질 VFX 프리팹")]
        public GameObject passiveBuffVfxPrefab;

        // ── 공격 VFX ───────────────────────────────────────────
        [Tooltip("타격 명중 시 VFX 프리팹. null이면 FXManager 기본 hitEffect 사용")]
        public GameObject hitVfxPrefab;

        // ── 스킬 파라미터 (0 = WeaponHighGradeSkill 기본값 사용) ──

        // HIT_STUN · HIT_ICE · HIT_FIRE
        [Tooltip("상태이상 지속 시간 (기본 1.5)")] public float p_stunDuration;
        [Tooltip("빙결 해제 시 추가 피해 HIT_ICE (기본 0)")] public float p_freezeThawDmg;

        // HIT_CLONE_*
        [Tooltip("클론 이동 속도 (기본 10)")] public float p_cloneSpeed;
        [Tooltip("클론 생존 시간 (기본 1.8)")] public float p_cloneLifetime;
        [Tooltip("클론 크기 배율 (기본 2)")] public float p_cloneScale;
        [Tooltip("클론 투명도 0~1 (기본 0.45)")] public float p_cloneAlpha;

        // SHOOT_SUB
        [Tooltip("서브 발사 각도 오프셋 (기본 18)")] public float p_spreadAngle;
        [Tooltip("서브 데미지 비율 (기본 0.6)")] public float p_subDmgRatio;
        [Tooltip("서브 속도 비율 (기본 0.9)")] public float p_subSpeedRatio;

        // HIT_SLASH
        [Tooltip("슬래시 투사체 프리팹")] public GameObject p_slashPrefab;
        [Tooltip("슬래시 데미지 비율 (기본 0.5)")] public float p_slashDmgRatio;
        [Tooltip("슬래시 투사체 속도 (기본 9)")] public float p_slashSpeed;

        // HIT_SPIN
        [Tooltip("공전 반경 배율 (기본 1.4)")] public float p_spinRadiusMult;
        [Tooltip("공전 속도 배율 (기본 1.35)")] public float p_spinSpeedMult;
        [Tooltip("클론 생성 확률 (기본 0.3)")] public float p_spinCloneChance;
        [Tooltip("클론 공전 반경 (기본 1.5)")] public float p_spinCloneRadius;
        [Tooltip("클론 공전 속도 deg/s (기본 220)")] public float p_spinCloneSpeed;
        [Tooltip("클론 지속 시간 (기본 3)")] public float p_spinCloneDuration;
        [Tooltip("클론 타격 반경 (기본 0.4)")] public float p_spinCloneHitRadius;
        [Tooltip("클론 투명도 (기본 0.5)")] public float p_spinCloneAlpha;
        [Tooltip("최대 동시 클론 수 (기본 3, 0=기본)")] public int  p_spinCloneMax;

        // HIT_SHOCK
        [Tooltip("충격파 반경 (기본 4.5)")] public float p_shockRadius;
        [Tooltip("충격파 데미지 비율 (기본 0.4)")] public float p_shockDmgRatio;

        // BOSS_SPIKE · HIT_SPIKE
        [Tooltip("틱 피해량 (기본 5)")] public float p_spikeTickDmg;
        [Tooltip("틱 간격 (기본 0.5)")] public float p_spikeTickInterval;
        [Tooltip("스파이크 지속 시간 (기본 5)")] public float p_spikeDuration;
        [Tooltip("사망 폭발 반경 (기본 2.5)")] public float p_spikeDeathRadius;
        [Tooltip("사망 폭발 피해 (기본 20)")] public float p_spikeDeathDmg;

        // BOSS_BIND
        [Tooltip("구속 시간 (기본 2)")] public float p_bindDuration;

        // BOSS_GROGY
        [Tooltip("그로기 시간 (기본 2)")] public float p_groggySeconds;

        // HIT_SPEAR
        [Tooltip("창 프리팹")] public GameObject p_spearPrefab;
        [Tooltip("착지 VFX 프리팹")] public GameObject p_spearLandingVfx;
        [Tooltip("낙하 높이 (기본 8)")] public float p_spearDropHeight;
        [Tooltip("사선 수평 오프셋 (기본 3)")] public float p_spearSideOffset;
        [Tooltip("이동 속도 (기본 18)")] public float p_spearSpeed;
        [Tooltip("데미지 비율 (기본 1)")] public float p_spearDmgRatio;
        [Tooltip("타격 반경 (기본 0.45)")] public float p_spearHitRadius;

        // SHOOT_SPREAD
        [Tooltip("피해 비율 (기본 0.12)")] public float p_sprayDmgRatio;

        // HIT_CLONE_TRAIL
        [Tooltip("클론 프리팹")] public GameObject p_trailClonePrefab;
        [Tooltip("마커 프리팹")] public GameObject p_trailMarkerPrefab;
        [Tooltip("이동 속도 (기본 6)")] public float p_trailSpeed;
        [Tooltip("이동 거리 (기본 8)")] public float p_trailRange;
        [Tooltip("데미지 비율 (기본 0.4)")] public float p_trailDmgRatio;
        [Tooltip("마커 지속 시간 (기본 2)")] public float p_trailDuration;
        [Tooltip("마커 생성 간격 (기본 0.15)")] public float p_trailSpawnInterval;
        [Tooltip("마커 타격 반경 (기본 0.5)")] public float p_trailHitRadius;
        [Tooltip("클론 투명도 (기본 0.5)")] public float p_trailCloneAlpha;
    }

    // ── 인스펙터 필드 ─────────────────────────────────────────────

    [Header("챕터별 상태효과 VFX 프리팹")]
    public ChapterEntry[] chapters;

    [Header("무기별 특수효과 설정")]
    public WeaponEntry[] weaponEntries = Array.Empty<WeaponEntry>();

    // ── 챕터 VFX 접근자 ───────────────────────────────────────────

    public GameObject GetFreeze(int chapter) => Get(chapter)?.freezeVfxPrefab;
    public GameObject GetStun  (int chapter) => Get(chapter)?.stunVfxPrefab;
    public GameObject GetFire  (int chapter) => Get(chapter)?.fireVfxPrefab;

    // ── 공통 ─────────────────────────────────────────────────────

    public float GetTriggerChance(string weaponId, float fallback)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) return e.triggerChance > 0f ? e.triggerChance : fallback;
        return fallback;
    }

    // ── HIT_CLONE ─────────────────────────────────────────────────

    public GameObject GetClonePrefab    (string weaponId) => FindEntry(weaponId)?.cloneProjectilePrefab;
    public float      GetCloneOutTime   (string weaponId, float fb = 0.45f) => GetF(weaponId, e => e.cloneOutTime,    fb);
    public float      GetClonePauseTime (string weaponId, float fb = 0.40f) => GetF(weaponId, e => e.clonePauseTime,  fb);
    public float      GetCloneReturnTime(string weaponId, float fb = 0.35f) => GetF(weaponId, e => e.cloneReturnTime, fb);
    public float      GetCloneRange     (string weaponId, float fb = 6f)    => GetF(weaponId, e => e.cloneRange,      fb);

    // ── HIT_SPIKE ─────────────────────────────────────────────────

    public GameObject GetSpikePrefab(string weaponId) => FindEntry(weaponId)?.spikePrefab;

    // ── HIT_STUN 통합 ─────────────────────────────────────────────

    /// <summary>무기별 VFX → 없으면 챕터 기본값 순으로 반환.</summary>
    public GameObject GetHitEffectVfx(string weaponId, int chapter, string traitId)
    {
        var e = FindEntry(weaponId);
        if (e?.hitEffectVfxPrefab != null) return e.Value.hitEffectVfxPrefab;
        return traitId switch
        {
            "HIT_ICE"  => GetFreeze(chapter),
            "HIT_FIRE" => GetFire(chapter),
            _          => GetStun(chapter),
        };
    }

    /// <summary>weaponId 항목의 stun 타입을 반환합니다. 항목 없으면 traitId 기반 기본값.</summary>
    public HitStunType GetHitStunType(string weaponId, string traitIdFallback)
    {
        var entry = FindEntry(weaponId);
        if (entry != null) return entry.Value.hitStunType;
        return traitIdFallback switch
        {
            "HIT_ICE"  => HitStunType.Freeze,
            "HIT_FIRE" => HitStunType.Fire,
            _          => HitStunType.Stun,
        };
    }

    /// <summary>weaponId 항목의 stun 색상을 반환합니다. 미설정(alpha=0)이면 타입별 기본색.</summary>
    public Color GetHitStunColor(string weaponId, Color typeFallback)
    {
        var entry = FindEntry(weaponId);
        if (entry != null && entry.Value.hitStunColor.a > 0f)
            return entry.Value.hitStunColor;
        return typeFallback;
    }

    // ── HIT_SPIN ──────────────────────────────────────────────────

    public bool GetHitSpinPermanent(string weaponId)
    {
        var entry = FindEntry(weaponId);
        return entry?.hitSpinPermanent ?? false;
    }

    // ── SHOOT_SUB ─────────────────────────────────────────────────

    public GameObject GetSubProjectilePrefab(string weaponId)
        => FindEntry(weaponId)?.subProjectilePrefab;

    // ── BUFF_* 패시브 ─────────────────────────────────────────────

    public float      GetPassiveBuffPct(string weaponId, float fallback = 2f)
        => GetF(weaponId, e => e.passiveBuffPct, fallback);

    public GameObject GetPassiveBuffVfx(string weaponId)
        => FindEntry(weaponId)?.passiveBuffVfxPrefab;

    // ── 공격 VFX ─────────────────────────────────────────────────

    public GameObject GetHitVfxPrefab(string weaponId)
        => FindEntry(weaponId)?.hitVfxPrefab;

    // ── 스킬 오버라이드 ───────────────────────────────────────────

    public string GetSkillOverride(string weaponId)
        => FindEntry(weaponId)?.skillOverride;

    // ── 스킬 파라미터 오버라이드 헬퍼 ────────────────────────────

    public float GetP(string weaponId, System.Func<WeaponEntry, float> sel, float fallback)
    {
        var e = FindEntry(weaponId); if (e == null) return fallback;
        float v = sel(e.Value); return v > 0f ? v : fallback;
    }
    public int GetPI(string weaponId, System.Func<WeaponEntry, int> sel, int fallback)
    {
        var e = FindEntry(weaponId); if (e == null) return fallback;
        int v = sel(e.Value); return v > 0 ? v : fallback;
    }
    public Color GetPC(string weaponId, System.Func<WeaponEntry, Color> sel, Color fallback)
    {
        var e = FindEntry(weaponId); if (e == null) return fallback;
        Color c = sel(e.Value); return c.a > 0f ? c : fallback;
    }
    public GameObject GetPGO(string weaponId, System.Func<WeaponEntry, GameObject> sel)
        => FindEntry(weaponId) is { } e ? sel(e) : null;

    // ── 내부 헬퍼 ─────────────────────────────────────────────────

    private WeaponEntry? FindEntry(string weaponId)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) return e;
        return null;
    }

    private float GetF(string weaponId, Func<WeaponEntry, float> sel, float fallback)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) { float v = sel(e); return v > 0f ? v : fallback; }
        return fallback;
    }

    private ChapterEntry? Get(int chapter)
    {
        foreach (var e in chapters)
            if (e.chapter == chapter) return e;
        foreach (var e in chapters)
            if (e.chapter == 1) return e;
        return null;
    }

    // ── 싱글톤 ───────────────────────────────────────────────────

    private static StatusEffectConfig _shared;
    public static StatusEffectConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<StatusEffectConfig>("StatusEffectConfig");
            return _shared;
        }
    }
}
