using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마법 타입 행동.
/// - None: Melee 상태머신 + HitArea
/// - Lightning: 즉발 번개 연타
/// - Fire: 유도 화염 투사체
/// </summary>
[System.Serializable]
public class WeaponMagicBehaviour : WeaponMeleeBaseBehaviour
{
    [SerializeField] private MagicElement magicElement = MagicElement.None;

    // ── Lightning ─────────────────────────────────────────────────────
    [SerializeField] private Transform   muzzlePoint;
    [SerializeField] private GameObject  zapBoltVfxPrefab;
    [SerializeField] private int         lightningBurstCount    = 3;
    [SerializeField] private float       lightningBurstInterval = 0.07f;
    [SerializeField] private float       zapLineHitRadius       = 0.25f;

    private static readonly RaycastHit2D[]          _zapLineBuffer  = new RaycastHit2D[16];
    private static readonly HashSet<EnemyController> _zapHitEnemies = new();
    private GameObject _activeBolt;

    // ── Fire ─────────────────────────────────────────────────────────
    [ProjectileID]
    [SerializeField] private string     projectileID;
    [System.NonSerialized] private GameObject _projectilePrefab;
    [SerializeField] private float      projectileSpeed = 8f;

    // ── Visual ────────────────────────────────────────────────────────
    private SpriteRenderer _weaponSr;
    private Coroutine      _srColorCoroutine;
    private static readonly Color _fireRedColor = new Color(1f, 0.2f, 0.05f);

    private Transform MuzzleTransform => muzzlePoint != null ? muzzlePoint : Host.transform;

    public override float      GetPlayerAttackBonus(PlayerController player) => 0f;
    public override float      GetRelicBonusPct(RelicManager relic)         => relic.MagicAtkBonusPct;
    public override GameObject GetProjectilePrefab()                         => _projectilePrefab;
    public override float      GetProjectileSpeed()                          => projectileSpeed;

    public override void OnStart()
    {
        base.OnStart();
        _weaponSr = Host.WeaponSr;
        if (muzzlePoint != null) muzzlePoint.gameObject.SetActive(false);
        if (!string.IsNullOrEmpty(projectileID))
            _projectilePrefab = Resources.Load<GameObject>(projectileID);
    }

