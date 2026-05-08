using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 행동: 고정 순서 패턴.
/// 인스펙터에서 등록한 스킬 순서대로 반복 실행합니다.
///
/// chain = true로 체크된 항목은 Idle + Chase를 건너뛰고 바로 실행합니다.
///
/// DB 시퀀스 모드 (ApplySequenceData 호출 시):
///   스킬 실행 -> 딜레이(Dx) -> 추격(추격T초) -> 다음 스킬 ... 반복
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

    // DB 시퀀스 모드
    private struct DbEntry { public BossSkillBase skill; public float postDelay; }
    private List<DbEntry> _dbSequence;
    private float         _dbChaseTime;
    private bool          UseDbSequence => _dbSequence != null && _dbSequence.Count > 0;

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
        if (boss != null && boss.IsGrogy) return;
        Owner.StartCoroutine(UseDbSequence ? DbSequenceRoutine() : PatternRoutine());
    }

    // 일반 패턴 루틴
    private IEnumerator PatternRoutine()
    {
        isRunning = true;
        if (pattern == null || pattern.Count == 0) { isRunning = false; yield break; }

        var entry = pattern[patternIndex % pattern.Count];
        patternIndex++;

        if (entry.chain)
        {
            if (entry.chainDelay > 0f)
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
    }

    // DB 시퀀스 루틴: 스킬 -> 딜레이 -> 추격 -> 반복
    private IEnumerator DbSequenceRoutine()
    {
        isRunning = true;

        var entry = _dbSequence[patternIndex % _dbSequence.Count];
        patternIndex++;

        if (entry.skill != null)
            yield return entry.skill.Execute();

        if (entry.postDelay > 0f)
            yield return new WaitForSeconds(entry.postDelay);

        if (_dbChaseTime > 0f)
            yield return ChaseRoutine(_dbChaseTime);

        isRunning = false;
    }

    private IEnumerator ChaseRoutine(float duration)
    {
        float elapsed   = 0f;
        float speedMult = boss != null && boss.IsEnraged ? boss.EnrageSpeedMult : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
            if (dist > chaseStopDistance)
                Owner.MoveTowardPlayer(Owner.MoveSpeed * speedMult);

            yield return new WaitForFixedUpdate();
        }
    }

    private void OnDestroy()
    {
        if (boss != null) boss.OnEnraged -= OnBossEnraged;
    }

    private void OnBossEnraged()
    {
        idleDuration  = Mathf.Max(0.15f, idleDuration  * 0.5f);
        chaseDuration = Mathf.Max(0.4f,  chaseDuration * 0.7f);
        if (_dbChaseTime > 0f)
            _dbChaseTime = Mathf.Max(0.5f, _dbChaseTime * 0.7f);
    }

    private float IdleTime() => boss != null && boss.IsEnraged ? idleDuration * 0.6f : idleDuration;

    /// <summary>
    /// BossData 기반으로 DB 시퀀스 모드를 구성합니다.
    /// BossController.ApplyBossData() 에서 호출됩니다.
    /// </summary>
    public void ApplySequenceData(BossData data, BossSkillBase[] availableSkills)
    {
        var skillMap = new Dictionary<string, BossSkillBase>();
        foreach (var s in availableSkills)
            if (s != null && !string.IsNullOrEmpty(s.SkillId))
                skillMap[s.SkillId] = s;

        _dbChaseTime = data.chaseTime;
        _dbSequence  = new List<DbEntry>();

        for (int i = 0; i < data.skillIds.Length; i++)
        {
            string id = data.skillIds[i];
            if (string.IsNullOrEmpty(id)) continue;

            skillMap.TryGetValue(id, out var skill);
            _dbSequence.Add(new DbEntry { skill = skill, postDelay = data.delays[i] });

            if (skill == null)
                Debug.LogWarning("[BossPatternBehaviour] 스킬 ID '" + id + "'에 해당하는 컴포넌트 없음.");
        }

        patternIndex = 0;
    }
}
