using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 둔화 기물 스킬 (진흙 기물).
/// 설치 위치 radius 범위 내 적의 이동속도를 subValue% 감소시킵니다.
/// tickInterval마다 범위 내 적을 재검사하여 둔화를 갱신합니다.
/// 둔화 중 적의 SpriteRenderer가 보라색으로 변합니다.
/// 설치 시 mudVfxPrefab을 기물 위치에 스폰하고,
/// 수명이 다하면 VFX Animator bool "bDestroy"를 true로 설정한 뒤 파괴합니다.
///
/// mainValue : (미사용, 향후 DoT 피해 등으로 확장 가능)
/// subValue  : 이동속도 감소율 (%) — 예: 40 → 40% 감소
/// radius    : 둔화 적용 범위
/// tickInterval : 범위 재검사 간격(초)
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. slowDuration  : 둔화 지속시간 (범위 밖으로 나간 후 유지 시간)
///   3. mudVfxPrefab  : 스폰할 VFX 프리팹 (Animator + bool "bDestroy" 파라미터 필요)
///   4. destroyAnimDuration : bDestroy 설정 후 VFX 파괴까지 대기 시간
/// </summary>
public class SlowSkill : StructureSkillBase
{
    [Header("둔화 설정")]
    [SerializeField] private float slowDuration = 1.5f;
    [SerializeField] private Color slowColor    = new Color(0.6f, 0.3f, 0.9f, 1f);

    [Header("진흙 VFX 설정")]
    [SerializeField] private GameObject mudVfxPrefab;
    [SerializeField] private float      destroyAnimDuration = 0.5f;

    private readonly Dictionary<EnemyController, Coroutine> _activeSlows = new();
    private readonly Dictionary<EnemyBehaviourBase, float>  _savedSpeeds = new();

    private GameObject _vfxInstance;

    public override void Activate()
    {
        // VFX 스폰
        if (mudVfxPrefab != null)
            _vfxInstance = Instantiate(mudVfxPrefab, transform.position, Quaternion.identity);

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

        if (behaviour != null) ApplySlow(behaviour, subValue);
        if (sr) sr.color = slowColor;

        yield return new WaitForSeconds(slowDuration);

        if (behaviour != null) RemoveSlow(behaviour, subValue);
        if (sr && enemy != null) sr.color = originalColor;

        if (_activeSlows.ContainsKey(enemy))
            _activeSlows.Remove(enemy);
    }

    void ApplySlow(EnemyBehaviourBase beh, float pct)
    {
        var field = beh.GetType().GetField("moveSpeed",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(float))
        {
            float original = (float)field.GetValue(beh);
            field.SetValue(beh, original * (1f - pct / 100f));
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

    /// <summary>수명 만료 시 VFX Animator bDestroy=true 설정 후 파괴합니다.</summary>
    public override IEnumerator OnPreDestroy()
    {
        if (_vfxInstance == null) yield break;

        var anim = _vfxInstance.GetComponent<Animator>();
        if (anim != null)
            anim.SetBool("bDestroy", true);

        yield return new WaitForSeconds(destroyAnimDuration);

        if (_vfxInstance != null) Destroy(_vfxInstance);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
