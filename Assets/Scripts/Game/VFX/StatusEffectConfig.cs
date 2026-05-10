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
