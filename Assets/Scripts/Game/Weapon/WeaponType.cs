public enum WeaponType
{
    Melee,     // 근거리
    Ranged,    // 원거리
    Magic,     // 마법 (MagicElement로 세부 속성 구분)
    Boomerang, // 부메랑 (적 튕기고 귀환)
    Spin,      // 돌리기 (플레이어 주위 공전)
    Slash,     // 슬래시 (관통형 검기)
    Charge,    // 차징 후 강타
    Hammer,    // 해머 (회전 상승 → 내려치기)
}

/// <summary>Magic 타입 무기의 속성. WeaponPoint Inspector에서 설정합니다.</summary>
public enum MagicElement
{
    None,      // 기본 마법 (근거리 타격형)
    Lightning, // 번개 (즉발 비율 데미지)
    Fire,      // 불 (예정)
    Ice,       // 얼음 (예정)
}
