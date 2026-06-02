using System.Collections;
using System.Collections.Generic;
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
    private bool IsSpear    => _traitId == "HIT_SPEAR";
    private bool IsTrail    => _traitId == "HIT_CLONE_TRAIL";

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

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 발동 확률")]
    [SerializeField, Range(0f, 1f)] private float spinCloneChance = 0.3f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 공전 반경")]
    [SerializeField] private float spinCloneRadius = 1.5f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 공전 속도")]
    [SerializeField] private float spinCloneSpeed = 220f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 지속 시간")]
    [SerializeField] private float spinCloneDuration = 3f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 타격 반경")]
    [SerializeField] private float spinCloneHitRadius = 0.4f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("클론 투명도")]
    [SerializeField, Range(0f, 1f)] private float spinCloneAlpha = 0.5f;

    [BoxGroup("[HIT_SPIN] 스핀"), ShowIf(nameof(IsSpin)), LabelText("최대 동시 클론")]
    [SerializeField] private int spinCloneMaxCount = 3;

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

    // ── [HIT_SPEAR] 천공의 창 ───────────────────────────────────────────
    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float spearChance = 0.25f;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("창 프리팹")]
    [SerializeField] private GameObject spearPrefab;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("낙하 높이")]
    [SerializeField] private float spearDropHeight = 8f;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("사선 수평 오프셋")]
    [SerializeField] private float spearSideOffset = 3f;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("이동 속도")]
    [SerializeField] private float spearSpeed = 18f;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("데미지 비율")]
    [SerializeField] private float spearDmgRatio = 1f;

    [BoxGroup("[HIT_SPEAR] 천공의 창"), ShowIf(nameof(IsSpear)), LabelText("타격 반경")]
    [SerializeField] private float spearHitRadius = 0.45f;

    // ── [HIT_CLONE_TRAIL] 흔적 ──────────────────────────────────────────
    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("발동 확률")]
    [SerializeField, Range(0f, 1f)] private float trailChance = 0.3f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("클론 프리팹")]
    [SerializeField] private GameObject trailClonePrefab;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("마커 프리팹")]
    [SerializeField] private GameObject trailMarkerPrefab;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("이동 속도")]
    [SerializeField] private float trailSpeed = 6f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("이동 거리")]
    [SerializeField] private float trailRange = 8f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("데미지 비율")]
    [SerializeField] private float trailDmgRatio = 0.4f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("마커 지속 시간")]
    [SerializeField] private float trailDuration = 2f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("마커 생성 간격")]
    [SerializeField] private float trailSpawnInterval = 0.15f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("마커 타격 반경")]
    [SerializeField] private float trailHitRadius = 0.5f;

    [BoxGroup("[HIT_CLONE_TRAIL] 흔적"), ShowIf(nameof(IsTrail)), LabelText("클론 투명도")]
    [SerializeField, Range(0f, 1f)] private float trailCloneAlpha = 0.5f;

    // ── [신화] 피해 감소 (패시브) ───────────────────────────────────────
    [BoxGroup("[신화] 피해 감소"), ShowIf(nameof(IsReduction)), LabelText("보스 피해 감소율")]
    [SerializeField, Range(0f, 1f)] private float reductionPct = 0.2f;

    /// <summary>장착 중인 신화 피해 감소율 (PlayerController에서 참조).</summary>
    public static float ActiveDamageReduction { get; private set; }

    // ── 런타임 ───────────────────────────────────────────────────────────
    protected WeaponPoint WP { get; private set; }
    private int _activeSpinClones;

    private string TraitId => _traitId;

    // ── SO 파라미터 오버라이드 헬퍼 ─────────────────────────────────────
    private string Wid => WP?.WeaponId ?? "";
    private StatusEffectConfig Cfg => StatusEffectConfig.Shared;

    private float P(System.Func<StatusEffectConfig.WeaponEntry, float> sel, float def)
        => Cfg?.GetP(Wid, sel, def) ?? def;
    private int PI(System.Func<StatusEffectConfig.WeaponEntry, int> sel, int def)
        => Cfg?.GetPI(Wid, sel, def) ?? def;
    private Color PC(System.Func<StatusEffectConfig.WeaponEntry, Color> sel, Color def)
        => Cfg?.GetPC(Wid, sel, def) ?? def;
    private GameObject PGO(System.Func<StatusEffectConfig.WeaponEntry, GameObject> sel)
        => Cfg?.GetPGO(Wid, sel);

    // ── 초기화 ───────────────────────────────────────────────────────────

    public void Initialize(WeaponPoint wp) => WP = wp;

    public void OnEquip()
    {
        float rMult = P(e => e.p_spinRadiusMult, radiusMultiplier);
        float sMult = P(e => e.p_spinSpeedMult,  spinSpeedMultiplier);
        if (TraitId == "HIT_SPIN") WP?.ApplySpinBoost(rMult, sMult);
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
            case "HIT_FIRE":       DoMeleeHit(hitBuffer, colCount); break;
            case "HIT_SHOCK":      DoHammerHit(hitBuffer, colCount); break;
            case "HIT_SPIN":       DoSpinClone(); break;
            case "HIT_CLONE_RETURN": DoCloneReturn(); break;
            case "HIT_CLONE_SPREAD": DoCloneSpread(); break;
            case "HIT_CLONE_BURST":  DoCloneBurst();  break;
            case "HIT_CLONE_TRAIL":
                DoCloneTrail(WP != null ? (Vector2)WP.transform.position : (Vector2)transform.position, hitDir);
                break;
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
            case "HIT_SPIKE":  DoSpikeHit(boss, null);                          break;
            case "BOSS_BIND":  DoBindBoss(boss);                                 break;
            case "BOSS_GROGY": DoGroggyBoss(boss);                               break;
            case "HIT_SPEAR":  DoSpearHit(boss.transform.position, null, boss); break;
        }
    }

    public void OnEnemyHit(EnemyController enemy, Vector2 hitDir)
    {
        switch (TraitId)
        {
            case "BOSS_SPIKE":
            case "HIT_SPIKE": DoSpikeHit(null, enemy); break;
            case "HIT_SPEAR": DoSpearHit(enemy.transform.position, enemy); break;
        }
    }

    // ── 근접 상태이상 구현 ───────────────────────────────────────────────

    private void DoMeleeHit(Collider2D[] hitBuffer, int colCount)
    {
        float chance = P(e => e.triggerChance, meleChance);
        if (Random.value > chance) return;
        var status = TraitId switch
        {
            "HIT_ICE"  => StatusType.Freeze,
            "HIT_FIRE" => StatusType.Burn,
            _          => StatusType.Stun,
        };
        float dur    = P(e => e.p_stunDuration, meleeDuration);
        float thaw   = P(e => e.p_freezeThawDmg, freezeDamageOnThaw);
        Color fColor = PC(e => e.hitStunColor, freezeColor);
        for (int i = 0; i < colCount; i++)
        {
            if (hitBuffer[i] == null) continue;
            if (!hitBuffer[i].TryGetComponent<EnemyController>(out var e)) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            switch (status)
            {
                case StatusType.Stun:   e.Stun(dur); break;
                case StatusType.Freeze: e.Freeze(dur, thaw, fColor); break;
                case StatusType.Burn:   e.ApplyBleed(WP != null ? WP.BleedDamage + 1f : 5f); e.ApplySlow(dur, 0.5f); break;
            }
        }
    }

    // ── [전설] HIT_SPIN 공전 클론 ────────────────────────────────────────

    private void DoSpinClone()
    {
        float sChance = P(e => e.p_spinCloneChance, spinCloneChance);
        int   sMax    = PI(e => e.p_spinCloneMax,  spinCloneMaxCount);
        if (Random.value > sChance || _activeSpinClones >= sMax) return;
        var player = PlayerController.Instance;
        if (player == null) return;

        float dmg = WP != null ? WP.AttackDamage : 1f;
        GameObject go;

        float sAlpha = P(e => e.p_spinCloneAlpha, spinCloneAlpha);
        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(WP?.WeaponId);
        if (prefab != null)
        {
            go = Instantiate(prefab, player.transform.position, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null) proj.enabled = false;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            SetSpriteAlpha(go, sAlpha);
        }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            go = new GameObject("SpinOrbitClone");
            var cloneSr        = go.AddComponent<SpriteRenderer>();
            cloneSr.sprite         = sr.sprite;
            cloneSr.sortingLayerID = sr.sortingLayerID;
            cloneSr.sortingOrder   = sr.sortingOrder;
            cloneSr.color          = new Color(1f, 1f, 1f, sAlpha);
            go.transform.localScale = transform.lossyScale;
        }

        float sRadius   = P(e => e.p_spinCloneRadius,    spinCloneRadius);
        float sSpeed    = P(e => e.p_spinCloneSpeed,     spinCloneSpeed);
        float sDuration = P(e => e.p_spinCloneDuration,  spinCloneDuration);
        float sHitR     = P(e => e.p_spinCloneHitRadius, spinCloneHitRadius);
        _activeSpinClones++;
        go.AddComponent<SpinOrbitClone>().Init(
            player.transform, sRadius, sSpeed,
            sDuration, sHitR, dmg,
            () => _activeSpinClones--);
    }

    // ── [전설] 부메랑 구현 ───────────────────────────────────────────────

    private static readonly Vector2[] CardinalDirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private static readonly Vector2[] EightDirs    =
    {
        Vector2.up,   new Vector2( 0.707f,  0.707f), Vector2.right, new Vector2( 0.707f, -0.707f),
        Vector2.down, new Vector2(-0.707f, -0.707f), Vector2.left,  new Vector2(-0.707f,  0.707f),
    };

    private void DoCloneReturn()
    {
        if (Random.value > P(e => e.triggerChance, boomerangChance) || WP == null) return;

        Vector2 origin = WP.transform.parent != null
            ? (Vector2)WP.transform.parent.position : (Vector2)transform.position;
        Vector2 dir   = GetNearestEnemyDir(origin);
        float   range = StatusEffectConfig.Shared?.GetCloneRange(WP.WeaponId, 6f) ?? 6f;
        float   dmg   = WP.AttackDamage * 0.5f;
        float   cAlpha = P(e => e.p_cloneAlpha, cloneAlpha);
        float   cScale = P(e => e.p_cloneScale, cloneScale);

        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(WP.WeaponId);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, origin, Quaternion.identity);
            var proj = go.GetComponent<Projectile>(); if (proj != null) Destroy(proj);
            var rb = go.GetComponent<Rigidbody2D>(); if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            SetSpriteAlpha(go, cAlpha);
        }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            go = new GameObject("ReturnClone");
            go.transform.position   = origin;
            go.transform.localScale = transform.lossyScale * cScale;
            var csr            = go.AddComponent<SpriteRenderer>();
            csr.sprite         = sr.sprite;
            csr.sortingLayerID = sr.sortingLayerID;
            csr.sortingOrder   = sr.sortingOrder;
            csr.color          = new Color(1f, 1f, 1f, cAlpha);
        }

        float outTime    = P(e => e.cloneOutTime,    0.45f);
        float pauseTime  = P(e => e.clonePauseTime,  0.25f);
        float returnTime = P(e => e.cloneReturnTime, 0.35f);
        StartCoroutine(ReturnArcRoutine(go, origin, origin + dir * range, dmg, outTime, pauseTime, returnTime));
    }

    private static readonly Collider2D[] _returnBuf = new Collider2D[16];

    private IEnumerator ReturnArcRoutine(GameObject go, Vector2 from, Vector2 to, float damage,
        float outTime = 0.45f, float pauseTime = 0.25f, float returnTime = 0.35f)
    {
        var hitIds = new HashSet<int>();

        // 날아가기
        for (float t = 0f; t < outTime; t += Time.deltaTime)
        {
            if (go == null) yield break;
            go.transform.position = Vector2.Lerp(from, to, t / outTime);
            DealReturnHit(go.transform.position, damage, hitIds);
            yield return null;
        }
        if (go == null) yield break;
        go.transform.position = to;

        yield return new WaitForSeconds(pauseTime);

        // 귀환
        for (float t = 0f; t < returnTime; t += Time.deltaTime)
        {
            if (go == null) yield break;
            Vector2 playerPos = WP?.transform.parent != null
                ? (Vector2)WP.transform.parent.position : from;
            go.transform.position = Vector2.Lerp(to, playerPos, t / returnTime);
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private static void DealReturnHit(Vector2 pos, float damage, HashSet<int> hitIds)
    {
        int cnt = Physics2D.OverlapCircleNonAlloc(pos, 0.35f, _returnBuf);
        for (int i = 0; i < cnt; i++)
        {
            var col = _returnBuf[i]; if (col == null) continue;
            var boss = col.GetComponentInParent<BossController>();
            if (boss != null) { boss.TakeDamage(damage, false); continue; }
            var enemy = col.GetComponentInParent<EnemyController>();
            if (enemy != null && hitIds.Add(enemy.GetInstanceID()))
                enemy.TakeDamage(damage, false);
        }
    }

    private void DoBoomerangHit()
    {
        if (Random.value > P(e => e.triggerChance, boomerangChance)) return;
        Vector3 origin = transform.position;
        var sr = GetComponentInChildren<SpriteRenderer>();
        foreach (var dir in CardinalDirs)
            SpawnBoomerangClone(origin, dir, sr);
    }

    private void DoCloneBurst()
    {
        if (Random.value > P(e => e.triggerChance, boomerangChance)) return;
        Vector3 origin  = transform.position;
        var     sr      = GetComponentInChildren<SpriteRenderer>();
        Vector2 forward = GetNearestEnemyDir(origin);
        Vector2 perp    = new Vector2(-forward.y, forward.x);

        // 전방 방향으로 4발 병렬 발사 (perpendicular 오프셋으로 퍼트림)
        float[] slots = { -1.5f, -0.5f, 0.5f, 1.5f };
        foreach (float s in slots)
            SpawnBoomerangClone(origin + (Vector3)(perp * s * 0.3f), forward, sr);
    }

    private static Vector2 GetNearestEnemyDir(Vector3 origin)
    {
        float   bestSqr = float.MaxValue;
        Vector2 best    = Vector2.up;
        foreach (var e in EnemyController.All)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            float d = ((Vector2)(e.transform.position - origin)).sqrMagnitude;
            if (d >= bestSqr) continue;
            bestSqr = d;
            best    = (e.transform.position - origin).normalized;
        }
        return best;
    }

    private void DoCloneSpread()
    {
        if (Random.value > P(e => e.triggerChance, boomerangChance)) return;
        Vector3 origin = transform.position;
        var sr = GetComponentInChildren<SpriteRenderer>();
        foreach (var dir in EightDirs)
            SpawnBoomerangClone(origin, dir, sr);
    }

    private void SpawnBoomerangClone(Vector3 origin, Vector2 dir, SpriteRenderer sr)
    {
        float cSpeed    = P(e => e.p_cloneSpeed,    cloneSpeed);
        float cLifetime = P(e => e.p_cloneLifetime, cloneLifetime);
        float cScale    = P(e => e.p_cloneScale,    cloneScale);
        float cAlpha    = P(e => e.p_cloneAlpha,    cloneAlpha);

        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(WP?.WeaponId);
        if (prefab != null)
        {
            var go   = Instantiate(prefab, origin, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Init(WP != null ? WP.AttackDamage * 0.6f : 1f, dir, cSpeed, lifetime: cLifetime);
                go.transform.localScale *= cScale;
                SetSpriteAlpha(go, cAlpha);
            }
            else Destroy(go, cLifetime);
            return;
        }
        if (sr == null || sr.sprite == null) return;
        var ghost              = new GameObject("BoomerangClone");
        ghost.transform.position   = origin;
        ghost.transform.localScale = transform.lossyScale * cScale;
        ghost.transform.rotation   = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        var ghostSr            = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite         = sr.sprite;
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder;
        ghostSr.color          = new Color(1f, 1f, 1f, cAlpha);
        ghost.AddComponent<BoomerangCloneMover>().Init(dir, cSpeed, cLifetime);
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
        float angle = P(e => e.p_spreadAngle,  spreadAngle);
        float dmg   = WP.AttackDamage * P(e => e.p_subDmgRatio,   rangedDmgRatio);
        float speed = WP.ProjectileSpeed * P(e => e.p_subSpeedRatio, rangedSpeedRatio);
        SpawnRangedSub(prefab, firePos, RotateDir(fireDir, -angle), dmg, speed);
        SpawnRangedSub(prefab, firePos, RotateDir(fireDir,  angle), dmg, speed);
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
        var prefab = PGO(e => e.p_slashPrefab) ?? slashProjectilePrefab;
        if (prefab == null || WP == null) return;
        float ratio = P(e => e.p_slashDmgRatio, slashDmgRatio);
        float spd   = P(e => e.p_slashSpeed,    slashProjSpeed);
        var go   = Instantiate(prefab, vfxPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }
        proj.Init(WP.AttackDamage * ratio, slashDir, spd,
            criticalRate: WP.CriticalRate, criticalMultiplier: WP.CriticalDamage,
            baseDamage: WP.AttackDamage * ratio, knockbackPower: WP.KnockbackPower * 0.5f);
    }

    // ── [전설] 해머 구현 ─────────────────────────────────────────────────

    private static readonly Collider2D[] _shockBuffer = new Collider2D[32];

    private void DoHammerHit(Collider2D[] hitBuffer, int colCount)
    {
        if (WP == null) return;
        float radius = P(e => e.p_shockRadius,   shockwaveRadius);
        float ratio  = P(e => e.p_shockDmgRatio, shockwaveDmgRatio);
        int   shockCount = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _shockBuffer);
        float dmg        = WP.AttackDamage * ratio;
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
        if (prefab == null || Random.value > P(e => e.triggerChance, spikeChance)) return;
        float tick     = P(e => e.p_spikeTickDmg,      tickDamage);
        float interval = P(e => e.p_spikeTickInterval, tickInterval);
        float dur      = P(e => e.p_spikeDuration,     spikeDuration);
        float aoeR     = P(e => e.p_spikeDeathRadius,  deathAoeRadius);
        float aoeDmg   = P(e => e.p_spikeDeathDmg,     deathAoeDamage);
        if (boss != null)
        {
            var s = Instantiate(prefab, boss.transform.position, Quaternion.identity);
            s.AddComponent<SpikeTicker>().InitBoss(boss, tick, interval, dur);
        }
        else if (enemy != null)
        {
            var s = Instantiate(prefab, enemy.transform.position, Quaternion.identity);
            s.AddComponent<SpikeTicker>().InitEnemy(enemy, tick, interval, dur, aoeR, aoeDmg);
        }
    }

    // ── [신화] 구속 구현 ─────────────────────────────────────────────────

    private void DoBindBoss(BossController boss)
    {
        if (boss == null || boss.IsMoveBound || boss.IsGrogy) return;
        if (Random.value > P(e => e.triggerChance, bindChance)) return;
        boss.BindMovement(P(e => e.p_bindDuration, bindDuration));
    }

    // ── [신화] 그로기 구현 ───────────────────────────────────────────────

    private void DoGroggyBoss(BossController boss)
    {
        if (boss == null || boss.IsGrogy) return;
        if (Random.value > P(e => e.triggerChance, groggyChance)) return;
        boss.TriggerGroggy(P(e => e.p_groggySeconds, groggySeconds));
    }

    // ── [HIT_SPEAR] 천공의 창 구현 ──────────────────────────────────────

    private int _lastSpearFrame = -1;

    internal void DoSpearHit(Vector2 targetPos, EnemyController enemy = null, BossController boss = null)
    {
        // 같은 프레임 내 중복 발동 방지 (TrySpecialEffectEnemy + ApplyGradeProc 동시 경로 대응)
        if (Time.frameCount == _lastSpearFrame) return;
        if (WP == null || Random.value > P(e => e.triggerChance, spearChance)) return;
        _lastSpearFrame = Time.frameCount;

        var prefab = PGO(e => e.p_spearPrefab) ?? spearPrefab;
        if (prefab == null) return;

        float dmg    = WP.AttackDamage * P(e => e.p_spearDmgRatio,    spearDmgRatio);
        float height = P(e => e.p_spearDropHeight, spearDropHeight);
        float side   = P(e => e.p_spearSideOffset, spearSideOffset);
        float speed  = P(e => e.p_spearSpeed,      spearSpeed);
        float hitR   = P(e => e.p_spearHitRadius,  spearHitRadius);

        Vector2 startPos = targetPos + new Vector2(side, height);
        Vector2 dir      = (targetPos - startPos).normalized;
        float   rotZ     = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        var landingVfx = PGO(e => e.p_spearLandingVfx);
        var go = Instantiate(prefab, (Vector3)startPos, Quaternion.Euler(0f, 0f, rotZ));
        go.AddComponent<SpearDrop>().Init(targetPos, speed, dmg, hitR, enemy, boss, landingVfx);
    }

    // ── [HIT_CLONE_TRAIL] 흔적 구현 ─────────────────────────────────────

    private int _lastTrailFrame = -1;

    internal void DoCloneTrail(Vector2 origin, Vector2 dir)
    {
        if (Time.frameCount == _lastTrailFrame) return;
        if (WP == null || Random.value > P(e => e.triggerChance, trailChance)) return;
        _lastTrailFrame = Time.frameCount;

        float dmg        = WP.AttackDamage * P(e => e.p_trailDmgRatio,      trailDmgRatio);
        float alpha      = P(e => e.p_trailCloneAlpha,  trailCloneAlpha);
        float speed      = P(e => e.p_trailSpeed,        trailSpeed);
        float range      = P(e => e.p_trailRange,        trailRange);
        float duration   = P(e => e.p_trailDuration,     trailDuration);
        float interval   = P(e => e.p_trailSpawnInterval,trailSpawnInterval);
        float hitR       = P(e => e.p_trailHitRadius,    trailHitRadius);
        var   clonePrefab  = PGO(e => e.p_trailClonePrefab)  ?? trailClonePrefab;
        var   markerPrefab = PGO(e => e.p_trailMarkerPrefab) ?? trailMarkerPrefab;

        GameObject cloneGo;
        if (clonePrefab != null)
        {
            cloneGo = Instantiate(clonePrefab, origin, Quaternion.identity);
            var proj = cloneGo.GetComponent<Projectile>(); if (proj != null) Destroy(proj);
            var rb   = cloneGo.GetComponent<Rigidbody2D>(); if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            SetSpriteAlpha(cloneGo, alpha);
        }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            cloneGo = new GameObject("TrailClone");
            cloneGo.transform.position = origin;
            var csr            = cloneGo.AddComponent<SpriteRenderer>();
            csr.sprite         = sr.sprite;
            csr.sortingLayerID = sr.sortingLayerID;
            csr.sortingOrder   = sr.sortingOrder;
            csr.color          = new Color(1f, 1f, 1f, alpha);
        }

        cloneGo.AddComponent<TrailClone>().Init(
            dir, speed, range, dmg, duration, interval, hitR, markerPrefab);
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

