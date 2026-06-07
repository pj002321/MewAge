public enum ImprovType
{
    // ── CMN 공통 ─────────────────────────────────────────────────
    AUG_HP_HEAL          = 0,   // 현재 체력 회복
    AUG_HP_POTION        = 1,   // 전투 중 일정확률로 회복 구슬 스폰
    AUG_STOP_ATK_SPD     = 2,   // 멈춰있는 동안 공격속도 증가
    AUG_LOWHP_ATK        = 3,   // 체력 20% 이하 시 공격력 증가
    AUG_MOV_DOWN_ATK_UP  = 4,   // 이동속도 감소, 공격속도 증가

    // ── C1 원시시대 ───────────────────────────────────────────────
    AUG_FIRST_HUNT       = 100, // 첫 구매 무기 2성
    AUG_WILD_FUSION      = 101, // 무기 6개 장착 시 하나로 합성
    AUG_SPAWN_TURRET     = 102, // 주기적으로 터렛 설치물 소환
    AUG_PET_BEAST        = 103, // 애완동물 소환
    AUG_MUD_OPENING      = 104, // 웨이브 시작 시 진흙 3발 둔화
    AUG_ICE_AGE          = 105, // 웨이브 시작 시 빙하기 → 공속 증가
    AUG_DELAYED_FISHBONE = 106, // 에픽 무기 명중 시 생선뼈 → 3초 후 폭발
    AUG_ROTTEN_CATNIP    = 107, // 명중 시 확률로 독
    AUG_BURNING_PAWS     = 108, // 명중 시 확률로 화상
}
