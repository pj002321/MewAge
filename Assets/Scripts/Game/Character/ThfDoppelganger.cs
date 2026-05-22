using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SkillThf — 시프냥 분신 엔티티.
/// 프리팹에 부착하세요. 빠른 이동 중 고스트 트레일이 남습니다.
/// </summary>
public class ThfDoppelganger : MonoBehaviour
{
    [Header("고스트 트레일")]
    [SerializeField] private float ghostSpawnInterval = 0.04f;
    [SerializeField] private float ghostFadeDuration  = 0.18f;
    [SerializeField] private Color ghostTint          = new Color(0.55f, 0.8f, 1f, 0.5f);

    private int   _hitsPerEnemy;
    private float _hitInterval;
    private float _damageMult;
    private float _knockbackForce;
    private float _moveSpeed;

    private readonly List<GameObject> _activeGhosts = new List<GameObject>();

    private void OnDestroy()
    {
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

        StartCoroutine(BehaviorLoop());
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

        float damage = CalcDamage();
        for (int i = 0; i < _hitsPerEnemy; i++)
        {
            if (target == null) yield break;
            target.TakeDamage(damage);
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

            var ghostSr         = ghost.AddComponent<SpriteRenderer>();
            ghostSr.sprite      = sr.sprite;
            ghostSr.sortingLayerID = sr.sortingLayerID;
            ghostSr.sortingOrder   = sr.sortingOrder - 1;
            ghostSr.flipX       = sr.flipX;
            ghostSr.color       = ghostTint;

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

    // ── 데미지 계산 ───────────────────────────────────────────────────

    private float CalcDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return 0f;
        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        return (total / wps.Count) * _damageMult;
    }
}
