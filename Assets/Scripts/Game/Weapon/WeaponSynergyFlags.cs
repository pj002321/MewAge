/// <summary>
/// 무기 시너지 달성 시 켜지는 런타임 플래그.
/// SynergyEffectManager가 갱신합니다. 각 시스템에서 읽기 전용으로 사용합니다.
/// </summary>
public static class WeaponSynergyFlags
{
    /// <summary>SYN_ALL_LEGEND_PROC — 공격 시 일정 확률로 캐릭터 스킬 발동</summary>
    public static bool LegendProcActive;

    /// <summary>SYN_ALL_MYTH_REVIVE — 사망 시 1회 무료 부활 가능</summary>
    public static bool MythReviveAvailable;

    /// <summary>SYN_RANGE4_PROJECTILE_X2 — 투사체 수 2배</summary>
    public static bool RangeDoubleProjectile;

    /// <summary>SYN_MAGIC3_SLOW — 모든 적 둔화 적용</summary>
    public static bool MagicSlowActive;

    public static void Reset()
    {
        LegendProcActive      = false;
        MythReviveAvailable   = false;
        RangeDoubleProjectile = false;
        MagicSlowActive       = false;
    }
}
