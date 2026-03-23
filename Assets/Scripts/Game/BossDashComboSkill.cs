using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진하면서 연속 타격.
/// 플레이어 방향으로 돌진하며, 범위 안에 들어올 때마다 hitCount번 타격합니다.
/// 타격마다 애니메이션 트리거를 발동합니다.
/// </summary>
public class BossDashComboSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float   dashSpeed     = 10f;
    [SerializeField] private float   dashDuration  = 0.6f;

    [Header("연속 타격")]
    [SerializeField] private int     hitCount      = 3;
    [SerializeField] private float   hitInterval   = 0.18f;   // 타격 사이 간격
    [SerializeField] private float   hitRange      = 1.6f;
    [SerializeField] private float   hitDamage     = 0f;      // 0이면 ContactDamage 사용
    [SerializeField] private string  attackTrigger = "tAttack";

    [Header("경계")]
    [SerializeField] private Vector2 fallbackBoundsSize = new(10f, 18f);

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        float   speed   = dashSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);
        float   dmg     = hitDamage > 0f ? hitDamage : Owner.ContactDamage;
        Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        // 경계 계산
        var     spawner = EnemySpawner.Shared;
        Vector2 ac      = spawner != null ? spawner.AreaCenter : Vector2.zero;
        Vector2 half    = (spawner != null ? spawner.AreaSize : fallbackBoundsSize) * 0.5f;
        float   minX = ac.x - half.x, maxX = ac.x + half.x;
        float   minY = ac.y - half.y, maxY = ac.y + half.y;

        // ── 돌진 (경계 클램프 포함) ─────────────────────────────
        float elapsed  = 0f;
        int   hits     = 0;
        float hitTimer = 0f;

        while (elapsed < dashDuration && hits < hitCount)
        {
            Vector2 pos  = Owner.Rb.position;
            Vector2 next = pos + dir * speed * Time.fixedDeltaTime;

            // 경계 도달 시 클램프 후 중단
            if (next.x <= minX || next.x >= maxX || next.y <= minY || next.y >= maxY)
            {
                next = new Vector2(Mathf.Clamp(next.x, minX, maxX),
                                   Mathf.Clamp(next.y, minY, maxY));
                Owner.Rb.MovePosition(next);
                Owner.FlipToward(dir);
                break;
            }

            Owner.MoveInDirection(dir, speed);

            // 범위 안이면 타격 타이머 감소
            float dist = Owner.PlayerTransform != null
                ? Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position)
                : float.MaxValue;

            if (dist <= hitRange)
            {
                hitTimer -= Time.fixedDeltaTime;
                if (hitTimer <= 0f)
                {
                    Owner.EnemyAnimator?.SetTrigger(attackTrigger);
                    PlayerController.Instance?.TakeDamage(dmg * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f));
                    hits++;
                    hitTimer = hitInterval;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 돌진 후 남은 타격 소진 (범위 내 정지 상태에서)
        while (hits < hitCount)
        {
            if (Owner.PlayerTransform == null) break;
            float dist = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
            if (dist > hitRange) break;

            Owner.EnemyAnimator?.SetTrigger(attackTrigger);
            PlayerController.Instance?.TakeDamage(dmg * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f));
            hits++;
            yield return new WaitForSeconds(hitInterval);
        }

        yield return new WaitForSeconds(0.25f);
    }
}
