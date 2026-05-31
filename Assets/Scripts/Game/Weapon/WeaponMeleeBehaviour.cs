using UnityEngine;

/// <summary>
/// 근거리(Melee) 타격 무기 행동.
/// Pullback(느리게 수축) → 적 머리로 고속 직선 타격 → Returning.
/// </summary>
[System.Serializable]
public class WeaponMeleeBehaviour : WeaponMeleeBaseBehaviour
{
    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    // 타격 목표 (적 머리) — BeginStrikeSetup에서 고정
    private Vector3 _headTarget;
    private bool    _headTargetReady;
    private Vector2 _toTarget;
    private Vector2 _lastHitDir;

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        _target   = best;
        _toTarget = ((Vector2)best.Position - (Vector2)Host.transform.parent.position).normalized;
        if (_toTarget.sqrMagnitude < 0.001f) _toTarget = Vector2.up;

        _headTargetReady = false;
        // 타겟 반대 방향으로 수축 — 팔을 뒤로 당기는 느낌
        StartPullback(best, -(Vector3)_toTarget * pullbackDist);
    }

    // 풀백 완료 시 타격 목표(적 머리)를 확정
    protected override void OnBeginStrikeSetup()
    {
        base.OnBeginStrikeSetup();
        _headTarget      = GetEnemyHeadPosition();
        _headTargetReady = true;
    }

    // 적 머리를 향해 직선 고속 타격
    protected override void UpdateStriking()
    {
        if (!_target.IsValid)
        {
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            BeginReturn(false);
            return;
        }

        // 보스가 사정거리 초과하면 취소
        if (_target.Boss != null &&
            ((Vector2)_target.Position - (Vector2)Host.transform.parent.position).sqrMagnitude
            > Host.attackRange * Host.attackRange * 4f)
        {
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            BeginReturn(false);
            return;
        }

        Vector3 dest = _headTargetReady ? _headTarget : _target.Position;

        Host.transform.position = Vector3.MoveTowards(
            Host.transform.position, dest, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(Host.transform.position, dest) <= hitDistance)
        {
            Vector2 hitDir = ((Vector2)(dest - (Vector3)Host.transform.parent.position)).normalized;
            if (hitDir.sqrMagnitude < 0.001f) hitDir = _toTarget;
            _lastHitDir = hitDir;

            OnStrikeHit(_target, hitDir);
            _overshootTarget = dest + (Vector3)(hitDir * overshootDistance);
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            EnterOvershooting();
        }
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);

    public override Vector2? GetAttackLookDir()
    {
        if (_inWaveIntro || _state == State.Pullback) return _toTarget;  // 당기는 중에도 타겟 방향 유지 (뒤집힘 방지)
        if (_state == State.Idle)                     return null;
        if (_state == State.Striking)                 return _toTarget;
        if (_lastHitDir.sqrMagnitude > 0.001f)        return _lastHitDir;
        return _toTarget;
    }

    // 적 콜라이더 상단(머리) 좌표
    private Vector3 GetEnemyHeadPosition()
    {
        if (_target.Enemy != null)
        {
            var col = _target.Enemy.GetComponentInChildren<Collider2D>();
            if (col != null)
                return new Vector3(col.bounds.center.x, col.bounds.max.y, 0f);
            return (Vector3)_target.Enemy.transform.position + Vector3.up * 0.4f;
        }
        if (_target.Boss != null)
        {
            var col = _target.Boss.GetComponentInChildren<Collider2D>();
            if (col != null)
                return new Vector3(col.bounds.center.x, col.bounds.max.y, 0f);
            return (Vector3)_target.Boss.transform.position + Vector3.up * 0.8f;
        }
        return _target.Position;
    }
}
