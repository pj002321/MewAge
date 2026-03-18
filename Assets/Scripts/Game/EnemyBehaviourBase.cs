using UnityEngine;
using Mewage.DB;

/// <summary>
/// 적 행동(이동 + 공격)의 기반 클래스.
/// 새로운 적 유형 추가 방법:
///   1. 이 클래스를 상속한 새 컴포넌트를 만드세요.
///   2. 적 프리팹에 EnemyController와 함께 새 컴포넌트를 부착하세요.
///   3. DB에서 불러오는 수치가 있으면 ApplyDBData()를 오버라이드하세요.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public abstract class EnemyBehaviourBase : MonoBehaviour
{
    protected EnemyController Owner { get; private set; }

    /// <summary>EnemyController.Awake()에서 자동 호출됩니다.</summary>
    public virtual void Init(EnemyController controller)
    {
        Owner = controller;
    }

    /// <summary>InitFromDB 시 행동-특화 DB 수치를 적용합니다.</summary>
    public virtual void ApplyDBData(DB_유닛 unit) { }

    /// <summary>웨이브 스케일링 시 행동-특화 수치를 보정합니다.</summary>
    public virtual void ApplyWaveScaling(float multiplier) { }

    /// <summary>Update에서 호출 — 공격 / 스킬 타이머 처리.</summary>
    public virtual void OnAttackUpdate() { }

    /// <summary>FixedUpdate에서 호출 — 이동 처리. 기본: 플레이어를 향해 이동.</summary>
    public virtual void OnMoveFixedUpdate()
    {
        Owner.MoveTowardPlayer(Owner.MoveSpeed);
    }
}
