using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진 (Charge).
/// 플레이어 방향으로 빠르게 돌진하며, MovementBounds 경계에 닿으면 즉시 중단합니다.
///
/// Fix: 게임오버 상태이거나 플레이어가 null이면 루프를 즉시 종료합니다.
/// </summary>
public class BossChargeSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float chargeSpeed    = 18f;
    [SerializeField] private float chargeDuration = 0.4f;

    protected override IEnumerator OnExecute()
    {
        // 플레이어 없음 또는 게임오버 → 즉시 중단
        if (Owner.PlayerTransform == null) yield break;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

        Owner.EnemyAnimator?.SetTrigger("tCharge");

        float   speed   = chargeSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);
        // 시작 시점의 플레이어 방향을 고정으로 사용 (방향이 도중에 바뀌지 않도록)
        Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
        float   elapsed = 0f;

        while (elapsed < chargeDuration)
        {
            // 루프 중 게임오버 또는 PlayerTransform 소멸 시 즉시 중단
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
            if (Owner == null || Owner.PlayerTransform == null) yield break;

            Vector2 next = Owner.Rb.position + dir * speed * Time.fixedDeltaTime;

            // MovementBounds 밖으로 나가면 경계에서 멈춤
            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
            {
                Owner.Rb.MovePosition(mb.ClosestPointOnBoundary(next));
                Owner.FlipToward(dir);
                yield break;   // break → yield break: 완전 종료
            }

            Owner.MoveInDirection(dir, speed);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }
}
