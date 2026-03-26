using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// DB_웨이브를 기반으로 적을 스폰합니다.
/// 웨이브 행 탐색: f_챕터 == currentChapter 이고, f_웨이브 <= currentWave 중 가장 큰 값.
/// 보스 웨이브(f_보스웨이브 == true): f_보스 유닛만 스폰, 스폰 루프 없음.
/// </summary>
public class EnemySpawner : MonoBehaviourSingleton<EnemySpawner>
{
    [Header("Prefab")]
    [SerializeField] private GameObject spawnMarkerPrefab;

    [Header("보스 스폰 위치 (null이면 랜덤 엣지)")]
    [SerializeField] private Transform bossSpawnPoint;

    [Header("글로벌 상한 (0 = DB f_최대스폰 우선)")]
    [SerializeField] private int maxEnemiesOnMap = 30;

    private float currentInterval  = 3f;
    private int   currentSpawnCount = 3;
    private int   currentMaxOnMap;          // DB f_최대스폰 (0 = 무제한)
    private int   currentWave    = 1;
    private int   currentChapter = 1;
    private bool  spawning       = true;
    private bool  isBossWave     = false;

    protected override bool DontDestroy => false;

    /// <summary>현재 SpawnZone AABB — 돌진형/무리 적의 경계 클램프에 사용합니다.</summary>
    public Vector2 AreaCenter => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsCenter : Vector2.zero;
    public Vector2 AreaSize   => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsSize   : new Vector2(10f, 18f);

    // ── 초기화 ──────────────────────────────────────────────────────
    private void Start()
    {
        ApplyRule(currentWave);
        if (isBossWave) SpawnBoss();
        else            StartCoroutine(SpawnLoop());
    }

