using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 공통 행동 상태 머신: Idle → Chase → Skill → 반복.
///
/// Chase 는 이 클래스에 고정되어 있으며,
/// 실제 스킬 내용은 같은 GameObject 에 붙은 BossSkillBase 파생 컴포넌트에 정의합니다.
///   ex) BossSlamSkill, BossChargeSkill, BossMagicSkill, BossScratchSkill …
///
/// 스킬 컴포넌트가 없으면 Idle → Chase 만 반복합니다.
/// </summary>
public class BossBehaviour : EnemyBehaviourBase
{
    [Header("추격")]
    [Tooltip("플레이어와 이 거리 이하가 되면 추격을 멈춥니다 (너무 붙지 않도록)")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [Tooltip("스킬 실행 전 플레이어를 추격하는 시간 (초)")]
    [SerializeField] private float chaseDuration     = 2.5f;

    [Header("대기 (스킬 사이 쉬는 시간)")]
    [Tooltip("스킬 → 다음 추격 시작 전 잠깐 멈추는 시간 (초). 격노 시 자동으로 줄어듭니다")]
    [SerializeField] private float idleDuration = 0.6f;

    private BossController boss;
    private bool           isRunning;

    // ── 초기화 ──────────────────────────────────────────────────────
    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        boss = host.SelfTransform.GetComponent<BossController>();
    }

    // EnemyController 가 호출하는 이동 훅 — 코루틴에서 직접 처리
    public override void OnMoveFixedUpdate() { }

    // ── 패턴 진입 ──────────────────────────────────────────────────
    public override void OnAttackUpdate()
    {
        if (isRunning) return;
        StartCoroutine(PatternRoutine());
    }

    // ══════════════════════════════════════════════════════════════
    //  패턴 루프: Idle → Chase → Skill
    // ══════════════════════════════════════════════════════════════

    private IEnumerator PatternRoutine()
    {
        isRunning = true;

        // 1) Idle
        yield return new WaitForSeconds(IdleTime());

        // 2) Chase
        yield return ChaseRoutine();

        // 3) 스킬 순차 실행
        var skill = boss?.NextPatternSkill();
        if (skill != null)
            yield return skill.Execute();

        isRunning = false;
    }

    // ── Chase: 플레이어 방향으로 접근 ────────────────────────────────
    private IEnumerator ChaseRoutine()
    {
        float elapsed = 0f;

        while (elapsed < chaseDuration)
        {
            elapsed += Time.fixedDeltaTime;

            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.SelfTransform.position, Owner.PlayerTransform.position);
            if (dist > chaseStopDistance)
                Owner.MoveTowardPlayer(Owner.MoveSpeed);

            yield return new WaitForFixedUpdate();
        }
    }

    private float IdleTime() => idleDuration;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, chaseStopDistance);
    }
}
