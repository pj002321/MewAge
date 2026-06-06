using UnityEngine;

/// <summary>
/// 증강 공통 유틸리티 — AugId 문자열 ↔ 한국어 표시명 / StatType 변환, 저주·축복 스탯 적용.
/// ImprovCard, ImprovManager, ImprovPanel 등에서 공유합니다.
/// </summary>
public static class AugmentHelper
{
    // ── AugId 문자열 → 한국어 스탯명 ────────────────────────────────────
    public static string ToKorean(string augIdStr) => augIdStr switch
    {
        "AUG_HP_HEAL"         => "즉시 회복",
        "AUG_HP_POTION"       => "회복 구슬",
        "AUG_STOP_ATK_SPD"    => "공격속도",
        "AUG_LOWHP_ATK"       => "공격력",
        "AUG_MOV_DOWN_ATK_UP" => "이동속도",
        _                     => augIdStr,
    };

    // ── AugId 문자열 → StatType ──────────────────────────────────────────
    // null = 해당 AugId에 대응하는 StatType 없음 (특수 효과 계열)
    public static StatType? ToStatType(string augIdStr) => augIdStr switch
    {
        "AUG_STOP_ATK_SPD"    => StatType.AtkSpeed,
        "AUG_LOWHP_ATK"       => StatType.ATK,
        "AUG_MOV_DOWN_ATK_UP" => StatType.MoveSpeed,
        _                     => (StatType?)null,
    };

    // ── 저주·축복 스탯 모디파이어 적용 ───────────────────────────────────
    // positive=true → 축복(이득, +value%), positive=false → 저주(손해, -value%)
    public static void ApplyStatMod(PlayerStatManager sm, string augIdStr, float value, bool positive)
    {
        if (sm == null || string.IsNullOrEmpty(augIdStr) || value <= 0f) return;
        var stat = ToStatType(augIdStr);
        if (stat == null) return;
        sm.AddPctMod(stat.Value, positive ? value : -value);
    }

    // ── 저주·축복 설명 한 줄 생성 ────────────────────────────────────────
    // positive=true → "+30%" 초록, positive=false → "-15%" 빨강 (타입이 ATK여도 무조건 손해)
    public static string BuildEffectLine(string augIdStr, float value, bool positive)
    {
        if (string.IsNullOrEmpty(augIdStr) || value <= 0f) return null;
        string statName = ToKorean(augIdStr);
        string color    = positive ? "#55FF88" : "#FF5555";
        string sign     = positive ? "+" : "-";
        string valStr   = value == Mathf.Floor(value)
            ? ((int)value).ToString()
            : value.ToString("0.##");
        return $"{statName} <color={color}>{sign}{valStr}%</color>";
    }

    // ── CurseRewardCard 전용 — 이중 화살표 ↑↑/↓↓, 수치 없이 방향만 표시 ──
    public static string BuildEffectLineDouble(string augIdStr, float value, bool positive)
    {
        if (string.IsNullOrEmpty(augIdStr) || value <= 0f) return null;
        string statName = ToKorean(augIdStr);
        string color    = positive ? "#55FF88" : "#FF5555";
        string arrows   = positive ? "↑↑" : "↓↓";
        return $"{statName} <color={color}>{arrows}</color>";
    }
}
