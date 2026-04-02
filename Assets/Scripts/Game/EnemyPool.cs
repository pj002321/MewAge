using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 프리팹별 오브젝트 풀.
/// SetActive를 사용하지 않아 Rigidbody2D 물리 재등록 비용 없음.
/// All 리스트 관리를 직접 수행 (OnEnable/OnDisable 의존 제거).
/// </summary>
public class EnemyPool : MonoBehaviourSingleton<EnemyPool>
{
    protected override bool DontDestroy => false;

    // 화면 밖 대기 위치 (물리 연산에서 사실상 격리)
    static readonly Vector2 ParkPos = new Vector2(-9999f, -9999f);

    private readonly Dictionary<int, Queue<EnemyController>> _pools  = new();
    private readonly Dictionary<EnemyController, int>        _idMap  = new();

    // ── 꺼내기 ────────────────────────────────────────────────────

    public EnemyController Get(GameObject prefab, Vector2 pos)
    {
        int id = prefab.GetInstanceID();
        if (!_pools.TryGetValue(id, out var queue))
            _pools[id] = queue = new Queue<EnemyController>();

        EnemyController enemy;
        if (queue.Count > 0)
        {
            enemy = queue.Dequeue();
            enemy.transform.SetPositionAndRotation(pos, Quaternion.identity);
            enemy.transform.localScale = Vector3.one;
            enemy.WakeFromPool();            // 렌더러·콜라이더 켜기, All 등록
        }
        else
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            enemy  = go.GetComponent<EnemyController>();
            if (enemy != null) _idMap[enemy] = id;
            // 새 인스턴스는 Awake에서 All.Add 처리
        }
        return enemy;
    }

    // ── 돌려보내기 ────────────────────────────────────────────────

    public void Return(EnemyController enemy)
    {
        if (enemy == null) return;

        enemy.SleepToPool();                            // 렌더러·콜라이더 끄기, All 제거
        enemy.transform.SetPositionAndRotation(ParkPos, Quaternion.identity);

        if (_idMap.TryGetValue(enemy, out int id) && _pools.TryGetValue(id, out var queue))
            queue.Enqueue(enemy);
        else
            Destroy(enemy.gameObject);
    }

    // ── 전체 반환 ────────────────────────────────────────────────

    public void ReturnAll()
    {
        var snapshot = new List<EnemyController>(EnemyController.All);
        foreach (var e in snapshot)
            Return(e);
    }
}
