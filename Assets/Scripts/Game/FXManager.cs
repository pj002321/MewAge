using System.Collections;
using UnityEngine;

/// <summary>
/// 데미지 텍스트 / 히트 이펙트 / 사망 파편 스폰을 담당하는 싱글톤 매니저.
/// Inspector에서 각 Prefab을 연결해주세요.
/// </summary>
public class FXManager : MonoBehaviourSingleton<FXManager>
{
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private GameObject goldOrbDebrisPrefab;
    [SerializeField] private GameObject expOrbDebrisPrefab;

    protected override bool DontDestroy => false;

    public void SpawnDamageText(Vector3 worldPos, float damage, bool isCritical = false)
    {
        if (damageTextPrefab == null) return;

        // 여러 텍스트가 겹치지 않도록 수평 랜덤 오프셋
        Vector3 offset = new(Random.Range(-0.2f, 0.2f), 0.3f, 0f);
        var go = Instantiate(damageTextPrefab, worldPos + offset, Quaternion.identity);
        go.GetComponent<DamageText>()?.Init(damage, isCritical);
    }

    public void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab == null) return;
        Instantiate(hitEffectPrefab, worldPos, Quaternion.identity);
    }

    // ── 사망 파편 ────────────────────────────────────────────────

    public void SpawnGoldOrbDebris(Vector3 pos)
    {
        if (goldOrbDebrisPrefab == null) return;
        StartCoroutine(DebrisRoutine(pos, goldOrbDebrisPrefab));
    }

    public void SpawnExpOrbDebris(Vector3 pos)
    {
        if (expOrbDebrisPrefab == null) return;
        StartCoroutine(DebrisRoutine(pos, expOrbDebrisPrefab));
    }

    public void SpawnDeathDebris(Vector3 pos)
    {
        if (debrisPrefab == null) return;
        StartCoroutine(DebrisRoutine(pos, debrisPrefab));
    }

    private IEnumerator DebrisRoutine(Vector3 pos, GameObject prefab)
    {
        const int count = 8;
        var gos  = new GameObject[count];
        var vels = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            gos[i] = Instantiate(prefab, pos, Quaternion.identity);
            gos[i].transform.localScale *= Random.Range(0.7f, 1.0f);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(2f, 5f);
            vels[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        }

        float duration = 0.5f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                vels[i] *= 1f - 5f * Time.deltaTime;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (gos[i] != null) Destroy(gos[i]);
    }
}