/// <summary>HIT_SPIN 고급 특성 — 플레이어 주위를 공전하며 적에게 피해를 주는 복제본.</summary>
public class SpinOrbitClone : MonoBehaviour
{
    private Transform       _center;
    private float           _radius;
    private float           _speed;
    private float           _hitRadius;
    private float           _damage;
    private float           _angle;
    private System.Action   _onRemove;
    private float           _bossTimer;

    private static readonly Collider2D[] _buf = new Collider2D[16];
    private readonly System.Collections.Generic.Dictionary<EnemyController, float> _hitCd = new();
    private readonly System.Collections.Generic.List<EnemyController>              _expired = new();

    public void Init(Transform center, float radius, float speed, float duration,
                     float hitRadius, float damage, System.Action onRemove)
    {
        _center    = center;
        _radius    = radius;
        _speed     = speed;
        _hitRadius = hitRadius;
        _damage    = damage;
        _onRemove  = onRemove;
        _angle     = Random.Range(0f, 360f);
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        if (_center == null) { Destroy(gameObject); return; }

        _angle += _speed * Time.deltaTime;
        float rad = _angle * Mathf.Deg2Rad;
        transform.position = _center.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius;
        transform.rotation = Quaternion.Euler(0f, 0f, _angle - 90f);

        // 쿨타임 감소
        _expired.Clear();
        foreach (var k in _hitCd.Keys)
        {
            _hitCd[k] -= Time.deltaTime;
            if (_hitCd[k] <= 0f) _expired.Add(k);
        }
        foreach (var k in _expired) _hitCd.Remove(k);

        _bossTimer -= Time.deltaTime;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, _hitRadius, _buf);
        for (int i = 0; i < count; i++)
        {
            if (_buf[i] == null) continue;
            var boss = _buf[i].GetComponentInParent<BossController>();
            if (boss != null && _bossTimer <= 0f)
            {
                boss.TakeDamage(_damage, false);
                _bossTimer = 0.5f;
                continue;
            }
            var enemy = _buf[i].GetComponentInParent<EnemyController>();
            if (enemy != null && !_hitCd.ContainsKey(enemy))
            {
                enemy.TakeDamage(_damage, false);
                _hitCd[enemy] = 0.5f;
            }
        }
    }

    private void OnDestroy() => _onRemove?.Invoke();
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

