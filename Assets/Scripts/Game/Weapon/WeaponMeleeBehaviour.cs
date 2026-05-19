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

        var col = PlayerController.Instance != null
            ? PlayerController.Instance.GetComponent<Collider2D>()
            : null;
        Vector3 centerWorld = col != null ? col.bounds.center : Host.transform.parent.position;
        Vector3 pullbackLocal = Host.transform.parent.InverseTransformPoint(centerWorld);

        StartPullback(best, pullbackLocal);
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);
}
