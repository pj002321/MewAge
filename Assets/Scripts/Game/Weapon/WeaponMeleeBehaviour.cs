using UnityEngine;

/// <summary>근거리(Melee) 타격 무기 행동. Pullback → Striking → HitArea → Returning.</summary>
[System.Serializable]
public class WeaponMeleeBehaviour : WeaponMeleeBaseBehaviour
{
    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        _target = best;
        Vector2 toTarget = ((Vector2)best.Position - (Vector2)Host.transform.parent.position).normalized;
        StartPullback(best, -(Vector3)toTarget * pullbackDist);
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);
}
