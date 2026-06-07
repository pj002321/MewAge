using System.Collections;
using UnityEngine;

/// <summary>원거리 투사체 발사 행동. 직선 또는 포물선 발사.</summary>
[System.Serializable]
public class WeaponRangedBehaviour : WeaponBehaviourBase
{
    // 기본 투사체 속도 — Override 체크 해제 시 모든 원거리 무기에 통일 적용
    private const float DefaultProjectileSpeed = 25f;

    [ProjectileID("Weapons")]
    [SerializeField] private string     projectileID;
    [System.NonSerialized] private GameObject _projectilePrefab;
    [SerializeField] private bool       useParabolicShot    = false;
    [SerializeField] private float      parabolicArcHeight  = 1.5f;
    [SerializeField] private Vector3    parabolicSpawnLocalPos;

    [SerializeField] private bool  useGrenadeMode  = false;

    [Header("부채꼴 발사")]
    [SerializeField] private bool  useFanShot      = false;
    [SerializeField] [Range(2, 7)] private int   fanCount     = 3;
    [SerializeField] private float fanAngleSpan    = 60f;
    [SerializeField] [Range(0.1f, 1f)] private float fanDamageRatio = 1f;

    [SerializeField] private bool  shootRecoil     = true;
    [SerializeField] private float recoilDist      = 0.15f;
    [SerializeField] private float recoilBackDur   = 0.06f;
    [SerializeField] private float recoilReturnDur = 0.14f;

    [Header("속도 오버라이드")]
    [SerializeField] private bool  _overrideSpeed    = false;
    [SerializeField] private float projectileSpeed   = DefaultProjectileSpeed;

    private float EffectiveSpeed => _overrideSpeed ? projectileSpeed : DefaultProjectileSpeed;

    private Coroutine  _recoilCoroutine;
    private Coroutine  _vfxCoroutine;
    [SerializeField] private GameObject _shootVfx;
    [SerializeField] private float      _shootVfxDuration = 0.15f;

    public override float      GetPlayerAttackBonus(PlayerController player) => player.RangedAttackBonus;
    public override float      GetRelicBonusPct(RelicManager relic)         => relic.RangedAtkBonusPct;
    public override GameObject GetProjectilePrefab()                         => _projectilePrefab;
    public override float      GetProjectileSpeed()                          => EffectiveSpeed;

    public override bool OverridesRotation => useParabolicShot;

    public override void OnStart()
    {
        base.OnStart();
        if (!string.IsNullOrEmpty(projectileID))
            _projectilePrefab = Resources.Load<GameObject>(projectileID);

        if (_shootVfx != null) _shootVfx.SetActive(false);
    }

    public override void OnUpdate()
    {
        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;
        if (!useGrenadeMode && _projectilePrefab == null) return;

        var best = Host.FindBestTarget();
        if (best.IsNone) return;

        Shoot(best);
        Host.cooldownTimer = Host.attackCooldown;
    }

    private void Shoot(WeaponPoint.CombatTarget tgt)
    {
        if (useGrenadeMode) { ShootGrenade(tgt); return; }

        Vector2 aimDir   = ((Vector2)tgt.Position - (Vector2)Host.transform.position).normalized;
        Vector3 spawnPos = useParabolicShot ? GetParabolicSpawnPos() : Host.transform.position;

        if (!useParabolicShot) Host.SnapRotationToDir(aimDir);
        SoundManager.Shared?.PlayWeaponShoot(Host.WeaponId);

        if (useFanShot && !useParabolicShot)
        {
            ShootFan(spawnPos, aimDir);
        }
        else
        {
            var (finalDmg, isCrit) = Host.RollCrit(Host.attackDamage);
            float spd = EffectiveSpeed;
            var go    = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            var proj  = go.GetComponent<Projectile>();
            if (proj == null) { Object.Destroy(go); return; }

            if (useParabolicShot)
            {
                proj.InitParabolic(finalDmg, tgt.Position, spd, parabolicArcHeight,
                    isPrimaryCritical: isCrit, lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                    areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
                    criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
                    baseDamage: Host.attackDamage, knockbackPower: Host.knockbackPower);
            }
            else
            {
                proj.Init(finalDmg, aimDir, spd, isPrimaryCritical: isCrit,
                    lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                    areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
                    criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
                    baseDamage: Host.attackDamage, knockbackPower: Host.knockbackPower,
                    weaponId: Host.WeaponId);
                Host.HighGradeSkill?.OnProjectileSpawned(proj);
            }

            Host.ApplyShootSpecialEffect(proj, spawnPos, aimDir);
            if (WeaponSynergyFlags.RangeDoubleProjectile && !useParabolicShot)
                FireSynergyExtraProjectiles(spawnPos, aimDir, finalDmg * 0.8f, isCrit);
        }

        Host.TriggerAttackAnimation();
        TriggerRecoil(aimDir);
        Host.HighGradeSkill?.OnBeginAttack(aimDir);
        if (Host.Grade >= WeaponGrade.Legendary) Host.HighGradeSkill?.OnShoot(spawnPos, aimDir);
        PlayShootVfx();
    }

