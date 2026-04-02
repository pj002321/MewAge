using System.Collections;
using UnityEngine;

public class SpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 1.5f;

    private GameObject spawnPrefab;
    private int        spawnWave;
    private UnitData   _unitData;

    private static int _activeCount;
    public  static int ActiveCount => _activeCount;

    private void OnDestroy() => _activeCount--;

    public void Init(GameObject prefab, int wave = 0, UnitData unitData = null)
    {
        _activeCount++;
        spawnPrefab = prefab;
        spawnWave   = wave;
        _unitData   = unitData;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        SetAlpha(1f);
        StartCoroutine(FadeAndSpawn());
    }

    private IEnumerator FadeAndSpawn()
    {
        float elapsed = 0f;

        while (elapsed < warningDuration)
        {
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

        if (spawnPrefab != null)
        {
            EnemyController enemy;
            if (EnemyPool.Shared != null)
            {
                enemy = EnemyPool.Shared.Get(spawnPrefab, transform.position);
            }
            else
            {
                var obj = Instantiate(spawnPrefab, transform.position, Quaternion.identity);
                enemy   = obj.GetComponent<EnemyController>();
            }

            if (enemy != null)
            {
                enemy.InitForWave(spawnWave);
                if (_unitData != null) enemy.ApplyUnitData(_unitData);
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
