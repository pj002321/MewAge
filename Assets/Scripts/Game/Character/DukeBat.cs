using System.Collections;
using UnityEngine;

/// <summary>
/// 듀크냥 스킬 박쥐 엔티티.
/// 단일 코루틴 안에서 _isAttacking 플래그로 Follow/Attack 전환합니다.
/// </summary>
public class DukeBat : MonoBehaviour
{
    public const int PecksPerCombo = 3;

    private Transform _player;
    private float     _orbitAngle;
    private float     _orbitRadius;
    private float     _orbitSpeed;
    private float     _moveSpeed;
    private bool      _isAttacking;

    private float _damageMult;
    private float _dotMult;
    private float _dotDuration;

    private const float ArriveThreshold = 0.35f;

    // ── 초기화 ────────────────────────────────────────────────────────

    public void InitFollow(Transform player, float startAngle, float orbitRadius, float orbitSpeed, float moveSpeed)
    {
        _player      = player;
        _orbitAngle  = startAngle;
        _orbitRadius = orbitRadius;
        _orbitSpeed  = orbitSpeed;
        _moveSpeed   = moveSpeed;
        _isAttacking = false;

        StartCoroutine(BehaviorLoop());
    }

    public void StartAttacking(float damageMult, float dotMult, float dotDuration)
    {
        _damageMult  = damageMult;
        _dotMult     = dotMult;
        _dotDuration = dotDuration;
        _isAttacking = true;
    }

    public void StopAttacking()
    {
        _isAttacking = false;
    }

    // ── 통합 행동 루프 ────────────────────────────────────────────────

    private IEnumerator BehaviorLoop()
    {
        while (true)
        {
            if (!_isAttacking)
            {
                OrbitStep();
                yield return null;
            }
            else
            {
                var target = PickTarget();
                if (target == null) { OrbitStep(); yield return null; continue; }

                yield return StartCoroutine(MoveAndAttack(target));
            }
        }
    }

    // ── 궤도 비행 ─────────────────────────────────────────────────────

    private void OrbitStep()
    {
        if (_player == null) return;

        _orbitAngle += _orbitSpeed * Time.deltaTime;
        Vector2 orbitTarget = (Vector2)_player.position
            + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * _orbitRadius;

        FlipToward(orbitTarget.x >= transform.position.x);
        transform.position = Vector2.MoveTowards(transform.position, orbitTarget, _moveSpeed * Time.deltaTime);
    }

    // ── 공격 ──────────────────────────────────────────────────────────

    private EnemyController PickTarget()
    {
        var all = EnemyController.All;
        if (all.Count == 0) return null;

        EnemyController nearest   = null;
        float           minSqDist = float.MaxValue;
        foreach (var e in all)
        {
            if (e == null) continue;
            float sq = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sq < minSqDist) { minSqDist = sq; nearest = e; }
        }
        return nearest;
    }

    private IEnumerator MoveAndAttack(EnemyController target)
    {
        // 이동 — 현재 위치 기준으로 적의 반대편에서 접근해 두 박쥐가 겹치지 않음
        while (target != null)
        {
            Vector2 enemyPos = (Vector2)target.transform.position;
            Vector2 current  = (Vector2)transform.position;
            float   side     = enemyPos.x >= current.x ? 1f : -1f;
            Vector2 dest     = enemyPos + Vector2.right * (-side * 0.3f);

            if (Vector2.Distance(current, dest) <= ArriveThreshold) break;

            FlipToward(side > 0f);
            transform.position = Vector2.MoveTowards(current, dest, _moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (target == null) yield break;

        // 3타
        for (int i = 0; i < PecksPerCombo; i++)
        {
            if (target == null) yield break;
            target.TakeDamage(CalcHitDamage());
            yield return null;
        }

        // DoT
        if (target != null) target.ApplyPurpleDot(CalcDotDamage(), _dotDuration);
    }

    // ── 유틸 ──────────────────────────────────────────────────────────

    private void FlipToward(bool facingRight)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.flipX = !facingRight;
    }

    private float CalcHitDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return 0f;
        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        return total * _damageMult;
    }

    private float CalcDotDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return 0f;
        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        return total * _dotMult;
    }
}
