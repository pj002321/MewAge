using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 둔화 기물 스킬.
/// 설치 위치 radius 범위 내 적의 이동속도를 subValue% 감소시킵니다.
/// tickInterval마다 범위 내 적을 재검사하여 둔화를 갱신합니다.
/// 둔화 중 적의 SpriteRenderer가 보라색으로 변합니다.
///
/// mainValue : (미사용, 향후 DoT 피해 등으로 확장 가능)
/// subValue  : 이동속도 감소율 (%) — 예: 40 → 40% 감소
/// radius    : 둔화 적용 범위
/// tickInterval : 범위 재검사 간격(초)
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. slowDuration : 둔화 지속시간 (범위 밖으로 나간 후 유지 시간)
/// </summary>
public class SlowSkill : StructureSkillBase
{
    [Header("둔화 설정")]
    [SerializeField] private float slowDuration = 1.5f;   // 범위 이탈 후 유지 시간
    [SerializeField] private Color slowColor    = new Color(0.6f, 0.3f, 0.9f, 1f);

    // 현재 둔화 적용된 적 → 코루틴 핸들 관리
    private readonly Dictionary<EnemyController, Coroutine> _activeSlows = new();

    public override void Activate()
    {
        StartCoroutine(TickRoutine());
    }

    IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            ApplySlowInRange();
            yield return wait;
        }
    }

    void ApplySlowInRange()
    {
        var cols = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyController>(out var enemy)) continue;

            // 이미 둔화 중이면 코루틴 재시작(갱신)
            if (_activeSlows.TryGetValue(enemy, out var existing) && existing != null)
                StopCoroutine(existing);

            _activeSlows[enemy] = StartCoroutine(SlowRoutine(enemy));
        }
    }

    IEnumerator SlowRoutine(EnemyController enemy)
    {
        if (enemy == null) yield break;

        var behaviour = enemy.GetComponent<EnemyBehaviourBase>();
        var sr        = enemy.GetComponent<SpriteRenderer>();
        Color originalColor = sr != null ? sr.color : Color.white;

        // 둔화 적용
        if (behaviour != null) ApplySlow(behaviour, subValue);
        if (sr) sr.color = slowColor;

        yield return new WaitForSeconds(slowDuration);

        // 복구
        if (behaviour != null) RemoveSlow(behaviour, subValue);
        if (sr && enemy != null) sr.color = originalColor;

        if (_activeSlows.ContainsKey(enemy))
            _activeSlows.Remove(enemy);
    }

    // EnemyBehaviourBase에 moveSpeed 필드가 있다면 직접 조정,
    // 없으면 enabled 토글 대신 moveSpeedMultiplier 방식을 사용.
    // 현재 구조에서는 컴포넌트 스케일 방식으로 처리합니다.
    void ApplySlow(EnemyBehaviourBase beh, float pct)
    {
        // EnemyBehaviourBase에 moveSpeedMultiplier가 있으면 사용,
        // 없으면 리플렉션으로 _moveSpeed 직접 조정합니다.
        var field = beh.GetType().GetField("moveSpeed",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(float))
        {
            float original = (float)field.GetValue(beh);
            field.SetValue(beh, original * (1f - pct / 100f));
            // 복구를 위해 원본값을 따로 보관
            _savedSpeeds[beh] = original;
        }
    }

    void RemoveSlow(EnemyBehaviourBase beh, float pct)
    {
        if (_savedSpeeds.TryGetValue(beh, out float original))
        {
            var field = beh.GetType().GetField("moveSpeed",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
                field.SetValue(beh, original);
            _savedSpeeds.Remove(beh);
        }
    }

    private readonly Dictionary<EnemyBehaviourBase, float> _savedSpeeds = new();

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
