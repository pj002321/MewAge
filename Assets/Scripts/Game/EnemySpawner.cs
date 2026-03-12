using System.Collections;
using UnityEngine;

/// <summary>
/// 스폰 영역 가장자리에서 적을 스폰합니다.
/// WaveRule 배열로 웨이브별 스폰 규칙을 Inspector에서 자유롭게 설정하세요.
/// </summary>
public class EnemySpawner : MonoBehaviourSingleton<EnemySpawner>
{
    // ── 웨이브별 스폰 규칙 ─────────────────────────────────────
    [System.Serializable]
    public class WaveRule
    {
        [Tooltip("이 웨이브 번호부터 적용")]
        public int   fromWave     = 1;
        [Tooltip("스폰 간격 (초)")]
        public float spawnInterval = 3f;
        [Tooltip("한 번에 스폰되는 적 수")]
        public int   spawnCount   = 1;
    }

    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject spawnMarkerPrefab;

    [Header("웨이브 스폰 규칙 (fromWave 오름차순으로 작성)")]
    [SerializeField] private WaveRule[] waveRules = new WaveRule[]
    {
        new() { fromWave = 1, spawnInterval = 3.0f, spawnCount = 1 },
        new() { fromWave = 3, spawnInterval = 2.2f, spawnCount = 2 },
        new() { fromWave = 5, spawnInterval = 1.8f, spawnCount = 3 },
        new() { fromWave = 8, spawnInterval = 1.3f, spawnCount = 4 },
        new() { fromWave = 12, spawnInterval = 0.8f, spawnCount = 5 },
    };

    [Header("스폰 영역 (Gizmo로 표시됨)")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize   = new(10f, 18f);
    [Tooltip("스폰 영역 외곽에서 얼마나 더 바깥쪽에 스폰할지")]
    [SerializeField] private float spawnEdgeOffset = 0.3f;

    private float currentInterval;
    private int   currentSpawnCount;
    private int   currentWave = 1;
    private bool  spawning    = true;

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
        if (enemyPrefab == null) return;

        Vector2 spawnPos = GetRandomEdgePosition();

        if (spawnMarkerPrefab != null)
        {
            GameObject markerObj = Instantiate(spawnMarkerPrefab, spawnPos, Quaternion.identity);
            if (markerObj.TryGetComponent<SpawnMarker>(out var marker))
                marker.Init(enemyPrefab, currentWave);
        }
        else
        {
            GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
            {
                enemy.ApplyWaveScaling(currentWave);
                enemy.PlaySpawnBounce();
            }
        }
    }

    // ── Wave 규칙 적용 ─────────────────────────────────────────
    private void ApplyRule(int wave)
    {
        WaveRule matched = waveRules[0];
        foreach (var rule in waveRules)
        {
            if (rule.fromWave <= wave) matched = rule;
        }
        currentInterval   = matched.spawnInterval;
        currentSpawnCount = matched.spawnCount;
        Debug.Log($"[EnemySpawner] Wave {wave} → 간격: {currentInterval:F1}s, 동시 스폰: {currentSpawnCount}");
    }

    public void OnWaveUp(int wave)
    {
        currentWave = wave;
        ApplyRule(wave);
        // 새 웨이브 시작 시 코루틴 재시작 (새 간격 즉시 적용)
        StopAllCoroutines();
        if (spawning) StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        spawning = false;
        StopAllCoroutines();
    }

    // ── 스폰 위치 ──────────────────────────────────────────────
    private Vector2 GetRandomEdgePosition()
    {
        float halfW  = spawnAreaSize.x * 0.5f + spawnEdgeOffset;
        float halfH  = spawnAreaSize.y * 0.5f + spawnEdgeOffset;
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

    // ── Gizmo ──────────────────────────────────────────────────
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
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0,   hh),  0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0,  -hh),  0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(-hw, 0),   0.15f);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2( hw, 0),   0.15f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            spawnAreaCenter + new Vector2(-spawnAreaSize.x * 0.5f, spawnAreaSize.y * 0.5f + spawnEdgeOffset + 0.2f),
            "[ Spawn Area ]");
#endif
    }
}
