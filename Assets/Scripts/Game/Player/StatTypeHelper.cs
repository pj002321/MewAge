/// <summary>StatType 확장 메서드 — UI 한국어 표시명 변환.</summary>
public static class StatTypeHelper
{
    public static string ToKorean(this StatType stat) => stat switch
    {
        StatType.ATK          => "공격력",
        StatType.DEF          => "방어력",
        StatType.MaxHP        => "최대 체력",
        StatType.BleedPower   => "출혈 피해",
        StatType.HPRegen      => "체력 재생",
        StatType.MoveSpeed    => "이동속도",
        StatType.MagnetRadius => "흡수 반경",
        StatType.AtkSpeed     => "공격속도",
        StatType.Evasion      => "회피율",
        StatType.CritChance   => "치명타 확률",
        StatType.CritDmg           => "치명타 피해",
        StatType.StructureCooldown => "기물 쿨타임",
        StatType.Knockback         => "넉백",
        StatType.HealOnKill   => "처치 회복",
        _                     => stat.ToString(),
    };
}