/// <summary>HIT_SPEAR — 사선으로 낙하해 대상에 꽂히는 창.</summary>
public class SpearDrop : MonoBehaviour
{
    private Vector2         _target;
    private float           _speed;
    private float           _damage;
    private float           _hitRadius;
    private EnemyController _enemy;
    private BossController  _boss;
    private GameObject      _landingVfx;
    private bool            _landed;

    public void Init(Vector2 target, float speed, float damage, float hitRadius,
                     EnemyController enemy, BossController boss, GameObject landingVfx = null)
    {
        _target     = target;
        _speed      = speed;
        _damage     = damage;
        _hitRadius  = hitRadius;
        _enemy      = enemy;
        _boss       = boss;
        _landingVfx = landingVfx;
        Destroy(gameObject, 4f);
    }

    private void Update()
    {
        if (_landed) return;
        Vector2 pos  = transform.position;
        Vector2 diff = _target - pos;
        float   step = _speed * Time.deltaTime;

        if (diff.magnitude <= step)
        {
            transform.position = _target;
            Land();
            return;
        }
        transform.position = pos + diff.normalized * step;
    }

    private void Land()
    {
        _landed = true;
        if (_landingVfx != null)
            Instantiate(_landingVfx, _target, Quaternion.identity);
        if (_enemy != null && _enemy.gameObject.activeInHierarchy)
            _enemy.TakeDamage(_damage, false);
        else if (_boss != null && _boss.gameObject.activeInHierarchy)
            _boss.TakeDamage(_damage, false);
        Destroy(gameObject, 0.3f);
    }
}

