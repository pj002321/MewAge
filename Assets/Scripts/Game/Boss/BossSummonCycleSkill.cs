using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 소환체 스폰.
/// executeInPattern = true 로 PatternLoop에서 다른 스킬과 번갈아 실행됩니다.
/// maxActiveMinions 이상 생존 중이면 소환을 건너뜁니다.
/// </summary>
[System.Serializable]
public class BossSummonSkill : BossSkillBase
{
    [Header("소환 설정")]
    [SerializeField] private int   summonCount      = 2;
    [Tooltip("동시 최대 생존 수. 0이면 제한 없음")]
    [SerializeField] private int   maxActiveMinions = 4;
    [SerializeField] private float minionDuration   = 10f;
    [SerializeField] private float minionMoveSpeed  = 2.5f;
    [SerializeField] private float minionContactDmg = 5f;
    [SerializeField] private float spawnRadius      = 3f;
    [SerializeField] private float markerWarningTime = 0.8f;

    [Header("프리팹")]
    [Tooltip("BossSummonMinion 컴포넌트가 없으면 자동 추가")]
    [SerializeField] private GameObject minionPrefab;
    [Tooltip("ImpactMarker 컴포넌트가 붙은 스폰 마커 프리팹. null이면 마커 없이 즉시 소환")]
    [SerializeField] private GameObject spawnMarkerPrefab;

    private readonly List<BossSummonMinion> _activeMinions = new();

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (minionPrefab == null || Boss == null) return;

        // 파괴됐거나 비활성화된 소환체 정리
        _activeMinions.RemoveAll(m => m == null || !m.isActiveAndEnabled);

        // 최대 수 초과 시 건너뜀
        if (maxActiveMinions > 0 && _activeMinions.Count >= maxActiveMinions) return;

        int canSpawn = maxActiveMinions > 0
            ? Mathf.Min(summonCount, maxActiveMinions - _activeMinions.Count)
            : summonCount;

        // 스폰 위치 결정
        var positions = new Vector3[canSpawn];
        for (int i = 0; i < canSpawn; i++)
        {
            float   angle  = Random.Range(0f, 360f);
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                         Mathf.Sin(angle * Mathf.Deg2Rad)) * spawnRadius;
            positions[i] = Boss.transform.position + (Vector3)offset;
        }

        // 마커 표시
        var markers       = new ImpactMarker[canSpawn];
        var markerObjects = new GameObject[canSpawn];
        if (spawnMarkerPrefab != null)
        {
            for (int i = 0; i < canSpawn; i++)
            {
                var obj = Object.Instantiate(spawnMarkerPrefab, positions[i], Quaternion.identity);
                markerObjects[i] = obj;
                markers[i]       = obj.GetComponent<ImpactMarker>();
            }
            await UniTask.Delay(System.TimeSpan.FromSeconds(markerWarningTime), cancellationToken: ct);
        }

        // 마커 제거
        for (int i = 0; i < canSpawn; i++)
        {
            if (markers[i] != null) markers[i].Dismiss();
            else if (markerObjects[i] != null) Object.Destroy(markerObjects[i]);
        }

        if (Boss == null || ct.IsCancellationRequested) return;

        // 소환
        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        for (int i = 0; i < canSpawn; i++)
        {
            BossController.NextIsClone = true;
            var go     = Object.Instantiate(minionPrefab, positions[i], Quaternion.identity);
            BossController.NextIsClone = false;

            var minion = go.GetComponent<BossSummonMinion>();
            if (minion == null) minion = go.AddComponent<BossSummonMinion>();
            minion.Init(minionDuration, minionMoveSpeed, minionContactDmg);
            _activeMinions.Add(minion);

            // 보스 프리팹 소환체: BossClone으로 등록해 실보스 사망 시 함께 제거
            if (go.GetComponent<BossController>() != null)
            {
                var clone = go.AddComponent<BossClone>();
                clone.Init(minionDuration, null);
            }
        }
    }
}