    // ── Lightning / Fire: Idle에서 직접 발동 후 쿨타임 처리 ──────────
    protected override void OnUpdateIdle()
    {
        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;

        var best = Host.FindBestTarget();
        if (best.IsNone) return;

        switch (magicElement)
        {
            case MagicElement.Lightning:
                StartCoroutine(BurstZapRoutine(MuzzleTransform, best.Enemy, best.Boss));
                Host.cooldownTimer = Host.attackCooldown;
                return;
            case MagicElement.Fire:
                if (_projectilePrefab != null)
                {
                    FireHoming(best);
                    Host.cooldownTimer = Host.attackCooldown;
                    return;
                }
                break;
        }

        // None (또는 Fire 프리팹 없음): Melee 상태머신 진입
        BeginAttack(best);
    }

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        _target = best;
        Vector2 toTarget = ((Vector2)best.Position - (Vector2)Host.transform.parent.position).normalized;
        StartPullback(best, -(Vector3)toTarget * pullbackDist);
    }

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);

    // ── Lightning ─────────────────────────────────────────────────────
    private IEnumerator BurstZapRoutine(Transform muzzle, EnemyController enemy, BossController boss)
    {
        if (muzzlePoint != null) muzzlePoint.gameObject.SetActive(true);

        for (int i = 0; i < lightningBurstCount; i++)
        {
            Transform targetTf = enemy != null ? enemy.transform : boss?.transform;
            if (targetTf == null) break;
            bool alive = enemy != null
                ? enemy.gameObject.activeInHierarchy
                : boss != null && boss.gameObject.activeInHierarchy;
            if (!alive) break;

            SpawnZapBolt(muzzle, targetTf);
            ZapLineDamage(muzzle.position, targetTf.position, enemy, boss);
            SoundManager.Shared?.Play(SoundKey.Structure_Thunder);
            if (i == 0) Host.TriggerScalePulse();

            if (i < lightningBurstCount - 1)
                yield return new WaitForSeconds(lightningBurstInterval);
        }

        if (muzzlePoint != null) muzzlePoint.gameObject.SetActive(false);
    }

    private void ZapLineDamage(Vector3 from, Vector3 to, EnemyController primary, BossController primaryBoss)
    {
        Vector2 dir      = ((Vector2)(to - from)).normalized;
        float   dist     = Vector2.Distance(from, to);
        int     hitCount = Physics2D.CircleCastNonAlloc(from, zapLineHitRadius, dir, _zapLineBuffer, dist);

        _zapHitEnemies.Clear();
        bool bossHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            var col = _zapLineBuffer[i].collider;
            if (col == null) continue;

            if (!bossHit)
            {
                var b = col.GetComponentInParent<BossController>();
                if (b != null)
                {
                    bossHit = true;
                    var (dmg, crit) = Host.RollCrit(b == primaryBoss
                        ? Host.attackDamage : Host.attackDamage * Host.areaHitDamageRatio);
                    b.TakeDamage(dmg, crit, dir, Host.knockbackPower);
                    Host.ApplyLifeSteal(dmg);
                    continue;
                }
            }

            var e = col.GetComponentInParent<EnemyController>();
            if (e == null || _zapHitEnemies.Contains(e)) continue;
            _zapHitEnemies.Add(e);

            var (dmg2, crit2) = Host.RollCrit(e == primary
                ? Host.attackDamage : Host.attackDamage * Host.areaHitDamageRatio);
            e.TakeDamage(dmg2, crit2, dir, Host.knockbackPower);
            Host.ApplyLifeSteal(dmg2);
            if (Host.dotDamage > 0f) e.ApplyBleed(Host.dotDamage);
        }
    }

    private void SpawnZapBolt(Transform from, Transform to)
    {
        if (zapBoltVfxPrefab == null) return;
        if (_activeBolt != null) Object.Destroy(_activeBolt);
        var go  = Object.Instantiate(zapBoltVfxPrefab, from.position, Quaternion.identity);
        var vis = go.GetComponent<LightningVisual>();
        if (vis != null) vis.Setup(from, to);
        else             Object.Destroy(go, 1f);
        _activeBolt = go;
    }

    // ── Fire ─────────────────────────────────────────────────────────
    private void FireHoming(WeaponPoint.CombatTarget tgt)
    {
        var (finalDmg, isCrit) = Host.RollCrit(Host.attackDamage);
        var go     = Object.Instantiate(_projectilePrefab, Host.transform.position, Quaternion.identity);
        var homing = go.GetComponent<FireHomingProjectile>();
        if (homing == null) homing = go.AddComponent<FireHomingProjectile>();

        if (tgt.Enemy != null)
            homing.Init(finalDmg, projectileSpeed, tgt.Enemy,
                lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                knockbackPower: Host.knockbackPower,
                critRate: Host.criticalRate, critMult: Host.criticalMultiplier);
        else if (tgt.Boss != null)
            homing.InitBoss(finalDmg, projectileSpeed, tgt.Boss,
                lifeSteal: Host.lifeSteal, dotDamage: Host.dotDamage,
                knockbackPower: Host.knockbackPower,
                critRate: Host.criticalRate, critMult: Host.criticalMultiplier);

        TriggerFireSpriteColor();
        Host.TriggerScalePulse();
    }

    private void TriggerFireSpriteColor()
    {
        if (_weaponSr == null) return;
        if (_srColorCoroutine != null) Host.StopCoroutine(_srColorCoroutine);
        _srColorCoroutine = StartCoroutine(FireSpriteColorRoutine());
    }

    private IEnumerator FireSpriteColorRoutine()
    {
        Color orig = _weaponSr.color;
        float half = Host.attackCooldown * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _weaponSr.color = Color.Lerp(orig, _fireRedColor, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _weaponSr.color = Color.Lerp(_fireRedColor, orig, t / half);
            yield return null;
        }
        _weaponSr.color     = orig;
        _srColorCoroutine   = null;
    }
}
