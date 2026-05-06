using System;
using UnityEngine;

/// <summary>
/// 시너지 아이콘 · 등급 배지 스프라이트를 한 곳에서 관리하는 ScriptableObject.
/// Resources/Synergy/SynergyVisualConfig 에 저장해두면 Shared 로 전역 접근 가능합니다.
///
/// 사용처: SynergySlot
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Synergy Visual Config", fileName = "SynergyVisualConfig")]
public class SynergyVisualConfig : ScriptableObject
{
    // ── 등급 배지 ────────────────────────────────────────────────────
    [Serializable]
    public struct GradeEntry
    {
        [Tooltip("DB f_등급 값 — 예: S / A / B")]
        public string grade;
        public Sprite sprite;
    }

    // ── 시너지 아이콘 ─────────────────────────────────────────────────
    [Serializable]
    public struct IconEntry
    {
        [SynergyID]
        public string synergyID;
        public Sprite icon;
    }

    [Header("등급 배지 스프라이트 (S / A / B …)")]
    public GradeEntry[] gradeSprites;

    [Header("시너지 아이콘 (f_시너지ID → Sprite)")]
    public IconEntry[] icons;

    // ── 조회 헬퍼 ────────────────────────────────────────────────────

    /// <summary>등급 문자열에 해당하는 배지 스프라이트를 반환합니다. 없으면 null.</summary>
    public Sprite GetGradeSprite(string grade)
    {
        foreach (var e in gradeSprites)
            if (e.grade == grade) return e.sprite;
        return null;
    }

    /// <summary>시너지 ID에 해당하는 아이콘을 반환합니다. 없으면 null.</summary>
    public Sprite GetIcon(string synergyID)
    {
        if (string.IsNullOrEmpty(synergyID)) return null;
        foreach (var e in icons)
            if (string.Equals(e.synergyID, synergyID, StringComparison.OrdinalIgnoreCase))
                return e.icon;
        return null;
    }

    // ── 전역 접근 ────────────────────────────────────────────────────
    private static SynergyVisualConfig _shared;
    public static SynergyVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<SynergyVisualConfig>("Synergy/SynergyVisualConfig");
            return _shared;
        }
    }
}
