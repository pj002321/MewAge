using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 고속 추격.
/// 일정 시간 동안 매우 빠르게 플레이어를 쫓아다닙니다.
/// </summary>
public class BossFastChaseSkill : BossSkillBase
{
    [Header("고속 추격")]
    [SerializeField] private float chaseSpeed    = 16f;
    [SerializeField] private float chaseDuration = 2.0f;
    [SerializeField] private float stopDistance  = 1.0f;

    protected override IEnumerator OnExecute()
    {
        float elapsed   = 0f;
        float speedMult = Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f;

        while (elapsed < chaseDuration)
        {
            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
            if (dist > stopDistance)
            {
                Vector2 dir = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
                Owner.MoveInDirection(dir, chaseSpeed * speedMult);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }
}
