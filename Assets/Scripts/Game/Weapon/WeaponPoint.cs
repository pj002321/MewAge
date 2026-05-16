using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 자식(weaponPoint)에 부착. 스탯·서비스·특수효과 코디네이터.
/// 공격 동작은 [SerializeReference] _behaviour에 위임합니다.
/// </summary>
public class WeaponPoint : MonoBehaviour
{
    // ── 행동 (Inspector 드롭다운으로 타입 선택) ─────────────────────
    [SerializeReference] private WeaponBehaviourBase _behaviour;

    // ── DB_무기에서 Init()으로 주입 (Inspector 설정 불필요) ─────────
    internal float attackDamage;
    internal float attackCooldown;
    internal float attackRange;
    internal float areaHitRadius;
    internal float areaHitDamageRatio = 0.5f;
    internal float criticalRate;
    internal float criticalMultiplier = 2f;
    internal float knockbackPower;
    internal float dotDamage;
    internal float lifeSteal;
    internal float cooldownTimer;
    internal Vector3 originLocalPos;

    // ── 읽기 전용 프로퍼티 (WeaponInfoPanel용) ───────────────────────
    public float AttackDamage   => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float AttackRange    => attackRange;
    public float CriticalRate   => criticalRate;
    public float CriticalDamage => criticalMultiplier;
    public float BleedDamage    => dotDamage;
    public float LifeSteal      => lifeSteal;
    public float KnockbackPower => knockbackPower;
    public float ProjectileSpeed { get; private set; }
    public float AreaDamageRatio => areaHitDamageRatio;

    // ── 잔상 ─────────────────────────────────────────────────────────
    [SerializeField] private bool  useTrail        = true;
    [SerializeField] private float trailTime       = 0.10f;
    [SerializeField] private float trailStartWidth = 0.28f;
    [SerializeField] private Color trailColor      = new Color(1f, 1f, 1f, 0.55f);

    // ── 스케일 펄스 ──────────────────────────────────────────────────
    [SerializeField] private bool  shootScalePulse    = true;
    [SerializeField] private float scalePulseMax      = 1.3f;
    [SerializeField] private float scalePulseDuration = 0.12f;

    // ── 런타임 ───────────────────────────────────────────────────────
    private bool              _initialized;
    internal WeaponGrade      _grade = WeaponGrade.Common;
    internal WeaponLegendarySkill _legendarySkill;
    internal WeaponMythicSkill    _mythicSkill;
    private SpriteRenderer    _weaponSr;
    private TrailRenderer     _trail;
    private Vector3           _originalScale;
    private Coroutine         _scalePulseCoroutine;
    private float             _baseCooldown;
    private float             _statMultiplier  = 1f;
    private float             _bonusMultiplier = 1f;

    // ── 내부 접근자 (behaviour 전용) ──────────────────────────────────
    internal SpriteRenderer       WeaponSr       => _weaponSr;
    internal WeaponGrade          Grade          => _grade;
    internal WeaponLegendarySkill LegendarySkill => _legendarySkill;
    internal WeaponMythicSkill    MythicSkill    => _mythicSkill;

    // ── 공유 NonAlloc 버퍼 ────────────────────────────────────────────
    public static readonly Collider2D[] SharedHitBuffer = new Collider2D[32];


