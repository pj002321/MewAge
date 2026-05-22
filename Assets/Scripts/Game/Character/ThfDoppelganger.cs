using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SkillThf — 시프냥 분신 엔티티.
/// 스폰/소멸 시 플레이어를 부모로 삼아 로컬 좌표로 연출합니다.
/// </summary>
public class ThfDoppelganger : MonoBehaviour
{
    [Header("스폰 / 소멸 연출")]
    [SerializeField] private float spawnDuration  = 0.35f;
    [SerializeField] private float spawnBackOffset = 0.8f;
    [SerializeField] private float mergeDuration  = 0.5f;

    [Header("고스트 트레일")]
    [SerializeField] private float ghostSpawnInterval = 0.04f;
    [SerializeField] private float ghostFadeDuration  = 0.18f;
    [SerializeField] private Color ghostTint          = new Color(0.55f, 0.8f, 1f, 0.5f);

    private int   _hitsPerEnemy;
    private float _hitInterval;
    private float _damageMult;
    private float _knockbackForce;
    private float _moveSpeed;

    private bool    _despawning;
    private Color[] _originalColors;
    private readonly List<GameObject> _activeGhosts = new List<GameObject>();

    private void OnDestroy()
    {
        if (_despawning) return;
        foreach (var g in _activeGhosts)
            if (g != null) Destroy(g);
        _activeGhosts.Clear();
    }

    // ── 초기화 ────────────────────────────────────────────────────────

    public void Init(int hitsPerEnemy, float hitInterval,
                     float damageMult, float knockbackForce, float moveSpeed)
    {
        _hitsPerEnemy   = hitsPerEnemy;
        _hitInterval    = hitInterval;
        _damageMult     = damageMult;
        _knockbackForce = knockbackForce;
        _moveSpeed      = moveSpeed;

        StartCoroutine(SpawnAndRun());
    }

    // ── 스폰 연출 ─────────────────────────────────────────────────────

    private IEnumerator SpawnAndRun()
    {
        yield return StartCoroutine(SpawnAnimation());
        if (!_despawning)
            StartCoroutine(BehaviorLoop());
    }

    private IEnumerator SpawnAnimation()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>();
        CacheOriginalColors(srs);

        var player = PlayerController.Instance;
        if (player == null) { RestoreOriginalColors(srs); yield break; }

        // 플레이어 자식 편입 — 로컬 원점(= 플레이어 중심)에서 시작
        transform.SetParent(player.transform, false);
        transform.localPosition = Vector3.zero;
        SetSilhouette(srs);

