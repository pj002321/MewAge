using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>플레이어 주위를 공전하며 닿는 적을 타격하는 돌리기 행동.</summary>
[System.Serializable]
public class WeaponSpinBehaviour : WeaponBehaviourBase
{
    [SerializeField] private float orbitRadius     = 1.2f;
    [SerializeField] private float orbitSpeed      = 200f;
    [SerializeField] private float spinHitRadius   = 0.35f;
    [SerializeField] private float spinHitCooldown = 0.5f;
    [SerializeField] private float spinKnockback   = 7f;

    // ── 런타임 상태 ──────────────────────────────────────────────────────
    private float currentAngle;
    private readonly Dictionary<EnemyController, float> _hitTimers  = new();
    private readonly List<EnemyController>               _expiredKeys = new();
    private readonly List<EnemyController>               _keys        = new();
    private bool  _hasCollider;
    private float _bossSpinHitTimer;

    // HIT_SPIN 부스트
    private int   _boostStack;
    private float _boostOrbit;
    private float _boostSpeed;
    private float _boostTimer;
    private const int   BoostMaxStack = 5;
    private const float BoostDecay    = 3f;

    private readonly List<GameObject> _ghosts = new();

    private BossController _cachedBossForCol;
    private Collider2D     _cachedBossCol;

    public override float GetRelicBonusPct(RelicManager relic) => relic.SpinAtkBonusPct;

    public override void OnInitData(WeaponData data)
    {
        orbitSpeed    = -Mathf.Abs(orbitSpeed);
        spinKnockback = data.knockbackPower;
    }

    public override void OnStart()
    {
        base.OnStart();
        _hasCollider = Host.GetComponent<Collider2D>() != null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        foreach (var g in _ghosts) if (g != null) Object.Destroy(g);
        _ghosts.Clear();
    }

    public override void OnUpdate()
    {
        if (_inWaveIntro) return;

        // 부스트 타이머
        if (_boostTimer > 0f)
        {
            _boostTimer -= Time.deltaTime;
            if (_boostTimer <= 0f) { _boostStack = 0; _boostOrbit = 0f; _boostSpeed = 0f; }
        }

        float effectOrbitSpeed  = orbitSpeed  + _boostSpeed;
        float effectOrbitRadius = orbitRadius + _boostOrbit;
        currentAngle += effectOrbitSpeed * Time.deltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;
        Host.transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * effectOrbitRadius;
        Host.transform.rotation      = Quaternion.Euler(0f, 0f, currentAngle - 90f);

        // 보스 쿨타임
        if (_bossSpinHitTimer > 0f) _bossSpinHitTimer -= Time.deltaTime;

        // 적 재타격 쿨타임 감소
        _keys.Clear();
        _keys.AddRange(_hitTimers.Keys);
        for (int i = 0; i < _keys.Count; i++)
        {
            var key = _keys[i];
            _hitTimers[key] -= Time.deltaTime;
            if (_hitTimers[key] <= 0f) _expiredKeys.Add(key);
        }
        for (int i = 0; i < _expiredKeys.Count; i++) _hitTimers.Remove(_expiredKeys[i]);
        _expiredKeys.Clear();

        SpinBossDirectHit();
        if (!_hasCollider) SpinOverlapHit();
    }

    public override void OnTriggerHit(Collider2D col) => SpinProcessHit(col);

    // ── 공격 처리 ────────────────────────────────────────────────────────
    private void SpinBossDirectHit()
    {
        if (_bossSpinHitTimer > 0f) return;
        var boss = BossController.Current;
        if (boss == null || !boss.gameObject.activeInHierarchy) return;

        Vector2 weaponPos = Host.transform.position;
        if (_cachedBossForCol != boss)
        {
            _cachedBossForCol = boss;
            _cachedBossCol    = boss.GetComponentInChildren<Collider2D>();
        }
        float dist = _cachedBossCol != null
            ? ((Vector2)_cachedBossCol.ClosestPoint(weaponPos) - weaponPos).magnitude
            : Vector2.Distance(weaponPos, (Vector2)boss.transform.position);

        if (dist > spinHitRadius) return;

        var (bossDmg, bossCrit) = Host.RollCrit(Host.attackDamage);
        Vector2 bossKnock = (boss.transform.position - Host.transform.position).normalized;
        boss.TakeDamage(bossDmg, bossCrit, bossKnock, spinKnockback);
        Host.ApplyLifeSteal(bossDmg);
        Host.TrySpecialEffectBoss(boss, bossDmg);
        _bossSpinHitTimer = spinHitCooldown;
    }

