using UnityEngine;

/// <summary>차징 후 강타 행동. 차징 타이머 → Pullback → Striking.</summary>
[System.Serializable]
public class WeaponChargeBehaviour : WeaponMeleeBaseBehaviour
{
    [SerializeField] private float chargeDuration = 1f;

    private float _chargeTimer;
    private bool  _isCharging;

    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        _target       = best;
        _chargeTimer  = chargeDuration;
        _isCharging   = true;
        Host.cooldownTimer = Host.attackCooldown;
        Host.TriggerAttackAnimation();
        // Charge 전용 상태: 타이머 후 Pullback으로 전환
    }

    protected override void OnUpdateIdle()
    {
        if (_isCharging)
        {
            // 차징 타이머 처리 (Idle 상태에서 대기)
            if (!_target.IsValid) { _isCharging = false; return; }

            _chargeTimer -= Time.deltaTime;
            if (_chargeTimer <= 0f)
            {
                _isCharging = false;
                Vector2 toTarget  = ((Vector2)_target.Position - (Vector2)Host.transform.parent.position).normalized;
                StartPullback(_target, -(Vector3)toTarget * pullbackDist);
            }
            return;
        }

        base.OnUpdateIdle();
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);

    protected override void OnBeginStrikeSetup() => Host.TriggerAttackAnimation();
}
