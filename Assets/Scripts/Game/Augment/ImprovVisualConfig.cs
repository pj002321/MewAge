using System;
using UnityEngine;

// ── 증강 ID 열거형 ────────────────────────────────────────────────────
// DB_증강.f_증강ID 문자열과 1:1 대응합니다.
public enum AugId
{
    None,
    AUG_SIZE_HP_UP,
    AUG_SIZE_HP_DOWN,
    AUG_DEF,
    AUG_SPD_MOV,
    AUG_ATK,
    AUG_HEAL,
    AUG_EVA,
    AUG_MAX_HP,
    AUG_REWARD_RNG,
    AUG_DOT_DMG,
    AUG_CRI_RATE,
    AUG_CRI_DMG,
    AUG_HEAL_DEAD,
    AUG_SPW_HP_SKL,
    AUG_SPD_ATK,
}

/// <summary>
/// 증강 아이콘 스프라이트를 한 곳에서 관리하는 ScriptableObject.
/// Resources/AUG/ImprovVisualConfig 에 저장해두면 Shared로 전역 접근 가능합니다.
///
/// 사용처: ImprovCard
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Improv Visual Config", fileName = "ImprovVisualConfig")]
public class ImprovVisualConfig : ScriptableObject
{
    // ── 증강 아이콘 ──────────────────────────────────────────────────
    [Serializable]
    public struct AugEntry
    {
        public AugId  augId;
        public Sprite sprite;
    }

    [Header("증강 아이콘 (AugId → Sprite)")]
    public AugEntry[] augIcons;

    // ── 조회 헬퍼 ───────────────────────────────────────────────────
    public Sprite GetSprite(AugId id)
    {
        if (id == AugId.None) return null;
        foreach (var e in augIcons)
            if (e.augId == id) return e.sprite;
        return null;
    }

    /// <summary>DB 문자열 ID를 enum으로 변환 후 조회합니다.</summary>
    public Sprite GetSprite(string idStr)
    {
        if (string.IsNullOrEmpty(idStr)) return null;
        return Enum.TryParse<AugId>(idStr, out var id) ? GetSprite(id) : null;
    }

    // ── 전역 접근 ────────────────────────────────────────────────────
    private static ImprovVisualConfig _shared;
    public static ImprovVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<ImprovVisualConfig>("AUG/ImprovVisualConfig");
            return _shared;
        }
    }
}
