using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 연속 근접 공격 (ComboSlash).
/// 흐름: 플레이어 범위 진입까지 추격 → 정지 → hitCount 회 근접 공격.
/// 거리 판정은 보스 Collider2D 표면 기준 (ClosestPoint).
/// 각 공격의 데미지 판정 시점에 플레이어가 slashRange 밖이면 미스.
/// </summary>
public class BossComboSlashSkill : BossSkillBase
{
    [Header("추격")]
    [Tooltip("범위 진입 추격 최대 시간 (초). 이 시간이 지나도 못 들어오면 공격 취소.")]
    [SerializeField] private float chaseTimeout = 3f;

    [Header("공격")]
    [SerializeField] private int   hitCount       = 2;
    [SerializeField] private float slashDamage    = 12f;
    [Tooltip("보스 콜라이더 표면으로부터 추가 판정 거리. 0이면 콜라이더에 닿아야 적중.")]
    [SerializeField] private float slashRange     = 0.3f;
    [Tooltip("공격 직전 조준 딜레이 (초)")]
    [SerializeField] private float preDelay       = 0.2f;
    [Tooltip("공격 트리거 후 데미지 판정까지 대기 시간 (애니메이션 히트 프레임에 맞춤)")]
    [SerializeField] private float hitDelay       = 0.3f;
    [Tooltip("공격과 다음 공격 사이 딜레이 (초)")]
    [SerializeField] private float betweenDelay   = 0.45f;

    [Header("VFX")]
    [SerializeField] private GameObject slashVfxPrefab;

    private Collider2D _collider;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        _collider = boss.GetComponent<Collider2D>();
    }

    /// <summary>보스 콜라이더 표면과 플레이어 사이 거리를 반환합니다.</summary>
    private float DistanceToPlayer(Vector2 playerPos)
    {
        if (_collider != null)
            return Vector2.Distance(_collider.ClosestPoint(playerPos), playerPos);
        return Vector2.Distance((Vector2)Owner.transform.position, playerPos);
    }

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        // 1) 범위 안에 들어올 때까지 추격
        yield return ChaseUntilInRange();
        if (Owner.PlayerTransform == null) yield break;

        // 2) 정지 + 조준 딜레이
        Owner.EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        Boss.IsFiringSkill = true;
        try
        {
            if (preDelay > 0f)
                yield return new WaitForSeconds(preDelay);

            // 3) 근접 공격 hitCount 회
            for (int i = 0; i < hitCount; i++)
            {
                if (Owner.PlayerTransform == null) yield break;

                // 공격 직전 플레이어 방향으로 플립
                Vector2 toPlayer = (Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position;
                Owner.FlipToward(toPlayer);

                Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

                if (slashVfxPrefab != null)
                    Instantiate(slashVfxPrefab, Owner.transform.position, Quaternion.identity);

                // 히트 프레임까지 대기
                yield return new WaitForSeconds(hitDelay);

                // 데미지 판정 — 콜라이더 표면 기준, 범위 안에 있어야 적중
                var player = PlayerController.Instance;
                if (player != null &&
                    DistanceToPlayer((Vector2)player.transform.position) <= slashRange)
                {
                    player.TakeDamage(slashDamage, sourceName: Owner.gameObject.name);
                }

                if (i < hitCount - 1)
                    yield return new WaitForSeconds(betweenDelay);
            }

            // 공격 후 경직
            yield return new WaitForSeconds(0.25f);
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    /// <summary>슬래시 범위 안에 들어올 때까지 직접 추격합니다.</summary>
    private IEnumerator ChaseUntilInRange()
    {
        float elapsed = 0f;
        while (elapsed < chaseTimeout)
        {
            if (Owner.PlayerTransform == null) yield break;

            float dist = DistanceToPlayer((Vector2)Owner.PlayerTransform.position);
            if (dist <= slashRange) yield break;

            Vector2 dir = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
            Owner.MoveInDirection(dir, Owner.MoveSpeed);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider2D>();
        Vector3 pos      = col != null ? col.bounds.center : transform.position;
        float baseRadius = col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) : 0f;
        float totalRange = baseRadius + slashRange;

        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.8f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, totalRange);
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.8f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, totalRange);
    }
#endif
}
