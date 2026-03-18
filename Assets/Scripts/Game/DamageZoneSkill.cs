using UnityEngine;

/// <summary>콜라이더 영역을 밟는 적에게 즉시 피해를 줍니다.</summary>
public class DamageZoneSkill : StructureSkillBase
{
    public override void Activate() { }  // 트리거 충돌로 처리

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<EnemyController>(out var enemy))
            enemy.TakeDamage(mainValue);
    }
}
