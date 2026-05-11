using System.Collections;
using UnityEngine;

/// <summary>
/// 일반 적 타입: 돌진 후 묶음 공격.
/// 대기 → 돌진 → 근접 후 연속 공격 × N회 → 반복.
/// 실제 피해는 Animation Event → OnDashComboHit() 또는 직접 처리.
/// </summary>
public class DashComboBehaviour : EnemyBehaviourBase
{
    [Header("대기")]
    [SerializeField] private float idleDuration  = 1.2f;

    [Header("돌진")]
    [SerializeField] private float dashSpeed     = 8f;
    [SerializeField] private float dashDuration  = 0.4f;
    [SerializeField] private float dashStopDist  = 1.0f;   // 이 거리 이내면 돌진 중단

    [Header("연속 공격")]
    [SerializeField] private int   hitCount      = 3;
    [SerializeField] private float hitInterval   = 0.25f;
    [SerializeField] private float hitRange      = 1.4f;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;

    private bool isRunning;
    private float _pendingDamage;
    private WaitForSeconds _waitIdle;
    private WaitForSeconds _waitHitInterval;

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        _waitIdle        = new WaitForSeconds(idleDuration);
        _waitHitInterval = new WaitForSeconds(hitInterval);
    }

    public override void OnMoveFixedUpdate() { }   // 코루틴에서 직접 이동

    public override void OnAttackUpdate()
    {
        if (isRunning) return;
        StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        isRunning = true;

        while (true)
        {
            // 1) 대기
            yield return _waitIdle;

            if (Owner.PlayerTransform == null) continue;

            // 2) 돌진
            Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;
            float   elapsed = 0f;

            while (elapsed < dashDuration)
            {
                if (Owner.PlayerTransform == null) break;

                float dist = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
                if (dist <= dashStopDist) break;

                Owner.MoveInDirection(dir, dashSpeed);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // 3) 연속 공격
            for (int i = 0; i < hitCount; i++)
            {
                if (Owner.PlayerTransform == null) break;

                Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

                if (attackVfxPrefab != null)
                    Instantiate(attackVfxPrefab, Owner.SelfTransform.position, Quaternion.identity);

                yield return _waitHitInterval;
            }
        }
    }

    /// <summary>
    /// Animation Event 방식으로 타격 판정.
    /// 애니메이션 클립에 이 메서드를 이벤트로 등록하세요.
    /// </summary>
    public void OnDashComboHit()
    {
        if (Owner == null || Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(Owner.SelfTransform.position, Owner.PlayerTransform.position);
        if (dist <= hitRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage);
    }
}
