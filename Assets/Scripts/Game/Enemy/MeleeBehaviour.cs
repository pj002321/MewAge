using UnityEngine;

/// <summary>
/// 근거리 공격형 — 플레이어에게 접근 후 공격 애니메이션을 재생합니다.
/// 실제 피해는 애니메이터의 Animation Event → OnMeleeAttackHit()로 처리됩니다.
/// </summary>
public class MeleeBehaviour : EnemyBehaviourBase
{
    [Header("근거리 설정")]
    [SerializeField] private float attackRange    = 1.2f;
    [SerializeField] private float meleeHitRange  = 1.3f;
    [SerializeField] private float attackInterval = 1.0f;
    [SerializeField] private bool  useAnimationEvent = true; // false면 타이머에서 직접 피해

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;

    private float attackTimer;

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        attackTimer = attackInterval;
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        // 사정거리 안팎 모두 타이머 리셋 (진입 즉시 공격 방지)
        attackTimer = attackInterval;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
        {
            Owner.EnemyAnimator?.SetTrigger("tAttack");
            if (attackVfxPrefab != null)
                Instantiate(attackVfxPrefab, transform.position, Quaternion.identity);
            if (!useAnimationEvent)
                PlayerController.Instance?.TakeDamage(Owner.ContactDamage);
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        Vector2 toPlayer = (Vector2)Owner.PlayerTransform.position - Owner.Rb.position;
        float dist = toPlayer.magnitude;

        if (dist > attackRange)
            Owner.MoveInDirection(toPlayer / Mathf.Max(dist, 0.001f), Owner.MoveSpeed);
        else
            Owner.FlipToward(toPlayer);
    }

    /// <summary>
    /// 공격 애니메이션의 타격 프레임에 Animation Event로 호출됩니다.
    /// 이 시점에 플레이어가 근접해 있으면 피해를 입힙니다.
    /// </summary>
    public void OnMeleeAttackHit()
    {
        if (Owner == null || Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= meleeHitRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage);
    }
}
