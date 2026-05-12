using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 장착 무기 목록을 받아 달성된 시너지 ID 집합을 반환합니다.
/// DB_시너지 시트와 동일한 ID 문자열로 결과를 구성합니다.
/// </summary>
public static class SynergyConditionChecker
{
    public static HashSet<string> CheckAll(IReadOnlyList<WeaponData> equipped)
    {
        var achieved = new HashSet<string>();
        if (equipped == null) return achieved;

        var active = new List<WeaponData>(equipped.Count);
        foreach (var w in equipped)
            if (w != null) active.Add(w);

        if (active.Count == 0) return achieved;

        int n = active.Count;

        // ── 등급 기반 ────────────────────────────────────────────────
        // SYN_ALL2_UPGRADE3: 모든 무기 2성 달성
        if (active.All(w => w.star >= 2))
            achieved.Add("SYN_ALL2_UPGRADE3");

        // SYN_ALL3_HIGHEST_EXTRA: 모든 무기 3성 달성
        if (active.All(w => w.star >= 3))
            achieved.Add("SYN_ALL3_HIGHEST_EXTRA");

        // SYN_ALL_LEGEND_PROC: 모든 무기 전설 등급 이상
        if (active.All(w => w.grade >= WeaponGrade.Legendary))
            achieved.Add("SYN_ALL_LEGEND_PROC");

        // SYN_ALL_MYTH_REVIVE: 모든 무기 신화 등급
        if (active.All(w => w.grade == WeaponGrade.Mythic))
            achieved.Add("SYN_ALL_MYTH_REVIVE");

        // SYN_RARE4_LOWGRADE_UP: 희귀(Epic) 등급 이상 4개 이상 장착
        if (active.Count(w => w.grade >= WeaponGrade.Epic) >= 4)
            achieved.Add("SYN_RARE4_LOWGRADE_UP");

        // ── 타입 기반 ────────────────────────────────────────────────
        // SYN_MELEE4_ATK: 근접형 4개 이상
        if (active.Count(w => w.weaponType == WeaponType.Melee) >= 4)
            achieved.Add("SYN_MELEE4_ATK");

        // SYN_RANGE4_PROJECTILE_X2: 원거리형 4개 이상
        if (active.Count(w => w.weaponType == WeaponType.Ranged) >= 4)
            achieved.Add("SYN_RANGE4_PROJECTILE_X2");

        // SYN_SPIN3_MOVE: 회전형 3개 이상
        if (active.Count(w => w.weaponType == WeaponType.Spin) >= 3)
            achieved.Add("SYN_SPIN3_MOVE");

        // SYN_HAMMER3_DEF: 해머형 3개 이상
        if (active.Count(w => w.weaponType == WeaponType.Hammer) >= 3)
            achieved.Add("SYN_HAMMER3_DEF");

        // SYN_BOOMERANG3_CRIT: 부메랑형 3개 이상
        if (active.Count(w => w.weaponType == WeaponType.Boomerang) >= 3)
            achieved.Add("SYN_BOOMERANG3_CRIT");

        // SYN_MAGIC3_SLOW: 마법형 3개 이상
        if (active.Count(w => w.weaponType == WeaponType.Magic) >= 3)
            achieved.Add("SYN_MAGIC3_SLOW");

        // SYN_ALLDIFF_ASPD: 장착 무기 모두 다른 형태 (2개 이상)
        if (n >= 2 && active.Select(w => w.weaponType).Distinct().Count() == n)
            achieved.Add("SYN_ALLDIFF_ASPD");

        return achieved;
    }
}
