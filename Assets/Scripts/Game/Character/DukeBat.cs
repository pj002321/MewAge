using System.Collections;
using UnityEngine;

/// <summary>
/// 듀크냥 스킬 박쥐.
/// - 평상시: 앵커(집) 위치를 타원으로 공전
/// - 스킬 발동 시: 적 위에서 크게 내려꽂은 뒤 바닥에서 다타 데미지, 이후 다음 적 탐색
/// </summary>
public class DukeBat : MonoBehaviour
{
    public const int HitsPerPeck = 3;

    private const float PeckDashDuration = 0.15f;  // 내려꽂기 시간
    private const float PeckPullDuration = 0.12f;  // 복귀 시간
    private const float PeckHitInterval  = 0.04f;  // 바닥 다타 간격
    private const float ArriveThreshold  = 0.4f;

    // 공전
    private const float OrbitRadius = 0.5f;
    private const float OrbitSpeed  = 2.2f;
    private const float ReturnSpeed = 8f;

    private Transform  _anchor;
    private float      _orbitAngle;
    private float      _moveSpeed;
    private bool       _isAttacking;
    private bool       _inCombo;

    private float _damageMult;

    private System.Func<IEnemyHost> _getOtherTarget;

    private IEnemyHost       _target;
    private IEnemyHost       _lastTarget;
    private Rigidbody2D      _rb;
    private SpriteRenderer[] _srs;

    public IEnemyHost CurrentTarget => _target;

    // ── 초기화 ────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _srs = GetComponentsInChildren<SpriteRenderer>();
        if (_rb != null)
        {
            _rb.bodyType     = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
        }
    }

    public void Init(Transform anchor, float moveSpeed,
                     System.Func<IEnemyHost> getOtherTarget = null,
                     float orbitPhase = 0f)
    {
        _anchor         = anchor;
        _moveSpeed      = moveSpeed;
        _getOtherTarget = getOtherTarget;
        _orbitAngle     = orbitPhase;
        _isAttacking    = false;
        _inCombo        = false;
        _target         = null;
    }

    public void StartAttacking(float damageMult)
    {
        _damageMult  = damageMult;
        _isAttacking = true;
    }

    public void StopAttacking()
    {
        _isAttacking = false;
        _target      = null;
        _lastTarget  = null;
    }

    // ── Update ────────────────────────────────────────────────────────

    private void Update()
    {
        if (_inCombo) return;

        if (_isAttacking)
        {
            if (!IsAlive(_target))
                _target = PickTarget();

            if (_target != null)
                ChaseTarget();
            else
                MoveToAnchor();
        }
        else
        {
            _target = null;
            MoveToAnchor();
        }
    }

    // ── 추적 & 공격 ───────────────────────────────────────────────────

    private void ChaseTarget()
    {
        Vector2 enemyPos = (Vector2)_target.SelfTransform.position;
        Vector2 current  = GetPos();
        float   side     = enemyPos.x >= current.x ? 1f : -1f;
        Vector2 dest     = enemyPos + new Vector2(-side * 0.4f, 1.3f);

        FlipToward(side > 0f);

        if (Vector2.Distance(current, dest) <= ArriveThreshold)
        {
            _inCombo = true;
            StartCoroutine(DoPeck(_target, dest));
        }
        else
        {
            MoveTo(dest);
        }
    }

    private IEnumerator DoPeck(IEnemyHost target, Vector2 peckOrigin)
    {
        Vector2 strikePos = IsAlive(target)
            ? (Vector2)target.SelfTransform.position
            : peckOrigin;

        // 크게 내려꽂기
        float t = 0f;
        while (t < PeckDashDuration)
        {
            t += Time.deltaTime;
            SetPos(Vector2.Lerp(peckOrigin, strikePos,
                                Mathf.Clamp01(t / PeckDashDuration)));
            yield return null;
        }

        // 바닥에서 빠르게 다타
        for (int i = 0; i < HitsPerPeck; i++)
        {
            if (IsAlive(target))
                target.TakeDamage(CalcDamage());
            if (i < HitsPerPeck - 1)
                yield return new WaitForSeconds(PeckHitInterval);
        }

        // 복귀
        t = 0f;
        while (t < PeckPullDuration)
        {
            t += Time.deltaTime;
            SetPos(Vector2.Lerp(strikePos, peckOrigin,
                                Mathf.Clamp01(t / PeckPullDuration)));
            yield return null;
        }
        SetPos(peckOrigin);

        _lastTarget = target;
        _target     = null;
        _inCombo    = false;
    }

    // ── 집으로 이동 (공전) ────────────────────────────────────────────

    private void MoveToAnchor()
    {
        if (_anchor == null) return;
        _orbitAngle += OrbitSpeed * Time.deltaTime;
        Vector2 orbitPos = (Vector2)_anchor.position + new Vector2(
            Mathf.Cos(_orbitAngle) * OrbitRadius,
            Mathf.Sin(_orbitAngle) * OrbitRadius * 0.6f
        );
        Vector2 dir = orbitPos - GetPos();
        if (dir.sqrMagnitude > 0.01f) FlipToward(dir.x >= 0f);
        SetPos(Vector2.MoveTowards(GetPos(), orbitPos, ReturnSpeed * Time.deltaTime));
    }

    // ── 타겟 탐색 ─────────────────────────────────────────────────────

    private IEnemyHost PickTarget()
    {
        var otherTarget = _getOtherTarget?.Invoke();

        var result = FindNearest(otherTarget, _lastTarget);
        if (result == null) result = FindNearest(otherTarget, null);
        if (result == null) result = FindNearest(null, null);
        return result;
    }

    private IEnemyHost FindNearest(IEnemyHost exclude1, IEnemyHost exclude2)
    {
        IEnemyHost nearest = null;
        float      minSq   = float.MaxValue;

        foreach (var e in EnemyController.All)
        {
            if (e == null || e == exclude1 || e == exclude2) continue;
            float sq = ((Vector2)e.transform.position - GetPos()).sqrMagnitude;
            if (sq < minSq) { minSq = sq; nearest = e; }
        }

        var boss = BossController.Current;
        if (boss != null && boss != exclude1 && boss != exclude2)
        {
            float sq = ((Vector2)boss.transform.position - GetPos()).sqrMagnitude;
            if (sq < minSq) { nearest = boss; }
        }

        return nearest;
    }

    // ── 이동 유틸 ─────────────────────────────────────────────────────

    private void MoveTo(Vector2 dest)
    {
        SetPos(Vector2.MoveTowards(GetPos(), dest, _moveSpeed * Time.deltaTime));
    }

    private Vector2 GetPos() => _rb != null ? _rb.position : (Vector2)transform.position;

    private void SetPos(Vector2 pos)
    {
        if (_rb != null)
            _rb.position = pos;
        else
            transform.position = pos;
    }

    private void FlipToward(bool facingRight)
    {
        if (_srs == null) return;
        foreach (var sr in _srs)
            if (sr != null) sr.flipX = !facingRight;
    }

    private static bool IsAlive(IEnemyHost h) =>
        h != null && (h as Object) != null;

    private float CalcDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return 0f;
        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        return total * _damageMult;
    }
}
