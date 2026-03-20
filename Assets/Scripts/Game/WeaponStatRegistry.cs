using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_무기 내 속성 정의 행(f_속성ID / f_속성내용)을 로드해 캐싱합니다.
/// 속성 설명 포맷 + 실제 스탯 적용 로직을 제공합니다.
///
/// 속성 ID 규칙:
///   ATK_ADD  → attackDamage   += base * value%
///   CD_RED   → attackCooldown -= base * value%  (쿨다운 단축)
///   RNG_ADD  → attackRange    += base * value%
///   CRIT_ADD → criticalRate   += value / 100f   (절대값 추가)
///   DOT_ADD  → dotDamage      += base * value%
/// </summary>
public static class WeaponStatRegistry
{
    // id → 설명 템플릿 (예: "ATK_ADD" → "공격력 +{0}%")
    private static Dictionary<string, string> _templates;

    private static bool _loaded;

    // ────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────

    /// <summary>DB에서 속성 정의를 로드합니다. WeaponData.FromDB() 첫 호출 시 자동 실행됩니다.</summary>
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
    // 설명 생성
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 속성 ID와 수치를 받아 포맷된 설명 문자열을 반환합니다.
    /// 예) GetDescription("ATK_ADD", 25) → "공격력 +25%"
    /// </summary>
    public static string GetDescription(string statId, int value)
    {
        if (string.IsNullOrEmpty(statId)) return string.Empty;
        EnsureLoaded();
        return _templates.TryGetValue(statId, out var tmpl)
            ? string.Format(tmpl, value)
            : $"{statId} +{value}";
    }

    /// <summary>WeaponStarBonus를 채워 반환합니다.</summary>
    public static WeaponStarBonus Build(string statId, int value)
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

    /// <summary>
    /// bonus.statId에 따라 WeaponData의 스탯을 기본값 기준으로 누적 보정합니다.
    /// (attackDamage += base * value%, attackCooldown -= base * value%, ...)
    /// </summary>
    public static void ApplyBonus(WeaponData data, WeaponStarBonus bonus)
    {
        if (bonus.IsEmpty) return;

        switch (bonus.statId)
        {
            case "ATK_ADD":
                data.attackDamage   += data.baseAttackDamage   * bonus.value / 100f;
                break;
            case "CD_RED":
                data.attackCooldown -= data.baseAttackCooldown * bonus.value / 100f;
                break;
            case "RNG_ADD":
                data.attackRange    += data.baseAttackRange    * bonus.value / 100f;
                break;
            case "CRIT_ADD":
                data.criticalRate   += bonus.value / 100f;
                break;
            case "BLEED_ADD":
                data.bleedDamage  += data.baseBleedDamage > 0f
                    ? data.baseBleedDamage * bonus.value / 100f
                    : bonus.value;   // 기본 출혈이 0이면 절대값으로 추가
                break;
            case "LIFESTEAL":
                data.lifeSteal    += bonus.value / 100f;   // 절대값 추가 (%)
                break;
        }
    }

    // ────────────────────────────────────────────────────────
    // 보너스 델타 텍스트 (UI 서브값용)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 스탯(stat)과 기본 스탯(baseStat)의 차이를 서브값 문자열로 반환합니다.
    /// 차이가 없으면 빈 문자열을 반환합니다.
    /// </summary>
    public static string DeltaText(float stat, float baseStat, string format = "+{0:0.#}")
    {
        float delta = stat - baseStat;
        if (Mathf.Approximately(delta, 0f)) return string.Empty;
        return string.Format(format, delta > 0f ? delta : delta); // 음수 포함
    }
}
