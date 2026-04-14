using System.Collections;
using UnityEngine;

/// <summary>범위 내에 있는 플레이어를 주기적으로 회복합니다.</summary>
public class HealZoneSkill : StructureSkillBase
{


    public override void Activate() => StartCoroutine(HealRoutine());

    private IEnumerator HealRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            var player = PlayerController.Instance;
            if (player != null && Vector2.Distance(transform.position, player.Position) <= radius)
                player.HealHP(mainValue);
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
