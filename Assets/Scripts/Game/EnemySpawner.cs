using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 스폰 영역 가장자리에서 적을 스폰합니다.
/// DB_웨이브에서 스폰 간격/수를, DB_스폰에서 적 종류/가중치/웨이브 범위를 읽습니다.
/// </summary>
public class EnemySpawner : MonoBehaviourSingleton<EnemySpawner>
{
    [Header("Prefab")]
    [SerializeField] private GameObject spawnMarkerPrefab;

    [Header("스폰 영역 (Gizmo로 표시됨)")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize   = new(10f, 18f);
    [Tooltip("스폰 영역 외곽에서 얼마나 더 바깥쪽에 스폰할지")]
    [SerializeField] private float spawnEdgeOffset   = 0.3f;

    private float currentInterval;
    private int   currentSpawnCount;
    private int   currentWave    = 1;
    private int   currentChapter = 1;
    private bool  spawning       = true;

    protected override bool DontDestroy => false;

    private void Start()
    {
        ApplyRule(currentWave);
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (spawning)
        {
            yield return new WaitForSeconds(currentInterval);
            if (!spawning) yield break;
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

            for (int i = 0; i < currentSpawnCount; i++)
                SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        DB_유닛 unit = PickUnit();
        if (unit == null || unit.f_프리팹 == null) return;

        Vector2 spawnPos = GetRandomEdgePosition();

        if (spawnMarkerPrefab != null)
        {
            GameObject markerObj = Instantiate(spawnMarkerPrefab, spawnPos, Quaternion.identity);
            if (markerObj.TryGetComponent<SpawnMarker>(out var marker))
                marker.Init(unit, currentWave);
        }
        else
        {
            GameObject obj = Instantiate(unit.f_프리팹, spawnPos, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
                enemy.InitFromDB(unit, currentWave);
        }
    }

    /// <summary>
    /// 현재 웨이브 조건(최소/최대)을 만족하는 DB_스폰 항목 중 가중치 랜덤으로 DB_유닛 선택.
    /// f_최대웨이브 == 0 이면 상한 없음.
    /// </summary>
    private DB_유닛 PickUnit()
    {
        List<DB_스폰> candidates = DB_스폰.FindEntities(e =>
            e.f_챕터 == currentChapter &&
            e.f_유닛 != null &&
            e.f_유닛.f_프리팹 != null &&
            e.f_최소웨이브 <= currentWave &&
            (e.f_최대웨이브 == 0 || e.f_최대웨이브 >= currentWave));

        if (candidates == null || candidates.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in candidates) totalWeight += e.f_가중치;
        if (totalWeight == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int acc  = 0;
        foreach (var e in candidates)
        {
            acc += e.f_가중치;
            if (roll < acc) return e.f_유닛;
        }
        return null;
    }

    // ── Wave 규칙 적용 (DB_웨이브) ────────────────────────────────
    private void ApplyRule(int wave)
    {
        DB_웨이브 matched = null;
        DB_웨이브.ForEachEntity(e =>
        {
            if (e.f_챕터 == currentChapter && e.f_시작웨이브 <= wave)
                if (matched == null || e.f_시작웨이브 > matched.f_시작웨이브)
                    matched = e;
        });

        if (matched == null)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {wave}에 맞는 DB_웨이브 항목 없음");
            return;
        }

        currentInterval   = matched.f_스폰간격;
        currentSpawnCount = matched.f_동시스폰수;
        Debug.Log($"[EnemySpawner] Wave {wave} → 간격: {currentInterval:F1}s, 동시 스폰: {currentSpawnCount}");
    }

    public void OnWaveUp(int wave)
    {
        currentWave = wave;
        ApplyRule(wave);
        StopAllCoroutines();
        if (spawning) StartCoroutine(SpawnLoop());
    }

    public void OnChapterUp(int chapter)
    {
        currentChapter = chapter;
        currentWave    = 1;
        ApplyRule(currentWave);
        StopAllCoroutines();
        if (spawning) StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        spawning = false;
        StopAllCoroutines();
    }

    /// <summary>씬에 남아있는 모든 적 오브젝트를 즉시 제거합니다.</summary>
    public void ClearAllEnemies()
    {
        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            Destroy(enemy.gameObject);
    }

    /// <summary>부활 등으로 게임이 재개될 때 호출 — spawning 플래그를 복구 후 스폰 재시작</summary>
    public void ResumeSpawning(int wave)
    {
        spawning = true;
        OnWaveUp(wave);
    }

    // ── 스폰 위치 ──────────────────────────────────────────────────
    private Vector2 GetRandomEdgePosition()
    {
        float halfW = spawnAreaSize.x * 0.5f + spawnEdgeOffset;
        float halfH = spawnAreaSize.y * 0.5f + spawnEdgeOffset;
        Vector2 center = spawnAreaCenter;

        int side = Random.Range(0, 4);
        return side switch
        {
            0 => new Vector2(center.x + Random.Range(-halfW, halfW), center.y + halfH),
            1 => new Vector2(center.x + Random.Range(-halfW, halfW), center.y - halfH),
            2 => new Vector2(center.x - halfW, center.y + Random.Range(-halfH, halfH)),
            _ => new Vector2(center.x + halfW, center.y + Random.Range(-halfH, halfH)),
        };
    }

    // ── Gizmo ──────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Vector2 outerSize = spawnAreaSize + Vector2.one * spawnEdgeOffset * 2f;
        Gizmos.DrawWireCube(spawnAreaCenter, outerSize);

        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);

        Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
        float hw = spawnAreaSize.x * 0.5f + spawnEdgeOffset;
        float hh = spawnAreaSize.y * 0.5f + spawnEdgeOffset;
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0,    hh), 0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0,   -hh), 0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(-hw,   0), 0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2( hw,   0), 0.15f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            spawnAreaCenter + new Vector2(-spawnAreaSize.x * 0.5f, spawnAreaSize.y * 0.5f + spawnEdgeOffset + 0.2f),
            "[ Spawn Area ]");
#endif
    }
}
