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
        public GameObject cloneProjectilePrefab;
        public float      cloneOutTime;
        public float      clonePauseTime;
        public float      cloneReturnTime;
        public float      cloneRange;
        public GameObject spikePrefab;
    }

    [Header("챕터별 상태효과 VFX 프리팹")]
    public ChapterEntry[] chapters;

    [Header("무기별 특수효과 프리팹 (HIT_CLONE / BOSS_SPIKE)")]
    public WeaponEntry[] weaponEntries = Array.Empty<WeaponEntry>();

    public GameObject GetFreeze(int chapter) => Get(chapter)?.freezeVfxPrefab;
    public GameObject GetStun  (int chapter) => Get(chapter)?.stunVfxPrefab;
    public GameObject GetFire  (int chapter) => Get(chapter)?.fireVfxPrefab;

    public float GetTriggerChance(string weaponId, float fallback)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) return e.triggerChance > 0f ? e.triggerChance : fallback;
        return fallback;
    }

    public GameObject GetClonePrefab(string weaponId)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) return e.cloneProjectilePrefab;
        return null;
    }

    public float GetCloneOutTime   (string weaponId, float fallback = 0.45f) => GetF(weaponId, e => e.cloneOutTime,    fallback);
    public float GetClonePauseTime (string weaponId, float fallback = 0.40f) => GetF(weaponId, e => e.clonePauseTime,  fallback);
    public float GetCloneReturnTime(string weaponId, float fallback = 0.35f) => GetF(weaponId, e => e.cloneReturnTime, fallback);
    public float GetCloneRange     (string weaponId, float fallback = 6f)    => GetF(weaponId, e => e.cloneRange,      fallback);

    private float GetF(string weaponId, System.Func<WeaponEntry, float> sel, float fallback)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) { float v = sel(e); return v > 0f ? v : fallback; }
        return fallback;
    }

    public GameObject GetSpikePrefab(string weaponId)
    {
        foreach (var e in weaponEntries)
            if (e.weaponId == weaponId) return e.spikePrefab;
        return null;
    }

    private ChapterEntry? Get(int chapter)
    {
        foreach (var e in chapters)
            if (e.chapter == chapter) return e;
        foreach (var e in chapters)
            if (e.chapter == 1) return e;
        return null;
    }

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
