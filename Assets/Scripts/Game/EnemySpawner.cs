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
    private int   currentStage   = 1;
    private int   wavesPerStage  = 30;      // DB_챕터.f_웨이브에서 로드
    private bool  spawning       = true;
    private bool  isBossWave     = false;

    private int CalcGlobalWave(int stage, int wave) => (stage - 1) * wavesPerStage + wave;

    protected override bool DontDestroy => false;

    public Vector2 AreaCenter => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsCenter : Vector2.zero;
    public Vector2 AreaSize   => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsSize   : new Vector2(10f, 18f);

    private readonly System.Collections.Generic.Dictionary<string, UnitData> _unitCache = new();
    private readonly System.Collections.Generic.Dictionary<string, BossData> _bossCache = new();

    // ── 초기화 ──────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        DB_유닛.ForEachEntity(row =>
        {
            var d = UnitData.FromDB(row);
            if (!string.IsNullOrEmpty(d.unitId)) _unitCache[d.unitId] = d;
        });
        DB_보스.ForEachEntity(row =>
        {
            var d = BossData.FromDB(row);
            if (!string.IsNullOrEmpty(d.bossId)) _bossCache[d.bossId] = d;
        });
    }

    private void Start()
    {
        ApplyRule(currentWave);
        if (isBossWave) SpawnBoss();
        else            StartCoroutine(SpawnLoop());
    }

    // ── DB 조회 ─────────────────────────────────────────────────────
    /// <summary>챕터+스테이지+웨이브에 해당하는 DB_웨이브 행. 없으면 null.</summary>
    private DB_웨이브 GetActiveRow(int chapter, int stage, int wave)
    {
        string targetId = $"C{chapter}_S{stage}_W{wave}";
        DB_웨이브 matched = null;
        DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_웨이브ID == targetId)
                matched = row;
        });
        return matched;
    }

    /// <summary>챕터+웨이브에 해당하는 BossData. waveIndex = wave/10-1 로 매핑. 없으면 null.</summary>
    private BossData FindBossData(int chapter, int wave)
    {
        int expectedWaveIndex = wave / 10 - 1;
        foreach (var kv in _bossCache)
        {
            if (kv.Value.chapter == chapter && kv.Value.waveIndex == expectedWaveIndex)
                return kv.Value;
        }
        return null;
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
        var data = PickUnitData();
        if (data?.prefab == null) return;
        DoSpawn(data.prefab, GetRandomEdgePosition(), data);
    }


    /// <summary>DB_웨이브 슬롯에서 비율 가중치 랜덤으로 UnitData 선택.</summary>
    private UnitData PickUnitData()
    {
        var row = GetActiveRow(currentChapter, currentStage, currentWave);
        if (row == null) return null;

        var slots = new List<(string unitId, float ratio)>
        {
            (row.f_유닛1, row.f_비율1),
            (row.f_유닛2, row.f_비율2),
            (row.f_유닛3, row.f_비율3),
            (row.f_유닛4, row.f_비율4),
        };

        float total = 0f;
        foreach (var (unitId, ratio) in slots)
            if (!string.IsNullOrEmpty(unitId) && ratio > 0f) total += ratio;

        if (total <= 0f) return null;

        float roll = Random.value * total;
        float acc  = 0f;
        foreach (var (unitId, ratio) in slots)
        {
            if (string.IsNullOrEmpty(unitId) || ratio <= 0f) continue;
            acc += ratio;
            if (roll <= acc)
            {
                _unitCache.TryGetValue(unitId, out var d);
                return d;
            }
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

        var bossData = FindBossData(currentChapter, currentWave);
        if (bossData == null || bossData.prefab == null)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {currentWave}: 보스 프리팹을 찾을 수 없습니다.");
            return;
        }

        Vector2 spawnPos = bossSpawnPoint != null
            ? (Vector2)bossSpawnPoint.position
            : GetRandomEdgePosition();

        var obj = Instantiate(bossData.prefab, spawnPos, Quaternion.identity);
        if (obj.TryGetComponent<BossController>(out var boss))
        {
            boss.ApplyBossData(bossData);
        }

        Debug.Log($"[EnemySpawner] 보스 스폰! Wave {currentWave} (ID={bossData.bossId})");
    }

    private void DoSpawn(GameObject prefab, Vector2 pos, UnitData data = null)
    {
        Debug.Log($"[EnemySpawner] 스폰 시도: prefab={prefab?.name}, pos={pos}, data={data?.unitId}");

        int globalWave       = CalcGlobalWave(currentStage, currentWave);
        int designGlobalWave = data != null ? data.designGlobalWave : globalWave;

        if (spawnMarkerPrefab != null)
        {
            var markerObj = Instantiate(spawnMarkerPrefab, pos, Quaternion.identity);
            if (markerObj.TryGetComponent<SpawnMarker>(out var marker))
                marker.Init(prefab, globalWave, designGlobalWave, data, wavesPerStage);
        }
        else
        {
            var enemy = EnemyPool.Shared != null
                ? EnemyPool.Shared.Get(prefab, pos)
                : Instantiate(prefab, pos, Quaternion.identity).GetComponent<EnemyController>();

            if (enemy != null)
            {
                if (data != null) enemy.ApplyUnitData(data);      // 먼저 — InitForWave가 ActiveUnitData 필요
                enemy.InitForWave(globalWave, designGlobalWave, wavesPerStage);
                enemy.PlaySpawnBounce();
            }
            else
            {
                Debug.LogWarning($"[EnemySpawner] EnemyController 컴포넌트를 찾을 수 없음: prefab={prefab?.name}");
            }
        }
    }

    // ── 웨이브 규칙 적용 ────────────────────────────────────────────
    private void ApplyRule(int wave)
    {
        isBossWave = (wave % 10 == 0);

        var row = GetActiveRow(currentChapter, currentStage, wave);
        if (row == null)
        {
            Debug.LogWarning($"[EnemySpawner] C{currentChapter}_S{currentStage}_W{wave} 해당하는 DB_웨이브 행 없음");
            return;
        }

        currentInterval   = row.f_스폰간격 > 0f ? row.f_스폰간격 : 3f;
        currentSpawnCount = row.f_동시스폰  > 0  ? row.f_동시스폰  : 3;
        currentMaxOnMap   = row.f_최대스폰;

        Debug.Log($"[EnemySpawner] Wave {wave} → 보스: {isBossWave}, 간격: {currentInterval:F1}s, 동시: {currentSpawnCount}, 최대: {currentMaxOnMap}");
    }

    // ── 외부 호출 API ────────────────────────────────────────────────

    /// <summary>GameManager.Start()에서 선택된 챕터ID("C1_S2" 형식)로 초기화.</summary>
    public void SetChapterStage(string chapterID)
    {
        var row = DB_챕터.FindEntity(r => r.f_챕터ID == chapterID);
        if (row == null)
        {
            Debug.LogWarning($"[EnemySpawner] DB_챕터에서 '{chapterID}' 를 찾을 수 없습니다.");
            return;
        }

        ParseChapterID(row.f_챕터ID, out int chapter, out int stage);
        currentChapter = chapter;
        currentStage   = stage;
        wavesPerStage  = row.f_웨이브 > 0 ? row.f_웨이브 : 30;

        currentWave = 1;
        StopAllCoroutines();
        ApplyRule(currentWave);
        if (isBossWave) SpawnBoss();
        else if (spawning) StartCoroutine(SpawnLoop());
    }

    private static void ParseChapterID(string chapterID, out int chapter, out int stage)
    {
        chapter = 1; stage = 1;
        var parts = chapterID.Split('_');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0].Substring(1), out int c) &&
            int.TryParse(parts[1].Substring(1), out int s))
        {
            chapter = c;
            stage   = s;
        }
    }

    public void OnWaveUp(int wave)
    {
        Debug.Log($"[EnemySpawner] OnWaveUp: wave={wave}, isBossWave={isBossWave}, spawning={spawning}");
        currentWave = wave;
        ApplyRule(wave);
        StopAllCoroutines();

        if (isBossWave) SpawnBoss();
        else if (spawning) StartCoroutine(SpawnLoop());
    }

    public void OnChapterUp(int chapter)
    {
        currentChapter = chapter;
        currentStage   = 1;
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
        // EnemyPool은 SetActive를 사용하지 않으므로 FindObjectsByType으로 접근하면
        // 풀 대기 중인 적(-9999 위치)까지 Destroy되어 풀 큐가 깨짐 — All만 사용할 것
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            var enemy = EnemyController.All[i];
            if (enemy == null) continue;
            enemy.StopAllCoroutines();   // DieRoutine 즉시 중단
            enemy.gameObject.SetActive(false); // 시각적으로 즉시 숨김
            Destroy(enemy.gameObject);
        }
        // All.Clear()는 호출하지 않음 — OnDestroy에서 AllRemoveSwapBack이 자연스럽게 처리

        foreach (var marker in Object.FindObjectsByType<SpawnMarker>(FindObjectsSortMode.None))
            Destroy(marker.gameObject);

        // 안전망: SpawnMarker가 이미 Instantiate했지만 Start()가 안 돌아 All에 미등록된 적 처리
        // SetActive(false)인 풀 대기 적은 건드리지 않음
        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.gameObject.activeSelf)
            {
                enemy.StopAllCoroutines();
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
        }
    }

    public void ResumeSpawning(int wave)
    {
        spawning = true;
        OnWaveUp(wave);
    }

    /// <summary>fromWave보다 큰 가장 가까운 보스 웨이브 번호. DB에 해당 웨이브 행이 없으면 -1 반환.</summary>
    public int GetNextBossWave(int fromWave)
    {
        int next = ((fromWave / 10) + 1) * 10;
        return GetActiveRow(currentChapter, currentStage, next) != null ? next : -1;
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
