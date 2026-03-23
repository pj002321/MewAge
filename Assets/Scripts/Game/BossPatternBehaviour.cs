using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 행동: 고정 순서 패턴.
/// 인스펙터에서 등록한 스킬 순서대로 반복 실행합니다.
///   예) [BossChargeSkill, BossComboSlashSkill] → 돌진 → 콤보 → 돌진 → 콤보 …
///
/// BossBehaviour 대신 이 컴포넌트를 프리팹에 붙이세요.
/// BossController + BossSkillBase 파생 스킬들도 함께 붙여야 합니다.
/// </summary>
public class BossPatternBehaviour : EnemyBehaviourBase
{
    [Header("고정 패턴 순서")]
    [Tooltip("실행할 스킬을 순서대로 등록합니다. 끝에 도달하면 처음으로 돌아갑니다.")]
    [SerializeField] private List<BossSkillBase> pattern = new();

    [Header("스킬 사이 추적")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [SerializeField] private float chaseDuration     = 2.0f;

    [Header("대기")]
    [SerializeField] private float idleDuration = 0.5f;

    private BossController boss;
    private bool           isRunning;
    private int            patternIndex;

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        boss = controller.GetComponent<BossController>();
        if (boss != null) boss.OnEnraged += OnBossEnraged;
    }

    public override void OnMoveFixedUpdate() { }

    public override void OnAttackUpdate()
    {
        if (isRunning) return;
        Owner.StartCoroutine(PatternRoutine());
    }

    // ══════════════════════════════════════════════════════════════
    //  패턴 루프
    // ══════════════════════════════════════════════════════════════
    private IEnumerator PatternRoutine()
    {
        isRunning = true;

        // 1) Idle
        yield return new WaitForSeconds(IdleTime());

        // 2) 플레이어 방향으로 짧게 추적
        yield return ChaseRoutine();

        // 3) 패턴에서 다음 스킬 실행
        if (pattern != null && pattern.Count > 0)
        {
            var skill = pattern[patternIndex % pattern.Count];
            patternIndex++;

            if (skill != null)
                yield return skill.Execute();
        }

        isRunning = false;
    }

    private IEnumerator ChaseRoutine()
    {
        float elapsed   = 0f;
        float speedMult = boss != null && boss.IsEnraged ? boss.EnrageSpeedMult : 1f;

        while (elapsed < chaseDuration)
        {
            elapsed += Time.fixedDeltaTime;

            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
            if (dist > chaseStopDistance)
                Owner.MoveTowardPlayer(Owner.MoveSpeed * speedMult);

            yield return new WaitForFixedUpdate();
        }
    }

    private void OnBossEnraged()
    {
        idleDuration  = Mathf.Max(0.2f, idleDuration  * 0.5f);
        chaseDuration = Mathf.Max(0.5f, chaseDuration * 0.7f);
    }

    private float IdleTime() => boss != null && boss.IsEnraged ? idleDuration * 0.6f : idleDuration;
}
