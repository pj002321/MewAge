using System.Buffers;
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

    [Header("스킬 게이지 오브")]
    [SerializeField] private GameObject skillOrbPrefab;   // null 이면 goldOrbDebrisPrefab 사용
    [SerializeField] private float      skillOrbArcHeight = 2.5f;
    [SerializeField] private float      skillOrbDuration  = 0.45f;
    [SerializeField] private int        skillOrbCount     = 4;    // 한 번에 스폰할 오브 개수
    private const int MaxActiveOrbs = 12;
    private int _activeOrbCount;

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
    private readonly Dictionary<GameObject, SpriteRenderer>   _srCache        = new();

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
        if (!_prefabScales.ContainsKey(prefab)) _prefabScales[prefab] = prefab.transform.localScale;
        _srCache[inst] = inst.GetComponent<SpriteRenderer>();
        return inst;
    }

    private void ReturnDebris(GameObject prefab, GameObject go)
    {
        if (go == null) return;
        if (_srCache.TryGetValue(go, out var sr) && sr != null)
        {
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f);
        }
        go.transform.localScale = _prefabScales.TryGetValue(prefab, out var s) ? s : Vector3.one;
        go.SetActive(false);
        if (!_debrisPool.TryGetValue(prefab, out var q))
            _debrisPool[prefab] = q = new Queue<GameObject>();
        q.Enqueue(go);
    }

    private static void SwapRemove<T>(List<T> list, T item) where T : class
    {
        int idx = list.LastIndexOf(item);
        if (idx < 0) return;
        list[idx] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
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

    /// <summary>실제 수치를 룰렛 연출로 표시하는 상승 텍스트를 표시합니다.</summary>
    public void SpawnPlayerTextRoulette(int value, Color color)
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
        ft?.InitRoulette(value, color);
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
        SwapRemove(_activeHitEffects, go);
        _hitEffectPool.Enqueue(go);
    }

    public void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab == null) return;
        GameObject go;
        while (_hitEffectPool.Count > 0 && _hitEffectPool.Peek() == null)
            _hitEffectPool.Dequeue();

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

    /// <summary>무기별 커스텀 hitVfxPrefab 지정 버전. null이면 기본 SpawnHitEffect 호출.</summary>
    public void SpawnHitEffectAt(Vector3 worldPos, GameObject prefab)
    {
        if (prefab == null) { SpawnHitEffect(worldPos); return; }
        var go = Instantiate(prefab, worldPos, Quaternion.identity);
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
        _activeOrbCount = 0;

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

        for (int i = MortarShell.All.Count - 1; i >= 0; i--)
            if (MortarShell.All[i] != null) Destroy(MortarShell.All[i].gameObject);

        // 보스/적 스킬이 남긴 경고선·텔레포트 마커 일괄 제거
        EnemyCombatRoutines.WarningLine.DestroyAll();
        EnemyCombatRoutines.DestroyAllMarkers();
        var lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        foreach (var lr in lineRenderers)
            if (lr != null && lr.gameObject.name == "WarnLine")
                Destroy(lr.gameObject);

        var markers = FindObjectsByType<ImpactMarker>(FindObjectsSortMode.None);
        foreach (var m in markers)
            if (m != null) Destroy(m.gameObject);

        this.DLog("[FXManager] ClearAllFX — 파편·투사체·경고선·마커 정리 완료");
    }

    // ── 스킬 게이지 오브 ─────────────────────────────────────────────

    /// <summary>적 머리에서 사방으로 튀었다가 플레이어 머리로 흡수되는 스킬 포인트 오브 (복수).</summary>
    public void SpawnSkillOrb(Vector3 from, System.Action onArrive)
    {
        var prefab = skillOrbPrefab != null ? skillOrbPrefab : goldOrbDebrisPrefab;
        if (prefab == null) { onArrive?.Invoke(); return; }

        // 동시 활성 오브가 상한을 초과하면 연출 생략 (순수 시각 효과이므로 게임플레이 무관)
        if (_activeOrbCount >= MaxActiveOrbs) { onArrive?.Invoke(); return; }

        int count = Mathf.Max(1, skillOrbCount);
        for (int i = 0; i < count; i++)
        {
            bool isLast = i == count - 1;
            StartCoroutine(SkillOrbRoutine(from, prefab, isLast ? onArrive : null, i, count));
        }
    }

    private IEnumerator SkillOrbRoutine(Vector3 from, GameObject prefab, System.Action onArrive, int orbIndex = 0, int orbTotal = 1)
    {
        // 각 오브 독립 랜덤 지연 — 파박파박 따로 꽂히는 느낌
        float initDelay = Random.Range(0f, 0.28f);
        if (initDelay > 0.01f) yield return new WaitForSeconds(initDelay);

        _activeOrbCount++;

        var go = GetDebris(prefab, from);
        activeDebris.Add(go);

        _prefabScales.TryGetValue(prefab, out var baseScale);
        if (baseScale == Vector3.zero) baseScale = Vector3.one;

        Vector3 GetTarget()
        {
            var anchor = PlayerController.Instance?.FloatingTextAnchor;
            if (anchor != null) return anchor.position;
            if (PlayerController.Instance != null) return PlayerController.Instance.transform.position;
            return from;
        }

        float baseAngle    = (orbIndex / (float)orbTotal) * 360f * Mathf.Deg2Rad;
        float angle        = baseAngle + Random.Range(-0.4f, 0.4f);
        float radius       = skillOrbArcHeight * Random.Range(2.5f, 4.0f);
        Vector3 ctrlOffset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

        // 오브마다 다른 이동 속도 — 먼저 꽂히는 것과 늦게 꽂히는 것이 생김
        float duration = skillOrbDuration * Random.Range(0.55f, 1.6f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t  = elapsed / duration;
            float t2 = t * t;

            if (go != null)
            {
                Vector3 to   = GetTarget();
                Vector3 ctrl = to + ctrlOffset;
                go.transform.position = Vector3.Lerp(
                    Vector3.Lerp(from, ctrl, t2),
                    Vector3.Lerp(ctrl, to,   t2),
                    t2);
            }

            // 후반부(t>=0.5)부터 플레이어에 근접할수록 스케일 축소 → 흡수 연출
            if (go != null)
            {
                float scaleT = Mathf.Clamp01((t - 0.5f) / 0.5f);
                float s = Mathf.Lerp(1f, 0f, scaleT * scaleT);
                go.transform.localScale = baseScale * s;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null)
        {
            SwapRemove(activeDebris, go);
            ReturnDebris(prefab, go);
        }
        _activeOrbCount = Mathf.Max(0, _activeOrbCount - 1);
        onArrive?.Invoke();
    }

    /// <summary>파편이 위로 상승하며 페이드아웃 (흡수 시 연출)</summary>
    private IEnumerator RiseDebrisRoutine(Vector3 pos, GameObject prefab)
    {
        const int count = 8;
        var gos     = ArrayPool<GameObject>.Shared.Rent(count);
        var vels    = ArrayPool<Vector2>.Shared.Rent(count);
        var renders = ArrayPool<SpriteRenderer>.Shared.Rent(count);

        for (int i = 0; i < count; i++)
        {
            gos[i] = GetDebris(prefab, pos);
            activeDebris.Add(gos[i]);
            _srCache.TryGetValue(gos[i], out renders[i]);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(7f, 13f);
            vels[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        }

        float duration = 0.28f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                vels[i] *= 1f - 8f * Time.deltaTime;

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
                SwapRemove(activeDebris, gos[i]);
                ReturnDebris(prefab, gos[i]);
            }
        }

        ArrayPool<GameObject>.Shared.Return(gos, clearArray: true);
        ArrayPool<Vector2>.Shared.Return(vels);
        ArrayPool<SpriteRenderer>.Shared.Return(renders, clearArray: true);
    }

    private IEnumerator DebrisRoutine(Vector3 pos, GameObject prefab, Vector2 deathDir = default)
    {
        const int count = 8;
        var gos           = ArrayPool<GameObject>.Shared.Rent(count);
        var vels          = ArrayPool<Vector2>.Shared.Rent(count);
        var srs           = ArrayPool<SpriteRenderer>.Shared.Rent(count);
        var initialScales = ArrayPool<Vector3>.Shared.Rent(count);

        bool  hasDir  = deathDir.sqrMagnitude > 0.001f;
        float baseRad = hasDir ? Mathf.Atan2(deathDir.y, deathDir.x) * Mathf.Rad2Deg : 0f;

        _prefabScales.TryGetValue(prefab, out var baseScale);

        for (int i = 0; i < count; i++)
        {
            gos[i] = GetDebris(prefab, pos);
            activeDebris.Add(gos[i]);
            _srCache.TryGetValue(gos[i], out srs[i]);

            float randomScale = Random.Range(0.7f, 1.2f);
            gos[i].transform.localScale = baseScale * randomScale;
            initialScales[i] = gos[i].transform.localScale;

            float angleDeg, speed;
            if (hasDir)
            {
                if (i < count * 0.6f)
                {
                    angleDeg = baseRad + Random.Range(-35f, 35f);
                    speed    = Random.Range(12f, 20f);
                }
                else
                {
                    angleDeg = baseRad + Random.Range(-100f, 100f);
                    speed    = Random.Range(6f, 12f);
                }
            }
            else
            {
                angleDeg = Random.Range(0f, 360f);
                speed    = Random.Range(8f, 16f);
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            vels[i]   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

            if (hasDir)
                vels[i] += deathDir * Random.Range(2f, 4f);
        }

        float duration = 0.35f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                vels[i] *= 1f - 3.5f * Time.deltaTime;

                if (t > 0.4f)
                {
                    float fadeT = (t - 0.4f) / 0.6f;
                    if (srs[i] != null)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, fadeT);
                        var c = srs[i].color;
                        srs[i].color = new Color(c.r, c.g, c.b, alpha);
                    }
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
                SwapRemove(activeDebris, gos[i]);
                ReturnDebris(prefab, gos[i]);
            }
        }

        ArrayPool<GameObject>.Shared.Return(gos, clearArray: true);
        ArrayPool<Vector2>.Shared.Return(vels);
        ArrayPool<SpriteRenderer>.Shared.Return(srs, clearArray: true);
        ArrayPool<Vector3>.Shared.Return(initialScales);
    }
}