/// <summary>HIT_CLONE_TRAIL — 한 방향으로 이동하며 순차적으로 흔적 마커를 생성하는 클론.</summary>
public class TrailClone : MonoBehaviour
{
    private Vector2    _dir;
    private float      _speed;
    private float      _remainDist;
    private float      _damage;
    private float      _markerDuration;
    private float      _spawnInterval;
    private float      _hitRadius;
    private GameObject _markerPrefab;
    private float      _nextSpawn;

    public void Init(Vector2 dir, float speed, float range, float damage,
                     float markerDuration, float spawnInterval, float hitRadius,
                     GameObject markerPrefab)
    {
        _dir            = dir.normalized;
        _speed          = speed;
        _remainDist     = range;
        _damage         = damage;
        _markerDuration = markerDuration;
        _spawnInterval  = spawnInterval;
        _hitRadius      = hitRadius;
        _markerPrefab   = markerPrefab;

        float rotZ = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZ);

        SpawnMarker();
        _nextSpawn = _spawnInterval;
    }

    private void Update()
    {
        if (_remainDist <= 0f) { Destroy(gameObject); return; }

        float step = _speed * Time.deltaTime;
        transform.position += (Vector3)(_dir * step);
        _remainDist -= step;

        _nextSpawn -= Time.deltaTime;
        if (_nextSpawn <= 0f)
        {
            SpawnMarker();
            _nextSpawn = _spawnInterval;
        }
    }

    private void SpawnMarker()
    {
        var go = _markerPrefab != null
            ? Instantiate(_markerPrefab, transform.position, Quaternion.identity)
            : new GameObject("TrailMarker");
        if (_markerPrefab == null) go.transform.position = transform.position;
        go.AddComponent<TrailMarker>().Init(_damage, _markerDuration, _hitRadius);
    }
}

