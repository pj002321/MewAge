/// <summary>
/// 증강 효과 종류 열거형.
/// DB_증강의 f_효과타입 필드와 동일한 이름을 사용합니다.
/// </summary>
public enum ImprovType
{
    AUG_HEAL         = 0,   // 체력 회복
    AUG_MAX_HP       = 1,   // 최대 체력 증가
    AUG_ATK          = 2,   // 공격력 증가
    AUG_DEF          = 3,   // 방어력 증가
    AUG_SPD_MOV      = 4,   // 이동속도 증가
    AUG_SIZE_HP_UP   = 5,   // 몸집 커지고 최대 체력 증가
    AUG_SIZE_HP_DOWN = 6,   // 몸집 작아지고 최대 체력 감소
    AUG_SPD_ATK      = 7,   // 공격속도 증가
    AUG_EVA          = 8,   // 회피 확률 증가
    AUG_REWARD_RNG   = 10,  // 골드·경험치 흡수 반경 증가
    AUG_DOT_DMG      = 11,  // 출혈 데미지 증가
    AUG_CRI_RATE     = 12,  // 크리티컬 확률 증가
    AUG_CRI_DMG      = 13,  // 크리티컬 데미지 증가
    AUG_HEAL_DEAD    = 14,  // 적 처치 시 일정 확률로 체력 회복
    AUG_SPW_HP_SKL   = 15,  // 전투 중 일정 확률로 회복 구슬 스폰
}
