using System.Collections;
using UnityEngine;

public class SpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 1.5f;

    private GameObject spawnPrefab;
    private int        _globalWave;
    private int        _designGlobalWave;
    private int        _wavesPerStage;
    private UnitData   _unitData;

    private static int _activeCount;
    public  static int ActiveCount => _activeCount;

    public static readonly System.Collections.Generic.List<SpawnMarker> All = new();

    private void OnDestroy()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        All.Remove(this);
    }

    /// <summary>씬에 있는 모든 SpawnMarker를 즉시 취소합니다. StopSpawning 시 호출.</summary>
    public static void CancelAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i] != null)
            {
                All[i].StopAllCoroutines(); // Destroy는 지연 처리 — 코루틴 먼저 즉시 중단
                Destroy(All[i].gameObject);
            }
        }
        All.Clear();
        _activeCount = 0;
    }

    public void Init(GameObject prefab, int globalWave = 0, int designGlobalWave = 0, UnitData unitData = null, int wavesPerStage = 30)
    {
        _activeCount++;
        All.Add(this);
        spawnPrefab       = prefab;
        _globalWave       = globalWave;
        _designGlobalWave = designGlobalWave;
        _wavesPerStage    = wavesPerStage;
        _unitData         = unitData;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        SetAlpha(1f);
        StartCoroutine(FadeAndSpawn());
    }

    private IEnumerator FadeAndSpawn()
    {
        float elapsed = 0f;

        while (elapsed < warningDuration)
        {
            if (EnemySpawner.Shared != null && !EnemySpawner.Shared.IsSpawning)
            {
                Destroy(gameObject);
                yield break;
            }
            elapsed += Time.deltaTime;
            SetAlpha(1f - elapsed / warningDuration);
            yield return null;
        }

        SetAlpha(0f);
        gameObject.SetActive(false);

        if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
        {
            Destroy(gameObject);
            yield break;
        }

        // 웨이브 클리어로 스폰이 중단된 경우 스폰하지 않음
        if (EnemySpawner.Shared != null && !EnemySpawner.Shared.IsSpawning)
        {
            Destroy(gameObject);
            yield break;
        }

        if (spawnPrefab != null)
        {
            var go    = Instantiate(spawnPrefab, transform.position, Quaternion.identity);
            var enemy = go.GetComponent<EnemyController>();

            if (enemy != null)
            {
                if (_unitData != null) enemy.ApplyUnitData(_unitData);
                enemy.InitForWave(_globalWave, _designGlobalWave, _wavesPerStage);
                enemy.PlaySpawnBounce();
            }
        }

        Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
