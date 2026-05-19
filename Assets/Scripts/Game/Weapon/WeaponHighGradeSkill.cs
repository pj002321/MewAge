using Mewage.DB;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 전설/신화 등급 무기 특수 스킬 통합 컴포넌트.
/// f_특성(SpecialEffectId)에 따라 효과가 자동 분기됩니다.
/// </summary>
public class WeaponHighGradeSkill : MonoBehaviour
{
    private enum StatusType { Stun, Freeze, Burn }

    // ── 무기 특성 (DB 자동) ──────────────────────────────────────────────
    [BoxGroup("무기 특성"), ShowInInspector, ReadOnly, LabelText("특성 (DB)")]
    private string DbTraitId
    {
        get
        {
            var wid = GetComponent<WeaponPoint>()?.WeaponId;
            if (string.IsNullOrEmpty(wid)) return "(weaponId 없음)";
            string trait = "";
            DB_무기.ForEachEntity(row => { if (row.f_무기ID == wid) trait = row.f_특성 ?? ""; });
            return trait.Length > 0 ? trait : "(특성 없음)";
        }
    }

    // ── ShowIf 조건 ──────────────────────────────────────────────────────
    private string _traitId => WP?.SpecialEffectId is { Length: > 0 } s ? s : DbTraitId;
    private bool IsMelee    => _traitId is "HIT_STUN" or "HIT_ICE" or "HIT_FIRE";
    private bool IsClone    => _traitId.StartsWith("HIT_CLONE");
    private bool IsRanged   => _traitId == "SHOOT_SUB";
    private bool IsSlash    => _traitId == "HIT_SLASH";
    private bool IsSpin     => _traitId == "HIT_SPIN";
    private bool IsHammer   => _traitId == "HIT_SHOCK";
    private bool IsSpike    => _traitId is "BOSS_SPIKE" or "HIT_SPIKE";
    private bool IsBind     => _traitId == "BOSS_BIND";
    private bool IsGroggy   => _traitId == "BOSS_GROGY";
    private bool IsReduction => _traitId == "(신화 피해감소)";

