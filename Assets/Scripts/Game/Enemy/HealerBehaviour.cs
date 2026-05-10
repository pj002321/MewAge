using UnityEngine;

/// <summary>
/// 힐러형 — 주변 아군 적을 주기적으로 회복합니다.
/// TODO: 아군 적 회복 로직, 회복 이펙트 구현.
/// </summary>
public class HealerBehaviour : EnemyBehaviourBase
{
    [Header("힐러 설정")]
    [SerializeField] private float healInterval = 3f;
    [SerializeField] private float healRange    = 4f;
    [SerializeField] private float healAmount   = 20f;
    [SerializeField] private GameObject healVfxPrefab;

    [Header("이동 — 다른 적 뒤에서 지원")]
    [SerializeField] private float safeDistance = 5f;

    private float healTimer;
    private static readonly Collider2D[] _healBuffer = new Collider2D[32];

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        healTimer = healInterval;
    }

    public override void OnAttackUpdate()
    {
        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            healTimer = healInterval;
            HealNearbyAllies();
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        // 힐러는 플레이어와 안전 거리를 유지하며 후방에서 지원
        float   dist     = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        if (dist < safeDistance)
            Owner.MoveInDirection(-toPlayer, Owner.MoveSpeed);  // 도망
        else
            Owner.FlipToward(toPlayer);
    }

    private void HealNearbyAllies()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, healRange, _healBuffer);
        for (int i = 0; i < count; i++)
        {
            if (!_healBuffer[i].TryGetComponent<EnemyController>(out var ally)) continue;
            if (ally == Owner) continue;

            // TODO: EnemyController에 HealHP(float) 추가 후 활성화
            // ally.HealHP(healAmount);

            if (healVfxPrefab != null)
                Instantiate(healVfxPrefab, ally.transform.position, Quaternion.identity);
        }

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRange);
    }
}
