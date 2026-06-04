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

    [Header("엘리트 스폰 마커 프리팹 (null이면 일반 마커 사용)")]
    [SerializeField] private GameObject eliteSpawnMarkerPrefab;

    [Header("보스 스폰 위치 (null이면 랜덤 엣지)")]
    [SerializeField] private Transform bossSpawnPoint;

    [Header("보스 스폰 마커 프리팹 (null이면 즉시 스폰)")]
    [SerializeField] private GameObject bossSpawnMarkerPrefab;

    [Header("글로벌 상한 (0 = DB f_최대스폰 우선)")]
    [SerializeField] private int maxEnemiesOnMap = 30;

    [Header("플레이어로부터 최소 스폰 거리")]
    [SerializeField] private float minDistanceFromPlayer = 9f;

    [Header("분산 스폰")]
    [SerializeField] [Range(4, 12)] private int sectorCount = 8; // 360도를 몇 섹터로 나눌지

    [Header("그룹 스폰")]
    [SerializeField] [Range(0f, 1f)] private float groupSpawnChance  = 0.5f;
    [SerializeField] private float groupSpawnMinDist  = 5f;
    [SerializeField] private float groupSpawnMaxDist  = 8f;
    [SerializeField] private float groupSpawnRadius   = 1.2f;

    private int[] _shuffledSectors;
    private int   _sectorCursor;

    private BossSpawnMarker _pendingBossMarker;

    private float currentInterval  = 0.5f;
    private int   currentMaxOnMap;          // DB f_최대스폰 (0 = 무제한)
    private int   currentWave    = 1;
    private int   currentChapter = 1;
    private int   currentStage   = 1;
    private int   wavesPerStage  = 30;      // DB_챕터.f_웨이브에서 로드
    private bool  spawning       = false;   // 초기에는 스폰하지 않고 대기
    private bool  isBossWave     = false;

    private int CalcGlobalWave(int stage, int wave) => (stage - 1) * wavesPerStage + wave;

    protected override bool DontDestroy => false;

    public Vector2 AreaCenter => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsCenter : Vector2.zero;
    public Vector2 AreaSize   => SpawnZone.Shared != null ? SpawnZone.Shared.BoundsSize   : new Vector2(10f, 18f);

    private readonly System.Collections.Generic.Dictionary<string, UnitData> _unitCache = new();
    private readonly System.Collections.Generic.Dictionary<string, BossData> _bossCache = new();

    // PickUnitData 재사용 버퍼 — 매 스폰마다 List 할당 방지
    private readonly (string unitId, float ratio)[] _unitSlots = new (string, float)[4];

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

    private void Start() { }

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

    /// <summary>챕터+스테이지+웨이브에 해당하는 BossData. waveIndex = wave/10-1 로 매핑. 없으면 null.</summary>
    private BossData FindBossData(int chapter, int stage, int wave)
    {
        int expectedWaveIndex = wave / 10 - 1;
        foreach (var kv in _bossCache)
        {
            if (kv.Value.chapter == chapter && kv.Value.stage == stage && kv.Value.waveIndex == expectedWaveIndex)
                return kv.Value;
        }
        return null;
    }

    // ── 스폰 루프 ───────────────────────────────────────────────────
    private IEnumerator SpawnLoop()
    {
        // 웨이브 시작: 마커 없이 즉시 최대까지 채우기
        FillToCapDirect();

        while (spawning)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

            int cap   = currentMaxOnMap > 0 ? Mathf.Min(currentMaxOnMap, maxEnemiesOnMap) : maxEnemiesOnMap;
            int alive = CountActiveEnemies();

            if (alive < cap)
                SpawnEnemy(); // 마커 포함 → 카운트에 즉시 반영되어 과스폰 방지

            yield return new WaitForSeconds(currentInterval);
        }
    }

    /// <summary>웨이브 시작 시 마커 없이 즉시 최대 수까지 적을 채웁니다.</summary>
    private void FillToCapDirect()
    {
        int cap   = currentMaxOnMap > 0 ? Mathf.Min(currentMaxOnMap, maxEnemiesOnMap) : maxEnemiesOnMap;
        int count = EnemyController.ActiveCount;
        while (count < cap)
        {
            SpawnEnemyDirect();
            count++;
        }
    }

    private void SpawnEnemyDirect()
    {
        var data = PickUnitData();
        if (data?.prefab == null) return;
        DoSpawnDirect(data.prefab, GetRandomEdgePosition(), data);
    }

    private void DoSpawnDirect(GameObject prefab, Vector2 pos, UnitData data = null)
    {
        int globalWave       = CalcGlobalWave(currentStage, currentWave);
        int designGlobalWave = data != null ? data.designGlobalWave : globalWave;

        var go    = Instantiate(prefab, pos, Quaternion.identity);
        var enemy = go.GetComponent<EnemyController>();
        if (enemy != null)
        {
            if (data != null) enemy.ApplyUnitData(data);
            enemy.InitForWave(globalWave, designGlobalWave, wavesPerStage);
            enemy.PlaySpawnBounce();
        }
        else
            Debug.LogWarning($"[EnemySpawner] EnemyController 없음: prefab={prefab?.name}");
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


    private void SpawnEnemyGroup(int slotCount)
    {
        Vector2 center    = GetGroupCenter();
        int     groupSize = Mathf.Min(slotCount, Random.Range(3, 5)); // 3~4마리

        for (int i = 0; i < groupSize; i++)
        {
            var data = PickUnitData();
            if (data?.prefab == null) continue;
            Vector2 offset = Random.insideUnitCircle * groupSpawnRadius;
            DoSpawn(data.prefab, center + offset, data);
        }

        // 남은 슬롯은 개별 스폰
        for (int i = groupSize; i < slotCount; i++)
            SpawnEnemy();
    }

    private Vector2 GetGroupCenter()
    {
        Vector2 playerPos = PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position
            : Vector2.zero;

        Bounds b = SpawnZone.Shared != null
            ? new Bounds(SpawnZone.Shared.BoundsCenter, SpawnZone.Shared.BoundsSize * 0.85f)
            : new Bounds(Vector3.zero, new Vector3(10f, 18f, 0f));

        for (int i = 0; i < 25; i++)
        {
            float   angle = Random.Range(0f, Mathf.PI * 2f);
            float   dist  = Random.Range(groupSpawnMinDist, groupSpawnMaxDist);
            Vector2 pos   = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            if (pos.x >= b.min.x && pos.x <= b.max.x && pos.y >= b.min.y && pos.y <= b.max.y)
                return pos;
        }

        float fb = Random.Range(0f, Mathf.PI * 2f);
        return playerPos + new Vector2(Mathf.Cos(fb), Mathf.Sin(fb)) * groupSpawnMinDist;
    }

    /// <summary>DB_웨이브 슬롯에서 비율 가중치 랜덤으로 UnitData 선택.</summary>
    private UnitData PickUnitData()
    {
        var row = GetActiveRow(currentChapter, currentStage, currentWave);
        if (row == null) return null;

        _unitSlots[0] = (row.f_유닛1, row.f_비율1);
        _unitSlots[1] = (row.f_유닛2, row.f_비율2);
        _unitSlots[2] = (row.f_유닛3, row.f_비율3);
        _unitSlots[3] = (row.f_유닛4, row.f_비율4);

        float total = 0f;
        for (int i = 0; i < 4; i++)
            if (!string.IsNullOrEmpty(_unitSlots[i].unitId) && _unitSlots[i].ratio > 0f)
                total += _unitSlots[i].ratio;

        if (total <= 0f) return null;

        float roll = Random.value * total;
        float acc  = 0f;
        for (int i = 0; i < 4; i++)
        {
            if (string.IsNullOrEmpty(_unitSlots[i].unitId) || _unitSlots[i].ratio <= 0f) continue;
            acc += _unitSlots[i].ratio;
            if (roll <= acc)
            {
                _unitCache.TryGetValue(_unitSlots[i].unitId, out var d);
                return d;
            }
        }
        return null;
    }

    // ── 보스 스폰 ───────────────────────────────────────────────────
    private void SpawnBoss()
    {
        if (BossController.Current != null)
        {
            Debug.Log("[EnemySpawner] 보스가 이미 존재합니다. 중복 스폰 방지.");
            return;
        }

        if (_pendingBossMarker != null)
        {
            Debug.Log("[EnemySpawner] 보스 마커가 이미 대기 중입니다. 중복 스폰 방지.");
            return;
        }

        var bossData = FindBossData(currentChapter, currentStage, currentWave);
        if (bossData == null || bossData.prefab == null)
        {
            Debug.LogWarning($"[EnemySpawner] Wave {currentWave}: 보스 프리팹을 찾을 수 없습니다.");
            return;
        }

        // 보스 배경 프리팹의 bossSpawnPoint 우선, 그 다음 Inspector 설정, 마지막으로 랜덤 엣지
        Transform envSpawnPoint = WaveEnvironmentSwitcher.Shared?.CurrentBossSpawnPoint;
        Vector2 spawnPos = envSpawnPoint != null  ? (Vector2)envSpawnPoint.position
                         : bossSpawnPoint != null ? (Vector2)bossSpawnPoint.position
                         : GetRandomEdgePosition();

        if (bossSpawnMarkerPrefab != null)
        {
            var markerObj = Instantiate(bossSpawnMarkerPrefab, spawnPos, Quaternion.identity);
            if (markerObj.TryGetComponent<BossSpawnMarker>(out var marker))
            {
                _pendingBossMarker = marker;
                marker.Init(bossData.prefab, bossData);
                marker.OnSpawnComplete += () => _pendingBossMarker = null;
            }
            else
                Debug.LogWarning("[EnemySpawner] bossSpawnMarkerPrefab에 BossSpawnMarker 컴포넌트가 없습니다.");
        }
        else
        {
            var obj = Instantiate(bossData.prefab, spawnPos, Quaternion.identity);
            if (obj.TryGetComponent<BossController>(out var boss))
                boss.ApplyBossData(bossData);
        }

        Debug.Log($"[EnemySpawner] 보스 스폰! Wave {currentWave} (ID={bossData.bossId})");
    }

    private void DoSpawn(GameObject prefab, Vector2 pos, UnitData data = null)
    {
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
            var go    = Instantiate(prefab, pos, Quaternion.identity);
            var enemy = go.GetComponent<EnemyController>();

            if (enemy != null)
            {
                if (data != null) enemy.ApplyUnitData(data);
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

        currentInterval = row.f_스폰간격 > 0f ? row.f_스폰간격 : 0.5f;
        currentMaxOnMap = row.f_최대스폰;

        Debug.Log($"[EnemySpawner] Wave {wave} → 보스: {isBossWave}, 리필간격: {currentInterval:F1}s, 최대: {currentMaxOnMap}");
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

    public bool IsSpawning => spawning;
    public bool IsOverCap()
    {
        int cap = currentMaxOnMap > 0 ? Mathf.Min(currentMaxOnMap, maxEnemiesOnMap) : maxEnemiesOnMap;
        return EnemyController.ActiveCount >= cap;
    }


    public void StopSpawning()
    {
        spawning = false;
        StopAllCoroutines();
        SpawnMarker.CancelAll(); // 대기 중인 마커도 즉시 취소
        _pendingBossMarker = null;
    }

    public void ClearAllEnemies()
    {
        // SpawnMarker 먼저 정리 — FadeAndSpawn 코루틴이 추가 스폰하지 못하도록
        SpawnMarker.CancelAll();

        // 스냅샷 순회 — Destroy 중 All이 수정되므로 복사본 사용
        var snapshot = new System.Collections.Generic.List<EnemyController>(EnemyController.All);
        foreach (var enemy in snapshot)
        {
            if (enemy == null) continue;
            Destroy(enemy.gameObject);
        }
        EnemyController.All.Clear();

        // 안전망: All에 등록되지 못한 적(엘리트 포함) 씬 전체 스캔 후 제거
        // 웨이브 클리어 시 1회만 호출되므로 성능 문제 없음
        var remaining = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in remaining)
        {
            if (enemy == null) continue;
            Destroy(enemy.gameObject);
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
    /// <summary>
    /// 엘리트 몬스터를 SpawnMarker 경고 연출과 함께 스폰합니다.
    /// GameManager.SpawnElite()에서 호출됩니다.
    /// </summary>
    public void SpawnEliteAt(GameObject prefab)
    {
        if (prefab == null) return;
        StartCoroutine(EliteSpawnRoutine(prefab));
    }

    private IEnumerator EliteSpawnRoutine(GameObject prefab)
    {
        Vector2 pos        = GetRandomEdgePosition();
        int     globalWave = CalcGlobalWave(currentStage, currentWave);

        // 마커는 비주얼 전용 — SpawnMarker 로직(IsSpawning/IsOverCap 체크) 완전 우회
        var markerPrefab = eliteSpawnMarkerPrefab != null ? eliteSpawnMarkerPrefab : spawnMarkerPrefab;
        if (markerPrefab != null)
        {
            var markerObj = Instantiate(markerPrefab, pos, Quaternion.identity);
            Destroy(markerObj, 1.5f);
            yield return new WaitForSeconds(1.5f);
        }

        if (GameManager.Shared == null || GameManager.Shared.IsGameOver)
            yield break;

        var go    = Instantiate(prefab, pos, Quaternion.identity);
        var enemy = go.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.InitForWave(globalWave, globalWave, wavesPerStage);
            enemy.PlaySpawnBounce();
        }
    }

    private Vector2 GetRandomEdgePosition()
    {
        Vector2 playerPos = PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position
            : Vector2.zero;

        // 섹터 순환: 한 사이클이 끝나면 재셔플 → 8방향 균등 분산
        if (_shuffledSectors == null || _shuffledSectors.Length != sectorCount)
            ReshuffleSectors();
        if (_sectorCursor >= _shuffledSectors.Length)
            ReshuffleSectors();

        int   sector     = _shuffledSectors[_sectorCursor++];
        float sectorDeg  = 360f / sectorCount;
        float sectorMin  = sector * sectorDeg;

        // 해당 섹터 + 거리 조건 만족 위치 탐색
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 pos = SampleEdge();
            if (Vector2.Distance(pos, playerPos) < minDistanceFromPlayer) continue;

            float angle = Mathf.Repeat(
                Mathf.Atan2(pos.y - playerPos.y, pos.x - playerPos.x) * Mathf.Rad2Deg,
                360f);
            if (Mathf.Repeat(angle - sectorMin, 360f) <= sectorDeg)
                return pos;
        }

        // fallback: 가장 먼 위치 반환 (거리 조건 미충족 시에도 최선)
        Vector2 best     = SampleEdge();
        float   bestDist = Vector2.Distance(best, playerPos);
        for (int attempt = 1; attempt < 15; attempt++)
        {
            Vector2 pos  = SampleEdge();
            float   dist = Vector2.Distance(pos, playerPos);
            if (dist >= minDistanceFromPlayer) return pos;
            if (dist > bestDist) { best = pos; bestDist = dist; }
        }
        return best;
    }

    private void ReshuffleSectors()
    {
        if (_shuffledSectors == null || _shuffledSectors.Length != sectorCount)
            _shuffledSectors = new int[sectorCount];

        for (int i = 0; i < sectorCount; i++) _shuffledSectors[i] = i;
        for (int i = sectorCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffledSectors[i], _shuffledSectors[j]) = (_shuffledSectors[j], _shuffledSectors[i]);
        }
        _sectorCursor = 0;
    }

    private Vector2 SampleEdge()
    {
        if (SpawnZone.Shared != null) return SpawnZone.Shared.GetRandomEdgePosition();
        int side = Random.Range(0, 4);
        return side switch
        {
            0 => new Vector2(Random.Range(-5f,  5f),  9.5f),
            1 => new Vector2(Random.Range(-5f,  5f), -9.5f),
            2 => new Vector2(-5.5f, Random.Range(-9f, 9f)),
            _ => new Vector2( 5.5f, Random.Range(-9f, 9f)),
        };
    }

    // ── 포메이션 스폰 ────────────────────────────────────────────

    /// <summary>
    /// 지정한 형태로 적을 포메이션 스폰합니다.
    /// GameManager 등 외부에서 StartCoroutine으로 호출하세요.
    /// </summary>
    /// <param name="shape">X형·사각형·H형·십자형·직선·원형</param>
    /// <param name="spacing">적 간 간격 (유닛)</param>
    /// <param name="countPerArm">팔/열/변당 적 수</param>
    /// <param name="interval">각 적 스폰 사이 딜레이 (초). 0이면 동시 스폰.</param>
    /// <summary>StageEnvironmentSO.FormationEntry에서 직접 호출합니다.</summary>
    public void TriggerFormation(StageEnvironmentSO.FormationEntry entry)
        => StartCoroutine(SpawnFormation(entry.shape, entry.spacing, entry.countPerArm, entry.interval, entry.startDelay));

    public IEnumerator SpawnFormation(
        FormationLayout.Shape shape,
        float spacing     = 2f,
        int   countPerArm = 3,
        float interval    = 0.08f,
        float startDelay  = 0f)
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        Vector2 center    = GetFormationCenter();
        var     positions = FormationLayout.GetPositions(shape, center, spacing, countPerArm);

        foreach (var pos in positions)
        {
            int cap = currentMaxOnMap > 0 ? Mathf.Min(currentMaxOnMap, maxEnemiesOnMap) : maxEnemiesOnMap;
            if (CountActiveEnemies() >= cap) break;

            var data = PickUnitData();
            if (data?.prefab != null)
                DoSpawn(data.prefab, pos, data);

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>플레이어에서 minDist 이상 떨어진 랜덤 내부 위치를 반환합니다.</summary>
    private Vector2 GetFormationCenter(float minDist = 3.5f)
    {
        Vector2 playerPos = PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position
            : Vector2.zero;

        Bounds b = SpawnZone.Shared != null
            ? new Bounds(SpawnZone.Shared.BoundsCenter, SpawnZone.Shared.BoundsSize * 0.65f)
            : new Bounds(Vector3.zero, new Vector3(8f, 14f, 0f));

        for (int i = 0; i < 20; i++)
        {
            var pos = new Vector2(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y));
            if (Vector2.Distance(pos, playerPos) >= minDist)
                return pos;
        }

        // fallback: 플레이어 기준 랜덤 방향 오프셋
        return playerPos + Random.insideUnitCircle.normalized * minDist;
    }
}