    private void SpinOverlapHit()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(Host.transform.position, spinHitRadius, WeaponPoint.SharedHitBuffer);
        for (int i = 0; i < hitCount; i++) SpinProcessHit(WeaponPoint.SharedHitBuffer[i]);
    }

    private void SpinProcessHit(Collider2D col)
    {
        var boss = col.GetComponentInParent<BossController>();
        if (boss != null && _bossSpinHitTimer <= 0f)
        {
            var (bossDmg, bossCrit) = Host.RollCrit(Host.attackDamage);
            Vector2 bossKnock = (boss.transform.position - Host.transform.position).normalized;
            boss.TakeDamage(bossDmg, bossCrit, bossKnock, spinKnockback);
            Host.ApplyLifeSteal(bossDmg);
            Host.TrySpecialEffectBoss(boss, bossDmg);
            _bossSpinHitTimer = spinHitCooldown;
            return;
        }

        var enemy = col.GetComponentInParent<EnemyController>();
        if (enemy == null || _hitTimers.ContainsKey(enemy)) return;

        var (finalDmg, isCrit) = Host.RollCrit(Host.attackDamage);
        Vector2 knockDir = (enemy.transform.position - Host.transform.position).normalized;
        enemy.TakeDamage(finalDmg, isCrit, knockDir, spinKnockback);
        Host.ApplyLifeSteal(finalDmg);
        if (Host.dotDamage > 0f) enemy.ApplyBleed(Host.dotDamage);
        Host.TrySpecialEffectEnemy(enemy, finalDmg, knockDir);
        SoundManager.Shared?.PlayWeaponHit(Host.WeaponId);
        _hitTimers[enemy] = spinHitCooldown;
    }

    // ── HIT_SPIN 부스트 ──────────────────────────────────────────────────
    public void ApplySpinBoost()
    {
        _boostStack = Mathf.Min(_boostStack + 1, BoostMaxStack);
        float sign  = orbitSpeed < 0 ? -1f : 1f;
        _boostOrbit = _boostStack * 0.25f;
        _boostSpeed = _boostStack * sign * 40f;
        _boostTimer = BoostDecay;
        StartSpinGhosts();
    }

    public void ApplyOrbitMultiplier(float radiusMult, float speedMult)
    {
        orbitRadius *= radiusMult;
        orbitSpeed  *= speedMult;
    }

    private void StartSpinGhosts()
    {
        StartCoroutine(SpinGhostRoutine());
    }

#if UNITY_EDITOR
    public override void DrawGizmos(Vector3 playerCenter, Vector3 weaponPos)
    {
        // 공전 궤도 (플레이어 기준)
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Handles.DrawWireDisc(playerCenter, Vector3.forward, orbitRadius);

        // 타격 판정 반경 (무기 정중앙)
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(weaponPos, spinHitRadius);
    }
#endif

    private IEnumerator SpinGhostRoutine()
    {
        var sr = Host.WeaponSr;
        if (sr == null || sr.sprite == null) yield break;

        int ghostCount = _boostStack;
        for (int i = 0; i < ghostCount; i++)
        {
            var go  = new GameObject("SpinGhost");
            var gsr = go.AddComponent<SpriteRenderer>();
            gsr.sprite         = sr.sprite;
            gsr.sortingLayerID = sr.sortingLayerID;
            gsr.sortingOrder   = sr.sortingOrder - 1;
            gsr.color          = new Color(1f, 1f, 1f, 0.4f);
            go.transform.position   = Host.transform.position;
            go.transform.rotation   = Host.transform.rotation;
            go.transform.localScale = Host.transform.lossyScale;
            _ghosts.Add(go);

            float dur = 0.4f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (go == null) break;
                Color c = gsr.color;
                c.a       = Mathf.Lerp(0.4f, 0f, t / dur);
                gsr.color = c;
                yield return null;
            }
            if (go != null) { _ghosts.Remove(go); Object.Destroy(go); }
        }
    }
}
