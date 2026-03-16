using System.Collections;
using UnityEngine;
using Mewage.DB;

public class SpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 1.5f;

    private DB_유닛 unit;
    private int wave;

    public void Init(DB_유닛 unit, int currentWave)
    {
        this.unit = unit;
        this.wave = currentWave;

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

        if (unit != null && unit.f_프리팹 != null)
        {
            GameObject obj = Instantiate(unit.f_프리팹, transform.position, Quaternion.identity);
            if (obj.TryGetComponent<EnemyController>(out var enemy))
                enemy.InitFromDB(unit, wave);
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