    // ── 클론 루틴 전용 (Unity 6 List 기반 오버랩) ────────────────────
    private static readonly List<Collider2D> _sharedHitList = new();
    private static ContactFilter2D _enemyFilter;
    private static bool            _enemyFilterReady;
    private static ContactFilter2D EnemyFilter
    {
        get
        {
            if (!_enemyFilterReady)
            {
                _enemyFilter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask    = 1 << LayerMask.NameToLayer("Enemy"),
                    useTriggers  = true,
                };
                _enemyFilterReady = true;
            }
            return _enemyFilter;
        }
    }

    // ── 특수효과 ─────────────────────────────────────────────────────
    private string            _specialEffectId;
    private int               _weaponChapter = 1;
    private string            _weaponId;
    private bool              _cloneFlying;
    private int               _cloneActiveCount;
    private readonly List<GameObject> _activeClones = new();
    private StatType?         _passiveBuffStat;
    private float             _passiveBuffAmount;

    // ── 진단 ─────────────────────────────────────────────────────────
    private float       _noAttackDiagTimer;
    private const float DiagLogInterval = 5f;

    // ── 이벤트 ───────────────────────────────────────────────────────
    public event System.Action<Vector3, Vector2> OnSlashVfxSpawned;
    public static event System.Action OnRecallAll;
    public static void RecallAll() => OnRecallAll?.Invoke();

    // ─────────────────────────────────────────────────────────────────
    // CombatTarget
    // ─────────────────────────────────────────────────────────────────

    public readonly struct CombatTarget
    {
        public readonly EnemyController Enemy;
        public readonly BossController  Boss;

        public CombatTarget(EnemyController e) { Enemy = e; Boss = null; }
        public CombatTarget(BossController  b) { Enemy = null; Boss = b; }

        public static readonly CombatTarget None = default;
        public bool IsNone  => Enemy == null && Boss == null;
        public bool IsValid => (Enemy != null && !Enemy.IsPooled && Enemy.gameObject.activeInHierarchy) ||
                               (Boss  != null && Boss.gameObject.activeInHierarchy);
        public Vector3 Position
        {
            get
            {
                if (Enemy != null) return Enemy.transform.position;
                if (Boss  != null) return Boss.transform.position;
                return Vector3.zero;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ═════════════════════════════════════════════════════════════════

    private void Start()
    {
        OnRecallAll += RecallClones;
        if (!_initialized)
        {
            _baseCooldown = attackCooldown;
            cooldownTimer = attackCooldown;
        }
        originLocalPos = transform.localPosition;
        _originalScale = transform.localScale;
        if (_weaponSr == null) _weaponSr = GetComponentInChildren<SpriteRenderer>();
        InitTrail();
        if (!_initialized)
        {
            _behaviour?.Init(this);
            _legendarySkill?.Initialize(this);
            _legendarySkill?.OnEquip();
            _mythicSkill?.Initialize(this);
            _mythicSkill?.OnEquip();
        }
        _behaviour?.OnStart();
    }

    private void OnDestroy()
    {
        if (_passiveBuffStat.HasValue)
            PlayerStatManager.Shared?.AddPctMod(_passiveBuffStat.Value, -_passiveBuffAmount);
        OnRecallAll -= RecallClones;
        RecallClones();
        _behaviour?.OnDestroy();
    }

    private void OnDisable() => RecallClones();

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        cooldownTimer -= Time.deltaTime;

        if (EnemyController.All.Count > 0 || BossController.Current != null)
        {
            _noAttackDiagTimer += Time.unscaledDeltaTime;
            if (_noAttackDiagTimer >= DiagLogInterval)
            {
                _noAttackDiagTimer = 0f;
                if (cooldownTimer <= 0f)
                {
                    var any = FindNearestAny();
                    Vector2 org = transform.parent != null
                        ? (Vector2)transform.parent.position : (Vector2)transform.position;
                    float d = any != null ? Vector2.Distance(org, any.transform.position) : -1f;
                    Debug.LogWarning(
                        $"[WeaponPoint] 미아 '{gameObject.name}'\n" +
                        $"  behaviour={_behaviour?.GetType().Name}  cooldown={cooldownTimer:F2}  " +
                        $"attackRange={attackRange}  nearestEnemy={d:F2}");
                }
            }
        }
        else
        {
            _noAttackDiagTimer = 0f;
        }

        if (_behaviour == null || !_behaviour.OverridesRotation)
            UpdateRotation();

        _behaviour?.OnUpdate();
    }

    private void OnTriggerStay2D(Collider2D other) => _behaviour?.OnTriggerHit(other);

    // ═════════════════════════════════════════════════════════════════
    // 초기화 (WeaponManager가 장착 시 호출)
    // ═════════════════════════════════════════════════════════════════

    public void Init(WeaponData data)
    {
        _initialized    = true;
        _legendarySkill = GetComponent<WeaponLegendarySkill>();
        _mythicSkill    = GetComponent<WeaponMythicSkill>();
        _weaponSr       = GetComponentInChildren<SpriteRenderer>();

        criticalRate       = data.criticalRate;
        criticalMultiplier = data.criticalDamage > 0f ? data.criticalDamage : 2f;
        lifeSteal          = data.lifeSteal;
        knockbackPower     = data.knockbackPower;
        dotDamage          = data.bleedDamage;

        _specialEffectId = data.trait;
        _weaponChapter   = data.chapter;
        _weaponId        = data.id;
        _passiveBuffStat        = null;
        _passiveBuffAmount      = 0f;

        if (!string.IsNullOrEmpty(_specialEffectId) && _specialEffectId.StartsWith("BUFF_"))
        {
            var stat = BuffTraitToStat(_specialEffectId);
            if (stat.HasValue)
            {
                var cfg        = StatusEffectConfig.Shared;
                float pct      = cfg?.GetPassiveBuffPct(_weaponId, 2f) ?? 2f;
                _passiveBuffStat   = stat;
                _passiveBuffAmount = pct;
                PlayerStatManager.Shared?.AddPctMod(stat.Value, pct);

                var vfxPrefab  = cfg?.GetPassiveBuffVfx(_weaponId);
                var buffPlayer = PlayerController.Instance;
                if (vfxPrefab != null && buffPlayer != null)
                    StartCoroutine(PlayBuffEquipVfx(vfxPrefab, buffPlayer.transform));
            }
        }

        attackCooldown = data.attackCooldown;
        _baseCooldown  = data.attackCooldown;
        cooldownTimer  = 0f;
        attackRange    = data.attackRange;
        areaHitRadius  = data.areaHitRadius;

        if (attackRange <= 0f)
            Debug.LogWarning($"[WeaponPoint] ⚠️ attackRange=0 — '{gameObject.name}' (DB f_탐지범위 확인)");

        _behaviour?.Init(this);
        _behaviour?.OnInitData(data);

        var player = PlayerController.Instance;
        if (player != null)
        {
            attackDamage  = data.attackDamage + player.Attack;
            if (_behaviour != null)
                attackDamage += _behaviour.GetPlayerAttackBonus(player);

            var relic = RelicManager.Shared;
            if (relic != null)
            {
                float pct = _behaviour?.GetRelicBonusPct(relic) ?? 0f;
                if (pct > 0f) attackDamage *= (1f + pct / 100f);
            }
        }

        _grade = data.grade;
        _legendarySkill?.Initialize(this);
        _legendarySkill?.OnEquip();
        _mythicSkill?.Initialize(this);
        _mythicSkill?.OnEquip();

        var outline = GetComponentInChildren<SpriteOutline>();
        outline?.SetStar(data.star);
        outline?.SetGrade(data.grade);

        var weaponSr = GetComponentInChildren<SpriteRenderer>();
        var aura = GetComponent<WeaponAura>() ?? gameObject.AddComponent<WeaponAura>();
        aura.Apply(weaponSr, data.grade);

        if (_behaviour is WeaponSlashBehaviour slashBeh)
            slashBeh.OnSlashVfxSpawned += (pos, dir) => OnSlashVfxSpawned?.Invoke(pos, dir);

        ProjectileSpeed = _behaviour?.GetProjectileSpeed() ?? 0f;
    }

    // ═════════════════════════════════════════════════════════════════
    // 공격속도 제어 (외부 → 캐릭터 스킬·장비)
    // ═════════════════════════════════════════════════════════════════

    public void SetStatSpeedPct(float reduction)
    {
        _statMultiplier = reduction;
        RecalcCooldown();
    }

    public void MultiplyAttackSpeed(float multiplier)
    {
        _bonusMultiplier *= multiplier;
        RecalcCooldown();
    }

    private void RecalcCooldown()
    {
        float reduced  = _baseCooldown - _statMultiplier;
        attackCooldown = Mathf.Max(0.1f, reduced / _bonusMultiplier);
    }

    /// <summary>LegendarySpinSkill에서 호출 — 공전 반경·속도 배율 적용.</summary>
    public void ApplySpinBoost(float radiusMult, float speedMult)
        => (_behaviour as WeaponSpinBehaviour)?.ApplyOrbitMultiplier(radiusMult, speedMult);

    public void RecallClones()
    {
        foreach (var go in _activeClones) if (go != null) Destroy(go);
        _activeClones.Clear();
        _cloneActiveCount = 0;
        _cloneFlying      = false;
    }

    // ═════════════════════════════════════════════════════════════════
    // 내부 서비스 메서드 (behaviour에서 호출)
    // ═════════════════════════════════════════════════════════════════

    internal (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        float playerCrit = PlayerController.Instance?.CritRateBonus ?? 0f;
        float totalCrit  = criticalRate + playerCrit;
        bool  crit       = totalCrit > 0f && Random.value < totalCrit;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    internal void ApplyLifeSteal(float dealtDamage)
    {
        if (lifeSteal <= 0f) return;
        PlayerController.Instance?.HealHP(dealtDamage * lifeSteal);
    }

    internal void SnapRotationToDir(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
    }

    internal void TriggerAttackAnimation() { }

    internal void TriggerScalePulse()
    {
        if (!shootScalePulse) return;
        if (_scalePulseCoroutine != null) StopCoroutine(_scalePulseCoroutine);
        _scalePulseCoroutine = StartCoroutine(ScalePulseRoutine());
    }

    internal void StartTrail()
    {
        if (_trail == null) return;
        _trail.Clear();
        _trail.emitting = true;
    }

    internal void StopTrail()
    {
        if (_trail != null) _trail.emitting = false;
    }

    internal void InvokeSlashVfxSpawned(Vector3 pos, Vector2 dir) => OnSlashVfxSpawned?.Invoke(pos, dir);

    internal CombatTarget FindBestTarget()
    {
        Vector2 origin   = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;
        int     colCount = Physics2D.OverlapCircle(origin, attackRange, EnemyFilter, _sharedHitList);

        CombatTarget bestBoss  = CombatTarget.None;
        CombatTarget bestElite = CombatTarget.None;
        CombatTarget bestEnemy = CombatTarget.None;
        float minBossSq  = float.MaxValue;
        float minEliteSq = float.MaxValue;
        float minEnemySq = float.MaxValue;

        for (int i = 0; i < colCount; i++)
        {
            var hit  = _sharedHitList[i];
            var boss = hit.GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                float sq = ((Vector2)boss.transform.position - origin).sqrMagnitude;
                if (sq < minBossSq) { minBossSq = sq; bestBoss = new CombatTarget(boss); }
                continue;
            }
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            float esq = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (enemy.GetComponent<EliteMarker>() != null)
            { if (esq < minEliteSq) { minEliteSq = esq; bestElite = new CombatTarget(enemy); } }
            else
            { if (esq < minEnemySq) { minEnemySq = esq; bestEnemy = new CombatTarget(enemy); } }
        }

        var db = BossController.Current;
        if (db != null && db.gameObject.activeInHierarchy)
        {
            float sq = ((Vector2)db.transform.position - origin).sqrMagnitude;
            if (sq <= attackRange * attackRange && sq < minBossSq)
                bestBoss = new CombatTarget(db);
        }

        if (!bestBoss.IsNone)  return bestBoss;
        if (!bestElite.IsNone) return bestElite;
        return bestEnemy;
    }

    internal EnemyController FindNearestAny()
    {
        EnemyController nearest   = null;
        float           minSqDist = float.MaxValue;
        Vector2         origin    = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;

        foreach (var e in EnemyController.All)
        {
            if (e == null || e.transform == null || !e.gameObject.activeInHierarchy) continue;
            float sq = ((Vector2)e.transform.position - origin).sqrMagnitude;
            if (sq < minSqDist) { minSqDist = sq; nearest = e; }
        }
        return nearest;
    }

    internal void HitArea(Vector2 hitDir, EnemyController primaryTarget)
    {
        const float MinHitRadius = 0.4f;
        float hitRadius = _grade >= WeaponGrade.Rare
            ? Mathf.Max(areaHitRadius, MinHitRadius) : MinHitRadius;

        int  colCount = Physics2D.OverlapCircle(transform.position, hitRadius, EnemyFilter, _sharedHitList);
        bool bossHit  = false;
        bool anyHit   = false;
        BossController hitBoss = null;

        float effectiveKb = _grade switch
        {
            WeaponGrade.Common => 0f,
            WeaponGrade.Rare   => knockbackPower,
            _                  => knockbackPower * 0.5f,
        };

        for (int i = 0; i < colCount; i++)
        {
            var hit = _sharedHitList[i];

            if (!bossHit)
            {
                var boss = hit.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    var (bd, bc) = RollCrit(attackDamage);
                    Vector2 bk   = ((Vector2)boss.transform.position - (Vector2)transform.position);
                    if (bk.sqrMagnitude < 0.001f) bk = hitDir;
                    if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
                    boss.TakeDamage(bd, bc, bk.normalized, effectiveKb);
                    ApplyLifeSteal(bd);
                    TrySpecialEffectBoss(boss, bd);
                    bossHit = true; hitBoss = boss; anyHit = true;
                    continue;
                }
            }

            if (!hit.TryGetComponent<EnemyController>(out var enemy)) continue;
            bool isPrimary = enemy == primaryTarget;
            float baseDmg = isPrimary ? attackDamage : attackDamage * areaHitDamageRatio;
            var (fd, fc)  = RollCrit(baseDmg);
            Vector2 kd    = ((Vector2)enemy.transform.position - (Vector2)transform.position);
            if (kd.sqrMagnitude < 0.001f) kd = hitDir;
            if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
            enemy.TakeDamage(fd, fc, kd.normalized, effectiveKb);
            ApplyLifeSteal(fd);
            if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
            if (isPrimary) TrySpecialEffectEnemy(enemy, fd, kd.normalized);
            anyHit = true;
        }
        FXManager.NextIsEpicHit = false;

        if (anyHit)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            ApplyGradeProc(colCount, hitDir, hitBoss, transform.position);
        }
    }

    internal void HitAreaBoomerang(Vector2 hitDir, int bounceCount, CombatTarget boomerangTarget,
        HashSet<EnemyController> hitEnemies, HashSet<BossController> hitBosses)
    {
        const float MinHitRadius = 0.4f;
        float hitRadius = _grade >= WeaponGrade.Rare
            ? Mathf.Max(areaHitRadius, MinHitRadius) : MinHitRadius;

        int    colCount = Physics2D.OverlapCircle(transform.position, hitRadius, EnemyFilter, _sharedHitList);
        float  pen      = Mathf.Pow(0.75f, bounceCount);
        bool   anyHit   = false;
        BossController hitBoss = null;

        float effectiveKb = _grade switch
        {
            WeaponGrade.Common => 0f,
            WeaponGrade.Rare   => knockbackPower,
            _                  => knockbackPower * 0.5f,
        };

        for (int i = 0; i < colCount; i++)
        {
            var hit   = _sharedHitList[i];
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                bool   isPrimary = boomerangTarget.Enemy == enemy;
                float  baseDmg   = Mathf.Max(1f, (isPrimary ? attackDamage : attackDamage * areaHitDamageRatio) * pen);
                var (fd, fc) = RollCrit(baseDmg);
                Vector2 kd   = ((Vector2)enemy.transform.position - (Vector2)transform.position);
                if (kd.sqrMagnitude < 0.001f) kd = hitDir;
                enemy.TakeDamage(fd, fc, kd.normalized, effectiveKb);
                ApplyLifeSteal(fd);
                if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
                if (isPrimary) TrySpecialEffectEnemy(enemy, fd, kd.normalized);
                anyHit = true;
            }
            var boss = hit.GetComponentInParent<BossController>();
            if (boss != null && !hitBosses.Contains(boss))
            {
                hitBosses.Add(boss);
                bool   isPrimary = boomerangTarget.Boss == boss;
                float  baseDmg   = Mathf.Max(1f, (isPrimary ? attackDamage : attackDamage * areaHitDamageRatio) * pen);
                var (fd, fc) = RollCrit(baseDmg);
                Vector2 kd   = ((Vector2)boss.transform.position - (Vector2)transform.position);
                if (kd.sqrMagnitude < 0.001f) kd = hitDir;
                boss.TakeDamage(fd, fc, kd.normalized, effectiveKb);
                ApplyLifeSteal(fd);
                if (dotDamage > 0f) boss.ApplyBleed(dotDamage);
                if (isPrimary) TrySpecialEffectBoss(boss, fd);
                hitBoss = boss; anyHit = true;
            }
        }

        if (anyHit)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            ApplyGradeProc(colCount, hitDir, hitBoss, transform.position);
        }
    }

    internal void ApplyGradeProc(int colCount, Vector2 hitDir, BossController hitBoss, Vector3 hitPos)
    {
        if (_grade >= WeaponGrade.Epic)
            StartCoroutine(EpicSecondHitRoutine(hitPos));

        if (_grade >= WeaponGrade.Legendary)
            _legendarySkill?.OnHit(hitDir, SharedHitBuffer, colCount);

        if (_grade >= WeaponGrade.Mythic)
        {
            if (hitBoss != null)
            {
                _mythicSkill?.OnBossHit(hitBoss, hitDir);
            }
            else if (_mythicSkill != null)
            {
                for (int i = 0; i < colCount; i++)
                {
                    var e = _sharedHitList[i].GetComponentInParent<EnemyController>();
                    if (e != null && e.gameObject.activeInHierarchy)
                        _mythicSkill.OnEnemyHit(e, hitDir);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 특수효과 디스패처
    // ─────────────────────────────────────────────────────────────────

    private float Chance(float fallback) =>
        StatusEffectConfig.Shared?.GetTriggerChance(_weaponId, fallback) ?? fallback;

    internal void TrySpecialEffectEnemy(EnemyController enemy, float damage, Vector2 hitDir)
    {
        if (string.IsNullOrEmpty(_specialEffectId) || enemy == null) return;
        switch (_specialEffectId)
        {
            case "HIT_ICE":
            case "HIT_FIRE":
            case "HIT_STUN":
            {
                float chance = _specialEffectId switch
                {
                    "HIT_ICE"  => Chance(0.30f),
                    "HIT_FIRE" => Chance(0.35f),
                    _          => Chance(0.25f),
                };
                if (Random.value > chance) break;
                ApplyHitStun(enemy, damage);
                break;
            }

            case "HIT_CLONE_RETURN":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneProjectiles(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_CROSS":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneCross(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_BURST":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneBurst(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_SPREAD":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneSpread(enemy.transform.position, damage);
                break;

            case "BOSS_SPIKE":
            case "HIT_SPIKE":
                if (Random.value > Chance(0.25f)) break;
                SpawnAoeSpike(enemy.transform.position, damage);
                break;

            case "HIT_DOT":
                if (Random.value > Chance(0.35f)) break;
                ApplyAreaFire(enemy.transform.position);
                break;

            case "HIT_SHOCK":
                if (Random.value > Chance(0.40f)) break;
                ApplyShockwave(enemy.transform.position, damage * 0.5f);
                break;

            case "HIT_SPIN":
            {
                if (!(_behaviour is WeaponSpinBehaviour spinBeh)) break;
                if (Random.value > Chance(0.30f)) break;
                spinBeh.ApplySpinBoost();
                break;
            }
        }
    }

    internal void TrySpecialEffectBoss(BossController boss, float damage)
    {
        if (string.IsNullOrEmpty(_specialEffectId) || boss == null) return;
        switch (_specialEffectId)
        {
            case "BOSS_GROGY":
                if (Random.value > Chance(0.20f)) break;
                boss.TriggerGroggy(5f);
                break;

            case "BOSS_BIND":
                if (Random.value > Chance(0.15f)) break;
                boss.BindMovement(3f);
                break;

            case "BOSS_SPIKE":
            case "HIT_SPIKE":
                if (Random.value > Chance(0.25f)) break;
                SpawnAoeSpike(boss.transform.position, damage);
                break;

            case "HIT_CLONE_RETURN":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneProjectiles(boss.transform.position, damage);
                break;

            case "HIT_CLONE_CROSS":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneCross(boss.transform.position, damage);
                break;

            case "HIT_CLONE_BURST":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneBurst(boss.transform.position, damage);
                break;

            case "HIT_CLONE_SPREAD":
                if (_cloneFlying || Random.value > Chance(0.25f)) break;
                SpawnCloneSpread(boss.transform.position, damage);
                break;

            case "HIT_DOT":
                if (Random.value > Chance(0.35f)) break;
                ApplyAreaFire(boss.transform.position);
                break;

            case "HIT_SHOCK":
                if (Random.value > Chance(0.40f)) break;
                ApplyShockwave(boss.transform.position, damage * 0.5f);
                break;
        }
    }

    internal void ApplyShootSpecialEffect(Projectile mainProj, Vector3 spawnPos, Vector2 aimDir)
    {
        if (!string.IsNullOrEmpty(_specialEffectId))
        {
            if (_specialEffectId.StartsWith("HIT_"))
            {
                mainProj.SetOnHitEnemy((e, dmg, dir) => TrySpecialEffectEnemy(e, dmg, dir));
                mainProj.SetOnHitBoss((b, dmg) => TrySpecialEffectBoss(b, dmg));
            }
            if (_specialEffectId.StartsWith("BOSS_"))
                mainProj.SetOnHitBoss((b, dmg) => TrySpecialEffectBoss(b, dmg));
        }

        if (string.IsNullOrEmpty(_specialEffectId)) return;
        switch (_specialEffectId)
        {
            case "SHOOT_PIERCE":
                mainProj.SetSpecialFlags(pierce: true, reflect: false);
                break;

            case "SHOOT_REFLECT":
                mainProj.SetSpecialFlags(pierce: false, reflect: true);
                break;

            case "SHOOT_MULTI":
                FireExtraProjectile(spawnPos, RotateVec(aimDir,  20f), attackDamage * 0.7f);
                FireExtraProjectile(spawnPos, RotateVec(aimDir, -20f), attackDamage * 0.7f);
                break;

            case "SHOOT_SUB":
                FireHomingProjectile(spawnPos, RotateVec(aimDir,  35f), attackDamage * 0.5f);
                FireHomingProjectile(spawnPos, RotateVec(aimDir, -35f), attackDamage * 0.5f);
                break;

            case "SHOOT_WAVE":
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    FireExtraProjectile(spawnPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), attackDamage * 0.4f);
                }
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 회전
    // ═════════════════════════════════════════════════════════════════

    private void UpdateRotation()
    {
        Vector2 dir = GetLookDirection();
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
    }

    private Vector2 GetLookDirection()
    {
        // 공격 시퀀스 중 — behaviour가 방향을 제공하면 사용
        var lookDir = _behaviour?.GetAttackLookDir();
        if (lookDir.HasValue && lookDir.Value.sqrMagnitude > 0.001f)
            return lookDir.Value;

        // 적 방향 (보스/엘리트/일반 우선순위)
        EnemyController nearest   = FindNearestAny();
        var             curBoss   = BossController.Current;
        bool            bossAlive = curBoss != null && curBoss.gameObject.activeInHierarchy;

        if (nearest != null && bossAlive)
        {
            float eDist = ((Vector2)nearest.transform.position   - (Vector2)transform.position).sqrMagnitude;
            float bDist = ((Vector2)curBoss.transform.position   - (Vector2)transform.position).sqrMagnitude;
            return bDist < eDist
                ? ((Vector2)curBoss.transform.position   - (Vector2)transform.position).normalized
                : ((Vector2)nearest.transform.position   - (Vector2)transform.position).normalized;
        }
        if (bossAlive)  return ((Vector2)curBoss.transform.position - (Vector2)transform.position).normalized;
        if (nearest != null) return ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;

        // 이동 방향 → 위쪽
        if (PlayerController.Instance != null)
        {
            Vector2 move = PlayerController.Instance.MoveDirection;
            if (move.sqrMagnitude > 0.001f) return move;
        }
        return Vector2.up;
    }

    // ═════════════════════════════════════════════════════════════════
    // 개인 헬퍼
    // ═════════════════════════════════════════════════════════════════

    private void InitTrail()
    {
        if (!useTrail) return;
        _trail = GetComponent<TrailRenderer>();
        if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();

        _trail.time             = trailTime;
        _trail.startWidth       = trailStartWidth;
        _trail.endWidth         = 0f;
        _trail.minVertexDistance = 0.02f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
            new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = gradient;

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) { _trail.sortingLayerID = sr.sortingLayerID; _trail.sortingOrder = sr.sortingOrder - 1; }
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.emitting = false;
    }

    private IEnumerator ScalePulseRoutine()
    {
        Vector3 peak = _originalScale * scalePulseMax;
        float   half = scalePulseDuration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(_originalScale, peak, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(peak, _originalScale, t / half);
            yield return null;
        }
        transform.localScale  = _originalScale;
        _scalePulseCoroutine  = null;
    }

    private IEnumerator EpicSecondHitRoutine(Vector3 hitPos)
    {
        yield return new WaitForSeconds(0.08f);
        const float MinHitRadius = 0.4f;
        float radius = _grade >= WeaponGrade.Rare ? Mathf.Max(areaHitRadius, MinHitRadius) : MinHitRadius;
        int   count  = Physics2D.OverlapCircle(hitPos, radius, EnemyFilter, _sharedHitList);
        float dmg    = attackDamage * Random.Range(0.20f, 0.25f);
        bool  anyHit = false;

        for (int i = 0; i < count; i++)
        {
            var boss = _sharedHitList[i].GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                var (fd, ic) = RollCrit(dmg);
                if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
                boss.TakeDamage(fd, ic);
                anyHit = true;
                continue;
            }
            if (_sharedHitList[i].TryGetComponent<EnemyController>(out var e) && e.gameObject.activeInHierarchy)
            {
                var (fd, ic) = RollCrit(dmg);
                if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
                e.TakeDamage(fd, ic);
                anyHit = true;
            }
        }
        FXManager.NextIsEpicHit = false;
        if (anyHit) FXManager.Shared?.SpawnHitEffect(hitPos);
    }

    // ═════════════════════════════════════════════════════════════════
    // 특수효과 헬퍼
    // ═════════════════════════════════════════════════════════════════

    private const float CloneOutTime    = 0.45f;
    private const float ClonePauseTime  = 0.40f;
    private const float CloneReturnTime = 0.35f;
    private const int   CloneBurstHits  = 5;
    private const float CloneBurstInterval = 0.06f;

    private void SpawnCloneProjectiles(Vector2 enemyPos, float damage)
    {
        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(_weaponId)
                     ?? _behaviour?.GetProjectilePrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"[HIT_CLONE_RETURN] 프리팹 없음 — weaponId='{_weaponId}'");
            return;
        }

        Vector2 origin  = transform.parent != null ? (Vector2)transform.parent.position : enemyPos;
        Vector2 forward = (enemyPos - origin);
        if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
        forward = forward.normalized;

        var   cfg       = StatusEffectConfig.Shared;
        float outTime   = cfg?.GetCloneOutTime   (_weaponId, CloneOutTime)    ?? CloneOutTime;
        float pauseTime = cfg?.GetClonePauseTime (_weaponId, ClonePauseTime)  ?? ClonePauseTime;
        float retTime   = cfg?.GetCloneReturnTime(_weaponId, CloneReturnTime) ?? CloneReturnTime;
        float range     = cfg?.GetCloneRange     (_weaponId, 6f)              ?? 6f;
        float cloneDmg  = damage * 0.5f;

        int cloneCount    = _grade >= WeaponGrade.Mythic ? 2 : 1;
        _cloneFlying      = true;
        _cloneActiveCount = cloneCount;

        var go1 = SpawnCloneVisual(prefab, origin);
        StartCoroutine(CloneArcRoutine(go1, origin, origin + RotateVec(forward,  15f) * range, cloneDmg, outTime, pauseTime, retTime));
        if (cloneCount >= 2)
        {
            var go2 = SpawnCloneVisual(prefab, origin);
            StartCoroutine(CloneArcRoutine(go2, origin, origin + RotateVec(forward, -15f) * range, cloneDmg, outTime, pauseTime, retTime));
        }
    }

    private GameObject SpawnCloneVisual(GameObject prefab, Vector2 pos, Vector2 dir = default)
    {
        var go = Instantiate(prefab, pos, Quaternion.identity);
        var p  = go.GetComponentInChildren<Projectile>();
        if (p != null)
        {
            if (dir != default && dir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - p.ForwardAngleDeg;
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            Destroy(p);
        }
        _activeClones.Add(go);
        return go;
    }

    private void DestroyClone(GameObject go)
    {
        _activeClones.Remove(go);
        if (go != null) Destroy(go);
        _cloneActiveCount--;
        if (_cloneActiveCount <= 0) { _cloneActiveCount = 0; _cloneFlying = false; }
    }

    private IEnumerator CloneArcRoutine(GameObject go, Vector2 from, Vector2 to, float damage,
        float outDuration, float pauseDuration, float returnDuration)
    {
        Vector3 baseScale = go.transform.localScale;
        Vector3 minScale  = baseScale * 0.5f;
        Vector3 maxScale  = baseScale * 1.5f;

        var  hitIds    = new HashSet<int>();
        bool bossHitFly = false;
        float t = 0f;
        while (t < outDuration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / outDuration);
            go.transform.position   = Vector2.Lerp(from, to, ratio);
            go.transform.localScale = Vector3.Lerp(minScale, maxScale, ratio);

            int colCount = Physics2D.OverlapCircle(go.transform.position, 0.35f, EnemyFilter, _sharedHitList);
            for (int ci = 0; ci < colCount; ci++)
            {
                var col = _sharedHitList[ci];
                if (!bossHitFly)
                {
                    var boss = col.GetComponentInParent<BossController>();
                    if (boss != null && hitIds.Add(boss.GetInstanceID()))
                    {
                        bossHitFly = true;
                        var (bd, bc) = RollCrit(damage);
                        boss.TakeDamage(bd, bc, ((Vector2)boss.transform.position - (Vector2)go.transform.position).normalized, knockbackPower);
                        continue;
                    }
                }
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !hitIds.Add(enemy.GetInstanceID())) continue;
                var (ed, ec) = RollCrit(damage);
                enemy.TakeDamage(ed, ec, ((Vector2)enemy.transform.position - (Vector2)go.transform.position).normalized, knockbackPower);
            }
            yield return null;
        }

        if (go == null) { DestroyClone(go); yield break; }
        go.transform.position   = to;
        go.transform.localScale = maxScale;

        float burstInterval = Mathf.Max(pauseDuration / CloneBurstHits, CloneBurstInterval);
        for (int h = 0; h < CloneBurstHits; h++)
        {
            if (go == null) { DestroyClone(go); yield break; }
            int burstCount = Physics2D.OverlapCircle(go.transform.position, 0.5f, EnemyFilter, _sharedHitList);
            bool bossHitBurst = false;
            for (int bci = 0; bci < burstCount; bci++)
            {
                var col = _sharedHitList[bci];
                if (!bossHitBurst)
                {
                    var boss = col.GetComponentInParent<BossController>();
                    if (boss != null)
                    {
                        bossHitBurst = true;
                        var (bd, bc) = RollCrit(damage);
                        boss.TakeDamage(bd, bc, ((Vector2)boss.transform.position - (Vector2)go.transform.position).normalized, knockbackPower);
                        continue;
                    }
                }
                var e = col.GetComponentInParent<EnemyController>();
                if (e == null) continue;
                var (ed, ec) = RollCrit(damage);
                e.TakeDamage(ed, ec, ((Vector2)e.transform.position - (Vector2)go.transform.position).normalized, knockbackPower);
            }
            FXManager.Shared?.SpawnHitEffect(go.transform.position);
            yield return new WaitForSeconds(burstInterval);
        }

        t = 0f;
        while (t < returnDuration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            float ratio     = Mathf.Clamp01(t / returnDuration);
            Vector2 playerPos = transform.parent != null ? (Vector2)transform.parent.position : from;
            go.transform.position   = Vector2.Lerp(to, playerPos, ratio);
            go.transform.localScale = Vector3.Lerp(maxScale, minScale, ratio);
            yield return null;
        }
        DestroyClone(go);
    }

    private void SpawnCloneCross(Vector2 targetPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        Vector2 origin = GetWeaponOrigin(targetPos);
        float   range  = StatusEffectConfig.Shared?.GetCloneRange(_weaponId, 6f) ?? 6f;
        var     cfg    = StatusEffectConfig.Shared;
        float ot = cfg?.GetCloneOutTime   (_weaponId, CloneOutTime)    ?? CloneOutTime;
        float pt = cfg?.GetClonePauseTime (_weaponId, ClonePauseTime)  ?? ClonePauseTime;
        float rt = cfg?.GetCloneReturnTime(_weaponId, CloneReturnTime) ?? CloneReturnTime;

        Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
        _cloneFlying = true; _cloneActiveCount = dirs.Length;
        foreach (var d in dirs)
        {
            var go = SpawnCloneVisual(prefab, origin);
            StartCoroutine(CloneArcRoutine(go, origin, origin + d * range, damage * 0.5f, ot, pt, rt));
        }
    }

    private void SpawnCloneBurst(Vector2 targetPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        Vector2 origin  = GetWeaponOrigin(targetPos);
        Vector2 forward = targetPos - origin;
        if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
        forward = forward.normalized;
        Vector2 perp       = new Vector2(-forward.y, forward.x);
        float   range      = StatusEffectConfig.Shared?.GetCloneRange   (_weaponId, 7f)   ?? 7f;
        float   travelTime = StatusEffectConfig.Shared?.GetCloneOutTime (_weaponId, 0.22f) ?? 0.22f;

        float[] slots = { -1.5f, -0.5f, 0.5f, 1.5f };
        _cloneFlying = true; _cloneActiveCount = slots.Length;
        StartCoroutine(BurstSequenceRoutine(prefab, origin, forward, perp, slots, range, damage * 0.4f, travelTime));
    }

    private IEnumerator BurstSequenceRoutine(GameObject prefab, Vector2 origin, Vector2 forward,
        Vector2 perp, float[] slots, float range, float damage, float travelTime)
    {
        const float gatherDuration = 0.12f;
        const float holdDuration   = 0.06f;
        const float minFireDelay   = 0.04f;
        const float maxFireDelay   = 0.12f;

        var    gos   = new GameObject[slots.Length];
        var    poses = new Vector2[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            poses[i] = origin + perp * slots[i];
            gos[i]   = SpawnCloneVisual(prefab, origin, forward);
        }

        float t = 0f;
        while (t < gatherDuration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, t / gatherDuration);
            for (int i = 0; i < gos.Length; i++)
                if (gos[i] != null) gos[i].transform.position = Vector2.Lerp(origin, poses[i], ratio);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        var remaining = new List<int>(new[] { 0, 1, 2, 3 });
        while (remaining.Count > 0)
        {
            int pick = Random.Range(0, remaining.Count);
            int idx  = remaining[pick];
            remaining.RemoveAt(pick);
            if (gos[idx] != null)
                StartCoroutine(CloneBurstRoutine(gos[idx], poses[idx], forward, range, damage, travelTime));
            if (remaining.Count > 0)
                yield return new WaitForSeconds(Random.Range(minFireDelay, maxFireDelay));
        }
    }

    private IEnumerator CloneBurstRoutine(GameObject go, Vector2 from, Vector2 dir,
        float range, float damage, float duration)
    {
        const int   maxHitsPerEnemy = 3;
        const float hitCooldown     = 0.15f;

        var   hitCounts = new Dictionary<int, int>();
        var   hitTimers = new Dictionary<int, float>();
        float t = 0f;

        while (t < duration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            go.transform.position = from + dir * (range * Mathf.Clamp01(t / duration));

            int cloneHitCount = Physics2D.OverlapCircle(go.transform.position, 0.35f, EnemyFilter, _sharedHitList);
            for (int ci = 0; ci < cloneHitCount; ci++)
            {
                var col = _sharedHitList[ci];
                var boss = col.GetComponentInParent<BossController>();
                if (boss != null) { boss.TakeDamage(damage); continue; }

                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                int id = enemy.GetInstanceID();
                hitCounts.TryGetValue(id, out int count);
                hitTimers.TryGetValue(id, out float lastHit);
                if (count >= maxHitsPerEnemy || (count > 0 && Time.time - lastHit < hitCooldown)) continue;
                var (ed, ec) = RollCrit(damage);
                enemy.TakeDamage(ed, ec, dir, knockbackPower);
                hitCounts[id] = count + 1;
                hitTimers[id] = Time.time;
            }
            yield return null;
        }
        DestroyClone(go);
    }

    private void SpawnCloneSpread(Vector2 hitPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        float range = StatusEffectConfig.Shared?.GetCloneRange  (_weaponId, 4f)                ?? 4f;
        float ot    = StatusEffectConfig.Shared?.GetCloneOutTime(_weaponId, CloneOutTime * 0.8f) ?? CloneOutTime * 0.8f;

        _cloneFlying = true; _cloneActiveCount = 8;
        for (int i = 0; i < 8; i++)
        {
            float   angle = i * 45f * Mathf.Deg2Rad;
            Vector2 d     = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            StartCoroutine(CloneOneWayRoutine(SpawnCloneVisual(prefab, hitPos), hitPos, hitPos + d * range, damage * 0.3f, ot));
        }
    }

    private GameObject GetClonePrefab()
    {
        var p = StatusEffectConfig.Shared?.GetClonePrefab(_weaponId) ?? _behaviour?.GetProjectilePrefab();
        if (p == null) Debug.LogWarning($"[HIT_CLONE] 프리팹 없음 — weaponId='{_weaponId}'");
        return p;
    }

    private Vector2 GetWeaponOrigin(Vector2 fallback)
        => transform.parent != null ? (Vector2)transform.parent.position : fallback;

    private IEnumerator CloneOneWayRoutine(GameObject go, Vector2 from, Vector2 to, float damage, float duration)
    {
        Vector3 baseScale = go.transform.localScale;
        var     hitIds    = new HashSet<int>();
        float   t         = 0f;
        while (t < duration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            go.transform.position   = Vector2.Lerp(from, to, ratio);
            go.transform.localScale = Vector3.Lerp(baseScale * 0.5f, baseScale * 1.5f, ratio);

            int oneWayCount = Physics2D.OverlapCircle(go.transform.position, 0.35f, EnemyFilter, _sharedHitList);
            for (int ci = 0; ci < oneWayCount; ci++)
            {
                var col  = _sharedHitList[ci];
                var boss = col.GetComponentInParent<BossController>();
                if (boss != null) { boss.TakeDamage(damage); continue; }
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !hitIds.Add(enemy.GetInstanceID())) continue;
                var (ed, ec) = RollCrit(damage);
                enemy.TakeDamage(ed, ec, ((Vector2)enemy.transform.position - (Vector2)go.transform.position).normalized, knockbackPower);
            }
            yield return null;
        }
        DestroyClone(go);
    }

    private void SpawnAoeSpike(Vector2 hitPos, float damage)
    {
        float r           = Mathf.Max(areaHitRadius, 2.5f);
        int   cnt         = Physics2D.OverlapCircle(hitPos, r, EnemyFilter, _sharedHitList);
        var   spikePrefab = StatusEffectConfig.Shared?.GetSpikePrefab(_weaponId);
        var   sprite      = _weaponSr?.sprite;
        float spikeDmg    = damage * 1.5f;

        for (int i = 0; i < cnt; i++)
        {
            var boss = _sharedHitList[i].GetComponentInParent<BossController>();
            if (boss != null) { BossSpike.Spawn(boss, sprite, spikeDmg, spikePrefab); continue; }
            var enemy = _sharedHitList[i].GetComponentInParent<EnemyController>();
            if (enemy != null) BossSpike.SpawnOnEnemy(enemy, sprite, spikeDmg, spikePrefab);
        }
    }

    private void ApplyAreaFire(Vector2 center)
    {
        float r      = Mathf.Max(areaHitRadius, 2f);
        int   cnt    = Physics2D.OverlapCircle(center, r, EnemyFilter, _sharedHitList);
        float dotDmg = attackDamage * 0.15f;
        for (int i = 0; i < cnt; i++)
        {
            var e = _sharedHitList[i].GetComponentInParent<EnemyController>();
            if (e != null && e.gameObject.activeInHierarchy)
                e.ApplyFire(dotDmg, 3f, 0.5f, _weaponChapter);
        }
    }

    private void ApplyHitStun(EnemyController enemy, float damage)
    {
        var cfg   = StatusEffectConfig.Shared;
        var type  = cfg?.GetHitStunType (_weaponId, _specialEffectId) ?? DefaultStunType();
        var color = cfg?.GetHitStunColor(_weaponId, DefaultStunColor(type)) ?? DefaultStunColor(type);

        switch (type)
        {
            case StatusEffectConfig.HitStunType.Stun:
                enemy.Stun(1.5f, _weaponChapter);
                break;

            case StatusEffectConfig.HitStunType.Freeze:
            {
                float r   = Mathf.Max(areaHitRadius, 1.5f);
                int   cnt = Physics2D.OverlapCircle(enemy.transform.position, r, EnemyFilter, _sharedHitList);
                for (int i = 0; i < cnt; i++)
                {
                    var e = _sharedHitList[i].GetComponentInParent<EnemyController>();
                    if (e != null) e.Freeze(2f, 0f, color, _weaponChapter);
                }
                break;
            }

            case StatusEffectConfig.HitStunType.Fire:
                enemy.ApplyFire(damage * 0.3f, 3f, weaponChapter: _weaponChapter);
                break;
        }
    }

    private StatusEffectConfig.HitStunType DefaultStunType() => _specialEffectId switch
    {
        "HIT_ICE"  => StatusEffectConfig.HitStunType.Freeze,
        "HIT_FIRE" => StatusEffectConfig.HitStunType.Fire,
        _          => StatusEffectConfig.HitStunType.Stun,
    };

    private static Color DefaultStunColor(StatusEffectConfig.HitStunType type) => type switch
    {
        StatusEffectConfig.HitStunType.Freeze => new Color(0.5f, 0.85f, 1f, 1f),
        StatusEffectConfig.HitStunType.Fire   => new Color(1f,   0.4f,  0.1f, 1f),
        _                                     => Color.white,
    };

    private void ApplyShockwave(Vector2 center, float damage)
    {
        float radius = Mathf.Max(areaHitRadius * 1.5f, 2f);
        int   cnt    = Physics2D.OverlapCircle(center, radius, EnemyFilter, _sharedHitList);
        for (int i = 0; i < cnt; i++)
        {
            var e = _sharedHitList[i].GetComponentInParent<EnemyController>();
            if (e != null)
            {
                Vector2 kDir = ((Vector2)e.transform.position - center).normalized;
                e.TakeDamage(damage, false, kDir, 8f);
                continue;
            }
            var b = _sharedHitList[i].GetComponentInParent<BossController>();
            if (b != null) b.TakeDamage(damage);
        }
        FXManager.Shared?.SpawnHitEffect(center);
    }

    private void FireExtraProjectile(Vector3 spawnPos, Vector2 dir, float dmg)
    {
        var prefab = _behaviour?.GetProjectilePrefab();
        if (prefab == null) return;
        float speed = _behaviour?.GetProjectileSpeed() ?? 8f;
        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p != null)
            p.Init(dmg, dir, speed, criticalRate: criticalRate,
                criticalMultiplier: criticalMultiplier, baseDamage: dmg,
                knockbackPower: knockbackPower);
    }

    private void FireHomingProjectile(Vector3 spawnPos, Vector2 initialDir, float dmg)
    {
        var prefab = StatusEffectConfig.Shared?.GetSubProjectilePrefab(_weaponId)
                     ?? _behaviour?.GetProjectilePrefab();
        if (prefab == null) return;
        float speed = _behaviour?.GetProjectileSpeed() ?? 8f;
        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p == null) return;
        p.Init(dmg, initialDir, speed, criticalRate: criticalRate,
            criticalMultiplier: criticalMultiplier, baseDamage: dmg,
            knockbackPower: knockbackPower);
        p.SetHoming(turnSpeed: 220f);
    }

    private IEnumerator PlayBuffEquipVfx(GameObject prefab, Transform playerTransform)
    {
        const float totalDuration = 0.7f;
        const float flipPeriod    = 0.12f;
        const float riseHeight    = 2.5f;

        var go = Instantiate(prefab, playerTransform.position + Vector3.up * 1.5f, Quaternion.identity);
        Destroy(go, totalDuration + 0.15f);
        var sr = go.GetComponentInChildren<SpriteRenderer>();

        Vector3 startPos  = go.transform.position;
        float   elapsed   = 0f;
        float   flipTimer = 0f;

        while (elapsed < totalDuration)
        {
            if (go == null) yield break;
            elapsed   += Time.deltaTime;
            flipTimer += Time.deltaTime;
            float ratio = elapsed / totalDuration;

            go.transform.position = startPos + Vector3.up * (riseHeight * Mathf.SmoothStep(0f, 1f, ratio));

            if (flipTimer >= flipPeriod * 0.5f)
            {
                flipTimer = 0f;
                var s = go.transform.localScale;
                go.transform.localScale = new Vector3(s.x * -1f, s.y, s.z);
            }

            if (sr != null && ratio > 0.5f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (ratio - 0.5f) / 0.5f);
                var   c     = sr.color;
                sr.color = new Color(c.r, c.g, c.b, alpha);
            }
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private static StatType? BuffTraitToStat(string traitId) => traitId switch
    {
        "BUFF_DEF"  => StatType.DEF,
        "BUFF_CRI"  => StatType.CritChance,
        "BUFF_ATK"  => StatType.ATK,
        "BUFF_HP"   => StatType.MaxHP,
        "BUFF_SPD"  => StatType.MoveSpeed,
        "BUFF_REGEN"=> StatType.HPRegen,
        _           => (StatType?)null,
    };

    private static Vector2 RotateVec(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r),
                           v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    // ═════════════════════════════════════════════════════════════════
    // 기즈모
    // ═════════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
