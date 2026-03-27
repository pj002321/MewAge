using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데미지 텍스트 / 히트 이펙트 / 사망 파편 스폰을 담당하는 싱글톤 매니저.
/// Inspector에서 각 Prefab을 연결해주세요.
/// </summary>
public class FXManager : MonoBehaviourSingleton<FXManager>
{
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private GameObject goldOrbDebrisPrefab;
    [SerializeField] private GameObject expOrbDebrisPrefab;

    [Header("Orb 파편 연출 on/off")]
    [SerializeField] private bool showGoldOrbDebris = true;
    [SerializeField] private bool showExpOrbDebris  = true;

    protected override bool DontDestroy => false;

    // 파편 오브젝트 추적 (웨이브 종료 시 일괄 제거용)
    private readonly List<GameObject> activeDebris = new();

    public void SpawnDamageText(Vector3 worldPos, float damage, bool isCritical = false)
    {
        if (damageTextPrefab == null) return;

        // 여러 텍스트가 겹치지 않도록 수평 랜덤 오프셋
        Vector3 offset = new(Random.Range(-0.2f, 0.2f), 0.3f, 0f);
        var go = Instantiate(damageTextPrefab, worldPos + offset, Quaternion.identity);
        go.GetComponent<DamageText>()?.Init(damage, isCritical);
    }

    /// <summary>플레이어 머리 위에 상승 텍스트를 표시합니다. (획득/회복/회피 피드백)</summary>
    public void SpawnPlayerText(string text, Color color)
    {
        if (floatingTextPrefab == null) return;
        var player = PlayerController.Instance;
        if (player == null) return;

        Vector3 pos = (Vector3)player.Position
            + new Vector3(Random.Range(-0.25f, 0.25f), 0.8f, 0f);
        var go = Instantiate(floatingTextPrefab, pos, Quaternion.identity);
        go.GetComponent<FloatingText>()?.Init(text, color);
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

    /// <summary>흡수 시 플레이어 머리 위로 수직 상승하며 사라지는 파편</summary>
    public void SpawnGoldOrbRise(Vector3 pos)
    {
        if (!showGoldOrbDebris || goldOrbDebrisPrefab == null) return;
        StartCoroutine(RiseDebrisRoutine(pos, goldOrbDebrisPrefab));
    }

    public void SpawnExpOrbRise(Vector3 pos)
    {
        if (!showExpOrbDebris || expOrbDebrisPrefab == null) return;
        StartCoroutine(RiseDebrisRoutine(pos, expOrbDebrisPrefab));
    }

    public void SpawnDeathDebris(Vector3 pos, Vector2 deathDir = default)
    {
        if (debrisPrefab == null) return;
        StartCoroutine(DebrisRoutine(pos, debrisPrefab, deathDir));
    }

    /// <summary>
    /// 웨이브 종료 시 호출 — 진행 중인 모든 파편 코루틴과 투사체 오브젝트를 즉시 정리합니다.
    /// GameManager.WaveClearRoutine()에서 호출하세요.
    /// </summary>
    public void ClearAllFX()
    {
        StopAllCoroutines();
        foreach (var go in activeDebris)
            if (go != null) Destroy(go);
        activeDebris.Clear();

        // 씬에 남아있는 투사체 일괄 제거
        foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            Destroy(p.gameObject);
        foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            Destroy(p.gameObject);
        foreach (var p in Object.FindObjectsByType<BoomerangProjectile>(FindObjectsSortMode.None))
            Destroy(p.gameObject);

        this.DLog("[FXManager] ClearAllFX — 파편·투사체 정리 완료");
    }

    /// <summary>파편이 위로 상승하며 페이드아웃 (흡수 시 연출)</summary>
    private IEnumerator RiseDebrisRoutine(Vector3 pos, GameObject prefab)
    {
        const int count = 8;
        var gos     = new GameObject[count];
        var vels    = new Vector2[count];
        var renders = new SpriteRenderer[count];

        for (int i = 0; i < count; i++)
        {
            gos[i] = Instantiate(prefab, pos, Quaternion.identity);
            gos[i].transform.localScale *= Random.Range(0.15f, 0.3f);   // 아주 작게
            activeDebris.Add(gos[i]);
            renders[i] = gos[i].GetComponent<SpriteRenderer>();

            // 360° 전방위 터짐 + 강한 속도
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed  = Random.Range(7f, 13f);
            vels[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        }

        float duration = 0.28f;   // 빠르게 사라짐
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                vels[i] *= 1f - 8f * Time.deltaTime; // 빠른 감속

                // 초반부터 페이드아웃
                if (renders[i] != null)
                {
                    float alpha = Mathf.Lerp(1f, 0f, t);
                    var c = renders[i].color;
                    renders[i].color = new Color(c.r, c.g, c.b, alpha);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (gos[i] != null)
            {
                activeDebris.Remove(gos[i]);
                Destroy(gos[i]);
            }
        }
    }

    private IEnumerator DebrisRoutine(Vector3 pos, GameObject prefab, Vector2 deathDir = default)
    {
        const int count = 14;
        var gos  = new GameObject[count];
        var vels = new Vector2[count];

        bool hasDir   = deathDir.sqrMagnitude > 0.001f;
        float baseRad = hasDir ? Mathf.Atan2(deathDir.y, deathDir.x) * Mathf.Rad2Deg : 0f;

        for (int i = 0; i < count; i++)
        {
            gos[i] = Instantiate(prefab, pos, Quaternion.identity);
            gos[i].transform.localScale *= Random.Range(0.7f, 1.0f);
            activeDebris.Add(gos[i]);  // 추적 목록 등록

            float angleDeg = hasDir
                ? (baseRad + Random.Range(-70f, 70f)) * Mathf.Deg2Rad
                : Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(3f, 7f);
            vels[i] = new Vector2(Mathf.Cos(angleDeg), Mathf.Sin(angleDeg)) * speed;
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
        {
            if (gos[i] != null)
            {
                activeDebris.Remove(gos[i]);
                Destroy(gos[i]);
            }
        }
    }
}
