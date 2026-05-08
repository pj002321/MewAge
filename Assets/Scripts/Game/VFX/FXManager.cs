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
    [SerializeField] private GameObject landingVfxPrefab;

    [Header("보스 입장 연출")]
    [Tooltip("보스 팝인 시 착지 VFX 프리팹")]
    [SerializeField] private GameObject bossEntryLandingVfxPrefab;
    [SerializeField] private float      bossEntryLandingVfxLifetime = 2f;

    [Header("Orb 파편 연출 on/off")]
    [SerializeField] private bool showGoldOrbDebris = true;
    [SerializeField] private bool showExpOrbDebris  = true;

    protected override bool DontDestroy => false;

    // 파편 오브젝트 추적 (웨이브 종료 시 일괄 제거용)
    private readonly List<GameObject> activeDebris = new();

    // 활성 히트 이펙트 추적 (FindObjectsByType 제거용)
    private readonly List<GameObject> _activeHitEffects = new();

    // ── 오브젝트 풀 ──────────────────────────────────────────────
    private readonly Dictionary<GameObject, Queue<GameObject>> _debrisPool     = new();
    private readonly Queue<DamageText>                         _damageTextPool = new();
    private readonly Queue<GameObject>                         _hitEffectPool  = new();
    private readonly Queue<FloatingText>                       _floatingTextPool = new();
    private readonly Dictionary<GameObject, Vector3>           _prefabScales   = new();

    private GameObject GetDebris(GameObject prefab, Vector3 pos)
    {
        if (!_debrisPool.TryGetValue(prefab, out var q))
            _debrisPool[prefab] = q = new Queue<GameObject>();

        if (q.Count > 0)
        {
            var go = q.Dequeue();
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.transform.localScale = _prefabScales.TryGetValue(prefab, out var s) ? s : Vector3.one;
            go.SetActive(true);
            return go;
        }
        var inst = Instantiate(prefab, pos, Quaternion.identity);
        if (!_prefabScales.ContainsKey(prefab)) _prefabScales[prefab] = inst.transform.localScale;
        return inst;
    }

    private void ReturnDebris(GameObject prefab, GameObject go)
    {
        if (go == null) return;
        // 색상 알파 복구
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, 1f); }
        go.transform.localScale = _prefabScales.TryGetValue(prefab, out var s) ? s : Vector3.one;
        go.SetActive(false);
        if (!_debrisPool.TryGetValue(prefab, out var q))
            _debrisPool[prefab] = q = new Queue<GameObject>();
        q.Enqueue(go);
    }

    public void ReturnDamageText(DamageText dt)
    {
        if (dt == null) return;
        dt.gameObject.SetActive(false);
        _damageTextPool.Enqueue(dt);
    }

    public static bool NextIsEpicHit;

    public void SpawnDamageText(Vector3 worldPos, float damage, bool isCritical = false, bool isBleed = false)
    {
        if (damageTextPrefab == null) return;

        bool epicHit  = NextIsEpicHit;
        NextIsEpicHit = false;

        float yOffset  = epicHit ? 0.7f : 0.3f;
        Vector3 offset = new(Random.Range(-0.2f, 0.2f), yOffset, 0f);
        Vector3 pos    = worldPos + offset;

        DamageText dt;
        if (_damageTextPool.Count > 0)
        {
            dt = _damageTextPool.Dequeue();
            dt.transform.position = pos;
            dt.gameObject.SetActive(true);
        }
        else
        {
            dt = Instantiate(damageTextPrefab, pos, Quaternion.identity).GetComponent<DamageText>();
        }
        dt?.Init(damage, isCritical, isBleed, epicHit, epicHit ? 0.75f : 1f);
    }

    public void ReturnFloatingText(FloatingText ft)
    {
        if (ft == null) return;
        ft.gameObject.SetActive(false);
        _floatingTextPool.Enqueue(ft);
    }

    /// <summary>플레이어 머리 위에 상승 텍스트를 표시합니다. (획득/회복/회피 피드백)</summary>
    public void SpawnPlayerText(string text, Color color)
    {
        if (floatingTextPrefab == null || PlayerController.Instance?.FloatingTextAnchor == null) return;
        FloatingText ft;
        if (_floatingTextPool.Count > 0)
        {
            ft = _floatingTextPool.Dequeue();
            ft.transform.SetParent(PlayerController.Instance.FloatingTextAnchor, false);
            ft.transform.localPosition = Vector3.zero;
            ft.gameObject.SetActive(true);
        }
        else
        {
            var go = Instantiate(floatingTextPrefab, PlayerController.Instance.FloatingTextAnchor, false);
            go.transform.localPosition = Vector3.zero;
            ft = go.GetComponent<FloatingText>();
        }
        ft?.Init(text, color);
    }

    public void ReturnHitEffect(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _activeHitEffects.Remove(go);
        _hitEffectPool.Enqueue(go);
    }

    public void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab == null) return;
        GameObject go;
        if (_hitEffectPool.Count > 0)
        {
            go = _hitEffectPool.Dequeue();
            go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            go.transform.localScale = _prefabScales.TryGetValue(hitEffectPrefab, out var s) ? s : Vector3.one;
            go.SetActive(true);
        }
        else
        {
            go = Instantiate(hitEffectPrefab, worldPos, Quaternion.identity);
            if (!_prefabScales.ContainsKey(hitEffectPrefab))
                _prefabScales[hitEffectPrefab] = go.transform.localScale;
        }
        _activeHitEffects.Add(go);
        go.GetComponent<HitEffect>()?.Play(go, this);
    }

    public void SpawnLandingVFX(Vector3 worldPos)
    {
        if (landingVfxPrefab == null) return;
        Instantiate(landingVfxPrefab, worldPos, Quaternion.identity);
    }

    public void SpawnBossEntryLandingVfx(Vector3 worldPos)
    {
        if (bossEntryLandingVfxPrefab == null) return;
        Destroy(Instantiate(bossEntryLandingVfxPrefab, worldPos, Quaternion.identity), bossEntryLandingVfxLifetime);
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

        // 활성 히트 이펙트 풀 반환 (static 추적 리스트 사용)
        for (int i = _activeHitEffects.Count - 1; i >= 0; i--)
        {
            var go = _activeHitEffects[i];
            if (go == null) continue;
            go.SetActive(false);
            _hitEffectPool.Enqueue(go);
        }
        _activeHitEffects.Clear();

        // 씬에 남아있는 투사체 일괄 제거 (static All 리스트 사용)
        for (int i = Projectile.All.Count - 1; i >= 0; i--)
            if (Projectile.All[i] != null) Destroy(Projectile.All[i].gameObject);

        for (int i = EnemyProjectile.All.Count - 1; i >= 0; i--)
            if (EnemyProjectile.All[i] != null) Destroy(EnemyProjectile.All[i].gameObject);

        for (int i = BoomerangProjectile.All.Count - 1; i >= 0; i--)
            if (BoomerangProjectile.All[i] != null) Destroy(BoomerangProjectile.All[i].gameObject);

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
            gos[i] = GetDebris(prefab, pos);
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
                ReturnDebris(prefab, gos[i]);
            }
        }
    }

    private IEnumerator DebrisRoutine(Vector3 pos, GameObject prefab, Vector2 deathDir = default)
    {
        const int count = 8;
        var gos  = new GameObject[count];
        var vels = new Vector2[count];
        var srs  = new SpriteRenderer[count];
        var initialScales = new Vector3[count];

        bool  hasDir  = deathDir.sqrMagnitude > 0.001f;
        float baseRad = hasDir ? Mathf.Atan2(deathDir.y, deathDir.x) * Mathf.Rad2Deg : 0f;

        for (int i = 0; i < count; i++)
        {
            gos[i] = GetDebris(prefab, pos);
            activeDebris.Add(gos[i]);
            srs[i] = gos[i].GetComponent<SpriteRenderer>();
            initialScales[i] = gos[i].transform.localScale;

            float angleDeg, speed;
            // 원본 스케일 저장
            _prefabScales.TryGetValue(prefab, out var initialScale);
            if (hasDir)
            {
                // 앞 절반: deathDir 방향 ±20° 좁은 cone — 빠르게 쌔게
                // 뒤 절반: ±70° 넓게 퍼지는 잔해 — 느리게
                if (i < count * 0.6f)
                {
                    angleDeg = baseRad + Random.Range(-20f, 20f);
                    speed    = Random.Range(6f, 10f);
                }
                else
                {
                    angleDeg = baseRad + Random.Range(-70f, 70f);
                    speed    = Random.Range(2f, 5f);
                }
            }
            else
            {
                angleDeg = Random.Range(0f, 360f);
                speed    = Random.Range(3f, 7f);
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            vels[i]   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

            // deathDir 방향 전체에 추가 부스트 (모든 파편이 그 방향으로 쏠림)
            if (hasDir)
                vels[i] += deathDir * Random.Range(1f, 2f);
        }

        float duration = 0.2f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                vels[i] *= 1f - 6f * Time.deltaTime; // 감속

                // 후반부터 페이드아웃 및 사이즈 축소
                if (t > 0.4f)
                {
                    float fadeT = (t - 0.4f) / 0.6f;
                    if (srs[i] != null)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, fadeT);
                        var c = srs[i].color;
                        srs[i].color = new Color(c.r, c.g, c.b, alpha);
                    }
                    // 사이즈 축소 (0.5배까지)
                    float scale = Mathf.Lerp(1f, 0.5f, fadeT);
                    gos[i].transform.localScale = initialScales[i] * scale;
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
                ReturnDebris(prefab, gos[i]);
            }
        }
    }
}
