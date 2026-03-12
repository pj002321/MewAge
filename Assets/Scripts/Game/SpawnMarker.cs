using System.Collections;
using UnityEngine;

public class SpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 1.5f;

    private GameObject enemyPrefab;
    private int wave;

    public void Init(GameObject prefab, int currentWave)
    {
        enemyPrefab = prefab;
        wave = currentWave;

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

        if (enemyPrefab != null)
        {
            GameObject obj = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
            {
                enemy.ApplyWaveScaling(wave);
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
