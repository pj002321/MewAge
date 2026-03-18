using UnityEngine;

/// <summary>
/// 탱커형 — 느리지만 고체력으로 플레이어에게 정면으로 돌진합니다.
/// TODO: 충격파, 방어막, 타격 시 피해 감소 메커니즘 구현.
/// </summary>
public class TankBehaviour : EnemyBehaviourBase
{
    [Header("탱커 설정")]
    [SerializeField] private float attackRange    = 1.5f;
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private float damageMultiplier = 1.5f;   // 타격 피해 배율
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

        attackTimer = attackInterval;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
        {
            Owner.EnemyAnimator?.SetTrigger("tAttack");
            if (attackVfxPrefab != null)
                Instantiate(attackVfxPrefab, transform.position, Quaternion.identity);

            // TODO: 충격파 AoE 또는 강화 타격 구현
        }
    }

    public override void OnMoveFixedUpdate()
    {
        // 탱커는 느리게 직선으로 접근
        Owner.MoveTowardPlayer(Owner.MoveSpeed);
    }

    /// <summary>Animation Event — 강화 근접 타격.</summary>
    public void OnTankAttackHit()
    {
        if (Owner == null || Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage * damageMultiplier);
    }
}