    // ── [HIT_STUN · HIT_ICE · HIT_FIRE] 근접 상태이상 ──────────────────
    [BoxGroup("[HIT_STUN·ICE·FIRE] 근접"), ShowIf(nameof(IsMelee))]
    [InfoBox("스턴/빙결/화상 VFX 프리팹은 MuAge/Weapon Special Effect Config Editor → 챕터별 VFX 섹션에서 설정하세요.", InfoMessageType.Info)]
    [BoxGroup("[HIT_STUN·ICE·FIRE] 근접"), ShowIf(nameof(IsMelee)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float meleChance = 0.25f;

    [BoxGroup("[HIT_STUN·ICE·FIRE] 근접"), ShowIf(nameof(IsMelee)), LabelText("지속 시간")]
    [SerializeField] private float meleeDuration = 1.5f;

    [BoxGroup("[HIT_STUN·ICE·FIRE] 근접"), ShowIf(nameof(IsMelee)), LabelText("해빙 데미지 (HIT_ICE)")]
    [SerializeField] private float freezeDamageOnThaw = 0f;

    [BoxGroup("[HIT_STUN·ICE·FIRE] 근접"), ShowIf(nameof(IsMelee)), LabelText("빙결 색상 (HIT_ICE)")]
    [SerializeField] private Color freezeColor = new Color(0.5f, 0.85f, 1f, 1f);

    // ── [HIT_CLONE_*] 클론 ───────────────────────────────────────────────
    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone))]
    [InfoBox("복제 투사체 프리팹은 MuAge/Weapon Special Effect Config Editor에서 weaponId별로 설정하세요.", InfoMessageType.Info)]
    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), ShowInInspector, ReadOnly, LabelText("복제 투사체 (Config)")]
    private GameObject ReadonlyClonePrefab => StatusEffectConfig.Shared?.GetClonePrefab(GetComponent<WeaponPoint>()?.WeaponId);

    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float boomerangChance = 0.3f;

    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), LabelText("복제 속도")]
    [SerializeField] private float cloneSpeed = 10f;

    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), LabelText("생존 시간")]
    [SerializeField] private float cloneLifetime = 1.8f;

    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), LabelText("크기 배율")]
    [SerializeField] private float cloneScale = 2f;

    [BoxGroup("[HIT_CLONE_*] 클론"), ShowIf(nameof(IsClone)), LabelText("투명도")]
    [SerializeField, Range(0f, 1f)] private float cloneAlpha = 0.45f;

    // ── [SHOOT_SUB] 원거리 ───────────────────────────────────────────────
    [BoxGroup("[SHOOT_SUB] 원거리"), ShowIf(nameof(IsRanged))]
    [InfoBox("서브 투사체 프리팹은 MuAge/Weapon Special Effect Config Editor에서 weaponId별로 설정하세요.", InfoMessageType.Info)]
    [BoxGroup("[SHOOT_SUB] 원거리"), ShowIf(nameof(IsRanged)), ShowInInspector, ReadOnly, LabelText("서브 투사체 (Config)")]
    private GameObject ReadonlySubPrefab => StatusEffectConfig.Shared?.GetSubProjectilePrefab(GetComponent<WeaponPoint>()?.WeaponId);

    [BoxGroup("[SHOOT_SUB] 원거리"), ShowIf(nameof(IsRanged)), LabelText("각도 오프셋")]
    [SerializeField] private float spreadAngle = 18f;

    [BoxGroup("[SHOOT_SUB] 원거리"), ShowIf(nameof(IsRanged)), LabelText("데미지 비율")]
    [SerializeField] private float rangedDmgRatio = 0.6f;

    [BoxGroup("[SHOOT_SUB] 원거리"), ShowIf(nameof(IsRanged)), LabelText("속도 비율")]
    [SerializeField] private float rangedSpeedRatio = 0.9f;

    // ── [HIT_SLASH] 슬래시 투사체 ──────────────────────────────────────
    [BoxGroup("[HIT_SLASH] 슬래시"), ShowIf(nameof(IsSlash)), LabelText("발사 투사체")]
    [SerializeField] private GameObject slashProjectilePrefab;

    [BoxGroup("[HIT_SLASH] 슬래시"), ShowIf(nameof(IsSlash)), LabelText("데미지 비율")]
    [SerializeField] private float slashDmgRatio = 0.5f;

    [BoxGroup("[HIT_SLASH] 슬래시"), ShowIf(nameof(IsSlash)), LabelText("투사체 속도")]
    [SerializeField] private float slashProjSpeed = 9f;

    // ── [HIT_SPIN] 스핀 ─────────────────────────────────────────────────
    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("반경 배율")]
    [SerializeField] private float radiusMultiplier = 1.4f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("속도 배율")]
    [SerializeField] private float spinSpeedMultiplier = 1.35f;

    // ── [HIT_SHOCK] 해머 충격파 ─────────────────────────────────────────
    [BoxGroup("[HIT_SHOCK] 해머"), ShowIf(nameof(IsHammer)), LabelText("충격파 반경")]
    [SerializeField] private float shockwaveRadius = 4.5f;

    [BoxGroup("[HIT_SHOCK] 해머"), ShowIf(nameof(IsHammer)), LabelText("충격파 데미지 비율")]
    [SerializeField] private float shockwaveDmgRatio = 0.4f;

    // ── [BOSS_SPIKE · HIT_SPIKE] 스파이크 ──────────────────────────────
    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike))]
    [InfoBox("스파이크 프리팹은 MuAge/Weapon Special Effect Config Editor에서 weaponId별로 설정하세요.", InfoMessageType.Info)]
    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), ShowInInspector, ReadOnly, LabelText("스파이크 프리팹 (Config)")]
    private GameObject ReadonlySpikePrefab => StatusEffectConfig.Shared?.GetSpikePrefab(GetComponent<WeaponPoint>()?.WeaponId);

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float spikeChance = 0.2f;

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("틱 데미지")]
    [SerializeField] private float tickDamage = 5f;

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("틱 간격")]
    [SerializeField] private float tickInterval = 0.5f;

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("지속 시간")]
    [SerializeField] private float spikeDuration = 5f;

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("사망 폭발 반경")]
    [SerializeField] private float deathAoeRadius = 2.5f;

    [BoxGroup("[BOSS_SPIKE·HIT_SPIKE] 스파이크"), ShowIf(nameof(IsSpike)), LabelText("사망 폭발 데미지")]
    [SerializeField] private float deathAoeDamage = 20f;

    // ── [BOSS_BIND] 구속 ────────────────────────────────────────────────
    [BoxGroup("[BOSS_BIND] 구속"), ShowIf(nameof(IsBind)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float bindChance = 0.2f;

    [BoxGroup("[BOSS_BIND] 구속"), ShowIf(nameof(IsBind)), LabelText("구속 시간")]
    [SerializeField] private float bindDuration = 2f;

    // ── [BOSS_GROGY] 그로기 ─────────────────────────────────────────────
    [BoxGroup("[BOSS_GROGY] 그로기"), ShowIf(nameof(IsGroggy)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float groggyChance = 0.15f;

    [BoxGroup("[BOSS_GROGY] 그로기"), ShowIf(nameof(IsGroggy)), LabelText("그로기 시간")]
    [SerializeField] private float groggySeconds = 2f;

    // ── [신화] 피해 감소 (패시브) ───────────────────────────────────────
    [BoxGroup("[신화] 피해 감소"), ShowIf(nameof(IsReduction)), LabelText("보스 피해 감소율")]
    [SerializeField, Range(0f, 1f)] private float reductionPct = 0.2f;

    /// <summary>장착 중인 신화 피해 감소율 (PlayerController에서 참조).</summary>
    public static float ActiveDamageReduction { get; private set; }

    // ── 런타임 ───────────────────────────────────────────────────────────
    protected WeaponPoint WP { get; private set; }

    private string TraitId => _traitId;

    // ── 초기화 ───────────────────────────────────────────────────────────

    public void Initialize(WeaponPoint wp) => WP = wp;

    public void OnEquip()
    {
        if (TraitId == "HIT_SPIN") WP?.ApplySpinBoost(radiusMultiplier, spinSpeedMultiplier);
        if (WP != null) WP.OnSlashVfxSpawned += HandleSlashVfx;
        if (IsReduction) ActiveDamageReduction = reductionPct;
    }

    private void OnDestroy()
    {
        if (WP != null) WP.OnSlashVfxSpawned -= HandleSlashVfx;
        if (IsReduction) ActiveDamageReduction = 0f;
    }

    // ── 콜백 ─────────────────────────────────────────────────────────────

    public void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount)
    {
        switch (TraitId)
        {
            case "HIT_STUN":
            case "HIT_ICE":
            case "HIT_FIRE":  DoMeleeHit(hitBuffer, colCount); break;
            case "HIT_SHOCK": DoHammerHit(hitBuffer, colCount); break;
            default:
                if (TraitId.StartsWith("HIT_CLONE")) DoBoomerangHit();
                break;
        }
    }

    public void OnShoot(Vector3 firePos, Vector2 fireDir)
    {
        if (TraitId == "SHOOT_SUB") DoRangedShoot(firePos, fireDir);
    }

    public void OnBossHit(BossController boss, Vector2 hitDir)
    {
        switch (TraitId)
        {
            case "BOSS_SPIKE":
            case "HIT_SPIKE":  DoSpikeHit(boss, null); break;
            case "BOSS_BIND":  DoBindBoss(boss);        break;
            case "BOSS_GROGY": DoGroggyBoss(boss);      break;
        }
    }

    public void OnEnemyHit(EnemyController enemy, Vector2 hitDir)
    {
        if (TraitId is "BOSS_SPIKE" or "HIT_SPIKE") DoSpikeHit(null, enemy);
    }

    // ── 근접 상태이상 구현 ───────────────────────────────────────────────

    private void DoMeleeHit(Collider2D[] hitBuffer, int colCount)
    {
        if (Random.value > meleChance) return;
        var status = TraitId switch
        {
            "HIT_ICE"  => StatusType.Freeze,
            "HIT_FIRE" => StatusType.Burn,
            _          => StatusType.Stun,
        };
        for (int i = 0; i < colCount; i++)
        {
            if (hitBuffer[i] == null) continue;
            if (!hitBuffer[i].TryGetComponent<EnemyController>(out var e)) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            switch (status)
            {
                case StatusType.Stun:   e.Stun(meleeDuration); break;
                case StatusType.Freeze: e.Freeze(meleeDuration, freezeDamageOnThaw, freezeColor); break;
                case StatusType.Burn:   e.ApplyBleed(WP != null ? WP.BleedDamage + 1f : 5f); e.ApplySlow(meleeDuration, 0.5f); break;
            }
        }
    }

    // ── [전설] 부메랑 구현 ───────────────────────────────────────────────

    private static readonly Vector2[] CardinalDirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

    private void DoBoomerangHit()
    {
        if (Random.value > boomerangChance) return;
        Vector3 origin = transform.position;
        var sr = GetComponentInChildren<SpriteRenderer>();
        foreach (var dir in CardinalDirs)
            SpawnBoomerangClone(origin, dir, sr);
    }

    private void SpawnBoomerangClone(Vector3 origin, Vector2 dir, SpriteRenderer sr)
    {
        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(WP?.WeaponId);
        if (prefab != null)
        {
            var go   = Instantiate(prefab, origin, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Init(WP != null ? WP.AttackDamage * 0.6f : 1f, dir, cloneSpeed, lifetime: cloneLifetime);
                go.transform.localScale *= cloneScale;
                SetSpriteAlpha(go, cloneAlpha);
            }
            else Destroy(go, cloneLifetime);
            return;
        }
        if (sr == null || sr.sprite == null) return;
        var ghost              = new GameObject("BoomerangClone");
        ghost.transform.position   = origin;
        ghost.transform.localScale = transform.lossyScale * cloneScale;
        ghost.transform.rotation   = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        var ghostSr            = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite         = sr.sprite;
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder;
        ghostSr.color          = new Color(1f, 1f, 1f, cloneAlpha);
        ghost.AddComponent<BoomerangCloneMover>().Init(dir, cloneSpeed, cloneLifetime);
    }

    private static void SetSpriteAlpha(GameObject go, float alpha)
    {
        foreach (var r in go.GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = r.color; c.a = alpha; r.color = c;
        }
    }

    // ── [전설] 원거리 구현 ───────────────────────────────────────────────

    private void DoRangedShoot(Vector3 firePos, Vector2 fireDir)
    {
        var prefab = StatusEffectConfig.Shared?.GetSubProjectilePrefab(WP?.WeaponId);
        if (prefab == null || WP == null) return;
        float dmg   = WP.AttackDamage * rangedDmgRatio;
        float speed = WP.ProjectileSpeed * rangedSpeedRatio;
        SpawnRangedSub(prefab, firePos, RotateDir(fireDir, -spreadAngle), dmg, speed);
        SpawnRangedSub(prefab, firePos, RotateDir(fireDir,  spreadAngle), dmg, speed);
    }

    private void SpawnRangedSub(GameObject prefab, Vector3 pos, Vector2 dir, float dmg, float speed)
    {
        var go   = Instantiate(prefab, pos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }
        proj.Init(dmg, dir, speed,
            criticalRate: WP.CriticalRate, criticalMultiplier: WP.CriticalDamage,
            baseDamage: dmg, knockbackPower: WP.KnockbackPower * 0.5f);
    }

    // ── [전설] 슬래시 구현 ───────────────────────────────────────────────

    private void HandleSlashVfx(Vector3 vfxPos, Vector2 slashDir)
    {
        if (slashProjectilePrefab == null || WP == null) return;
        var go   = Instantiate(slashProjectilePrefab, vfxPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }
        proj.Init(WP.AttackDamage * slashDmgRatio, slashDir, slashProjSpeed,
            criticalRate: WP.CriticalRate, criticalMultiplier: WP.CriticalDamage,
            baseDamage: WP.AttackDamage * slashDmgRatio, knockbackPower: WP.KnockbackPower * 0.5f);
    }

    // ── [전설] 해머 구현 ─────────────────────────────────────────────────

    private static readonly Collider2D[] _shockBuffer = new Collider2D[32];

    private void DoHammerHit(Collider2D[] hitBuffer, int colCount)
    {
        if (WP == null) return;
        int   shockCount = Physics2D.OverlapCircleNonAlloc(transform.position, shockwaveRadius, _shockBuffer);
        float dmg        = WP.AttackDamage * shockwaveDmgRatio;
        for (int i = 0; i < shockCount; i++)
        {
            if (_shockBuffer[i] == null) continue;
            bool alreadyHit = false;
            for (int j = 0; j < colCount; j++)
                if (hitBuffer[j] == _shockBuffer[i]) { alreadyHit = true; break; }
            if (alreadyHit) continue;

            var boss = _shockBuffer[i].GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                Vector2 d = ((Vector2)boss.transform.position - (Vector2)transform.position).normalized;
                boss.TakeDamage(dmg, false, d.sqrMagnitude > 0.001f ? d : Vector2.up, 0f);
                continue;
            }
            var e = _shockBuffer[i].GetComponentInParent<EnemyController>();
            if (e != null && e.gameObject.activeInHierarchy)
            {
                Vector2 d = ((Vector2)e.transform.position - (Vector2)transform.position).normalized;
                e.TakeDamage(dmg, false, d.sqrMagnitude > 0.001f ? d : Vector2.up, 0f);
            }
        }
    }

    // ── [신화] 스파이크 구현 ─────────────────────────────────────────────

    private void DoSpikeHit(BossController boss, EnemyController enemy)
    {
        var prefab = StatusEffectConfig.Shared?.GetSpikePrefab(WP?.WeaponId);
        if (prefab == null || Random.value > spikeChance) return;
        if (boss != null)
        {
            var s = Instantiate(prefab, boss.transform.position, Quaternion.identity);
            s.AddComponent<SpikeTicker>().InitBoss(boss, tickDamage, tickInterval, spikeDuration);
        }
        else if (enemy != null)
        {
            var s = Instantiate(prefab, enemy.transform.position, Quaternion.identity);
            s.AddComponent<SpikeTicker>().InitEnemy(enemy, tickDamage, tickInterval, spikeDuration, deathAoeRadius, deathAoeDamage);
        }
    }

    // ── [신화] 구속 구현 ─────────────────────────────────────────────────

    private void DoBindBoss(BossController boss)
    {
        if (boss == null || boss.IsMoveBound || boss.IsGrogy) return;
        if (Random.value > bindChance) return;
        boss.BindMovement(bindDuration);
    }

    // ── [신화] 그로기 구현 ───────────────────────────────────────────────

    private void DoGroggyBoss(BossController boss)
    {
        if (boss == null || boss.IsGrogy) return;
        if (Random.value > groggyChance) return;
        boss.TriggerGroggy(groggySeconds);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────

    private static Vector2 RotateDir(Vector2 dir, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 헬퍼 클래스
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>스파이크가 대상에 달라붙어 틱 피해를 주고, 대상 사망 시 폭발.</summary>
public class SpikeTicker : MonoBehaviour
{
    private BossController  _boss;
    private EnemyController _enemy;
    private float           _dmg;
    private float           _interval;
    private float           _timer;
    private float           _aoeRadius;
    private float           _aoeDmg;
    private bool            _targetDied;

    private static readonly Collider2D[] _aoeBuffer = new Collider2D[24];

    public void InitBoss(BossController boss, float dmg, float interval, float duration)
    {
        _boss = boss; _dmg = dmg; _interval = interval; _timer = interval;
        Destroy(gameObject, duration);
    }

    public void InitEnemy(EnemyController enemy, float dmg, float interval, float duration,
                          float aoeRadius, float aoeDmg)
    {
        _enemy = enemy; _dmg = dmg; _interval = interval; _timer = interval;
        _aoeRadius = aoeRadius; _aoeDmg = aoeDmg;
        enemy.OnDeath += OnTargetDied;
        Destroy(gameObject, duration);
    }

    private void OnTargetDied()
    {
        _targetDied = true;
        if (_enemy != null) _enemy.OnDeath -= OnTargetDied;
        DealAoe(transform.position);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_enemy != null && !_targetDied) _enemy.OnDeath -= OnTargetDied;
    }

    private void Update()
    {
        if (_targetDied) return;
        if (_boss != null)
        {
            if (!_boss.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            transform.position = _boss.transform.position;
            Tick(() => _boss.TakeDamage(_dmg, false));
        }
        else if (_enemy != null)
        {
            if (!_enemy.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            transform.position = _enemy.transform.position;
            Tick(() => _enemy.TakeDamage(_dmg, false));
        }
    }

    private void Tick(System.Action deal)
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) { deal(); _timer = _interval; }
    }

    private void DealAoe(Vector3 pos)
    {
        if (_aoeRadius <= 0f || _aoeDmg <= 0f) return;
        int count = Physics2D.OverlapCircleNonAlloc(pos, _aoeRadius, _aoeBuffer);
        for (int i = 0; i < count; i++)
        {
            if (_aoeBuffer[i] == null) continue;
            var b = _aoeBuffer[i].GetComponentInParent<BossController>();
            if (b != null && b.gameObject.activeInHierarchy) { b.TakeDamage(_aoeDmg, false); continue; }
            var e = _aoeBuffer[i].GetComponentInParent<EnemyController>();
            if (e != null && e.gameObject.activeInHierarchy) e.TakeDamage(_aoeDmg, false);
        }
    }
}

/// <summary>부메랑 복제본 직선 이동 후 자동 소멸.</summary>
public class BoomerangCloneMover : MonoBehaviour
{
    private Vector2 _dir;
    private float   _speed;

    public void Init(Vector2 dir, float speed, float lifetime)
    {
        _dir = dir; _speed = speed;
        Destroy(gameObject, lifetime);
    }

    private void Update() => transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
}
