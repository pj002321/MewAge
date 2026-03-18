using System.Collections;
using UnityEngine;

/// <summary>범위 내 적을 주기적으로 마비시킵니다.</summary>
public class ParalyzeSkill : StructureSkillBase
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
                    enemy.Paralyze(subValue > 0f ? subValue : tickInterval);
        }
    }
}
