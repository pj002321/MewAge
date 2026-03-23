using System.Collections;
using UnityEngine;

public class SpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 1.5f;

    private GameObject spawnPrefab;
    private int        spawnWave;

    public void Init(GameObject prefab, int wave = 0)
    {
        spawnPrefab = prefab;
        spawnWave   = wave;

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
            var obj = Instantiate(spawnPrefab, transform.position, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
            {
                enemy.InitForWave(spawnWave);
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
