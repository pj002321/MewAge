using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진 (Charge).
/// 플레이어 방향으로 빠르게 돌진하며, 스폰 영역 경계에 닿으면 즉시 중단합니다.
/// </summary>
public class BossChargeSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float   chargeSpeed      = 12f;
    [SerializeField] private float   chargeDuration   = 0.55f;
    [Tooltip("스폰 영역이 없을 때 사용할 폴백 경계 중심")]
    [SerializeField] private Vector2 chargeBoundsCenter = Vector2.zero;
    [Tooltip("스폰 영역이 없을 때 사용할 폴백 경계 크기")]
    [SerializeField] private Vector2 chargeBoundsSize   = new(10f, 18f);

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        float   speed   = chargeSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);
        Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
        float   elapsed = 0f;

        // 경계 계산 — EnemySpawner 영역 우선, 없으면 직렬화 필드 폴백
        var     spawner  = EnemySpawner.Shared;
        Vector2 ac       = spawner != null ? spawner.AreaCenter : chargeBoundsCenter;
        Vector2 aSize    = spawner != null ? spawner.AreaSize   : chargeBoundsSize;
        Vector2 half     = aSize * 0.5f;
        float   minX = ac.x - half.x, maxX = ac.x + half.x;
        float   minY = ac.y - half.y, maxY = ac.y + half.y;

        while (elapsed < chargeDuration)
        {
            Vector2 pos  = Owner.Rb.position;
            Vector2 next = pos + dir * speed * Time.fixedDeltaTime;

            // 경계를 넘으면 경계 안쪽으로 클램프 후 중단
            bool hitBound = next.x <= minX || next.x >= maxX ||
                            next.y <= minY || next.y >= maxY;
            if (hitBound)
            {
                next = new Vector2(Mathf.Clamp(next.x, minX, maxX),
                                   Mathf.Clamp(next.y, minY, maxY));
                Owner.Rb.MovePosition(next);
                Owner.FlipToward(dir);
                break;
            }

            Owner.MoveInDirection(dir, speed);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 돌진 후 짧은 경직
        yield return new WaitForSeconds(0.2f);
    }
}