/// <summary>HIT_CLONE_TRAIL — 일정 반경 내 적에게 지속적으로 피해를 주는 잔류 마커.</summary>
public class TrailMarker : MonoBehaviour
{
    private float _damage;
    private float _hitRadius;
    private float _bossCd;

    private readonly System.Collections.Generic.Dictionary<EnemyController, float> _hitCd   = new();
    private readonly System.Collections.Generic.List<EnemyController>              _expired = new();
    private readonly Collider2D[]                                                  _buf     = new Collider2D[16];

    public void Init(float damage, float duration, float hitRadius)
    {
        _damage    = damage;
        _hitRadius = hitRadius;
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        _expired.Clear();
        foreach (var k in _hitCd.Keys)
        {
            _hitCd[k] -= Time.deltaTime;
            if (_hitCd[k] <= 0f) _expired.Add(k);
        }
        foreach (var k in _expired) _hitCd.Remove(k);
        _bossCd -= Time.deltaTime;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, _hitRadius, _buf);
        for (int i = 0; i < count; i++)
        {
            if (_buf[i] == null) continue;
            var boss = _buf[i].GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy && _bossCd <= 0f)
            {
                boss.TakeDamage(_damage, false);
                _bossCd = 0.5f;
                continue;
            }
            var enemy = _buf[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.gameObject.activeInHierarchy && !_hitCd.ContainsKey(enemy))
            {
                enemy.TakeDamage(_damage, false);
                _hitCd[enemy] = 0.5f;
            }
        }
    }
}
