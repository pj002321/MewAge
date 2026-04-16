using System;
using UnityEngine;

// ── 열거형 ────────────────────────────────────────────────────────────────
public enum WareGrade { Normal, Rare, Epic, Legend, S }
public enum WareRole  { CC, Burst, Sustained, Buff }

/// <summary>
/// 기물 슬롯 비주얼 에셋을 한 곳에서 관리하는 ScriptableObject.
/// Resources/Ware/WareVisualConfig 에 저장해두면 Shared로 전역 접근 가능합니다.
///
/// 사용처: WareSlotView, HomeWarePanel
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Ware Visual Config", fileName = "WareVisualConfig")]
public class WareVisualConfig : ScriptableObject
{
    // ── 인벤토리 등급 프레임 ─────────────────────────────────────────
    [Serializable]
    public struct GradeEntry
    {
        public WareGrade grade;
        public Sprite    frame;
    }

    // ── 프리셋 슬롯 전용 등급 프레임 ────────────────────────────────
    [Serializable]
    public struct PresetGradeEntry
    {
        public WareGrade grade;
        public Sprite    presetFrame;
    }

    // ── 역할 아이콘 ──────────────────────────────────────────────────
    [Serializable]
    public struct RoleEntry
    {
        public WareRole role;
        public Sprite   icon;
    }

    // ── 챕터별 기물 아이콘 ───────────────────────────────────────────
    /// <summary>
    /// 챕터 하나의 기물 아이콘 묶음.
    /// entries[0] → C{chapter}_W0, entries[1] → C{chapter}_W1 …
    /// </summary>
    [Serializable]
    public struct ChapterIconGroup
    {
        [Range(1, 7)]
        public int      chapter;
        public Sprite[] entries;
    }

    [Header("인벤·합성 슬롯 등급 프레임")]
    public GradeEntry[] gradeFrames;

    [Header("프리셋 슬롯 전용 등급 프레임 (장착 시 표시)")]
    public PresetGradeEntry[] presetFrames;

    [Header("역할 아이콘")]
    public RoleEntry[] roleIcons;

    [Header("챕터별 기물 아이콘 (entries 순서 = W 번호)")]
    public ChapterIconGroup[] chapterIcons;

    // ── 조회 헬퍼 ───────────────────────────────────────────────────
    public Sprite GetGradeFrame(WareGrade grade)
    {
        foreach (var e in gradeFrames)
            if (e.grade == grade) return e.frame;
        return null;
    }

    public Sprite GetPresetFrame(WareGrade grade)
    {
        foreach (var e in presetFrames)
            if (e.grade == grade) return e.presetFrame;
        return null;
    }

    public Sprite GetRoleIcon(WareRole role)
    {
        foreach (var e in roleIcons)
            if (e.role == role) return e.icon;
        return null;
    }

    /// <summary>
    /// "C1_W3" 형식의 ID로 아이콘 반환.
    /// chapterIcons[1].entries[3] 에 해당하는 스프라이트를 반환합니다.
    /// </summary>
    public Sprite GetIcon(string id)
    {
        if (!TryParseWareId(id, out int chapter, out int index)) return null;
        foreach (var g in chapterIcons)
            if (g.chapter == chapter && g.entries != null && index < g.entries.Length)
                return g.entries[index];
        return null;
    }

    static bool TryParseWareId(string id, out int chapter, out int index)
    {
        chapter = 0; index = 0;
        if (string.IsNullOrEmpty(id)) return false;
        // 형식: C{N}_W{M}
        int wPos = id.IndexOf("_W", StringComparison.OrdinalIgnoreCase);
        if (wPos < 2) return false;
        return int.TryParse(id.Substring(1, wPos - 1), out chapter)
            && int.TryParse(id.Substring(wPos + 2), out index);
    }

    // ── DB 문자열 → 열거형 변환 ──────────────────────────────────────
    public static WareGrade ParseGrade(string grade) => grade switch
    {
        "RARE"   => WareGrade.Rare,
        "EPIC"   => WareGrade.Epic,
        "LEGEND" => WareGrade.Legend,
        "S"      => WareGrade.S,
        _        => WareGrade.Normal,
    };

    public static WareRole ParseRole(string role) => role switch
    {
        "버스트" => WareRole.Burst,
        "지속"  => WareRole.Sustained,
        "버프"  => WareRole.Buff,
        _      => WareRole.CC,
    };

    // ── 전역 접근 ────────────────────────────────────────────────────
    private static WareVisualConfig _shared;
    public static WareVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<WareVisualConfig>("Ware/WareVisualConfig");
            return _shared;
        }
    }
}
