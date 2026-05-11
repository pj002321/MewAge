using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 고속 추격.
/// 일정 시간 동안 매우 빠르게 플레이어를 쫓아다닙니다.
/// stopDistance 이내에 도달하면 TAttack 애니메이션을 재생하고,
/// 애니메이션 이벤트 OnFastChaseHit() 호출 시 피해를 적용합니다.
/// </summary>
public class BossFastChaseSkill : BossSkillBase
{
    [Header("고속 추격")]
    [SerializeField] private float chaseSpeed    = 16f;
    [SerializeField] private float chaseDuration = 2.0f;
    [SerializeField] private float stopDistance  = 1.0f;

    [Header("근접 공격")]
    [SerializeField] private float attackCooldown    = 0.6f;
    [SerializeField] private float animEventTimeout  = 0.5f;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfx;
    [SerializeField] private float      vfxDuration = 0.3f;

    private bool  _pendingHit;
    private float _dmgMult;

    protected override IEnumerator OnExecute()
    {
        float elapsed     = 0f;
        float attackTimer = 0f;
        float speedMult   = Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f;
        _dmgMult          = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;

        while (elapsed < chaseDuration)
        {
            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);

            if (dist > stopDistance && !(Boss != null && Boss.IsFiringSkill))
            {
                Vector2 dir = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
                Owner.MoveInDirection(dir, chaseSpeed * speedMult);
            }
            else
            {
                attackTimer -= Time.fixedDeltaTime;
                if (attackTimer <= 0f)
                {
                    Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                    if (attackVfx != null) StartCoroutine(ShowVfx());

                    _pendingHit = true;
                    if (animEventTimeout > 0f) StartCoroutine(AnimEventFallback());

                    attackTimer = attackCooldown;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>애니메이션 이벤트에서 호출됩니다.</summary>
    public void OnFastChaseHit()
    {
        if (!_pendingHit) return;
        _pendingHit = false;
        if (Owner == null || Owner.PlayerTransform == null) return;
        if (Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position) <= stopDistance)
            PlayerController.Instance?.TakeDamage(Boss.ContactDamage * _dmgMult);
    }

    private IEnumerator AnimEventFallback()
    {
        yield return new WaitForSeconds(animEventTimeout);
        if (!_pendingHit) yield break;
        _pendingHit = false;
        if (Owner == null || Owner.PlayerTransform == null) yield break;
        if (Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position) <= stopDistance)
            PlayerController.Instance?.TakeDamage(Boss.ContactDamage * _dmgMult);
    }

    private IEnumerator ShowVfx()
    {
        attackVfx.SetActive(true);
        yield return new WaitForSeconds(vfxDuration);
        if (attackVfx != null) attackVfx.SetActive(false);
    }
}
