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

    [Header("적 상한")]
    [SerializeField] private int maxEnemiesOnMap = 30;

    // 스폰 영역은 SpawnZone 컴포넌트(NormalEnv/BossEnv 하위)가 담당합니다.

    private float currentInterval;
    private int   currentSpawnCount;
    private int   currentWave    = 1;
    private int   currentChapter = 1;
    private bool  spawning       = true;
    private bool  isBossWave     = false;

    protected override bool DontDestroy => false;

    /// <summary>현재 SpawnZone AABB — 돌진형/무리 적의 경계 클램프에 사용합니다.</summary>
    public Vector2 AreaCenter => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsCenter : Vector2.zero;
    public Vector2 AreaSize   => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsSize   : new Vector2(10f, 18f);

    private void Start()
    {
        ApplyRule(currentWave);
        if (isBossWave) SpawnBoss();
        else            StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (spawning)
        {
            if (!spawning) yield break;
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

            int alive = CountActiveEnemies();
            int spawnThisTick = Mathf.Min(currentSpawnCount, maxEnemiesOnMap - alive);
            for (int i = 0; i < spawnThisTick; i++)
                SpawnEnemy();

            yield return new WaitForSeconds(currentInterval);
        }
    }

    private int CountActiveEnemies()
    {
        int enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length;
        int markers = Object.FindObjectsByType<SpawnMarker>(FindObjectsSortMode.None).Length;
        return enemies + markers;
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

    /// <summary>보스 웨이브 전용: 최소웨이브==currentWave 인 DB_스폰 항목에서 유닛을 선택합니다.</summary>
    private DB_유닛 PickBossUnit()
    {
        List<DB_스폰> candidates = DB_스폰.FindEntities(e =>
            e.f_챕터 == currentChapter &&
            e.f_유닛 != null &&
            e.f_유닛.f_프리팹 != null &&
            e.f_최소웨이브 == currentWave);

        if (candidates == null || candidates.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in candidates) totalWeight += e.f_가중치;
        if (totalWeight == 0) return candidates[0].f_유닛;

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

        // 보스 웨이브는 규칙의 시작웨이브와 현재 웨이브가 정확히 일치할 때만 활성화
        isBossWave        = matched.f_보스 && matched.f_시작웨이브 == wave;
        currentInterval   = matched.f_스폰간격;
        currentSpawnCount = matched.f_동시스폰수;
        Debug.Log($"[EnemySpawner] Wave {wave} → 보스: {isBossWave}, 간격: {currentInterval:F1}s, 동시 스폰: {currentSpawnCount}");
    }

    public void OnWaveUp(int wave)
    {
        currentWave = wave;
        ApplyRule(wave);
        StopAllCoroutines();

        if (isBossWave)
            SpawnBoss();
        else if (spawning)
            StartCoroutine(SpawnLoop());
    }

    private void SpawnBoss()
    {
        // 이미 보스가 씬에 존재하면 중복 스폰 방지 (부활 등 재호출 대응)
        if (Object.FindAnyObjectByType<BossController>() != null)
        {
            Debug.Log("[EnemySpawner] 보스가 이미 존재합니다. 중복 스폰 방지.");
            return;
        }

        // 보스 웨이브에서는 최소웨이브==currentWave 인 항목만 대상으로 삼습니다.
        DB_유닛 unit = PickBossUnit();
        if (unit == null || unit.f_프리팹 == null)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {currentWave} 보스 유닛을 DB에서 찾을 수 없습니다. (DB_스폰 최소웨이브={currentWave} 항목 확인)");
            return;
        }

        Vector2 spawnPos = GetRandomEdgePosition();
        if (spawnMarkerPrefab != null)
        {
            var markerObj = Instantiate(spawnMarkerPrefab, spawnPos, Quaternion.identity);
            if (markerObj.TryGetComponent<SpawnMarker>(out var marker))
                marker.Init(unit, currentWave);
        }
        else
        {
            var obj = Instantiate(unit.f_프리팹, spawnPos, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
                enemy.InitFromDB(unit, currentWave);
        }

        Debug.Log($"[EnemySpawner] 보스 스폰! Wave {currentWave}");
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

    /// <summary>씬에 남아있는 모든 적 오브젝트와 스폰 마커를 즉시 제거합니다.</summary>
    public void ClearAllEnemies()
    {
        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            Destroy(enemy.gameObject);

        foreach (var marker in Object.FindObjectsByType<SpawnMarker>(FindObjectsSortMode.None))
            Destroy(marker.gameObject);
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
        if (SpawnZone.Shared != null)
            return SpawnZone.Shared.GetRandomEdgePosition();

        // SpawnZone 없을 때 폴백 (중앙 기준 10×18 직사각형 외곽)
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
