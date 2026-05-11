using UnityEngine;

/// <summary>
/// 적 행동(이동 + 공격)의 기반 클래스.
/// 새로운 적 유형 추가 방법:
///   1. 이 클래스를 상속한 새 컴포넌트를 만드세요.
///   2. 적 프리팹에 EnemyController와 함께 새 컴포넌트를 부착하세요.
/// </summary>
public abstract class EnemyBehaviourBase : MonoBehaviour
{
    protected IEnemyHost Owner { get; private set; }
    protected EnemyController EC => Owner as EnemyController;

    /// <summary>EnemyController.Awake() 또는 BossSummonMinion.Init()에서 호출됩니다.</summary>
    public virtual void Init(IEnemyHost host)
    {
        Owner = host;
    }

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
