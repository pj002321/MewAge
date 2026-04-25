/// <summary>
/// 플레이어 캐릭터 능력치 종류.
/// PlayerStatManager에서 기본값 + % 모디파이어를 관리합니다.
/// </summary>
public enum StatType
{
    ATK               = 0,   // 공격력          — 장착 무기 전체 공격력에 더해질 보너스
    DEF               = 1,   // 방어력          — 받는 피해 감소
    MaxHP             = 2,   // 최대 체력
    BleedPower        = 3,   // 도트데미지 지수 — 출혈 데미지 배율
    HPRegen           = 4,   // 체력 회복 지수  — 초당 자동 회복량
    MoveSpeed         = 5,   // 이동속도
    MagnetRadius      = 6,   // 흡수 반경       — 골드·경험치 자동 수집 거리
    AtkSpeed          = 7,   // 공격속도        — 무기 쿨타임 단축 %
    Evasion           = 8,   // 회피 수치       — 피격 회피 확률 (0~0.75)
    CritChance        = 9,   // 크리티컬 확률   — %
    CritDmg           = 10,  // 크리티컬 데미지 — 기본 1.5x 위 추가 배율 %
    Knockback         = 11,  // 넉백 지수       — 적에게 가하는 넉백 강도
    HealOnKill        = 12,  // 처치 회복       — 적 처치 시 회복 발동 확률 (%)
}
