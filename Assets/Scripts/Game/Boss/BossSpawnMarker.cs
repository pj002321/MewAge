using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스폰 전 경고 연출 마커.
/// Init() 호출 → warningDuration 동안 페이드 아웃 → 보스 프리팹 스폰.
/// EnemySpawner.SpawnBoss()에서 bossSpawnMarkerPrefab으로 사용.
/// </summary>
public class BossSpawnMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float warningDuration = 2f;

    private GameObject _bossPrefab;
    private BossData   _bossData;

    public void Init(GameObject bossPrefab, BossData bossData)
    {
        _bossPrefab = bossPrefab;
        _bossData   = bossData;

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

        if (_bossPrefab != null)
        {
            var obj = Instantiate(_bossPrefab, transform.position, Quaternion.identity);
            if (obj.TryGetComponent<BossController>(out var boss) && _bossData != null)
                boss.ApplyBossData(_bossData);
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