        Vector3 targetLocal = BackLocalPos(player, spawnBackOffset);

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            transform.localPosition = Vector3.Lerp(Vector3.zero, targetLocal, t);
            LerpToOriginal(srs, t);
            yield return null;
        }

        transform.localPosition = targetLocal;
        RestoreOriginalColors(srs);

        // 자립 — 월드 위치 유지하며 부모 해제
        transform.SetParent(null, true);
    }

    // ── 소멸 연출 (플레이어에게 흡수) ────────────────────────────────

    public void Despawn()
    {
        if (_despawning) return;
        _despawning = true;

        StopAllCoroutines();
        foreach (var g in _activeGhosts)
            if (g != null) Destroy(g);
        _activeGhosts.Clear();

        StartCoroutine(MergeRoutine());
    }

    private IEnumerator MergeRoutine()
    {
        var srs         = GetComponentsInChildren<SpriteRenderer>();
        var startColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            startColors[i] = srs[i] != null ? srs[i].color : Color.white;

        var player = PlayerController.Instance;
        if (player == null) { Destroy(gameObject); yield break; }

        // 플레이어 자식 편입 — 현재 월드 위치 유지
        transform.SetParent(player.transform, true);
        Vector3 startLocal = transform.localPosition;

        float elapsed = 0f;
        while (elapsed < mergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / mergeDuration);

            // 로컬 원점(플레이어 중심)으로 수렴 — 플레이어 이동은 부모가 자동 처리
            transform.localPosition = Vector3.Lerp(startLocal, Vector3.zero, t);

            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                srs[i].color = Color.Lerp(startColors[i], Color.black, t);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    // ── 행동 루프 ─────────────────────────────────────────────────────

    private IEnumerator BehaviorLoop()
    {
        while (true)
        {
            var target = PickTarget();
            if (target == null) { yield return null; continue; }

            yield return StartCoroutine(MoveAndAttack(target));
        }
    }

    private EnemyController PickTarget()
    {
        var all   = EnemyController.All;
        var alive = new List<EnemyController>(all.Count);
        foreach (var e in all)
            if (e != null) alive.Add(e);

        return alive.Count == 0 ? null : alive[Random.Range(0, alive.Count)];
    }

    // ── 빠른 이동 + 히트 ──────────────────────────────────────────────

    private IEnumerator MoveAndAttack(EnemyController target)
    {
        yield return StartCoroutine(MoveToTarget(target));

        if (target == null) yield break;

        for (int i = 0; i < _hitsPerEnemy; i++)
        {
            if (target == null) yield break;
            var (dmg, isCrit) = RollDamage();
            target.TakeDamage(dmg, isCritical: isCrit);
            yield return new WaitForSeconds(_hitInterval);
        }

        if (target != null)
        {
            Vector2 kbDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            if (kbDir.sqrMagnitude < 0.01f) kbDir = Vector2.right;
            target.Knockback(kbDir, _knockbackForce);
        }
    }

    private IEnumerator MoveToTarget(EnemyController target)
    {
        const float arriveThreshold = 0.35f;

        var trailRoutine = StartCoroutine(GhostTrailLoop());

        while (target != null)
        {
            float side    = target.transform.position.x >= transform.position.x ? 1f : -1f;
            var   destPos = (Vector2)target.transform.position + Vector2.right * (-side * 0.4f);
            var   current = (Vector2)transform.position;

            if (Vector2.Distance(current, destPos) <= arriveThreshold) break;

            FlipToward(side > 0f);
            transform.position = Vector2.MoveTowards(current, destPos, _moveSpeed * Time.deltaTime);
            yield return null;
        }

        StopCoroutine(trailRoutine);
    }

    // ── 방향 처리 ─────────────────────────────────────────────────────

    private void FlipToward(bool facingRight)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.flipX = !facingRight;
    }

    // ── 고스트 트레일 ──────────────────────────────────────────────────

    private IEnumerator GhostTrailLoop()
    {
        while (true)
        {
            SpawnGhost();
            yield return new WaitForSeconds(ghostSpawnInterval);
        }
    }

    private void SpawnGhost()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null) continue;

            var ghost = new GameObject("DoppelGhost");
            ghost.transform.position   = sr.transform.position;
            ghost.transform.rotation   = sr.transform.rotation;
            ghost.transform.localScale = sr.transform.lossyScale;

            var ghostSr            = ghost.AddComponent<SpriteRenderer>();
            ghostSr.sprite         = sr.sprite;
            ghostSr.sortingLayerID = sr.sortingLayerID;
            ghostSr.sortingOrder   = sr.sortingOrder - 1;
            ghostSr.flipX          = sr.flipX;
            ghostSr.color          = ghostTint;

            _activeGhosts.Add(ghost);
            StartCoroutine(FadeGhost(ghostSr));
        }
    }

    private IEnumerator FadeGhost(SpriteRenderer sr)
    {
        float elapsed  = 0f;
        Color startCol = sr.color;

        while (elapsed < ghostFadeDuration)
        {
            elapsed += Time.deltaTime;
            float a  = Mathf.Lerp(startCol.a, 0f, elapsed / ghostFadeDuration);
            sr.color = new Color(startCol.r, startCol.g, startCol.b, a);
            yield return null;
        }

        _activeGhosts.Remove(sr.gameObject);
        Destroy(sr.gameObject);
    }

    // ── 색상 유틸 ─────────────────────────────────────────────────────

    private void CacheOriginalColors(SpriteRenderer[] srs)
    {
        _originalColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            _originalColors[i] = srs[i] != null ? srs[i].color : Color.white;
    }

    private void SetSilhouette(SpriteRenderer[] srs)
    {
        foreach (var sr in srs)
            if (sr != null) sr.color = Color.black;
    }

    private void LerpToOriginal(SpriteRenderer[] srs, float t)
    {
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].color = Color.Lerp(Color.black, _originalColors[i], t);
        }
    }

    private void RestoreOriginalColors(SpriteRenderer[] srs)
    {
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) srs[i].color = _originalColors[i];
    }

    // ── 위치 유틸 ─────────────────────────────────────────────────────

    // 플레이어 로컬 공간에서 등 뒤 위치 (VisualSlot 스케일 부호로 facing 판단)
    private Vector3 BackLocalPos(PlayerController player, float offset)
    {
        float facingSign = player.VisualSlot != null
            ? Mathf.Sign(player.VisualSlot.localScale.x)
            : 1f;
        return Vector3.right * (-facingSign * offset);
    }

    // ── 데미지 계산 ───────────────────────────────────────────────────

    private const float CritChance = 0.5f;

    private (float damage, bool isCrit) RollDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return (0f, false);
        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        float base_ = (total / wps.Count) * _damageMult;

        bool  isCrit = Random.value < CritChance;
        float mult   = isCrit ? 1f + (PlayerController.Instance?.CritDamageBonus ?? 1f) : 1f;
        return (base_ * mult, isCrit);
    }
}
