using UnityEngine;

/// <summary>
/// 마법 공격형 — 광역 마법 스킬을 사용합니다.
/// TODO: 광역 스킬 시전 로직, 마법 투사체/이펙트 구현.
/// </summary>
public class MagicBehaviour : EnemyBehaviourBase
{
    [Header("마법 설정")]
    [SerializeField] private float castInterval = 4f;
    [SerializeField] private float castRange    = 5f;
    [SerializeField] private float spellDamage  = 20f;
    [SerializeField] private GameObject spellPrefab;

    [Header("이동")]
    [SerializeField] private float preferredRange = 5f;

    private float castTimer;

    public override void OnAttackUpdate()
    {
        castTimer -= Time.deltaTime;
        if (castTimer <= 0f)
        {
            castTimer = castInterval;
            TryCastSpell();
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        float   dist     = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        // 마법사는 적정 거리를 유지
        Vector2 moveDir = dist < preferredRange * 0.8f ? -toPlayer :
                          dist > preferredRange * 1.2f ?  toPlayer : Vector2.zero;

        if (moveDir != Vector2.zero)
            Owner.MoveInDirection(moveDir, Owner.MoveSpeed);

        Owner.FlipToward(toPlayer);
    }

    private void TryCastSpell()
    {
        if (Owner.PlayerTransform == null) return;
        if (spellPrefab == null) return;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist > castRange) return;

        Owner.EnemyAnimator?.SetTrigger("tAttack");
        // TODO: 투사체/AoE 스킬 인스턴스화 및 데미지 처리
    }
}
