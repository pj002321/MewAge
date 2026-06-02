using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_무기 내 속성 정의(f_속성ID / f_속성내용)를 캐싱합니다.
/// 속성 설명 포맷 + 실제 스탯 적용 로직을 제공합니다.
///
/// 속성 ID 규칙:
///   ATK_ADD   → attackDamage   += base * value%    (정수 표기)
///   CD_RED    → attackCooldown -= base * value%    (정수 표기)
///   RNG_ADD   → attackRange    += base * value%    (정수 표기)
///   DMG_RNG   → areaHitRadius  += base * value%    (정수 표기)
///   NUK_ADD   → (넉백, 플레이어 스탯 반영용)        (정수 표기)
///   CRIT_ADD  → criticalRate   += value / 100f     (정수 표기)
///   DOT_AD    → bleedDamage    += value / 100f     (소수 표기)
///   CRT_RATE  → criticalRate   += value / 100f     (소수 표기)
///   HP_STL_ADD→ lifeSteal      += value / 100f     (소수 표기)
///   HEAL_ADD  → (자가치유, PlayerStatManager 반영) (소수 표기)
/// </summary>
public static class WeaponStatRegistry
{
    // 소수점 표기 속성 ID 목록
    private static readonly HashSet<string> DecimalStats = new()
    {
        "DOT_AD", "DOT_ADD", "CRT_RATE", "HP_STL_ADD", "HEAL_ADD"
    };

    // id → 설명 템플릿 (예: "ATK_ADD" → "공격력 +{0}%")
    private static Dictionary<string, string> _templates;
    private static bool _loaded;

    // ────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded    = true;
        _templates = new Dictionary<string, string>();

        DB_무기.ForEachEntity(row =>
        {
            var id   = row.f_속성ID;
            var tmpl = row.f_속성내용;
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(tmpl))
                _templates[id] = tmpl;
        });
    }

    // ────────────────────────────────────────────────────────
    // 소수/정수 판별
    // ────────────────────────────────────────────────────────

    public static bool IsDecimalStat(string statId) => DecimalStats.Contains(statId);

    // ────────────────────────────────────────────────────────
    // 스탯 한글 레이블 (단위 없는 이름만)
    // ────────────────────────────────────────────────────────

    public static string GetLabel(string statId) => statId switch
    {
        "ATK_ADD"    => "공격력",
        "CD_RED"     => "공격속도",
        "RNG_ADD"    => "무기 사거리",
        "DMG_RNG"    => "타격범위",
        "CRIT_ADD"   => "치명타데미지",
        "CRT_RATE"   => "치명타확률",
        "DOT_AD"     => "출혈지수",
        "DOT_ADD"    => "출혈지수",
        "HP_STL_ADD" => "흡혈수치량",
        "NUK_ADD"    => "넉백지수",
        "HEAL_ADD"   => "자가치유",
        "SKL_CD"     => "",
        "STAT_SKL_CD"=> "",
        _            => statId,
    };

    // ────────────────────────────────────────────────────────
    // 설명 생성
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 속성 ID와 수치를 받아 포맷된 설명 문자열을 반환합니다.
    /// 소수형 속성은 소수점, 정수형은 정수로 표기합니다.
    /// 예) "ATK_ADD", 25  → "공격력 +25%"
    ///     "CRT_RATE", 2.5 → "치명타확률 +2.5%"
    /// </summary>
    public static string GetDescription(string statId, float value)
    {
        if (string.IsNullOrEmpty(statId)) return string.Empty;
        EnsureLoaded();

        string formatted = IsDecimalStat(statId)
            ? value.ToString("0.##")
            : Mathf.RoundToInt(value).ToString();

        if (_templates.TryGetValue(statId, out var tmpl))
            return tmpl.Replace("{0}", formatted);

        string label = statId switch
        {
            "ATK_ADD"    => "공격력",
            "CD_RED"     => "쿨타임",
            "RNG_ADD"    => "사거리",
            "DMG_RNG"    => "타격범위",
            "CRIT_ADD"   => "치명타",
            "CRT_RATE"   => "치명타확률",
            "DOT_AD"     => "출혈",
            "DOT_ADD"    => "출혈",
            "HP_STL_ADD" => "흡혈",
            "NUK_ADD"    => "넉백",
            "HEAL_ADD"   => "자가치유",
            _            => statId,
        };
        return $"{label} +{formatted}";
    }

    /// <summary>WeaponStarBonus를 채워 반환합니다.</summary>
    public static WeaponStarBonus Build(string statId, float value)
    {
        return new WeaponStarBonus
        {
            statId      = statId,
            value       = value,
            description = GetDescription(statId, value),
        };
    }

    // ────────────────────────────────────────────────────────
    // 스탯 적용 (WeaponData 직접 수정)
    // ────────────────────────────────────────────────────────

    public static void ApplyBonus(WeaponData data, WeaponStarBonus bonus)
    {
        if (bonus.IsEmpty) return;

        switch (bonus.statId)
        {
            case "ATK_ADD":
                data.attackDamage   += data.baseAttackDamage   * bonus.value / 100f;
                break;
            case "CD_RED":
                data.attackCooldown -= data.baseAttackCooldown  * bonus.value / 100f;
                break;
            case "RNG_ADD":
                data.attackRange    += data.baseAttackRange     * bonus.value / 100f;
                break;
            case "DMG_RNG":
                data.areaHitRadius  += data.baseAreaHitRadius   * bonus.value / 100f;
                break;
            case "CRIT_ADD":
                data.criticalDamage += bonus.value / 100f;
                break;
            case "DOT_AD":
            case "DOT_ADD":
                data.bleedDamage    += bonus.value;
                break;
            case "CRT_RATE":
                data.criticalRate   += bonus.value / 100f;
                break;
            case "HP_STL_ADD":
                data.lifeSteal      += bonus.value / 100f;
                break;
            // NUK_ADD, HEAL_ADD는 PlayerStatManager에서 처리 (무기 자체 스탯 없음)
        }
    }
}
