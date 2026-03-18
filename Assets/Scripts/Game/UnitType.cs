/// <summary>
/// 적 유닛 분류 — DB의 f_타입 필드와 매핑됩니다.
/// 실제 행동 로직은 UnitType이 아닌 EnemyBehaviourBase 서브클래스로 결정됩니다.
/// (MeleeBehaviour, RangedBehaviour, ChargeBehaviour, MagicBehaviour, ...)
/// </summary>
public enum UnitType
{
    근거리 = 0,  // MeleeBehaviour
    원거리 = 1,  // RangedBehaviour
    돌진   = 2,  // ChargeBehaviour
    마법   = 3,  // MagicBehaviour
    힐러   = 4,  // HealerBehaviour
    탱커   = 5,  // TankBehaviour
}
