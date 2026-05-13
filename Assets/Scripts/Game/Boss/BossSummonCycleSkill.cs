using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 스킬: 주기적 소환 사이클.
/// BossProjectileCycleSkill과 동일하게 executeInPattern=false로 독립 루프 실행.
///
/// summonInterval초마다 보스 주변에 summonCount개의 소환체를 스폰합니다.
/// 현재 살아있는 소환체가 maxActiveMinions 이상이면 소환을 건너뜁니다.
/// 소환체(BossSummonMinion)는 minionDuration초 동안 플레이어를 추격하다 소멸합니다.
/// </summary>
public class BossSummonCycleSkill : BossSkillBase
{
    [Header("소환 프리팹")]
    [Tooltip("BossSummonMinion 컴포넌트가 없으면 자동으로 추가합니다.")]
    [SerializeField] private GameObject minionPrefab;
    [Tooltip("ImpactMarker 컴포넌트가 붙은 스폰 마커 프리팹. null이면 마커 없이 즉시 소환.")]
    [SerializeField] private GameObject spawnMarkerPrefab;

    [Header("소환 설정")]
    [SerializeField] private int   summonCount         = 2;    // 1회 소환 수
    [SerializeField] private int   maxActiveMinions    = 4;    // 동시 최대 생존 수 (0 = 제한 없음)
    [SerializeField] private float summonInterval      = 8f;   // 소환 주기 (초)
    [SerializeField] private float markerWarningTime   = 0.8f; // 마커 표시 시간 (초)
    [SerializeField] private float minionDuration      = 10f;  // 소환체 생존 시간 (초)
    [SerializeField] private float minionMoveSpeed     = 2.5f; // 소환체 이동 속도
    [SerializeField] private float minionContactDamage = 5f;   // 소환체 접촉 피해
    [SerializeField] private float spawnRadius         = 3f;   // 보스 주변 소환 반경

    private readonly List<BossSummonMinion> _activeMinions = new();
    private GameObject[] _pendingMarkers;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        executeInPattern = false;
        StartCoroutine(SummonLoop());
    }

    protected override IEnumerator OnExecute() { yield break; }

    private void OnDestroy()
    {
        if (_pendingMarkers == null) return;
        foreach (var m in _pendingMarkers)
            if (m != null) Destroy(m);
        _pendingMarkers = null;
    }

    private IEnumerator SummonLoop()
    {
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
                yield return new WaitUntil(() => GameManager.Shared == null || !GameManager.Shared.IsGameOver);

            if (Boss != null && Boss.IsGrogy)
                yield return new WaitUntil(() => Boss == null || !Boss.IsGrogy);

            if (Boss == null) yield break;

            yield return new WaitForSeconds(summonInterval);

            if (Boss == null) yield break;

            yield return StartCoroutine(SpawnMinions());
        }
    }

    private IEnumerator SpawnMinions()
    {
        if (minionPrefab == null || Boss == null) yield break;

        // 파괴됐거나 풀로 반환된(비활성) 소환체 정리
        _activeMinions.RemoveAll(m => m == null || !m.isActiveAndEnabled);

        // 최대 수 초과 시 건너뜀
        if (maxActiveMinions > 0 && _activeMinions.Count >= maxActiveMinions) yield break;

        int canSpawn = maxActiveMinions > 0
            ? Mathf.Min(summonCount, maxActiveMinions - _activeMinions.Count)
            : summonCount;

        // 스폰 위치 미리 결정
        var positions = new Vector3[canSpawn];
        for (int i = 0; i < canSpawn; i++)
        {
            float   angle  = Random.Range(0f, 360f);
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                         Mathf.Sin(angle * Mathf.Deg2Rad)) * spawnRadius;
            positions[i] = Boss.transform.position + (Vector3)offset;
        }

        // 마커 표시
        var markers      = new ImpactMarker[canSpawn];
        var markerObjects = new GameObject[canSpawn];
        if (spawnMarkerPrefab != null)
        {
            for (int i = 0; i < canSpawn; i++)
            {
                var obj = Instantiate(spawnMarkerPrefab, positions[i], Quaternion.identity);
                markerObjects[i] = obj;
                markers[i]       = obj.GetComponent<ImpactMarker>();
            }
            _pendingMarkers = markerObjects;
            yield return new WaitForSeconds(markerWarningTime);
            _pendingMarkers = null;
        }

        // 마커 제거
        for (int i = 0; i < canSpawn; i++)
        {
            if (markers[i] != null) markers[i].Dismiss();
            else if (markerObjects[i] != null) Destroy(markerObjects[i]);
        }

        if (Boss == null) yield break;

        // 소환체 스폰
        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        float dmgMult = Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
        for (int i = 0; i < canSpawn; i++)
        {
            var go     = Instantiate(minionPrefab, positions[i], Quaternion.identity);
            var minion = go.GetComponent<BossSummonMinion>();
            if (minion == null) minion = go.AddComponent<BossSummonMinion>();
            minion.Init(minionDuration, minionMoveSpeed, minionContactDamage * dmgMult);
            _activeMinions.Add(minion);
        }
    }
}
