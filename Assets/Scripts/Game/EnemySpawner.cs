using System.Collections;
using UnityEngine;

/// <summary>
/// 스폰 영역 가장자리에서 적을 스폰합니다.
/// Scene 뷰에서 Gizmo로 스폰 영역을 시각적으로 확인하고 조정할 수 있습니다.
/// </summary>
public class EnemySpawner : MonoBehaviourSingleton<EnemySpawner>
{
    [Header("Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject spawnMarkerPrefab;  // SpawnMarker 프리팹
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int spawnCountPerWave = 1;

    [Header("Spawn 영역 (Gizmo로 표시됨)")]
    [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 18f);
    [Tooltip("스폰 영역 외곽에서 얼마나 더 바깥쪽에 스폰할지")]
    [SerializeField] private float spawnEdgeOffset = 0.3f;

    [Header("Wave Scaling")]
    [SerializeField] private float intervalDecreasePerWave = 0.2f;
    [SerializeField] private float minSpawnInterval = 0.5f;

    private float currentInterval;
    private int currentWave = 1;
    private bool spawning = true;

    protected override bool DontDestroy => false;

    private void Start()
    {
        currentInterval = spawnInterval;
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (spawning)
        {
            yield return new WaitForSeconds(currentInterval);

            if (!spawning) yield break;
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

            for (int i = 0; i < spawnCountPerWave; i++)
                SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null) return;

        Vector2 spawnPos = GetRandomEdgePosition();

        // 마커 프리팹이 있으면 마커 → 스폰, 없으면 즉시 스폰
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

    /// <summary>
    /// 스폰 영역의 4면 가장자리 중 랜덤 위치를 반환합니다.
    /// </summary>
    private Vector2 GetRandomEdgePosition()
    {
        float halfW = spawnAreaSize.x * 0.5f + spawnEdgeOffset;
        float halfH = spawnAreaSize.y * 0.5f + spawnEdgeOffset;
        Vector2 center = spawnAreaCenter;

        int side = Random.Range(0, 4);
        return side switch
        {
            0 => new Vector2(center.x + Random.Range(-halfW, halfW), center.y + halfH),  // 위
            1 => new Vector2(center.x + Random.Range(-halfW, halfW), center.y - halfH),  // 아래
            2 => new Vector2(center.x - halfW, center.y + Random.Range(-halfH, halfH)),  // 왼쪽
            _ => new Vector2(center.x + halfW, center.y + Random.Range(-halfH, halfH)),  // 오른쪽
        };
    }

    public void OnWaveUp(int wave)
    {
        currentWave = wave;
        currentInterval = Mathf.Max(minSpawnInterval, spawnInterval - intervalDecreasePerWave * (wave - 1));
        spawnCountPerWave = 1 + (wave - 1) / 3;
        Debug.Log($"[EnemySpawner] Wave {wave} - 간격: {currentInterval:F1}s, 동시 스폰: {spawnCountPerWave}");
    }

    public void StopSpawning()
    {
        spawning = false;
        StopAllCoroutines();
    }

    // ──────────────────────────────────────────
    // Gizmo: Scene 뷰에서 스폰 영역 시각화
    // ──────────────────────────────────────────
    private void OnDrawGizmos()
    {
        // 외곽 스폰 라인 (실제 적이 나오는 위치)
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Vector2 outerSize = spawnAreaSize + Vector2.one * spawnEdgeOffset * 2f;
        Gizmos.DrawWireCube(spawnAreaCenter, outerSize);

        // 내부 플레이 영역 (이 안에서 싸운다)
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);

        // 네 모서리 스폰 포인트 예시 표시
        Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
        float hw = spawnAreaSize.x * 0.5f + spawnEdgeOffset;
        float hh = spawnAreaSize.y * 0.5f + spawnEdgeOffset;
        float dotSize = 0.15f;

        // 위/아래 중앙
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0, hh), dotSize);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(0, -hh), dotSize);
        // 좌/우 중앙
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(-hw, 0), dotSize);
        Gizmos.DrawSphere(spawnAreaCenter + new Vector2(hw, 0), dotSize);

        // 라벨
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            spawnAreaCenter + new Vector2(-spawnAreaSize.x * 0.5f, spawnAreaSize.y * 0.5f + spawnEdgeOffset + 0.2f),
            "[ Spawn Area ]");
#endif
    }
}
