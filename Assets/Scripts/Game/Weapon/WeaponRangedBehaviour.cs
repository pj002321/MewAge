using System.Collections;
using UnityEngine;

/// <summary>원거리 투사체 발사 행동. 직선 또는 포물선 발사.</summary>
[System.Serializable]
public class WeaponRangedBehaviour : WeaponBehaviourBase
{
    [ProjectileID("Weapons")]
    [SerializeField] private string     projectileID;
    [System.NonSerialized] private GameObject _projectilePrefab;
    [SerializeField] private float      projectileSpeed     = 8f;
    [SerializeField] private bool       useParabolicShot    = false;
    [SerializeField] private float      parabolicArcHeight  = 1.5f;
    [SerializeField] private Vector3    parabolicSpawnLocalPos;

    [SerializeField] private bool  shootRecoil     = true;
    [SerializeField] private float recoilDist      = 0.15f;
    [SerializeField] private float recoilBackDur   = 0.06f;
    [SerializeField] private float recoilReturnDur = 0.14f;

    private Coroutine _recoilCoroutine;

    public override float      GetPlayerAttackBonus(PlayerController player) => player.RangedAttackBonus;
    public override float      GetRelicBonusPct(RelicManager relic)         => relic.RangedAtkBonusPct;
    public override GameObject GetProjectilePrefab()                         => _projectilePrefab;
    public override float      GetProjectileSpeed()                          => projectileSpeed;

    public override bool OverridesRotation => useParabolicShot;

    public override void OnStart()
    {
        base.OnStart();
        if (!string.IsNullOrEmpty(projectileID))
            _projectilePrefab = Resources.Load<GameObject>(projectileID);
    }

    public override void OnUpdate()
    {
        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;
        if (_projectilePrefab == null) return;

        var best = Host.FindBestTarget();
        if (best.IsNone) return;

        Shoot(best);
        Host.cooldownTimer = Host.attackCooldown;
    }

    private void Shoot(WeaponPoint.CombatTarget tgt)
    {
        Vector2 aimDir = ((Vector2)tgt.Position - (Vector2)Host.transform.position).normalized;
        if (!useParabolicShot) Host.SnapRotationToDir(aimDir);

        SoundManager.Shared?.Play(SoundKey.Weapon_Shoot);
        var (finalDmg, isCrit) = Host.RollCrit(Host.attackDamage);

        Vector3 spawnPos = useParabolicShot ? GetParabolicSpawnPos() : Host.transform.position;
        var go   = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Object.Destroy(go); return; }

        if (useParabolicShot)
        {
            proj.InitParabolic(finalDmg, tgt.Position, projectileSpeed, parabolicArcHeight,
                isPrimaryCritical: isCrit, lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
                criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
                baseDamage: Host.attackDamage, knockbackPower: Host.knockbackPower);
        }
        else
        {
            proj.Init(finalDmg, aimDir, projectileSpeed, isPrimaryCritical: isCrit,
                lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
                criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
                baseDamage: Host.attackDamage, knockbackPower: Host.knockbackPower,
                acceleration: Mathf.Clamp(projectileSpeed * 0.05f, 0.1f, 0.4f));
        }

        Host.ApplyShootSpecialEffect(proj, spawnPos, aimDir);
        Host.TriggerAttackAnimation();
        TriggerRecoil(aimDir);
        if (Host.Grade >= WeaponGrade.Legendary) Host.HighGradeSkill?.OnShoot(spawnPos, aimDir);

        // SYN_RANGE4_PROJECTILE_X2: 원거리 4개 이상 시 추가 투사체 발사 (±15°, 데미지 80%)
        if (WeaponSynergyFlags.RangeDoubleProjectile && !useParabolicShot)
            FireSynergyExtraProjectiles(spawnPos, aimDir, finalDmg * 0.8f, isCrit);
    }

    private void FireSynergyExtraProjectiles(Vector3 spawnPos, Vector2 aimDir, float dmg, bool isCrit)
    {
        Vector2 perpOffset = new Vector2(-aimDir.y, aimDir.x) * 0.25f;
        SpawnExtra(spawnPos + (Vector3)perpOffset,  RotateDir(aimDir,  15f), dmg, isCrit);
        SpawnExtra(spawnPos - (Vector3)perpOffset,  RotateDir(aimDir, -15f), dmg, isCrit);
    }

    private void SpawnExtra(Vector3 pos, Vector2 dir, float dmg, bool isCrit)
    {
        var go   = Object.Instantiate(_projectilePrefab, pos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Object.Destroy(go); return; }
        proj.Init(dmg, dir, projectileSpeed, isPrimaryCritical: isCrit,
            lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
            areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
            criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
            baseDamage: dmg, knockbackPower: Host.knockbackPower,
            acceleration: Mathf.Clamp(projectileSpeed * 0.05f, 0.1f, 0.4f));
    }

    private static Vector2 RotateDir(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r),
                           v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    private Vector3 GetParabolicSpawnPos()
    {
        var parent = Host.transform.parent != null ? Host.transform.parent : Host.transform;
        return parent.TransformPoint(parabolicSpawnLocalPos);
    }

    private void TriggerRecoil(Vector2 shootDir)
    {
        if (!shootRecoil) return;
        if (_recoilCoroutine != null) StopCoroutine(_recoilCoroutine);
        _recoilCoroutine = StartCoroutine(RecoilRoutine(shootDir));
    }

    private IEnumerator RecoilRoutine(Vector2 shootDir)
    {
        Vector3 originLocal = Host.originLocalPos;
        Vector3 backOffset  = Host.transform.parent != null
            ? Host.transform.parent.InverseTransformVector((Vector3)(-shootDir.normalized) * recoilDist)
            : (Vector3)(-shootDir.normalized) * recoilDist;
        Vector3 recoilPos = originLocal + backOffset;

        for (float t = 0f; t < recoilBackDur; t += Time.deltaTime)
        {
            Host.transform.localPosition = Vector3.Lerp(originLocal, recoilPos, t / recoilBackDur);
            yield return null;
        }
        Host.transform.localPosition = recoilPos;

        for (float t = 0f; t < recoilReturnDur; t += Time.deltaTime)
        {
            Host.transform.localPosition = Vector3.Lerp(recoilPos, originLocal, t / recoilReturnDur);
            yield return null;
        }
        Host.transform.localPosition = originLocal;
        _recoilCoroutine = null;
    }
}
