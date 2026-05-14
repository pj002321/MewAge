using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 스킬: 복제체 궤도 공전 (BOSS_SPIN).
///
/// 흐름:
///   1) cloneCount 개의 복제 오브젝트를 플레이어 주변에 equidistant하게 배치
///   2) orbitDuration 동안 플레이어 중심으로 orbitSpeed(deg/s) 공전
///   3) 각 복제체가 자체 hitCooldown으로 플레이어에게 데미지 적용
///   4) 스킬 종료 시 모든 복제체 제거
/// </summary>
public class BossOrbitSkill : BossSkillBase
{
    [Header("공전 설정")]
    [SerializeField] private int   cloneCount    = 6;
    [SerializeField] private float orbitRadius   = 2.5f;
    [SerializeField] private float orbitSpeed    = 120f;   // deg/s
    [SerializeField] private float orbitDuration = 5f;

    [Header("피해")]
    [SerializeField] private float hitRadius   = 0.4f;
    [SerializeField] private float hitDamage   = 0f;   // 0이면 ContactDamage 사용
    [SerializeField] private float hitCooldown = 0.5f;

    [Header("복제체 프리팹")]
    [Tooltip("보스 스프라이트와 같은 비주얼 프리팹. 없으면 보스 SpriteRenderer를 복제합니다.")]
    [SerializeField] private GameObject clonePrefab;

    [Header("진입/퇴장 딜레이")]
    [SerializeField] private float preDelay  = 0.2f;
    [SerializeField] private float postDelay = 0.3f;

    private readonly List<GameObject>   _orbiters  = new();
    private readonly List<float>        _hitTimers = new();

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
        if (preDelay > 0f) yield return new WaitForSeconds(preDelay);
        if (Owner.PlayerTransform == null) yield break;

        SpawnOrbiters();

        float elapsed = 0f;
        float angle   = 0f;
        float dmg     = hitDamage > 0f ? hitDamage : Owner.ContactDamage;

        while (elapsed < orbitDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            angle   += orbitSpeed * dt;

            if (Owner.PlayerTransform == null) break;
            Vector2 center = Owner.PlayerTransform.position;

            float step = 360f / Mathf.Max(1, _orbiters.Count);
            var player = PlayerController.Instance;

            for (int i = 0; i < _orbiters.Count; i++)
            {
                if (_orbiters[i] == null) continue;

                float rad = (angle + step * i) * Mathf.Deg2Rad;
                Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
                _orbiters[i].transform.position = pos;

                // 피해 판정
                _hitTimers[i] = Mathf.Max(0f, _hitTimers[i] - dt);
                if (_hitTimers[i] <= 0f && player != null)
                {
                    float dist = Vector2.Distance(pos, (Vector2)player.transform.position);
                    if (dist <= hitRadius)
                    {
                        player.TakeDamage(dmg, sourceName: Owner.gameObject.name);
                        _hitTimers[i] = hitCooldown;
                    }
                }
            }

            yield return null;
        }

        DespawnOrbiters();

        if (postDelay > 0f) yield return new WaitForSeconds(postDelay);
    }

    private void SpawnOrbiters()
    {
        DespawnOrbiters();

        if (Owner.PlayerTransform == null) return;
        Vector2 center = Owner.PlayerTransform.position;
        float step = 360f / Mathf.Max(1, cloneCount);

        GameObject source = clonePrefab;
        if (source == null)
        {
            var sr = Boss != null ? Boss.GetComponentInChildren<SpriteRenderer>() : null;
            if (sr != null) source = sr.gameObject;
        }

        for (int i = 0; i < cloneCount; i++)
        {
            float rad = step * i * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;

            GameObject obj;
            if (source != null)
                obj = Instantiate(source, pos, Quaternion.identity);
            else
            {
                obj = new GameObject("BossOrbiter");
                obj.transform.position = pos;
            }

            // 복제체는 충돌 로직 없이 순수 위치 기반 판정 사용
            var col = obj.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            _orbiters.Add(obj);
            _hitTimers.Add(0f);
        }
    }

    private void DespawnOrbiters()
    {
        foreach (var obj in _orbiters)
            if (obj != null) Destroy(obj);
        _orbiters.Clear();
        _hitTimers.Clear();
    }

    private void OnDestroy() => DespawnOrbiters();
}