    // ── DB 조회 ─────────────────────────────────────────────────────
    /// <summary>챕터 + 웨이브에 해당하는 DB_웨이브 행. 없으면 null.</summary>
    private DB_웨이브 GetActiveRow(int chapter, int wave)
    {
        DB_웨이브 matched = null;
        DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_챕터 != chapter) return;
            if (row.f_웨이브 > wave)   return;
            if (matched == null || row.f_웨이브 > matched.f_웨이브)
                matched = row;
        });
        return matched;
    }

    // ── 스폰 루프 ───────────────────────────────────────────────────
    private IEnumerator SpawnLoop()
    {
        while (spawning)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

            int cap          = currentMaxOnMap > 0 ? Mathf.Min(currentMaxOnMap, maxEnemiesOnMap) : maxEnemiesOnMap;
            int alive        = CountActiveEnemies();
            int spawnThisTick = Mathf.Min(currentSpawnCount, cap - alive);

            for (int i = 0; i < spawnThisTick; i++)
                SpawnEnemy();

            yield return new WaitForSeconds(currentInterval);
        }
    }

    private int CountActiveEnemies()
        => EnemyController.ActiveCount + SpawnMarker.ActiveCount;

    // ── 일반 적 스폰 ────────────────────────────────────────────────
    private void SpawnEnemy()
    {
        var prefab = PickPrefab();
        if (prefab == null) return;

        DoSpawn(prefab, GetRandomEdgePosition());
    }

    /// <summary>DB_웨이브 슬롯에서 비율 가중치 랜덤으로 프리팹 선택.</summary>
    private GameObject PickPrefab()
    {
        var row = GetActiveRow(currentChapter, currentWave);
        if (row == null) return null;

        // (유닛, 비율) 페어 수집 (null 유닛 제외)
        var slots = new List<(DB_유닛 unit, float ratio)>
        {
            (row.f_유닛1, row.f_비율1),
            (row.f_유닛2, row.f_비율2),
            (row.f_유닛3, row.f_비율3),
            (row.f_유닛4, row.f_비율4),
            (row.f_유닛5, row.f_비율5),
        };

        float total = 0f;
        foreach (var (unit, ratio) in slots)
            if (unit != null && unit.f_프리팹 != null && ratio > 0f)
                total += ratio;

        if (total <= 0f) return null;

        float roll = Random.value * total;
        float acc  = 0f;
        foreach (var (unit, ratio) in slots)
        {
            if (unit == null || unit.f_프리팹 == null || ratio <= 0f) continue;
            acc += ratio;
            if (roll <= acc) return unit.f_프리팹;
        }
        return null;
    }

    // ── 보스 스폰 ───────────────────────────────────────────────────
    private void SpawnBoss()
    {
        if (Object.FindAnyObjectByType<BossController>() != null)
        {
            Debug.Log("[EnemySpawner] 보스가 이미 존재합니다. 중복 스폰 방지.");
            return;
        }

        var row = GetActiveRow(currentChapter, currentWave);
        if (row == null || row.f_보스 == null || row.f_보스.f_프리팹 == null)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {currentWave}: 보스 유닛 또는 프리팹이 DB에 없습니다.");
            return;
        }

        Vector2 spawnPos = bossSpawnPoint != null
            ? (Vector2)bossSpawnPoint.position
            : GetRandomEdgePosition();
        DoSpawn(row.f_보스.f_프리팹, spawnPos);
        Debug.Log($"[EnemySpawner] 보스 스폰! Wave {currentWave} ({row.f_보스.f_이름})");
    }

    private void DoSpawn(GameObject prefab, Vector2 pos)
    {
        if (spawnMarkerPrefab != null)
        {
            var markerObj = Instantiate(spawnMarkerPrefab, pos, Quaternion.identity);
            if (markerObj.TryGetComponent<SpawnMarker>(out var marker))
                marker.Init(prefab, currentWave);
        }
        else
        {
            var obj = Instantiate(prefab, pos, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
            {
                enemy.InitForWave(currentWave);
                enemy.PlaySpawnBounce();
            }
        }
    }

    // ── 웨이브 규칙 적용 ────────────────────────────────────────────
    private void ApplyRule(int wave)
    {
        var row = GetActiveRow(currentChapter, wave);
        if (row == null)
        {
            Debug.LogWarning($"[EnemySpawner] 챕터 {currentChapter} Wave {wave}에 해당하는 DB_웨이브 행 없음");
            return;
        }

        isBossWave = row.f_보스웨이브;

        if (!isBossWave)
        {
            currentInterval   = row.f_스폰간격 > 0f ? row.f_스폰간격 : 3f;
            currentSpawnCount = row.f_동시스폰  > 0  ? row.f_동시스폰  : 3;
            currentMaxOnMap   = row.f_최대스폰;
        }

        Debug.Log($"[EnemySpawner] Wave {wave} → 보스: {isBossWave}, 간격: {currentInterval:F1}s, 동시: {currentSpawnCount}, 최대: {currentMaxOnMap}");
    }

    // ── 외부 호출 API ────────────────────────────────────────────────
    public void OnWaveUp(int wave)
    {
        currentWave = wave;
        ApplyRule(wave);
        StopAllCoroutines();

        if (isBossWave) SpawnBoss();
        else if (spawning) StartCoroutine(SpawnLoop());
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

    public void ClearAllEnemies()
    {
        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            Destroy(enemy.gameObject);
        foreach (var marker in Object.FindObjectsByType<SpawnMarker>(FindObjectsSortMode.None))
            Destroy(marker.gameObject);
    }

    public void ResumeSpawning(int wave)
    {
        spawning = true;
        OnWaveUp(wave);
    }

    /// <summary>fromWave보다 큰 가장 가까운 보스 웨이브 번호. 없으면 -1.</summary>
    public int GetNextBossWave(int fromWave)
    {
        int result = -1;
        DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_챕터 != currentChapter) return;
            if (!row.f_보스웨이브)             return;
            if (row.f_웨이브 <= fromWave)      return;
            if (result < 0 || row.f_웨이브 < result)
                result = row.f_웨이브;
        });
        return result;
    }

    // ── 스폰 위치 ────────────────────────────────────────────────────
    private Vector2 GetRandomEdgePosition()
    {
        if (SpawnZone.Shared != null)
            return SpawnZone.Shared.GetRandomEdgePosition();

        int side = Random.Range(0, 4);
        return side switch
        {
            0 => new Vector2(Random.Range(-5f,  5f),  9.5f),
            1 => new Vector2(Random.Range(-5f,  5f), -9.5f),
            2 => new Vector2(-5.5f, Random.Range(-9f, 9f)),
            _ => new Vector2( 5.5f, Random.Range(-9f, 9f)),
        };
    }
}
