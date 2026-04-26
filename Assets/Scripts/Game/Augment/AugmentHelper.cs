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
        "AUG_ATK"          => "공격력",
        "AUG_DEF"          => "방어력",
        "AUG_MAX_HP"       => "최대 체력",
        "AUG_SPD_MOV"      => "이동속도",
        "AUG_SPD_ATK"      => "공격속도",
        "AUG_EVA"          => "회피율",
        "AUG_CRI_RATE"     => "치명타 확률",
        "AUG_CRI_DMG"      => "치명타 피해",
        "AUG_DOT_DMG"      => "출혈 피해",
        "AUG_REWARD_RNG"   => "흡수 반경",
        "AUG_SIZE_HP_UP"   => "체형 확대",
        "AUG_SIZE_HP_DOWN" => "체형 축소",
        "AUG_HEAL"         => "즉시 회복",
        "AUG_HEAL_DEAD"    => "처치 회복",
        "AUG_SPW_HP_SKL"   => "회복 구슬",
        _                  => augIdStr,
    };

    // ── AugId 문자열 → StatType ──────────────────────────────────────────
    // null = 해당 AugId에 대응하는 StatType 없음 (특수 효과 계열)
    public static StatType? ToStatType(string augIdStr) => augIdStr switch
    {
        "AUG_ATK"        => StatType.ATK,
        "AUG_DEF"        => StatType.DEF,
        "AUG_MAX_HP"     => StatType.MaxHP,
        "AUG_SPD_MOV"    => StatType.MoveSpeed,
        "AUG_SPD_ATK"    => StatType.AtkSpeed,
        "AUG_EVA"        => StatType.Evasion,
        "AUG_CRI_RATE"   => StatType.CritChance,
        "AUG_CRI_DMG"    => StatType.CritDmg,
        "AUG_DOT_DMG"    => StatType.BleedPower,
        "AUG_REWARD_RNG" => StatType.MagnetRadius,
        _                => (StatType?)null,
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
}
