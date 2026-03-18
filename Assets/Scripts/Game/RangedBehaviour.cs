using UnityEngine;
using Mewage.DB;

/// <summary>
/// 원거리 공격형 — 적정 거리를 유지하면서 투사체를 발사합니다.
/// </summary>
public class RangedBehaviour : EnemyBehaviourBase
{
    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float preferredRange  = 4f;
    [SerializeField] private float shootInterval   = 2f;
    [SerializeField] private float rangedDamage    = 0f;  // 0이면 ContactDamage 사용

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;

    private float shootTimer;

    public override void ApplyDBData(DB_유닛 unit)
    {
        preferredRange = unit.f_공격범위;
        if (unit.f_쿨타임 > 0f)       shootInterval  = unit.f_쿨타임;
        if (unit.f_원거리데미지 > 0f)  rangedDamage   = unit.f_원거리데미지;
        if (unit.f_투사체프리펩 != null) projectilePrefab = unit.f_투사체프리펩;
    }

    public override void ApplyWaveScaling(float multiplier)
    {
        if (rangedDamage > 0f) rangedDamage *= multiplier;
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            shootTimer = shootInterval;
            ShootProjectile();
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        float   dist     = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        Vector2 moveDir;
        if (dist < preferredRange * 0.75f)      moveDir = -toPlayer;  // 너무 가까우면 후퇴
        else if (dist > preferredRange * 1.3f)  moveDir = toPlayer;   // 너무 멀면 접근
        else                                    moveDir = Vector2.zero;

        if (moveDir != Vector2.zero)
            Owner.MoveInDirection(moveDir, Owner.MoveSpeed);

        Owner.FlipToward(toPlayer);
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null) return;

        Owner.EnemyAnimator?.SetTrigger("tAttack");
        if (attackVfxPrefab != null)
            Instantiate(attackVfxPrefab, transform.position, Quaternion.identity);

        float   dmg = rangedDamage > 0f ? rangedDamage : Owner.ContactDamage;
        Vector2 dir = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        GameObject proj = Instantiate(projectilePrefab, Owner.Rb.position, Quaternion.identity);
        if (proj.TryGetComponent<Projectile>(out var p))
            p.Init(dmg, dir);
    }
}
