/// <summary>
/// 장착된 무기들의 평균 스탯 기여치를 StatType별로 반환합니다.
/// StatFieldUI.Refresh()에서 플레이어 스탯과 합산하여 실제 전투 수치를 표시하는 데 사용됩니다.
///
/// 합산 대상 스탯:
///   ATK        — avg(weapon.attackDamage)  [player.Attack에 더해지는 플랫 보너스]
///   CritChance — avg(0.05 + weapon.criticalRate)  [기본 5% + 무기 치명타]
///   BleedPower — avg(weapon.bleedDamage)   [무기 기본 출혈 데미지]
///   HealOnKill — avg(weapon.lifeSteal)     [흡혈 — 스탯패널의 HealOnKill 슬롯에 표시]
/// </summary>
public static class WeaponStatHelper
{
    /// <summary>장착 무기 평균 기여치. 무기가 없으면 0.</summary>
    public static float GetAverageBonus(StatType stat)
    {
        var equipped = PlayerInventory.Shared?.Equipped;
        if (equipped == null || equipped.Count == 0) return 0f;

        int   count = 0;
        float total = 0f;
        foreach (var w in equipped)
        {
            if (w == null) continue;
            count++;
            total += GetWeaponValue(w, stat);
        }
        return count > 0 ? total / count : 0f;
    }

    /// <summary>이 statType에 무기 기여치가 있으면 true.</summary>
    public static bool HasWeaponBonus(StatType stat) => stat switch
    {
        StatType.ATK        => true,
        StatType.CritChance => true,
        StatType.BleedPower => true,
        StatType.HealOnKill => true,
        _ => false
    };

    private static float GetWeaponValue(WeaponData w, StatType stat) => stat switch
    {
        StatType.ATK        => w.attackDamage,
        StatType.CritChance => 0.05f + w.criticalRate,  // WeaponPoint와 동일: 기본 5% + 무기 치명타
        StatType.BleedPower => w.bleedDamage,
        StatType.HealOnKill => w.lifeSteal,
        _ => 0f
    };
}
