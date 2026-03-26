using System.Collections;
using UnityEngine;

/// <summary>
/// 범위 내 적에게 주기적으로 지속 피해(DoT)를 줍니다.
/// 최종 피해 = mainValue × (1 + BleedPower / 100)
/// · BleedPower는 PlayerStatManager.GetFinal(StatType.BleedPower) 로 읽습니다.
/// </summary>
public class DoTSkill : StructureSkillBase
{
    public override void Activate() => StartCoroutine(TickRoutine());

    private IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;

            // BleedPower 스탯 반영: 기본 피해 × (1 + BleedPower%)
            float bleedPower = PlayerStatManager.Shared != null
                ? PlayerStatManager.Shared.GetFinal(StatType.BleedPower)
                : 0f;
            float finalDamage = mainValue * (1f + bleedPower / 100f);

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
                if (col.TryGetComponent<EnemyController>(out var enemy))
                    enemy.TakeDamage(finalDamage);
        }
    }
}

// ── Unity 구현 가이드 ─────────────────────────────────────────────
// · mainValue: Inspector에서 기본 도트 피해량 설정
// · BleedPower 증강 선택 시 PlayerStatManager에 % 누적 → 자동 반영됨
