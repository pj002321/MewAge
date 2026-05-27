using System.Collections.Generic;
using UnityEngine;

/// <summary>부메랑 행동. 대상 체인 바운스 후 귀환.</summary>
[System.Serializable]
public class WeaponBoomerangBehaviour : WeaponMeleeBaseBehaviour
{
    [SerializeField] private int   maxBounces  = 3;
    [SerializeField] private float bounceRange = 5f;

    private WeaponPoint.CombatTarget _boomerang;
    private int                      _bouncesLeft;
    private readonly HashSet<EnemyController> _hitEnemies = new();
    private readonly HashSet<BossController>  _hitBosses  = new();

    public override float GetPlayerAttackBonus(PlayerController player) => player.RangedAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.BoomerangAtkBonusPct;

    public override void OnInitData(WeaponData data)
    {
        maxBounces  = data.maxBounces  > 0 ? data.maxBounces  : maxBounces;
        bounceRange = data.bounceRange > 0 ? data.bounceRange : bounceRange;
    }

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        _boomerang   = best;
        _bouncesLeft = GetTotalBounces();
        _hitEnemies.Clear();
        _hitBosses.Clear();

        Vector2 toBoomerang = ((Vector2)_boomerang.Position - (Vector2)Host.transform.parent.position).normalized;
        StartPullback(best, -(Vector3)toBoomerang * pullbackDist);
    }

    protected override WeaponPoint.CombatTarget GetActiveTarget() => _boomerang;

    protected override void OnBeginStrikeSetup() { /* 부메랑은 애니메이션 없음 */ }

    protected override void UpdateStriking()
    {
        if (_boomerang.IsNone) { BeginReturn(); return; }
        if (!_boomerang.IsValid) { _boomerang = WeaponPoint.CombatTarget.None; BeginReturn(false); return; }

        Vector3 targetPos = _boomerang.Position;
        Host.transform.position = Vector3.MoveTowards(
            Host.transform.position, targetPos, strikeSpeed * 0.35f * Time.deltaTime);

        if (Vector3.Distance(Host.transform.position, targetPos) > hitDistance) return;

        Vector2 hitDir       = ((Vector2)targetPos - (Vector2)Host.transform.parent.position).normalized;
        int     bouncesDone  = GetTotalBounces() - _bouncesLeft;
        Host.HitAreaBoomerang(hitDir, bouncesDone, _boomerang, _hitEnemies, _hitBosses);
        _bouncesLeft--;

        if (_bouncesLeft <= 0) { _boomerang = WeaponPoint.CombatTarget.None; BeginReturn(); return; }

        var next = FindNextTarget(targetPos);
        if (!next.IsNone)
        {
            _boomerang = next;
            _state     = State.Striking;
        }
        else
        {
            _boomerang = WeaponPoint.CombatTarget.None;
            BeginReturn();
        }
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
    {
        // 부메랑은 UpdateStriking()에서 직접 처리하므로 이 메서드는 사용하지 않습니다.
    }

    private int GetTotalBounces()
    {
        if (maxBounces > 0) return maxBounces;
        int total = EnemyController.All.Count + (BossController.Current != null ? 1 : 0);
        return Mathf.Max(3, total);
    }

    private WeaponPoint.CombatTarget FindNextTarget(Vector2 fromPos)
    {
        EnemyController nearestEnemy = null;
        BossController  nearestBoss  = null;
        float minDist = float.MaxValue;

        foreach (var e in EnemyController.All)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            if (_hitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist && d <= bounceRange) { minDist = d; nearestEnemy = e; }
        }

        var boss = BossController.Current;
        if (boss != null && boss.gameObject.activeInHierarchy && !_hitBosses.Contains(boss))
        {
            float d = Vector2.Distance(fromPos, boss.transform.position);
            if (d < minDist && d <= bounceRange) { minDist = d; nearestBoss = boss; nearestEnemy = null; }
        }

        if (nearestBoss  != null) return new WeaponPoint.CombatTarget(nearestBoss);
        if (nearestEnemy != null) return new WeaponPoint.CombatTarget(nearestEnemy);
        return WeaponPoint.CombatTarget.None;
    }
}
