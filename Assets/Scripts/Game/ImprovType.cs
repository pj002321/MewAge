/// <summary>
/// 증강 효과 종류 열거형
/// </summary>
public enum ImprovType
{
    HealHP              = 0,  // 체력 회복
    IncreaseMaxHP       = 1,  // 최대 체력 증가
    IncreaseAttack      = 2,  // 공격력 증가
    IncreaseDefense     = 3,  // 방어력 증가
    IncreaseMoveSpeed   = 4,  // 이동 속도 증가
    IncreaseSizeHP      = 5,  // 사이즈 증가 + 체력 증가
    DecreaseSizeHP      = 6,  // 사이즈 감소 + 체력 감소 (트레이드오프)
    IncreaseAttackSpeed = 7,  // 공격 속도 증가
    RangedDistanceBonus = 8,  // 원거리 무기 거리 비례 공격력 증가
    MeleeAttackBonus    = 9,  // 근접 무기 공격력 증가
    IncreaseDodge       = 10, // 회피 증가
    PeriodicKnockback   = 11, // 일정 시간마다 주변 적 밀어내기
    WaveHealHP              = 12, // 매 웨이브 시작 시 체력 회복 (패시브)
    UpgradeRandomEquipped   = 13, // 장비창 무기 중 랜덤 1성 업그레이드 (최대 4성)
}
