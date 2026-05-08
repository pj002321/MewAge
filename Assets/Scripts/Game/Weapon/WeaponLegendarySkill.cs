using UnityEngine;

/// <summary>
/// 전설(Legendary) 등급 무기 온히트 스킬 기반 클래스.
///
/// 무기 프리팹의 WeaponPoint와 같은 GameObject 또는 자식에 부착합니다.
/// WeaponPoint.Init() 시 자동으로 Initialize()가 호출됩니다.
///
/// 구현 예시:
///   public class LegendaryMeleeStunSkill : WeaponLegendarySkill
///   {
///       [SerializeField] private float stunChance = 0.3f;
///       [SerializeField] private float stunDuration = 1f;
///       public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount)
///       {
///           if (Random.value > stunChance) return;
///           for (int i = 0; i < colCount; i++)
///               if (hitBuffer[i].TryGetComponent&lt;EnemyController&gt;(out var e))
///                   e.Stun(stunDuration);
///       }
///   }
/// </summary>
public abstract class WeaponLegendarySkill : MonoBehaviour
{
    protected WeaponPoint WP { get; private set; }

    public void Initialize(WeaponPoint wp) => WP = wp;

    /// <summary>WeaponGrade.Legendary 이상 타격 시 호출됩니다.
    /// hitBuffer는 WeaponPoint의 공유 버퍼이므로 colCount 이내만 읽을 것.</summary>
    public abstract void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount);

    /// <summary>Ranged/Magic 발사 시 호출됩니다. 서브 투사체 생성에 사용하세요.</summary>
    public virtual void OnShoot(Vector3 firePos, Vector2 fireDir) { }

    /// <summary>무기 장착(Init) 시 1회 호출됩니다. Spin 크기 증가 등 패시브에 사용하세요.</summary>
    public virtual void OnEquip() { }
}
