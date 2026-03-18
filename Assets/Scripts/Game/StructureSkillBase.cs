using UnityEngine;

/// <summary>
/// 기물 스킬의 기반 클래스.
/// 새 스킬 추가 방법:
///   1. 이 클래스를 상속한 새 컴포넌트를 만드세요.
///   2. 기물 프리팹에 StructureController와 함께 새 컴포넌트를 부착하세요.
/// </summary>
public abstract class StructureSkillBase : MonoBehaviour
{
    [Header("스킬 수치")]
    [SerializeField] protected float mainValue    = 10f;   // 피해량 / 회복량 등
    [SerializeField] protected float subValue     = 1f;    // 마비 지속시간 등 보조 수치
    [SerializeField] protected float radius       = 1.5f;  // 범위
    [SerializeField] protected float tickInterval = 1f;    // 틱 간격

    /// <summary>StructureController.Start()에서 호출됩니다.</summary>
    public abstract void Activate();

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
