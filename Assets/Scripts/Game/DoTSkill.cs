using System.Collections;
using UnityEngine;

/// <summary>범위 내 적에게 주기적으로 지속 피해(DoT)를 줍니다.</summary>
public class DoTSkill : StructureSkillBase
{
    public override void Activate() => StartCoroutine(TickRoutine());

    private IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
                if (col.TryGetComponent<EnemyController>(out var enemy))
                    enemy.TakeDamage(mainValue);
        }
    }
}