    private void ShootFan(Vector3 spawnPos, Vector2 centerDir)
    {
        float spd      = EffectiveSpeed;
        float halfSpan = fanAngleSpan * 0.5f;
        float step     = fanCount > 1 ? fanAngleSpan / (fanCount - 1) : 0f;
        float baseDmg  = Host.attackDamage * fanDamageRatio;

        for (int i = 0; i < fanCount; i++)
        {
            float angle = fanCount > 1 ? -halfSpan + step * i : 0f;
            Vector2 dir = RotateDir(centerDir, angle);

            var go   = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj == null) { Object.Destroy(go); continue; }

            var (finalDmg, isCrit) = Host.RollCrit(baseDmg);
            proj.Init(finalDmg, dir, spd, isPrimaryCritical: isCrit,
                lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
                criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
                baseDamage: baseDmg, knockbackPower: Host.knockbackPower,
                weaponId: Host.WeaponId);
            Host.HighGradeSkill?.OnProjectileSpawned(proj);

            Host.ApplyShootSpecialEffect(proj, spawnPos, dir);
        }
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
        proj.Init(dmg, dir, EffectiveSpeed, isPrimaryCritical: isCrit,
            lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
            areaHitRadius: Host.areaHitRadius, areaHitDamageRatio: Host.areaHitDamageRatio,
            criticalRate: Host.criticalRate, criticalMultiplier: Host.criticalMultiplier,
            baseDamage: dmg, knockbackPower: Host.knockbackPower, weaponId: Host.WeaponId);
    }

    private void PlayShootVfx()
    {
        if (_shootVfx == null) return;
        if (_vfxCoroutine != null) StopCoroutine(_vfxCoroutine);
        _vfxCoroutine = StartCoroutine(ShootVfxRoutine());
    }

    private IEnumerator ShootVfxRoutine()
    {
        _shootVfx.SetActive(true);
        yield return new WaitForSeconds(_shootVfxDuration);
        _shootVfx.SetActive(false);
        _vfxCoroutine = null;
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

    private void ShootGrenade(WeaponPoint.CombatTarget tgt)
    {
        var weaponSr = Host.WeaponSr;
        if (weaponSr == null || weaponSr.sprite == null) return;

        SoundManager.Shared?.PlayWeaponShoot(Host.WeaponId);
        var (finalDmg, isCrit) = Host.RollCrit(Host.attackDamage);

        weaponSr.enabled = false;
        var trail = Host.GetComponent<TrailRenderer>();
        if (trail != null) trail.emitting = false;

        var go = new GameObject("GrenadeProjectile");
        go.transform.position = Host.transform.position;
        var grenade = go.AddComponent<GrenadeProjectile>();
        grenade.Init(
            weaponSr,
            finalDmg, Host.attackDamage, isCrit,
            Host.areaHitRadius, Host.areaHitDamageRatio,
            Host.criticalRate, Host.criticalMultiplier,
            Host.knockbackPower, Host.dotDamage, Host.lifeSteal,
            Host.WeaponId,
            tgt.Position, EffectiveSpeed, parabolicArcHeight,
            () =>
            {
                if (weaponSr != null) weaponSr.enabled = true;
                if (trail != null) trail.emitting = true;
            });

        Host.TriggerAttackAnimation();
        if (Host.Grade >= WeaponGrade.Legendary)
        {
            Vector2 aimDir = ((Vector2)tgt.Position - (Vector2)Host.transform.position).normalized;
            Host.HighGradeSkill?.OnShoot(Host.transform.position, aimDir);
        }
        PlayShootVfx();
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
