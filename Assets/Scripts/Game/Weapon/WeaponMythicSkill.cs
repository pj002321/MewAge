using UnityEngine;

/// <summary>
/// 신화(Mythic) 등급 보스 특화 스킬 기반 클래스.
///
/// WeaponLegendarySkill과 함께 무기 프리팹에 부착합니다.
/// 보스를 타격했을 때만 OnBossHit()이 호출됩니다.
///
/// TODO.md 신화 스킬 목록:
///   - 일정 확률로 2초 그로기 (BossController.Groggy)
///   - 일정 확률로 스파이크 프리팹 꽂기 (Instantiate + stay)
///   - 일정 확률로 보스 이동 바인드 (그로기 X, 이동만 정지)
///   - 보스 피해 감소 (passive — WeaponPoint._mythicSkill != null 로 PlayerController에서 처리)
///
/// 구현 예시:
///   public class MythicGroggySkill : WeaponMythicSkill
///   {
///       [SerializeField] private float chance = 0.2f;
///       public override void OnBossHit(BossController boss, Vector2 hitDir)
///       {
///           if (Random.value < chance) boss.Groggy(2f);
///       }
///   }
/// </summary>
public abstract class WeaponMythicSkill : MonoBehaviour
{
    protected WeaponPoint WP { get; private set; }

    public void Initialize(WeaponPoint wp) => WP = wp;

    /// <summary>WeaponGrade.Mythic 보스 타격 시 호출됩니다.</summary>
    public abstract void OnBossHit(BossController boss, Vector2 hitDir);

    /// <summary>WeaponGrade.Mythic 일반 적 타격 시 호출됩니다.</summary>
    public virtual void OnEnemyHit(EnemyController enemy, Vector2 hitDir) { }

    /// <summary>무기 장착(Init) 시 1회 호출됩니다. 패시브 스킬 초기화에 사용하세요.</summary>
    public virtual void OnEquip() { }
}
