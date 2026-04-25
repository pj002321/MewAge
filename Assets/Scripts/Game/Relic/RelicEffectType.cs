/// <summary>
/// 유물 효과 종류. DB_유물의 f_속성ID / f_특수속성ID 문자열과 1:1 대응합니다.
/// NONE은 해당 슬롯에 효과 없음을 나타냅니다.
/// </summary>
public enum RelicEffectType
{
    NONE,

    // ── 무기 타입별 공격력 증가 ──────────────────────────────────────
    MELEE_ATK,      // 근접무기 공격력 증가  (Melee, Charge, Slash)
    RANGED_ATK,     // 원거리무기 공격력 증가 (Ranged)
    MAGIC_ATK,      // 마법무기 공격력 증가  (Magic)
    BOOMERANG_ATK,  // 부메랑무기 공격력 증가 (Boomerang)
    SPIN_ATK,       // 스핀무기 공격력 증가   (Spin)

    // ── StatType 직접 연동 ───────────────────────────────────────────
    ATK,        // 전체 공격력 증가
    DEF,        // 방어력 증가
    MAX_HP,     // 최대 체력 증가
    MOV_SPD,    // 이동속도 증가
    ATK_SPD,    // 공격속도 증가
    EVA,        // 회피 증가
    CRT_RATE,   // 치명타 확률 증가
    CRT_DMG,    // 치명타 데미지 증가
    BLEED,      // 출혈 데미지 증가
    HP_REGEN,   // 체력 회복 증가
    MAGNET,     // 흡수 반경 증가
    KNOCKBACK,  // 넉백 증가
    HEAL_KILL,  // 처치 회복 증가

    // ── 특수 효과 ────────────────────────────────────────────────────
    BOSS_DMG,   // 보스에게 받는 피해 감소 (%)
    BOSS_ATK,   // 보스에게 주는 피해 증가 (%)
    ELITE_DMG,  // 엘리트에게 받는 피해 감소 (%)
}
