using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 행동: 고정 순서 패턴.
/// 인스펙터에서 등록한 스킬 순서대로 반복 실행합니다.
///
/// chain = true로 체크된 항목은 Idle + Chase를 건너뛰고 바로 실행합니다.
/// </summary>
public class BossPatternBehaviour : EnemyBehaviourBase
{
    [System.Serializable]
    public struct PatternEntry
    {
        public BossSkillBase skill;
        [Tooltip("체크하면 Idle+Chase 없이 즉시 실행 (연계용).")]
        public bool  chain;
        [Tooltip("chain=true일 때 실행 전 대기 시간 (0이면 즉시).")]
        public float chainDelay;
    }

    [Header("고정 패턴 순서")]
    [SerializeField] private List<PatternEntry> pattern = new();

    [Header("스킬 사이 추적")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [SerializeField] private float chaseDuration     = 1.5f;

    [Header("대기")]
    [SerializeField] private float idleDuration = 0.4f;

    private BossController boss;
    private bool           isRunning;
    private int            patternIndex;
    private Coroutine      _activeCoroutine;
    private bool           _skipIdleChaseOnce; // 그로기 해제 직후 첫 패턴은 즉시 실행

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        boss = host.SelfTransform.GetComponent<BossController>();
        if (boss != null)
        {
            boss.OnEnraged  += OnBossEnraged;
            boss.OnGrogyStart += OnGrogyStarted;
            boss.OnGrogyEnd   += OnGrogyEnded;
        }
    }

    private void OnGrogyStarted()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
            isRunning = false;
        }
        // 현재 패턴의 스킬이 StartCoroutine으로 생성한 자식 코루틴(FloatPlayer 등)을 중단하고 상태 복구
        foreach (var entry in pattern)
            entry.skill?.OnInterrupted();
    }

    private void OnGrogyEnded() => _skipIdleChaseOnce = true;

    public override void OnMoveFixedUpdate() { }

    public override void OnAttackUpdate()
    {
        if (isRunning) return;
        if (boss != null && boss.IsGrogy) return;
        _activeCoroutine = StartCoroutine(PatternRoutine());
    }

    private IEnumerator PatternRoutine()
    {
        isRunning = true;
        if (pattern == null || pattern.Count == 0) { isRunning = false; yield break; }

        var entry = pattern[patternIndex % pattern.Count];
        patternIndex++;

        bool skipDelay = entry.chain || _skipIdleChaseOnce;
        _skipIdleChaseOnce = false;

        if (skipDelay)
        {
            if (entry.chain && entry.chainDelay > 0f)
                yield return new WaitForSeconds(entry.chainDelay);
        }
        else
        {
            yield return new WaitForSeconds(IdleTime());
            yield return ChaseRoutine(chaseDuration);
        }

        if (entry.skill != null)
            yield return entry.skill.Execute();

        isRunning = false;
        _activeCoroutine = null;
    }

    private IEnumerator ChaseRoutine(float duration)
    {
        float elapsed   = 0f;
        float speedMult = boss != null && boss.IsEnraged ? boss.EnrageSpeedMult : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.SelfTransform.position, Owner.PlayerTransform.position);
            if (dist > chaseStopDistance && !(boss != null && boss.IsFiringSkill))
                Owner.MoveTowardPlayer(Owner.MoveSpeed * speedMult);

            yield return new WaitForFixedUpdate();
        }
    }

    private void OnDestroy()
    {
        if (boss != null)
        {
            boss.OnEnraged    -= OnBossEnraged;
            boss.OnGrogyStart -= OnGrogyStarted;
            boss.OnGrogyEnd   -= OnGrogyEnded;
        }
    }

    private void OnBossEnraged()
    {
        idleDuration  = Mathf.Max(0.15f, idleDuration  * 0.5f);
        chaseDuration = Mathf.Max(0.4f,  chaseDuration * 0.7f);
    }

    private float IdleTime() => boss != null && boss.IsEnraged ? idleDuration * 0.6f : idleDuration;
}
