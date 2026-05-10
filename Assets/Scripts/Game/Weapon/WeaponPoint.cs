using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 자식 오브젝트(weaponPoint)에 부착합니다.
/// 공격 흐름: Idle → Pullback(당김) → Striking(확! 돌진) → Overshooting(팍!) → Returning
/// 회전 우선순위: 적 존재 → 적 방향 / 없으면 → 이동 방향 / 없으면 → 위쪽
/// </summary>
public class WeaponPoint : MonoBehaviour
{
    [Header("무기 타입")]
    [SerializeField] private WeaponType weaponType = WeaponType.Melee;

    // ── DB_무기에서 런타임 로드 (Inspector 설정 불필요) ─────────────
    // f_기본공격력, f_공격속도, f_탐지범위, f_주변범위
    private float attackDamage;
    private float attackCooldown;
    private float attackRange;
    private float areaHitRadius;
    // ────────────────────────────────────────────────────────────────

    // ── DB_무기에서 Init()으로 주입 (Inspector 설정 불필요) ──────────
    private float criticalRate       = 0f;
    private float criticalMultiplier = 2f;
    private float knockbackPower     = 0f;
    private float dotDamage          = 0f;
    private float lifeSteal          = 0f;

    // ── 읽기 전용 프로퍼티 (WeaponInfoPanel용) ───────────────────
    public float AttackDamage    => attackDamage;
    public float AttackCooldown  => attackCooldown;
    public float AttackRange     => attackRange;
    public float CriticalRate    => criticalRate;
    public float CriticalDamage  => criticalMultiplier;
    public float BleedDamage     => dotDamage;
    public float LifeSteal       => lifeSteal;
    public float KnockbackPower  => knockbackPower;
    public float ProjectileSpeed => projectileSpeed;
    public float AreaDamageRatio => areaHitDamageRatio;

    [Header("근거리 이동 속도")]
    [SerializeField] private float pullbackDist     = 0.5f;  // 뒤로 빼는 거리 (로컬)
    [SerializeField] private float anticipationTime = 0.08f; // 백스윙 후 멈추는 시간
    [SerializeField] private float strikeSpeed   = 80f;
    [SerializeField] private float returnSpeed   = 25f;

    [Header("근거리 타격 판정")]
    [SerializeField] private float hitDistance = 0.4f;

    [Header("범위 타격 설정")]
    [SerializeField] private float areaHitDamageRatio = 0.5f; // 주변 적 데미지 배율 (0~1)

    [Header("오버슛 (타격 후 더 나아감)")]
    [SerializeField] private float overshootDistance = 0.5f;
    [SerializeField] private float overshootSpeed    = 60f;

    [Header("복귀 시 당기기 (Melee 전용)")]
    [SerializeField] private bool  pullOnReturn     = false;
    [SerializeField] private float pullRadius       = 2.0f;  // 당기기 범위
    [SerializeField] private float pullStrength     = 8f;    // 당기기 힘 (impulse)
    [SerializeField] private float pullDuration     = 0.25f; // 당기기 적용 시간(초)

    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 8f;
    [SerializeField] private bool       useParabolicShot = false;
    [SerializeField] private float      parabolicArcHeight = 1.5f;
    [SerializeField] private Vector3    parabolicSpawnLocalPos;   // 발사 위치 (부모 기준 로컬)

    [Header("발사 스케일 펄스 (근접/부메랑 등)")]
    [SerializeField] private bool  shootScalePulse    = true;
    [SerializeField] private float scalePulseMax      = 1.3f;
    [SerializeField] private float scalePulseDuration = 0.12f;

    [Header("원거리 반동 (Ranged 전용)")]
    [SerializeField] private bool  shootRecoil        = true;
    [SerializeField] private float recoilDist         = 0.15f;  // 뒤로 밀리는 거리
    [SerializeField] private float recoilBackDur      = 0.06f;  // 뒤로 당겨지는 시간
    [SerializeField] private float recoilReturnDur    = 0.14f;  // 원위치 복귀 시간

    [Header("잔상 효과 (Striking/Overshooting 구간)")]
    [SerializeField] private bool  useTrail        = true;
    [SerializeField] private float trailTime       = 0.10f;  // 잔상 유지 시간(초)
    [SerializeField] private float trailStartWidth = 0.28f;  // 시작 폭
    [SerializeField] private Color trailColor      = new Color(1f, 1f, 1f, 0.55f);

    [Header("부메랑 설정")]
    [SerializeField] private int   boomerangMaxBounces  = 3;
    [SerializeField] private float boomerangBounceRange = 5f;

    [Header("돌리기 설정 (WeaponType.Spin)")]
    [SerializeField] private float orbitRadius     = 1.2f;
    [SerializeField] private float orbitSpeed      = 200f;   // deg/s (Spin 타입은 시계방향으로 변경)
    [SerializeField] private float spinHitRadius   = 0.35f;  // 타격 판정 반경
    [SerializeField] private float spinHitCooldown = 0.5f;   // 동일 적 재타격 쿨타임
    [SerializeField] private float spinKnockback   = 7f;

    [Header("슬래시 설정 (WeaponType.Slash) — 반원 베기")]
    [SerializeField] private float      slashRadius          = 1.2f;   // 최소 베기 반경 (폴백)
    [SerializeField] private float      slashArcDegrees      = 180f;   // 호 각도
    [SerializeField] private float      slashSweepSpeed      = 480f;   // deg/s
    [SerializeField] private float      slashHitRadius       = 0.35f;
    [SerializeField] private GameObject slashVfxPrefab;                // 중간 지점에서 Instantiate, 프리팹이 스스로 파괴
    [SerializeField] private float      slashVfxDuration     = 1f;     // VFX 지속 시간 (초)
    [SerializeField] private float      slashTeleportMaxDist = 3.0f;   // 플레이어 기준 최대 이동 거리
    [SerializeField] private float      slashFadeTime        = 0.12f;  // 페이드 인/아웃 시간(초)

    [Header("차징 설정 (WeaponType.Charge)")]
    [SerializeField] private float chargeDuration = 1f;

    [Header("해머 설정 (WeaponType.Hammer)")]
    [SerializeField] private float      hammerRiseHeight      = 4f;     // 맵 정중앙 위 상승 높이
    [SerializeField] private float      hammerRiseTime        = 0.55f;  // 상승 시간(초)
    [SerializeField] private float      hammerSpinSpeed       = 900f;   // 상승/하강 중 자전 속도 (deg/s)
    [SerializeField] private float      hammerSlamSpeed       = 28f;    // 하강 속도
    [SerializeField] private Vector2    hammerImpactWorldPos  = Vector2.zero; // 착지 세계 좌표 (맵 정중앙)
    [SerializeField] private float      hammerImpactRadius    = 1.8f;   // 충격 범위
    [SerializeField] private GameObject hammerImpactVfxPrefab;           // 착지 VFX
    [SerializeField] private float      hammerHighlightScale  = 2.2f;   // 충격 순간 최대 스케일 배율
    [SerializeField] private float      hammerHighlightDur    = 0.35f;  // 하이라이트 지속시간(초)

    [Header("마법 속성 (WeaponType.Magic 전용)")]
    [SerializeField] private MagicElement magicElement = MagicElement.None;

    [Header("번개 Muzzle (Magic + Lightning)")]
    [SerializeField] private Transform   muzzlePoint;
    [SerializeField] private GameObject  zapBoltVfxPrefab;

    [Header("번개 연타")]
    [SerializeField] private int   lightningBurstCount    = 3;    // 연타 횟수
    [SerializeField] private float lightningBurstInterval = 0.07f; // 연타 간격(초)
    [SerializeField] private float zapLineHitRadius       = 0.25f; // 라인 피해 판정 반경

    private Transform MuzzleTransform => muzzlePoint != null ? muzzlePoint : transform;
    private GameObject _activeBolt;

    // Fire 마법 전용
    private SpriteRenderer _weaponSr;
    private Coroutine      _srColorCoroutine;
    private static readonly Color _fireRedColor = new Color(1f, 0.2f, 0.05f);

    private static readonly RaycastHit2D[]        _zapLineBuffer  = new RaycastHit2D[16];
    private static readonly HashSet<EnemyController> _zapHitEnemies = new();

    [Header("애니메이션")]
    [SerializeField] private Animator       weaponAnimator;
    [SerializeField] private AnimationClip  attackClip;       // tAttack 시 재생할 클립 (Melee/Ranged/Magic)
    [SerializeField] private string         attackClipSlotName = "Attack"; // AnimatorOverrideController 슬롯 이름

    private AnimatorOverrideController _animOverride;
    private float _chargeTimer;

    private readonly struct CombatTarget
    {
        public readonly EnemyController Enemy;
        public readonly BossController  Boss;

        public CombatTarget(EnemyController e) { Enemy = e; Boss = null; }
        public CombatTarget(BossController  b) { Enemy = null; Boss = b; }

        public static readonly CombatTarget None = default;
        public bool IsNone  => Enemy == null && Boss == null;
        public bool IsValid => (Enemy != null && Enemy.gameObject.activeInHierarchy) ||
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

    private enum State { Idle, Pullback, Anticipation, Striking, Overshooting, Returning, Slashing, Charging, SlashSequence, HammerSequence }
    private State state = State.Idle;

    private Vector3 originLocalPos;
    private Vector3 pullbackLocalPos;  // 뒤로 뺀 로컬 목표
    private CombatTarget _target;
    private Vector3 overshootTarget;
    private float cooldownTimer;
    private float anticipationTimer;

    private float _baseCooldown;
    private float _statMultiplier  = 1f;
    private WeaponGrade          _grade          = WeaponGrade.Common;
    private WeaponLegendarySkill _legendarySkill;
    private WeaponMythicSkill    _mythicSkill;

    // Slash VFX 생성 시 발생하는 이벤트 (LegendarySlashSkill 전용)
    public event System.Action<Vector3, Vector2> OnSlashVfxSpawned;

    // 미아 방지 — 비 Idle 상태에서 이 시간 초과 시 강제 복귀 (최후 안전망)
    private const float MaxStateTime = 4f;
    private float _stateTimer;
    private float _bonusMultiplier = 1f; // MultiplyAttackSpeed 등 일회성 보너스 누적

    // ── 진단용 ─────────────────────────────────────────────────────────
    // 마지막 공격 이후 실제(unscaled) 경과 시간 — 공격 시에만 0으로 리셋
    private float _noAttackDiagTimer;
    private const float DiagLogInterval = 5f; // 이 초마다 로그 1회
    private float _idleNoTargetTimer; // 구버전 호환용 (내부에서만 사용)

    private Coroutine    _scalePulseCoroutine;
    private Vector3      _originalScale;
    private TrailRenderer _trail;

    // Physics NonAlloc 버퍼 (매 프레임 배열 재할당 방지)
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    // Spin 전용
    private float currentAngle;
    private readonly Dictionary<EnemyController, float> spinHitTimers = new Dictionary<EnemyController, float>();
    private readonly List<EnemyController> _spinExpiredKeys = new List<EnemyController>();
    private readonly List<EnemyController> _spinKeys        = new List<EnemyController>();
    private bool _hasSpinCollider; // Collider2D가 있으면 OnTriggerStay2D 사용, 없으면 OverlapCircle 폴백

    // ── 특수효과 ────────────────────────────────────────────────────
    private string _specialEffectId;
    private int    _weaponChapter = 1;
    private string _weaponId;

    // HIT_CLONE_RETURN: 투사체 날아가는 동안 재발동 불가
    private bool             _cloneFlying = false;
    private int              _cloneActiveCount = 0;
    private readonly List<GameObject> _activeClones = new List<GameObject>();

    // HIT_SPIN 궤도 부스트 상태
    private int   _spinBoostStack;
    private float _spinBoostOrbit;
    private float _spinBoostSpeed;
    private float _spinBoostTimer;
    private const int   SpinBoostMaxStack = 5;
    private const float SpinBoostDecay    = 3f;

    // HIT_SPIN 고스트 클론
    private readonly List<GameObject> _spinGhosts = new();

    private StatType?   _passiveBuffStat;
    private float       _passiveBuffAmount;
    private GameObject  _passiveBuffVfxInstance;

    private float _bossSpinHitTimer;

    // 보스 Collider2D 캐시 (매 UpdateIdle 마다 GetComponentInChildren 방지)
    private BossController _cachedBossForCollider;
    private Collider2D     _cachedBossCollider;

    private Vector2 _attackLookDir; // 공격 시퀀스 전체에서 유지할 시선 방향

    // 부메랑 필드 (직접 이동 모드)
    private int _boomerangBouncesLeft;
    private CombatTarget _boomerang;
    private Vector3 _boomerangOvershootTarget;
    private readonly HashSet<EnemyController> _boomerangHitEnemies = new();
    private readonly HashSet<BossController>  _boomerangHitBosses  = new();

    // 슬래시 전용
    private float _slashCurrentAngle;
    private float _slashCenterAngle;
    private float _slashEndAngle;
    private float _slashActiveRadius;
    private bool  _slashVfxSpawned;
    private readonly HashSet<EnemyController> _slashHitEnemies = new();
    private bool _slashHitBoss;
    private Coroutine _slashSequenceCoroutine;
    private Coroutine _hammerSequenceCoroutine;

    public static event System.Action OnRecallAll;
    public static void RecallAll() => OnRecallAll?.Invoke();

    private void OnDestroy()
    {
        if (_passiveBuffStat.HasValue)
            PlayerStatManager.Shared?.AddPctMod(_passiveBuffStat.Value, -_passiveBuffAmount);
        OnRecallAll -= RecallClones;
        RecallClones();
        foreach (var g in _spinGhosts) if (g != null) Destroy(g);
        _spinGhosts.Clear();
    }

    private void OnDisable() => RecallClones();

    public void RecallClones()
    {
        foreach (var go in _activeClones)
            if (go != null) Destroy(go);
        _activeClones.Clear();
        _cloneActiveCount = 0;
        _cloneFlying      = false;
    }

    private void Start()
    {
        OnRecallAll += RecallClones;
        _baseCooldown  = attackCooldown;
        originLocalPos = transform.localPosition;
        _originalScale = transform.localScale;
        cooldownTimer  = attackCooldown;
        _weaponSr      = GetComponentInChildren<SpriteRenderer>();
        InitTrail();
        InitAnimator();
        if (weaponType == WeaponType.Spin)
            _hasSpinCollider = GetComponent<Collider2D>() != null;
        _legendarySkill = GetComponentInChildren<WeaponLegendarySkill>();
        _mythicSkill    = GetComponentInChildren<WeaponMythicSkill>();
    }

    private void InitAnimator()
    {
        if (weaponAnimator == null || attackClip == null) return;
        _animOverride = new AnimatorOverrideController(weaponAnimator.runtimeAnimatorController);
        _animOverride[attackClipSlotName] = attackClip;
        weaponAnimator.runtimeAnimatorController = _animOverride;
    }

    private void OnDrawGizmos()
    {
        // 슬래시: 호 중간지점 표시
        if (weaponType == WeaponType.Slash)
        {
            Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
            Vector2 weaponDir = (transform.position - center).sqrMagnitude > 0.001f
                ? ((Vector2)transform.position - (Vector2)center).normalized : Vector2.up;
            float radius = Mathf.Max(slashRadius, (transform.position - center).magnitude);
            Vector3 midPos = center + (Vector3)(weaponDir * radius);

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(midPos, 0.1f);
            Gizmos.DrawLine(midPos, midPos + (Vector3)(weaponDir * 0.5f));
            Gizmos.DrawSphere(midPos + (Vector3)(weaponDir * 0.5f), 0.05f);
        }

        // 포물선 원거리: 발사 위치 표시
        if (weaponType == WeaponType.Ranged && useParabolicShot)
        {
            var parent = transform.parent != null ? transform.parent : transform;
            Vector3 spawnWorld = parent.TransformPoint(parabolicSpawnLocalPos);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawSphere(spawnWorld, 0.1f);
            Gizmos.DrawLine(spawnWorld, spawnWorld + Vector3.up * 0.4f);
        }
    }

    private void InitTrail()
    {
        if (!useTrail || weaponType == WeaponType.Slash) return;

        _trail = GetComponent<TrailRenderer>();
        if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();

        bool isSpin = weaponType == WeaponType.Spin;
        _trail.time       = isSpin ? 0.22f : trailTime;
        _trail.startWidth = isSpin ? 0.18f : trailStartWidth;
        _trail.endWidth   = 0f;
        _trail.minVertexDistance = 0.02f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f,            1f)
            }
        );
        _trail.colorGradient = gradient;

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            _trail.sortingLayerID = sr.sortingLayerID;
            _trail.sortingOrder   = sr.sortingOrder - 1;
        }

        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.emitting = isSpin;
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        if (weaponType == WeaponType.Spin)
        {
            UpdateSpin();
            return;
        }

        cooldownTimer -= Time.deltaTime;

        // ── 진단: 쿨타임이 끝났는데도 일정 시간 공격이 없으면 진짜 미아
        // cooldownTimer > 0 이면 정상 대기 중 — 로그 스킵
        if (EnemyController.All.Count > 0 || BossController.Current != null)
        {
            _noAttackDiagTimer += Time.unscaledDeltaTime;
            if (_noAttackDiagTimer >= DiagLogInterval)
            {
                _noAttackDiagTimer = 0f;
                if (cooldownTimer <= 0f) // 쿨타임도 끝났는데 공격 못 하면 진짜 미아
                {
                    Vector2 diagOrigin = transform.parent != null
                        ? (Vector2)transform.parent.position : (Vector2)transform.position;
                    EnemyController anyEnemy = FindNearestAny();
                    float nearestDist = anyEnemy != null
                        ? Vector2.Distance(diagOrigin, anyEnemy.transform.position) : -1f;
                    Debug.LogWarning(
                        $"[WeaponPoint] 미아 진단 — '{gameObject.name}'\n" +
                        $"  state={state}  cooldownTimer={cooldownTimer:F2}  timeScale={Time.timeScale}\n" +
                        $"  attackRange={attackRange}  weaponType={weaponType}\n" +
                        $"  EnemyCount={EnemyController.All.Count}  Boss={BossController.Current != null}\n" +
                        $"  가장가까운적(제한없음)={nearestDist:F2}  (공격범위={attackRange})");
                }
            }
        }
        else
        {
            _noAttackDiagTimer = 0f;
        }

        // 코루틴이 위치·회전 전담하는 상태에서는 UpdateRotation 스킵
        if (state != State.Slashing && state != State.SlashSequence && state != State.HammerSequence)
            UpdateRotation();

        // 미아 방지: 코루틴 구동 상태는 타임아웃 제외
        if (state != State.Idle && state != State.Returning
            && state != State.SlashSequence && state != State.HammerSequence)
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= MaxStateTime)
            {
                _target   = CombatTarget.None;
                _boomerang = CombatTarget.None;
                BeginReturn(false); // 타임아웃 — 공격 미완료이므로 쿨다운 미리셋
                _stateTimer = 0f;
                return;
            }
        }
        else
        {
            _stateTimer = 0f;
        }

        switch (state)
        {
            case State.Idle:         UpdateIdle();         break;
            case State.Pullback:     UpdatePullback();     break;
            case State.Anticipation: UpdateAnticipation(); break;
            case State.Striking:     UpdateStriking();     break;
            case State.Overshooting: UpdateOvershooting(); break;
            case State.Returning:    UpdateReturning();    break;
            case State.Slashing:       UpdateSlash();      break;
            case State.Charging:       UpdateCharging();   break;
            case State.SlashSequence:                      break; // 코루틴이 전담
            case State.HammerSequence:                     break; // 코루틴이 전담
        }
    }

    // ─────────────────────────────────────────
    // Spin: 플레이어 주위를 공전하며 닿는 적 타격
    // ─────────────────────────────────────────
    private void UpdateSpin()
    {
        // HIT_SPIN 부스트 타이머
        if (_spinBoostTimer > 0f)
        {
            _spinBoostTimer -= Time.deltaTime;
            if (_spinBoostTimer <= 0f)
            {
                _spinBoostStack = 0;
                _spinBoostOrbit = 0f;
                _spinBoostSpeed = 0f;
            }
        }

        // 공전 각도 증가 → localPosition 갱신
        float effectOrbitSpeed  = orbitSpeed  + _spinBoostSpeed;
        float effectOrbitRadius = orbitRadius + _spinBoostOrbit;
        currentAngle += effectOrbitSpeed * Time.deltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * effectOrbitRadius;

        // 무기 스프라이트를 공전 방향으로 자전
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);

        // 피타격 쿨타임 감소 (재사용 리스트로 Dictionary 키 복사 제거)
        if (_bossSpinHitTimer > 0f) _bossSpinHitTimer -= Time.deltaTime;
        _spinKeys.Clear();
        _spinKeys.AddRange(spinHitTimers.Keys);
        for (int i = 0; i < _spinKeys.Count; i++)
        {
            var key = _spinKeys[i];
            spinHitTimers[key] -= Time.deltaTime;
            if (spinHitTimers[key] <= 0f) _spinExpiredKeys.Add(key);
        }
        for (int i = 0; i < _spinExpiredKeys.Count; i++) spinHitTimers.Remove(_spinExpiredKeys[i]);
        _spinExpiredKeys.Clear();

        // 보스는 레이어 매트릭스에 관계없이 직접 거리 검사
        SpinBossDirectHit();

        // Collider2D가 없는 경우에만 OverlapCircle 폴백 사용
        // Collider2D가 있으면 OnTriggerStay2D에서 처리
        if (!_hasSpinCollider)
            SpinOverlapHit();
    }

    private void SpinBossDirectHit()
    {
        if (_bossSpinHitTimer > 0f) return;
        var boss = BossController.Current;
        if (boss == null || !boss.gameObject.activeInHierarchy) return;

        Vector2 weaponPos = transform.position;

        if (_cachedBossForCollider != boss)
        {
            _cachedBossForCollider = boss;
            _cachedBossCollider    = boss.GetComponentInChildren<Collider2D>();
        }

        float dist = _cachedBossCollider != null
            ? ((Vector2)_cachedBossCollider.ClosestPoint(weaponPos) - weaponPos).magnitude
            : Vector2.Distance(weaponPos, (Vector2)boss.transform.position);

        if (dist > spinHitRadius) return;

        var (bossDmg, bossCrit) = RollCrit(attackDamage);
        Vector2 bossKnock = (boss.transform.position - transform.position).normalized;
        boss.TakeDamage(bossDmg, bossCrit, bossKnock, spinKnockback);
        ApplyLifeSteal(bossDmg);
        TrySpecialEffectBoss(boss, bossDmg);
        _bossSpinHitTimer = spinHitCooldown;
    }

    private void SpinOverlapHit()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, spinHitRadius, _hitBuffer);
        for (int i = 0; i < hitCount; i++)
            SpinProcessHit(_hitBuffer[i]);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (weaponType != WeaponType.Spin) return;
        SpinProcessHit(other);
    }

    private void SpinProcessHit(Collider2D col)
    {
        var boss = col.GetComponentInParent<BossController>();
        if (boss != null && _bossSpinHitTimer <= 0f)
        {
            var (bossDmg, bossCrit) = RollCrit(attackDamage);
            Vector2 bossKnock = (boss.transform.position - transform.position).normalized;
            boss.TakeDamage(bossDmg, bossCrit, bossKnock, spinKnockback);
            ApplyLifeSteal(bossDmg);
            TrySpecialEffectBoss(boss, bossDmg);
            _bossSpinHitTimer = spinHitCooldown;
            return;
        }

        var enemy = col.GetComponentInParent<EnemyController>();
        if (enemy == null || spinHitTimers.ContainsKey(enemy)) return;

        var (finalDmg, isCrit) = RollCrit(attackDamage);
        Vector2 knockDir = (enemy.transform.position - transform.position).normalized;
        enemy.TakeDamage(finalDmg, isCrit, knockDir, spinKnockback);
        ApplyLifeSteal(finalDmg);
        if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
        TrySpecialEffectEnemy(enemy, finalDmg, knockDir);
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        spinHitTimers[enemy] = spinHitCooldown;
    }

    // ─────────────────────────────────────────
    // Idle: 쿨타임 후 미예약 적 탐색 → Pullback 시작
    // ─────────────────────────────────────────
    private int GetBoomerangBounces()
    {
        if (boomerangMaxBounces <= 0)
        {
            int total = EnemyController.All.Count + (BossController.Current != null ? 1 : 0);
            return Mathf.Max(3, total);
        }
        return boomerangMaxBounces;
    }

    private void UpdateIdle()
    {
        if (cooldownTimer > 0f) return;

        CombatTarget best = FindBestTarget();
        if (best.IsNone) return;

        _noAttackDiagTimer = 0f;

        bool isLightning = weaponType == WeaponType.Magic && magicElement == MagicElement.Lightning;
        bool isFireMagic = weaponType == WeaponType.Magic && magicElement == MagicElement.Fire;

        if (isFireMagic)  { FireHoming(best); cooldownTimer = attackCooldown; return; }
        if (isLightning)  { Zap(best);        cooldownTimer = attackCooldown; return; }
        if (weaponType == WeaponType.Ranged)
        {
            Shoot(best);
            cooldownTimer = attackCooldown;
            return;
        }
        if (weaponType == WeaponType.Boomerang)
        {
            _boomerang = best;
            _boomerangBouncesLeft = GetBoomerangBounces();
            _boomerangHitEnemies.Clear();
            _boomerangHitBosses.Clear();
            Vector2 toBoomerang = ((Vector2)_boomerang.Position - (Vector2)transform.parent.position).normalized;
            pullbackLocalPos = -(Vector3)toBoomerang * pullbackDist;
            state = State.Pullback;
            return;
        }
        if (weaponType == WeaponType.Slash) { BeginSlash(best); return; }
        if (weaponType == WeaponType.Charge)
        {
            _target = best;
            _chargeTimer = chargeDuration;
            cooldownTimer = attackCooldown;
            weaponAnimator?.SetTrigger("tCharge");
            state = State.Charging;
            return;
        }
        if (weaponType == WeaponType.Hammer)
        {
            if (_hammerSequenceCoroutine != null) StopCoroutine(_hammerSequenceCoroutine);
            _hammerSequenceCoroutine = StartCoroutine(HammerSequenceRoutine(best.Enemy, best.Boss));
            return;
        }
        // Melee / Magic
        _target = best;
        Vector2 toTarget = ((Vector2)best.Position - (Vector2)transform.parent.position).normalized;
        pullbackLocalPos = -(Vector3)toTarget * pullbackDist;
        state = State.Pullback;
    }

    private Vector3 GetParabolicSpawnPos()
    {
        var parent = transform.parent != null ? transform.parent : transform;
        return parent.TransformPoint(parabolicSpawnLocalPos);
    }

    private void Shoot(CombatTarget tgt)
    {
        if (projectilePrefab == null) return;

        Vector2 aimDir = ((Vector2)tgt.Position - (Vector2)transform.position).normalized;
        if (!useParabolicShot)
            SnapRotationToDir(aimDir);

        SoundManager.Shared?.Play(SoundKey.Weapon_Shoot);
        var (finalDmg, isCrit) = RollCrit(attackDamage);

        Vector3 spawnPos = useParabolicShot ? GetParabolicSpawnPos() : transform.position;
        var go   = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }

        if (useParabolicShot)
        {
            proj.InitParabolic(finalDmg, tgt.Position, projectileSpeed, parabolicArcHeight,
                isPrimaryCritical: isCrit, lifeSteal: lifeSteal, dotDamage: dotDamage,
                areaHitRadius: areaHitRadius, areaHitDamageRatio: areaHitDamageRatio,
                criticalRate: criticalRate, criticalMultiplier: criticalMultiplier,
                baseDamage: attackDamage, knockbackPower: knockbackPower);
        }
        else
        {
            proj.Init(finalDmg, aimDir, projectileSpeed, isPrimaryCritical: isCrit,
                lifeSteal: lifeSteal, dotDamage: dotDamage, areaHitRadius: areaHitRadius,
                areaHitDamageRatio: areaHitDamageRatio, criticalRate: criticalRate,
                criticalMultiplier: criticalMultiplier, baseDamage: attackDamage, knockbackPower: knockbackPower,
                acceleration: Mathf.Clamp(projectileSpeed * 0.05f, 0.1f, 0.4f));
        }
        ApplyShootSpecialEffect(proj, spawnPos, aimDir);
        TriggerAttackAnimation();
        TriggerRecoil(aimDir);
        if (_grade >= WeaponGrade.Legendary) _legendarySkill?.OnShoot(spawnPos, aimDir);
    }

    private void Zap(CombatTarget tgt) => StartCoroutine(BurstZapRoutine(MuzzleTransform, tgt.Enemy, tgt.Boss));

    private void FireHoming(CombatTarget tgt)
    {
        if (projectilePrefab == null) return;
        var (finalDmg, isCrit) = RollCrit(attackDamage);
        var go     = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        var homing = go.GetComponent<FireHomingProjectile>();
        if (homing == null) homing = go.AddComponent<FireHomingProjectile>();
        if (tgt.Enemy != null)
            homing.Init(finalDmg, projectileSpeed, tgt.Enemy,
                lifeSteal: lifeSteal, dotDamage: dotDamage,
                knockbackPower: knockbackPower, critRate: criticalRate, critMult: criticalMultiplier);
        else if (tgt.Boss != null)
            homing.InitBoss(finalDmg, projectileSpeed, tgt.Boss,
                lifeSteal: lifeSteal, dotDamage: dotDamage,
                knockbackPower: knockbackPower, critRate: criticalRate, critMult: criticalMultiplier);
        TriggerFireSpriteColor();
        TriggerScalePulse();
    }

    private void TriggerFireSpriteColor()
    {
        if (_weaponSr == null) return;
        if (_srColorCoroutine != null) StopCoroutine(_srColorCoroutine);
        _srColorCoroutine = StartCoroutine(FireSpriteColorRoutine());
    }

    private System.Collections.IEnumerator FireSpriteColorRoutine()
    {
        float half = attackCooldown * 0.5f;

        // 흰색 → 빨간색
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (_weaponSr == null) yield break;
            _weaponSr.color = Color.Lerp(Color.white, _fireRedColor, t / half);
            yield return null;
        }
        // 빨간색 → 흰색
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (_weaponSr == null) yield break;
            _weaponSr.color = Color.Lerp(_fireRedColor, Color.white, t / half);
            yield return null;
        }
        if (_weaponSr != null) _weaponSr.color = Color.white;
        _srColorCoroutine = null;
    }

    private System.Collections.IEnumerator BurstZapRoutine(Transform muzzle, EnemyController enemy, BossController boss)
    {
        if (muzzlePoint != null) muzzlePoint.gameObject.SetActive(true);

        for (int i = 0; i < lightningBurstCount; i++)
        {
            Transform targetTf = enemy != null ? enemy.transform : boss?.transform;
            if (targetTf == null) break;
            bool alive = enemy != null ? enemy.gameObject.activeInHierarchy : boss.gameObject.activeInHierarchy;
            if (!alive) break;

            SpawnZapBolt(muzzle, targetTf);
            ZapLineDamage(muzzle.position, targetTf.position, enemy, boss);
            SoundManager.Shared?.Play(SoundKey.Structure_Thunder);
            if (i == 0) TriggerScalePulse();

            if (i < lightningBurstCount - 1)
                yield return new WaitForSeconds(lightningBurstInterval);
        }

        if (muzzlePoint != null) muzzlePoint.gameObject.SetActive(false);
    }

    private void ZapLineDamage(Vector3 from, Vector3 to, EnemyController primary, BossController primaryBoss)
    {
        Vector2 dir = ((Vector2)(to - from)).normalized;
        float dist  = Vector2.Distance(from, to);
        int hitCount = Physics2D.CircleCastNonAlloc(from, zapLineHitRadius, dir, _zapLineBuffer, dist);

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
                    var (dmg, crit) = RollCrit(b == primaryBoss ? attackDamage : attackDamage * areaHitDamageRatio);
                    b.TakeDamage(dmg, crit, dir, knockbackPower);
                    ApplyLifeSteal(dmg);
                    continue;
                }
            }

            var e = col.GetComponentInParent<EnemyController>();
            if (e == null || _zapHitEnemies.Contains(e)) continue;
            _zapHitEnemies.Add(e);

            var (dmg2, crit2) = RollCrit(e == primary ? attackDamage : attackDamage * areaHitDamageRatio);
            e.TakeDamage(dmg2, crit2, dir, knockbackPower);
            ApplyLifeSteal(dmg2);
            if (dotDamage > 0f) e.ApplyBleed(dotDamage);
        }
    }

    private void SpawnZapBolt(Transform from, Transform to)
    {
        if (zapBoltVfxPrefab == null) return;
        if (_activeBolt != null) Destroy(_activeBolt);
        var go  = Instantiate(zapBoltVfxPrefab, from.position, Quaternion.identity);
        var vis = go.GetComponent<LightningVisual>();
        if (vis != null)
            vis.Setup(from, to);
        else
            Destroy(go, 1f);
        _activeBolt = go;
    }



    private void BeginSlash(CombatTarget tgt)
    {
        _noAttackDiagTimer = 0f;
        if (tgt.IsNone) return;
        if (_slashSequenceCoroutine != null) StopCoroutine(_slashSequenceCoroutine);
        _slashSequenceCoroutine = StartCoroutine(SlashSequenceRoutine(tgt.Enemy, tgt.Boss));
    }

    private System.Collections.IEnumerator SlashSequenceRoutine(EnemyController enemy, BossController boss)
    {
        state         = State.SlashSequence;
        cooldownTimer = attackCooldown;

        Vector2 playerPos = transform.parent != null
            ? (Vector2)transform.parent.position : (Vector2)transform.position;

        try
        {
            // ── 1. 제자리에서 페이드 아웃 ──────────────────────────────────
            if (_trail != null) _trail.emitting = false;
            yield return StartCoroutine(FadeWeapon(1f, 0f, slashFadeTime));

            // ── 2. 적 방향으로 텔레포트 (최대 거리 제한) ───────────────────
            // FadeOut 도중 적이 사라질 수 있으므로 null 재확인
            Vector2 targetWorldPos;
            if      (enemy != null) targetWorldPos = enemy.transform.position;
            else if (boss  != null) targetWorldPos = boss.transform.position;
            else yield break; // finally에서 복구

            Vector2 toTarget    = targetWorldPos - playerPos;
            float   clampedDist = Mathf.Min(toTarget.magnitude * 0.85f, slashTeleportMaxDist);
            Vector2 localOffset = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized * clampedDist
                : Vector2.up * clampedDist;

            transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);

            // 회전을 타겟 방향으로 맞춤
            float faceAngle = Mathf.Atan2(localOffset.y, localOffset.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, faceAngle);

            // ── 3. 페이드 인 ───────────────────────────────────────────────
            yield return StartCoroutine(FadeWeapon(0f, 1f, slashFadeTime));

            // ── 4. 슬래시 호 초기화 ────────────────────────────────────────
            Vector2 weaponFromPlayer = (Vector2)transform.localPosition;
            _slashActiveRadius = Mathf.Max(weaponFromPlayer.magnitude, slashRadius);
            _slashCenterAngle  = Mathf.Atan2(weaponFromPlayer.y, weaponFromPlayer.x) * Mathf.Rad2Deg;
            _slashCurrentAngle = _slashCenterAngle + slashArcDegrees * 0.5f;
            _slashEndAngle     = _slashCenterAngle - slashArcDegrees * 0.5f;
            _slashHitEnemies.Clear();
            _slashHitBoss    = false;
            _slashVfxSpawned = false;

            float startRad = _slashCurrentAngle * Mathf.Deg2Rad;
            transform.localPosition = new Vector3(
                Mathf.Cos(startRad), Mathf.Sin(startRad), 0f) * _slashActiveRadius;

            // ── 5. 호를 그리며 베기 (타격 판정은 VFX Collider가 담당) ────
            while (_slashCurrentAngle > _slashEndAngle)
            {
                _slashCurrentAngle -= slashSweepSpeed * Time.deltaTime;
                float rad = _slashCurrentAngle * Mathf.Deg2Rad;
                transform.localPosition = new Vector3(
                    Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _slashActiveRadius;
                transform.rotation = Quaternion.Euler(0f, 0f, _slashCurrentAngle - 90f);

                // 호 중간 지점에서 VFX 스폰 — OnTriggerEnter2D가 피해 처리
                if (!_slashVfxSpawned && _slashCurrentAngle <= _slashCenterAngle)
                {
                    _slashVfxSpawned = true;
                    if (slashVfxPrefab != null)
                    {
                        float   midRad      = _slashCenterAngle * Mathf.Deg2Rad;
                        Vector2 midDir      = new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad));
                        Vector3 vfxWorldPos = (Vector3)playerPos + (Vector3)(midDir * _slashActiveRadius);
                        var vfx = Instantiate(slashVfxPrefab, vfxWorldPos, Quaternion.identity);
                        vfx.GetComponent<SlashVFX>()?.SetDirection(midDir);
                        vfx.AddComponent<SlashVfxBehaviour>().Init(
                            slashVfxDuration, 0.25f, midDir,
                            attackDamage, criticalRate, criticalMultiplier, knockbackPower, dotDamage, lifeSteal);
                        Destroy(vfx, slashVfxDuration);
                        if (_grade >= WeaponGrade.Legendary) OnSlashVfxSpawned?.Invoke(vfxWorldPos, midDir);
                    }
                }

                yield return null;
            }

            // ── 6. 베기 후 페이드 아웃 ────────────────────────────────────
            yield return StartCoroutine(FadeWeapon(1f, 0f, slashFadeTime));

            // ── 7. 원위치 복귀 ─────────────────────────────────────────────
            transform.localPosition = Vector3.zero;

            // ── 8. 페이드 인 ───────────────────────────────────────────────
            yield return StartCoroutine(FadeWeapon(0f, 1f, slashFadeTime));
        }
        finally
        {
            // 예외·yield break 어느 경로로 종료되든 무기 상태 복원
            if (_weaponSr != null) { Color c = _weaponSr.color; c.a = 1f; _weaponSr.color = c; }
            transform.localPosition = Vector3.zero;
            transform.rotation      = Quaternion.identity;
            if (_trail != null) _trail.emitting = true;
            state = State.Idle;
            _slashSequenceCoroutine = null;
        }
    }

    private System.Collections.IEnumerator FadeWeapon(float from, float to, float duration)
    {
        if (_weaponSr == null) yield break;
        Color c = _weaponSr.color;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(from, to, t / duration);
            _weaponSr.color = c;
            yield return null;
        }
        c.a = to;
        _weaponSr.color = c;
    }

    // ─────────────────────────────────────────
    // Hammer: 맵 정중앙 강하 시퀀스
    // ─────────────────────────────────────────
    private System.Collections.IEnumerator HammerSequenceRoutine(EnemyController targetEnemyArg = null, BossController targetBossArg = null)
    {
        state = State.HammerSequence;
        // 쿨타임은 시퀀스 종료 후 설정 (공격 불가 보장)

        // 원래 로컬 위치 저장 (복귀용)
        Vector3 startLocal    = transform.localPosition;
        Vector3 startWorldPos = transform.position;

        // 착지 위치: 가장 가까운 적 → 보스 → Inspector 설정값 순서
        Vector3 impactWorld;
        if (targetEnemyArg != null)
            impactWorld = targetEnemyArg.transform.position;
        else if (targetBossArg != null)
            impactWorld = targetBossArg.transform.position;
        else
            impactWorld = new Vector3(hammerImpactWorldPos.x, hammerImpactWorldPos.y, 0f);
        impactWorld.z = 0f;

        Vector3 peakWorld = impactWorld + Vector3.up * hammerRiseHeight;

        // 착지 방향 회전: 착지점을 향해 무기 전면이 맞닿도록 정렬
        Vector2 slamDir2D  = ((Vector2)impactWorld - (Vector2)peakWorld).normalized; // 거의 Vector2.down
        float   landAngle  = Mathf.Atan2(slamDir2D.y, slamDir2D.x) * Mathf.Rad2Deg - 90f;
        Quaternion landRot = Quaternion.Euler(0f, 0f, landAngle);

        float spinAngle = 0f;

        // ── 1. 현재 위치 → 정점(맵 중앙 상공)으로 회전하며 상승 ──────
        for (float t = 0f; t < hammerRiseTime; t += Time.deltaTime)
        {
            float f    = t / hammerRiseTime;
            float ease = f * f;  // ease-in
            transform.position = Vector3.Lerp(startWorldPos, peakWorld, ease);
            spinAngle += hammerSpinSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
            yield return null;
        }
        transform.position = peakWorld;

        // ── 2. 정점에서 짧은 정지 ─────────────────────────────────────
        yield return new WaitForSeconds(0.12f);

        // ── 3. 빠르게 하강 — 착지 방향으로 Slerp 정렬 ────────────────
        Quaternion slamStartRot = transform.rotation;
        float      totalDist    = Vector3.Distance(peakWorld, impactWorld);
        while ((transform.position - impactWorld).sqrMagnitude > 0.04f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, impactWorld, hammerSlamSpeed * Time.deltaTime);

            // 남은 거리 비율로 착지 방향에 점점 가까워지도록 회전
            float remaining = Vector3.Distance(transform.position, impactWorld);
            float aligned   = 1f - Mathf.Clamp01(remaining / totalDist);
            transform.rotation = Quaternion.Slerp(slamStartRot, landRot, aligned);
            yield return null;
        }
        transform.position = impactWorld;
        transform.rotation = landRot; // 착지 시 정확히 맞닿은 방향으로 스냅

        // ── 4. 착지 충격 — 범위 데미지 ───────────────────────────────
        Vector2 impactCenter = (Vector2)impactWorld;

        int hitCount = Physics2D.OverlapCircleNonAlloc(impactCenter, hammerImpactRadius, _hitBuffer);
        BossController hammerHitBoss = null;
        bool anyHit  = false;
        for (int i = 0; i < hitCount; i++)
        {
            var col = _hitBuffer[i];
            if (hammerHitBoss == null)
            {
                var b = col.GetComponentInParent<BossController>();
                if (b != null)
                {
                    var (bDmg, bCrit) = RollCrit(attackDamage);
                    Vector2 bKnock = ((Vector2)b.transform.position - impactCenter);
                    b.TakeDamage(bDmg, bCrit, bKnock.sqrMagnitude > 0.001f ? bKnock.normalized : Vector2.up, knockbackPower);
                    ApplyLifeSteal(bDmg);
                    hammerHitBoss = b; anyHit = true;
                    continue;
                }
            }
            var e = col.GetComponentInParent<EnemyController>();
            if (e == null) continue;
            var (eDmg, eCrit) = RollCrit(attackDamage);
            Vector2 eKnock = ((Vector2)e.transform.position - impactCenter);
            e.TakeDamage(eDmg, eCrit, eKnock.sqrMagnitude > 0.001f ? eKnock.normalized : Vector2.up, knockbackPower);
            ApplyLifeSteal(eDmg);
            if (dotDamage > 0f) e.ApplyBleed(dotDamage);
            anyHit = true;
        }
        if (anyHit)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            ApplyGradeProc(hitCount, Vector2.down, hammerHitBoss, (Vector3)impactCenter);
        }

        // VFX 스폰
        if (hammerImpactVfxPrefab != null)
            Destroy(Instantiate(hammerImpactVfxPrefab, (Vector3)impactCenter, Quaternion.identity), 2f);
        else
            Debug.LogWarning($"[Hammer] hammerImpactVfxPrefab 미할당 — {gameObject.name}");

        // ── 5. 착지 즉시 복귀 ─────────────────────────────────────────
        // (하이라이트 대기 없음)

        // ── 6. 빠르게 하늘로 솟구치기 ────────────────────────────────
        Vector3 returnPeakWorld = impactWorld + Vector3.up * hammerRiseHeight;
        float   returnRiseTime  = hammerRiseTime * 0.35f;
        Vector3 riseStart       = transform.position;
        for (float t = 0f; t < returnRiseTime; t += Time.deltaTime)
        {
            float f = t / returnRiseTime;
            float ease = f * (2f - f); // ease-out
            transform.position = Vector3.Lerp(riseStart, returnPeakWorld, ease);
            yield return null;
        }
        transform.position = returnPeakWorld;

        // ── 7. 원래 자리로 빠르게 낙하 ───────────────────────────────
        Vector3 returnTargetWorld = transform.parent.TransformPoint(startLocal);
        float   returnSlamSpeed   = hammerSlamSpeed * 1.5f;
        while ((transform.position - returnTargetWorld).sqrMagnitude > 0.01f)
        {
            // 부모가 움직일 수 있으므로 매 프레임 갱신
            returnTargetWorld = transform.parent.TransformPoint(startLocal);
            transform.position = Vector3.MoveTowards(
                transform.position, returnTargetWorld, returnSlamSpeed * Time.deltaTime);
            yield return null;
        }
        transform.localPosition = startLocal;
        transform.rotation      = Quaternion.identity;

        cooldownTimer = attackCooldown;
        state = State.Idle;
        _hammerSequenceCoroutine = null;
    }

    /// <summary>착지 순간 반투명 고스트가 확산되며 사라지는 파문 연출</summary>
    private System.Collections.IEnumerator HammerImpactHighlightRoutine()
    {
        // 고스트 2개를 살짝 시차를 두고 발사 (파문 느낌)
        StartCoroutine(HammerGhostWave(0f,    1f,   hammerHighlightScale));
        StartCoroutine(HammerGhostWave(0.08f, 0.6f, hammerHighlightScale * 0.7f));

        // 고스트가 퍼지는 동안 무기는 착지 위치에 정지
        yield return new WaitForSecondsRealtime(hammerHighlightDur);
    }

    /// <summary>현재 위치에서 고스트 스프라이트가 확산·페이드아웃</summary>
    private System.Collections.IEnumerator HammerGhostWave(float delay, float startAlpha, float scaleMult)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (_weaponSr == null || _weaponSr.sprite == null) yield break;

        var go = new GameObject("HammerGhost");
        go.transform.position   = transform.position;
        go.transform.rotation   = transform.rotation;
        go.transform.localScale = transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = _weaponSr.sprite;
        sr.sortingLayerID = _weaponSr.sortingLayerID;
        sr.sortingOrder   = _weaponSr.sortingOrder - 1;
        sr.color          = new Color(1f, 1f, 1f, startAlpha);

        float    duration   = hammerHighlightDur * 0.9f;
        Vector3  startScale = go.transform.localScale;
        Vector3  endScale   = startScale * scaleMult;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float f    = t / duration;
            float ease = 1f - (1f - f) * (1f - f); // ease-out
            go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, ease);
            Color c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, f);
            sr.color = c;
            yield return null;
        }

        Destroy(go);
    }

    // UpdateStriking에서 Slash 타입이 타격 거리에 도달했을 때 호출
    private void StartSlashSweep()
    {
        Vector2 weaponFromPlayer = (Vector2)transform.position - (Vector2)transform.parent.position;
        _slashActiveRadius = Mathf.Max(weaponFromPlayer.magnitude, slashRadius);

        _slashCenterAngle  = Mathf.Atan2(weaponFromPlayer.y, weaponFromPlayer.x) * Mathf.Rad2Deg;
        _slashCurrentAngle = _slashCenterAngle + slashArcDegrees * 0.5f;
        _slashEndAngle     = _slashCenterAngle - slashArcDegrees * 0.5f;
        _slashHitEnemies.Clear();
        _slashHitBoss = false;
        _slashVfxSpawned = false;

        float startRad = _slashCurrentAngle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(startRad), Mathf.Sin(startRad), 0f) * _slashActiveRadius;

        _target = CombatTarget.None;
        state = State.Slashing;
    }

    private void UpdateSlash()
    {
        _slashCurrentAngle -= slashSweepSpeed * Time.deltaTime;

        float rad = _slashCurrentAngle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _slashActiveRadius;

        // 플레이어를 축으로 공전 회전
        transform.rotation = Quaternion.Euler(0f, 0f, _slashCurrentAngle - 90f);

        // 호의 중간지점(centerAngle) 도달 시 VFX 스폰 — 타격 판정은 VFX Collider가 담당
        if (!_slashVfxSpawned && _slashCurrentAngle <= _slashCenterAngle)
        {
            _slashVfxSpawned = true;
            if (slashVfxPrefab != null)
            {
                Vector2 parentPos = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;
                float   midRad    = _slashCenterAngle * Mathf.Deg2Rad;
                Vector2 midDir    = new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad));
                Vector3 vfxWorldPos = (Vector3)parentPos + (Vector3)(midDir * _slashActiveRadius);
                GameObject vfx = Instantiate(slashVfxPrefab, vfxWorldPos, Quaternion.identity);
                vfx.GetComponent<SlashVFX>()?.SetDirection(midDir);
                vfx.AddComponent<SlashVfxBehaviour>().Init(
                    slashVfxDuration, 0.25f, midDir,
                    attackDamage, criticalRate, criticalMultiplier, knockbackPower, dotDamage, lifeSteal);
                Destroy(vfx, slashVfxDuration);
                if (_grade >= WeaponGrade.Legendary) OnSlashVfxSpawned?.Invoke(vfxWorldPos, midDir);
            }
        }

        if (_slashCurrentAngle <= _slashEndAngle)
            BeginReturn();
    }

    private void UpdateCharging()
    {
        if (!_target.IsValid)
        {
            _target = CombatTarget.None;
            BeginReturn(false);
            return;
        }

        _chargeTimer -= Time.deltaTime;
        if (_chargeTimer <= 0f)
        {
            Vector2 toTarget = ((Vector2)_target.Position - (Vector2)transform.parent.position).normalized;
            pullbackLocalPos = -(Vector3)toTarget * pullbackDist;
            state = State.Pullback;
        }
    }



    private void SnapRotationToDir(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void TriggerRecoil(Vector2 shootDir)
    {
        if (!shootRecoil) return;
        if (_scalePulseCoroutine != null) StopCoroutine(_scalePulseCoroutine);
        _scalePulseCoroutine = StartCoroutine(RecoilRoutine(shootDir));
    }

    private System.Collections.IEnumerator RecoilRoutine(Vector2 shootDir)
    {
        // 발사 방향 반대로 로컬 위치 오프셋 (부모 공간 기준)
        Vector3 backOffset = transform.parent != null
            ? transform.parent.InverseTransformVector((Vector3)(-shootDir.normalized) * recoilDist)
            : (Vector3)(-shootDir.normalized) * recoilDist;
        Vector3 recoilPos = originLocalPos + backOffset;

        // 뒤로 당겨지기
        for (float t = 0f; t < recoilBackDur; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(originLocalPos, recoilPos, t / recoilBackDur);
            yield return null;
        }
        transform.localPosition = recoilPos;

        // 원위치 복귀
        for (float t = 0f; t < recoilReturnDur; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(recoilPos, originLocalPos, t / recoilReturnDur);
            yield return null;
        }
        transform.localPosition = originLocalPos;
        _scalePulseCoroutine = null;
    }

    private void TriggerScalePulse()
    {
        if (!shootScalePulse) return;
        if (_scalePulseCoroutine != null) StopCoroutine(_scalePulseCoroutine);
        _scalePulseCoroutine = StartCoroutine(ScalePulseRoutine());
    }

    private System.Collections.IEnumerator ScalePulseRoutine()
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
        transform.localScale = _originalScale;
        _scalePulseCoroutine = null;
    }

    // ─────────────────────────────────────────
    // Pullback: 플레이어 중앙으로 땡겨짐
    // ─────────────────────────────────────────
    private void UpdatePullback()
    {
        // 타겟 유효성 조기 체크 — 비활성(풀 반환) 포함 모든 소멸 케이스 감지
        if (!_boomerang.IsValid && !_target.IsValid)
        {
            _target = CombatTarget.None;
            BeginReturn(false); // 타겟 소멸 — 타격 전 중단
            return;
        }

        // 타겟 반대 방향으로 뺌
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, pullbackLocalPos, returnSpeed * Time.deltaTime);

        if ((transform.localPosition - pullbackLocalPos).sqrMagnitude <= 0.001f)
        {
            transform.localPosition = pullbackLocalPos;
            anticipationTimer = anticipationTime;
            state = State.Anticipation;
        }
    }

    private void UpdateAnticipation()
    {
        // 타겟 유효성 조기 체크 — 비활성(풀 반환) 포함 모든 소멸 케이스 감지
        if (!_boomerang.IsValid && !_target.IsValid)
        {
            _target = CombatTarget.None;
            BeginReturn(false); // 타겟 소멸 — 타격 전 중단
            return;
        }

        // 제자리에서 잠깐 멈춤 → 시간 다 되면 팍! Strike
        anticipationTimer -= Time.deltaTime;
        if (anticipationTimer <= 0f)
            BeginStrike();
    }

    // ─────────────────────────────────────────
    // BeginStrike: 무기를 플레이어 중심에 스냅 후 즉시 돌진 시작
    // ─────────────────────────────────────────
    private void BeginStrike()
    {
        SoundManager.Shared?.Play(SoundKey.Weapon_Shoot);
        if (weaponType == WeaponType.Melee || weaponType == WeaponType.Magic)
            TriggerAttackAnimation();
        state = State.Striking;
        if (_trail != null && weaponType != WeaponType.Slash) { _trail.Clear(); _trail.emitting = true; }
    }

    private void TriggerAttackAnimation()
    {
        if (weaponAnimator == null) return;
        weaponAnimator.SetTrigger("tAttack");
    }

    // ─────────────────────────────────────────
    // Striking: 빠르게 돌진 → 범위 타격 → Overshooting으로 전환
    // ─────────────────────────────────────────
    private void UpdateStriking()
    {
        // 부메랑 모드 처리
        if (weaponType == WeaponType.Boomerang && !_boomerang.IsNone)
        {
            if (!_boomerang.IsValid)
            {
                _boomerang = CombatTarget.None;
                BeginReturn(false);
                return;
            }

            Vector3 boomerangTargetPos = _boomerang.Position;
            transform.position = Vector3.MoveTowards(transform.position, boomerangTargetPos, strikeSpeed * 0.35f * Time.deltaTime);
            if (Vector3.Distance(transform.position, boomerangTargetPos) <= hitDistance)
            {
                Vector2 hitDir = ((Vector2)boomerangTargetPos - (Vector2)transform.parent.position).normalized;
                int bounceHappened = GetBoomerangBounces() - _boomerangBouncesLeft;
                HitAreaBoomerang(hitDir, bounceHappened);
                _boomerangBouncesLeft--;
                if (_boomerangBouncesLeft <= 0)
                {
                    _boomerang = CombatTarget.None;
                    BeginReturn();
                    return;
                }
                CombatTarget next = FindNextBoomerangTarget(boomerangTargetPos);
                if (!next.IsNone)
                {
                    _boomerangOvershootTarget = boomerangTargetPos + (next.Position - boomerangTargetPos).normalized * overshootDistance;
                    _boomerang = next;
                    state = State.Overshooting;
                }
                else
                {
                    _boomerang = CombatTarget.None;
                    BeginReturn();
                }
            }
            return;
        }

        if (!_target.IsValid)
        {
            _target = CombatTarget.None;
            BeginReturn(false);
            return;
        }

        Vector3 targetPos = _target.Position;

        // 보스: 공격 범위의 2배 밖으로 벗어나면 추격 중단
        if (_target.Boss != null && ((Vector2)targetPos - (Vector2)transform.parent.position).sqrMagnitude > attackRange * attackRange * 4f)
        {
            _target = CombatTarget.None;
            BeginReturn(false);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, strikeSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            if (weaponType == WeaponType.Slash) { StartSlashSweep(); return; }
            Vector2 hitDir = ((Vector2)targetPos - (Vector2)transform.parent.position).normalized;
            HitArea(hitDir, _target.Enemy);
            overshootTarget = targetPos + (Vector3)(hitDir * overshootDistance);
            _target = CombatTarget.None;
            state = State.Overshooting;
        }
    }


    // ─────────────────────────────────────────
    // Overshooting: 타격 후 조금 더 나아갔다가 복귀
    // ─────────────────────────────────────────
    private void UpdateOvershooting()
    {
        Vector3 target = weaponType == WeaponType.Boomerang ? _boomerangOvershootTarget : overshootTarget;
        float speed = weaponType == WeaponType.Boomerang ? overshootSpeed * 0.35f : overshootSpeed;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) <= 0.05f)
        {
            if (weaponType == WeaponType.Boomerang && !_boomerang.IsNone)
                state = State.Striking; // 튕김 계속
            else
                BeginReturn();
        }
    }

    // ─────────────────────────────────────────
    // HitArea: 현재 위치 반경 내 모든 적 타격
    // ─────────────────────────────────────────
    private void HitArea(Vector2 hitDir, EnemyController primaryTarget)
    {
        // Common: 주변 반경 무시 (즉각 타격 반경만), Rare+: DB 값 사용
        float hitRadius = _grade >= WeaponGrade.Rare
            ? Mathf.Max(areaHitRadius, hitDistance)
            : hitDistance;
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, hitRadius, _hitBuffer);
        bool bossHit = false;
        bool anyHit = false;
        BossController hitBoss = null;
        // Common: 넉백 0, Rare: DB 값, Epic+: 절반 (카메라셰이크+2차타격 보상)
        float effectiveKnockback = _grade switch
        {
            WeaponGrade.Common => 0f,
            WeaponGrade.Rare   => knockbackPower,
            _                  => knockbackPower * 0.5f,
        };
        for (int i = 0; i < colCount; i++)
        {
            var hit = _hitBuffer[i];

            // 보스 타격
            if (!bossHit)
            {
                var boss = hit.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    var (bossDmg, bossCrit) = RollCrit(attackDamage);
                    Vector2 bossKnock = ((Vector2)boss.transform.position - (Vector2)transform.position);
                    if (bossKnock.sqrMagnitude < 0.001f) bossKnock = hitDir;
                    if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
                    boss.TakeDamage(bossDmg, bossCrit, bossKnock.normalized, effectiveKnockback);
                    ApplyLifeSteal(bossDmg);
                    TrySpecialEffectBoss(boss, bossDmg);
                    bossHit = true;
                    hitBoss = boss;
                    anyHit = true;
                    continue;
                }
            }

            if (!hit.TryGetComponent<EnemyController>(out var enemy)) continue;
            bool isPrimary = enemy == primaryTarget;
            float baseDmg = isPrimary ? attackDamage : attackDamage * areaHitDamageRatio;
            var (finalDmg, isCrit) = RollCrit(baseDmg);
            Vector2 knockDir = ((Vector2)enemy.transform.position - (Vector2)transform.position);
            if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
            if (_grade >= WeaponGrade.Epic) FXManager.NextIsEpicHit = true;
            enemy.TakeDamage(finalDmg, isCrit, knockDir.normalized, effectiveKnockback);
            ApplyLifeSteal(finalDmg);
            if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
            if (isPrimary) TrySpecialEffectEnemy(enemy, finalDmg, knockDir.normalized);
            anyHit = true;
        }
        FXManager.NextIsEpicHit = false; // TakeDamage 조기 리턴 시 잔류 플래그 정리
        if (anyHit)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            if (weaponType == WeaponType.Slash && projectilePrefab != null)
            {
                float angle = Mathf.Atan2(hitDir.y, hitDir.x) * Mathf.Rad2Deg - 90f;
                Instantiate(projectilePrefab, transform.position, Quaternion.Euler(0, 0, angle));
            }
            ApplyGradeProc(colCount, hitDir, hitBoss, transform.position);
        }
    }

    private void ApplyGradeProc(int colCount, Vector2 hitDir, BossController hitBoss, Vector3 hitPos)
    {
        if (_grade >= WeaponGrade.Epic)
        {
            CameraFollow.Shared?.Shake(0.08f, 0.06f);
            StartCoroutine(EpicSecondHitRoutine(hitPos));
        }

        if (_grade >= WeaponGrade.Legendary)
            _legendarySkill?.OnHit(hitDir, _hitBuffer, colCount);

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
                    if (_hitBuffer[i] == null) continue;
                    var e = _hitBuffer[i].GetComponentInParent<EnemyController>();
                    if (e != null && e.gameObject.activeInHierarchy)
                        _mythicSkill.OnEnemyHit(e, hitDir);
                }
            }
        }
    }

    private System.Collections.IEnumerator EpicSecondHitRoutine(Vector3 hitPos)
    {
        yield return new WaitForSeconds(0.08f);
        float radius = _grade >= WeaponGrade.Rare ? Mathf.Max(areaHitRadius, hitDistance) : hitDistance;
        int count = Physics2D.OverlapCircleNonAlloc(hitPos, radius, _hitBuffer);
        float dmg   = attackDamage * Random.Range(0.20f, 0.25f);
        bool anyHit = false;
        for (int i = 0; i < count; i++)
        {
            if (_hitBuffer[i] == null) continue;
            var boss = _hitBuffer[i].GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                var (fd, ic) = RollCrit(dmg);
                FXManager.NextIsEpicHit = true;
                boss.TakeDamage(fd, ic);
                anyHit = true;
                continue;
            }
            if (_hitBuffer[i].TryGetComponent<EnemyController>(out var e) && e.gameObject.activeInHierarchy)
            {
                var (fd, ic) = RollCrit(dmg);
                FXManager.NextIsEpicHit = true;
                e.TakeDamage(fd, ic);
                anyHit = true;
            }
        }
        FXManager.NextIsEpicHit = false; // TakeDamage 조기 리턴 시 잔류 플래그 정리
        if (anyHit) FXManager.Shared?.SpawnHitEffect(hitPos);
    }

    private void HitAreaBoomerang(Vector2 hitDir, int bounceCount)
    {
        float hitRadius = _grade >= WeaponGrade.Rare ? Mathf.Max(areaHitRadius, hitDistance) : hitDistance;
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, hitRadius, _hitBuffer);
        bool anyHit = false;
        float damagePenalty = Mathf.Pow(0.75f, bounceCount);
        float effectiveKnockback = _grade switch
        {
            WeaponGrade.Common => 0f,
            WeaponGrade.Rare   => knockbackPower,
            _                  => knockbackPower * 0.5f,
        };
        BossController hitBoss = null;

        for (int i = 0; i < colCount; i++)
        {
            var hit = _hitBuffer[i];
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && !_boomerangHitEnemies.Contains(enemy))
            {
                _boomerangHitEnemies.Add(enemy);
                bool isPrimary = _boomerang.Enemy == enemy;
                float baseDmg = Mathf.Max(1f, (isPrimary ? attackDamage : attackDamage * areaHitDamageRatio) * damagePenalty);
                var (finalDmg, isCrit) = RollCrit(baseDmg);
                Vector2 knockDir = ((Vector2)enemy.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
                enemy.TakeDamage(finalDmg, isCrit, knockDir.normalized, effectiveKnockback);
                ApplyLifeSteal(finalDmg);
                if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
                if (isPrimary) TrySpecialEffectEnemy(enemy, finalDmg, knockDir.normalized);
                anyHit = true;
            }
            var boss = hit.GetComponentInParent<BossController>();
            if (boss != null && !_boomerangHitBosses.Contains(boss))
            {
                _boomerangHitBosses.Add(boss);
                bool isPrimary = _boomerang.Boss == boss;
                float baseDmg = Mathf.Max(1f, (isPrimary ? attackDamage : attackDamage * areaHitDamageRatio) * damagePenalty);
                var (finalDmg, isCrit) = RollCrit(baseDmg);
                Vector2 knockDir = ((Vector2)boss.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
                boss.TakeDamage(finalDmg, isCrit, knockDir.normalized, effectiveKnockback);
                ApplyLifeSteal(finalDmg);
                if (dotDamage > 0f) boss.ApplyBleed(dotDamage);
                if (isPrimary) TrySpecialEffectBoss(boss, finalDmg);
                hitBoss = boss;
                anyHit = true;
            }
        }
        if (anyHit)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            ApplyGradeProc(colCount, hitDir, hitBoss, transform.position);
        }
    }

    private void ApplyLifeSteal(float dealtDamage)
    {
        if (lifeSteal <= 0f) return;
        PlayerController.Instance?.HealHP(dealtDamage * lifeSteal);
    }

    // ─────────────────────────────────────────
    // Returning: 원위치(부모 위치)로 복귀
    // ─────────────────────────────────────────
    private void UpdateReturning()
    {
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, Vector3.zero, returnSpeed * Time.deltaTime);

        if (transform.localPosition.sqrMagnitude <= 0.001f)
        {
            transform.localPosition = Vector3.zero;
            state = State.Idle;
        }
    }

    // resetCooldown=true: 타격 성공 후 복귀 (쿨다운 리셋 O)
    // resetCooldown=false: 타겟 소멸·이탈·타임아웃으로 인한 중단 복귀 (쿨다운 리셋 X → 즉시 재타겟)
    private void BeginReturn(bool resetCooldown = true)
    {
        state = State.Returning;
        _stateTimer = 0f;
        if (resetCooldown)
            cooldownTimer = attackCooldown;
        if (_trail != null && weaponType != WeaponType.Spin) _trail.emitting = false;

        if (resetCooldown && pullOnReturn && weaponType == WeaponType.Melee)
            StartCoroutine(PullEnemiesRoutine());
    }

    private System.Collections.IEnumerator PullEnemiesRoutine()
    {
        Vector2 playerPos = transform.parent != null
            ? (Vector2)transform.parent.position
            : (Vector2)transform.position;

        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pullDuration; // 0→1 (초반에 강하게)
            float forceMult = (1f - t) * pullStrength * Time.deltaTime / pullDuration;

            int count = Physics2D.OverlapCircleNonAlloc(playerPos, pullRadius, _hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

                Vector2 toPlayer = playerPos - (Vector2)enemy.transform.position;
                if (toPlayer.sqrMagnitude < 0.01f) continue;

                var rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.AddForce(toPlayer.normalized * pullStrength * (1f - t), ForceMode2D.Force);
                else
                    enemy.transform.position += (Vector3)(toPlayer.normalized * forceMult);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────
    // 회전: 적 > 이동방향 > 위쪽 우선순위
    // 포물선 원거리 무기는 회전 불필요 (발사 위치 고정)
    // ─────────────────────────────────────────
    private void UpdateRotation()
    {
        if (weaponType == WeaponType.Ranged && useParabolicShot) return;

        Vector2 dir = GetLookDirection();
        if (dir.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetLookDirection()
    {
        // 공격 시퀀스 전체(Pullback→Anticipation→Striking→Overshooting→Returning): 타겟 방향 고정
        if (state != State.Idle)
        {
            // 타겟이 살아있는 동안 방향 갱신
            if (state == State.Pullback || state == State.Striking)
            {
                CombatTarget active = !_boomerang.IsNone ? _boomerang : _target;
                if (active.IsValid)
                {
                    Vector2 dir = ((Vector2)active.Position - (Vector2)transform.position).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                        _attackLookDir = dir;
                }
            }
            // 타겟이 사라진 이후(Overshooting/Returning)에도 마지막 방향 유지
            if (_attackLookDir.sqrMagnitude > 0.001f)
                return _attackLookDir;
        }

        // 적이 있으면 가장 가까운 적 방향 (보스 포함)
        EnemyController nearest = FindNearestAny();
        var currentBoss = BossController.Current;
        bool bossAlive  = currentBoss != null && currentBoss.gameObject.activeInHierarchy;
        if (nearest != null && bossAlive)
        {
            float eDist = ((Vector2)nearest.transform.position - (Vector2)transform.position).sqrMagnitude;
            float bDist = ((Vector2)currentBoss.transform.position - (Vector2)transform.position).sqrMagnitude;
            return bDist < eDist
                ? ((Vector2)currentBoss.transform.position - (Vector2)transform.position).normalized
                : (nearest.transform.position - transform.position).normalized;
        }
        if (bossAlive)
            return ((Vector2)currentBoss.transform.position - (Vector2)transform.position).normalized;
        if (nearest != null)
            return (nearest.transform.position - transform.position).normalized;

        // 적 없으면 플레이어 이동 방향
        if (PlayerController.Instance != null)
        {
            Vector2 moveDir = PlayerController.Instance.MoveDirection;
            if (moveDir.sqrMagnitude > 0.001f)
                return moveDir;
        }

        // 이동도 없으면 위쪽
        return Vector2.up;
    }

    private CombatTarget FindNextBoomerangTarget(Vector2 fromPos)
    {
        EnemyController nearestEnemy = null;
        BossController  nearestBoss  = null;
        float minDist = float.MaxValue;

        foreach (var e in EnemyController.All)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            if (_boomerangHitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist) { minDist = d; nearestEnemy = e; }
        }

        var currentBoss = BossController.Current;
        if (currentBoss != null && currentBoss.gameObject.activeInHierarchy && !_boomerangHitBosses.Contains(currentBoss))
        {
            float d = Vector2.Distance(fromPos, currentBoss.transform.position);
            if (d < minDist) { minDist = d; nearestBoss = currentBoss; }
        }

        if (nearestEnemy != null && nearestBoss != null)
        {
            float dE = Vector2.Distance(fromPos, nearestEnemy.transform.position);
            float dB = Vector2.Distance(fromPos, nearestBoss.transform.position);
            return dE < dB ? new CombatTarget(nearestEnemy) : new CombatTarget(nearestBoss);
        }
        if (nearestEnemy != null) return new CombatTarget(nearestEnemy);
        if (nearestBoss  != null) return new CombatTarget(nearestBoss);
        return CombatTarget.None;
    }

    // ─────────────────────────────────────────
    // 적 탐색 유틸
    // ─────────────────────────────────────────
    private CombatTarget FindBestTarget()
    {
        Vector2 origin = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;
        int colCount = Physics2D.OverlapCircleNonAlloc(origin, attackRange, _hitBuffer, LayerMask.GetMask("Enemy"));

        CombatTarget best = CombatTarget.None;
        float minSqDist = float.MaxValue;

        for (int i = 0; i < colCount; i++)
        {
            var hit = _hitBuffer[i];

            var boss = hit.GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                float sq = ((Vector2)boss.transform.position - origin).sqrMagnitude;
                if (sq < minSqDist) { minSqDist = sq; best = new CombatTarget(boss); }
                continue;
            }

            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                float sq = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sq < minSqDist) { minSqDist = sq; best = new CombatTarget(enemy); }
            }
        }
        return best;
    }

    // 거리 제한 없이 씬 전체에서 가장 가까운 적 (회전용)
    private EnemyController FindNearestAny()
    {
        EnemyController nearest   = null;
        float           minSqDist = float.MaxValue;
        Vector2         origin    = transform.parent.position;

        foreach (var enemy in EnemyController.All)
        {
            if (enemy == null || enemy.transform == null || !enemy.gameObject.activeInHierarchy) continue;
            float sqDist = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqDist < minSqDist) { minSqDist = sqDist; nearest = enemy; }
        }
        return nearest;
    }

    /// <summary>WeaponManager가 장착 시 호출 — DB 스탯 및 플레이어 보너스 적용.</summary>
    public void Init(WeaponData data)
    {
        // weaponType / magicElement: 프리팹 Inspector가 기준.
        // DB f_타입은 상점 필터·표시용 — ParseWeaponType 폴백(Melee)이 프리팹을 덮어쓰지 않도록 하지 않음.
        if (data.weaponType != WeaponType.Melee)   // Melee는 폴백 기본값이므로 명시적인 타입일 때만 적용
            weaponType = data.weaponType;
        if (data.magicElement != MagicElement.None)
            magicElement = data.magicElement;

        // DB_무기에서 로드된 스탯 주입
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
        _passiveBuffVfxInstance = null;
        if (!string.IsNullOrEmpty(_specialEffectId) && _specialEffectId.StartsWith("BUFF_"))
        {
            var stat = BuffTraitToStat(_specialEffectId);
            if (stat.HasValue)
            {
                var cfg            = StatusEffectConfig.Shared;
                float pct          = cfg?.GetPassiveBuffPct(_weaponId, 2f) ?? 2f;
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
        attackRange    = data.attackRange;
        areaHitRadius  = data.areaHitRadius;

        if (attackRange <= 0f)
            Debug.LogWarning($"[WeaponPoint] ⚠️ attackRange=0 — 무기 '{gameObject.name}' (DB f_탐지범위 확인 필요).");

        if (weaponType == WeaponType.Spin)
        {
            orbitSpeed    = -Mathf.Abs(orbitSpeed);
            spinKnockback = data.knockbackPower;
        }

        if (weaponType == WeaponType.Boomerang)
        {
            boomerangMaxBounces  = data.maxBounces;
            boomerangBounceRange = data.bounceRange;
        }

        var player = PlayerController.Instance;
        if (player != null)
        {
            attackDamage  = data.attackDamage + player.Attack;
            attackDamage += weaponType == WeaponType.Melee     ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Ranged    ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Boomerang ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Spin      ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Slash      ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Charge    ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Hammer    ? player.MeleeAttackBonus  : 0f;

            var relic = RelicManager.Shared;
            if (relic != null)
            {
                float relicPct = weaponType switch
                {
                    WeaponType.Melee     => relic.MeleeAtkBonusPct,
                    WeaponType.Charge    => relic.MeleeAtkBonusPct,
                    WeaponType.Slash     => relic.MeleeAtkBonusPct,
                    WeaponType.Hammer    => relic.MeleeAtkBonusPct,
                    WeaponType.Ranged    => relic.RangedAtkBonusPct,
                    WeaponType.Magic     => relic.MagicAtkBonusPct,
                    WeaponType.Boomerang => relic.BoomerangAtkBonusPct,
                    WeaponType.Spin      => relic.SpinAtkBonusPct,
                    _                   => 0f,
                };
                if (relicPct > 0f) attackDamage *= (1f + relicPct / 100f);
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
    }

    /// <summary>크리티컬 판정. isCritical=true이면 데미지에 multiplier를 곱한 값 반환.</summary>
    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        float playerCrit = PlayerController.Instance?.CritRateBonus ?? 0f;
        float totalCrit  = criticalRate + playerCrit;
        bool  crit       = totalCrit > 0f && Random.value < totalCrit;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    /// <summary>PlayerStatManager AtkSpeed 값(초 감산량)으로 쿨타임을 재계산합니다.
    /// 예: AtkSpeed=0.2 → 기본 쿨타임에서 0.2초 차감. 최소 0.1초 보장.</summary>
    public void SetStatSpeedPct(float reduction)
    {
        _statMultiplier = reduction; // 감산값(초)으로 재사용
        RecalcCooldown();
    }

    /// <summary>공격속도를 multiplier배 빠르게 합니다 (일회성 보너스 누적).</summary>
    public void MultiplyAttackSpeed(float multiplier)
    {
        _bonusMultiplier *= multiplier;
        RecalcCooldown();
    }

    private void RecalcCooldown()
    {
        // _statMultiplier = AtkSpeed 감산량(초), _bonusMultiplier = 배율 보너스
        float reduced = _baseCooldown - _statMultiplier;
        attackCooldown = Mathf.Max(0.1f, reduced / _bonusMultiplier);
    }

    /// <summary>LegendarySpinSkill에서 호출 — 공전 반경과 속도에 배율 적용.</summary>
    public void ApplySpinBoost(float radiusMult, float speedMult)
    {
        orbitRadius *= radiusMult;
        orbitSpeed  *= speedMult;
    }

    // ── 특수효과 디스패처 ─────────────────────────────────────────────

    private float Chance(float fallback) =>
        StatusEffectConfig.Shared?.GetTriggerChance(_weaponId, fallback) ?? fallback;

    /// <summary>적 타격 시 특수효과 발동. 주 타겟(isPrimary)에만 호출됩니다.</summary>
    private void TrySpecialEffectEnemy(EnemyController enemy, float damage, Vector2 hitDir)
    {
        if (string.IsNullOrEmpty(_specialEffectId) || enemy == null) return;
        switch (_specialEffectId)
        {
            // ── HIT_ICE / HIT_FIRE / HIT_STUN 통합 ─────────────
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

            // ── HIT_CLONE 변형들 ─────────────────────────────────
            case "HIT_CLONE_RETURN":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneProjectiles(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_CROSS":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneCross(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_BURST":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneBurst(enemy.transform.position, damage);
                break;

            case "HIT_CLONE_SPREAD":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneSpread(enemy.transform.position, damage);
                break;

            // ── HIT_SPIKE: 타격 위치 주변 범위 AOE 스파이크 ──────
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
                if (weaponType != WeaponType.Spin) break;
                if (Random.value > Chance(0.30f)) break;
                _spinBoostStack = Mathf.Min(_spinBoostStack + 1, SpinBoostMaxStack);
                float boostSign = orbitSpeed < 0 ? -1f : 1f;
                _spinBoostOrbit = _spinBoostStack * 0.25f;
                _spinBoostSpeed = _spinBoostStack * boostSign * 40f;
                _spinBoostTimer = SpinBoostDecay;
                StartSpinGhosts();
                break;
        }
    }

    /// <summary>보스 타격 시 특수효과 발동.</summary>
    private void TrySpecialEffectBoss(BossController boss, float damage)
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

            // ── HIT_SPIKE: 보스 위치 주변 AOE 스파이크 ───────────
            case "BOSS_SPIKE":
            case "HIT_SPIKE":
                if (Random.value > Chance(0.25f)) break;
                SpawnAoeSpike(boss.transform.position, damage);
                break;

            // ── HIT_CLONE 변형들 ─────────────────────────────────
            case "HIT_CLONE_RETURN":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneProjectiles(boss.transform.position, damage);
                break;

            case "HIT_CLONE_CROSS":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneCross(boss.transform.position, damage);
                break;

            case "HIT_CLONE_BURST":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
                SpawnCloneBurst(boss.transform.position, damage);
                break;

            case "HIT_CLONE_SPREAD":
                if (_cloneFlying) break;
                if (Random.value > Chance(0.25f)) break;
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

    /// <summary>투사체 발사 직후 SHOOT 특수효과를 적용합니다.</summary>
    private void ApplyShootSpecialEffect(Projectile mainProj, Vector3 spawnPos, Vector2 aimDir)
    {
        // HIT_* 특수효과: 투사체가 적/보스를 맞출 때 WeaponPoint로 콜백
        if (!string.IsNullOrEmpty(_specialEffectId) && _specialEffectId.StartsWith("HIT_"))
        {
            mainProj.SetOnHitEnemy((e, dmg, dir) => TrySpecialEffectEnemy(e, dmg, dir));
            mainProj.SetOnHitBoss((b, dmg) => TrySpecialEffectBoss(b, dmg));
        }
        // BOSS_* 특수효과: 보스 타격 시 콜백
        if (!string.IsNullOrEmpty(_specialEffectId) && _specialEffectId.StartsWith("BOSS_"))
            mainProj.SetOnHitBoss((b, dmg) => TrySpecialEffectBoss(b, dmg));

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
                    float a   = i * 45f * Mathf.Deg2Rad;
                    var   dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    FireExtraProjectile(spawnPos, dir, attackDamage * 0.4f);
                }
                break;
        }
    }

    // ── 특수효과 헬퍼 ────────────────────────────────────────────────

    private const float CloneOutTime    = 0.45f;
    private const float ClonePauseTime  = 0.40f; // 연타 5회(0.06s×5) + 여유
    private const float CloneReturnTime = 0.35f;
    private const int   CloneBurstHits  = 5;
    private const float CloneBurstInterval = 0.06f;

    private void SpawnCloneProjectiles(Vector2 enemyPos, float damage)
    {
        var prefab = StatusEffectConfig.Shared?.GetClonePrefab(_weaponId);
        if (prefab == null) prefab = projectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[HIT_CLONE_RETURN] 프리팹 없음 — weaponId='{_weaponId}'. StatusEffectConfig Editor에서 클론 프리팹을 등록하세요.");
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
        float cloneDmg = damage * 0.5f;

        Vector2 dir1 = RotateVec(forward,  15f);
        Vector2 dir2 = RotateVec(forward, -15f);

        // 에픽=1개, 신화=2개
        int cloneCount    = _grade == WeaponGrade.Epic ? 1 : 2;
        _cloneFlying      = true;
        _cloneActiveCount = cloneCount;

        var go1 = SpawnCloneVisual(prefab, origin);
        StartCoroutine(CloneArcRoutine(go1, origin, origin + dir1 * range, cloneDmg, outTime, pauseTime, retTime));
        if (cloneCount >= 2)
        {
            var go2 = SpawnCloneVisual(prefab, origin);
            StartCoroutine(CloneArcRoutine(go2, origin, origin + dir2 * range, cloneDmg, outTime, pauseTime, retTime));
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
        if (_cloneActiveCount <= 0)
        {
            _cloneActiveCount = 0;
            _cloneFlying      = false;
        }
    }

    private IEnumerator CloneArcRoutine(GameObject go, Vector2 from, Vector2 to, float damage, float outDuration, float pauseDuration, float returnDuration)
    {
        Vector3 baseScale = go.transform.localScale;
        Vector3 minScale  = baseScale * 0.5f;
        Vector3 maxScale  = baseScale * 1.5f;

        // ── 전진 (작게 → 크게) ────────────────────────────────────
        var hitIds   = new HashSet<int>();
        bool bossHitFly = false;
        float t = 0f;
        while (t < outDuration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / outDuration);
            go.transform.position   = Vector2.Lerp(from, to, ratio);
            go.transform.localScale = Vector3.Lerp(minScale, maxScale, ratio);

            var cols = Physics2D.OverlapCircleAll(go.transform.position, 0.35f, LayerMask.GetMask("Enemy"));
            foreach (var col in cols)
            {
                if (!bossHitFly)
                {
                    var boss = col.GetComponentInParent<BossController>();
                    if (boss != null && hitIds.Add(boss.GetInstanceID()))
                    {
                        bossHitFly = true;
                        var (bd, bc) = RollCrit(damage);
                        Vector2 bk = ((Vector2)boss.transform.position - (Vector2)go.transform.position).normalized;
                        boss.TakeDamage(bd, bc, bk, knockbackPower);
                        continue;
                    }
                }
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !hitIds.Add(enemy.GetInstanceID())) continue;
                var (ed, ec) = RollCrit(damage);
                Vector2 knock = ((Vector2)enemy.transform.position - (Vector2)go.transform.position).normalized;
                enemy.TakeDamage(ed, ec, knock, knockbackPower);
            }
            yield return null;
        }

        if (go == null) { DestroyClone(go); yield break; }
        go.transform.position   = to;
        go.transform.localScale = maxScale;

        // ── 정지 + 연타 파바바박 ──────────────────────────────────
        float burstInterval = Mathf.Max(pauseDuration / CloneBurstHits, CloneBurstInterval);
        for (int h = 0; h < CloneBurstHits; h++)
        {
            if (go == null) { DestroyClone(go); yield break; }
            var burstCols = Physics2D.OverlapCircleAll(go.transform.position, 0.5f, LayerMask.GetMask("Enemy"));
            bool bossHitBurst = false;
            foreach (var col in burstCols)
            {
                if (!bossHitBurst)
                {
                    var boss = col.GetComponentInParent<BossController>();
                    if (boss != null)
                    {
                        bossHitBurst = true;
                        var (bd, bc) = RollCrit(damage);
                        Vector2 bk = ((Vector2)boss.transform.position - (Vector2)go.transform.position).normalized;
                        boss.TakeDamage(bd, bc, bk, knockbackPower);
                        continue;
                    }
                }
                var e = col.GetComponentInParent<EnemyController>();
                if (e == null) continue;
                var (ed, ec) = RollCrit(damage);
                Vector2 knock = ((Vector2)e.transform.position - (Vector2)go.transform.position).normalized;
                e.TakeDamage(ed, ec, knock, knockbackPower);
            }
            FXManager.Shared?.SpawnHitEffect(go.transform.position);
            yield return new WaitForSeconds(burstInterval);
        }

        // ── 복귀 (X1.5 → X0.5) ────────────────────────────────────
        t = 0f;
        while (t < returnDuration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            float ratio       = Mathf.Clamp01(t / returnDuration);
            Vector2 playerPos = transform.parent != null ? (Vector2)transform.parent.position : from;
            go.transform.position   = Vector2.Lerp(to, playerPos, ratio);
            go.transform.localScale = Vector3.Lerp(maxScale, minScale, ratio);
            yield return null;
        }

        DestroyClone(go);
    }


    // ── HIT_STUN 통합 핸들러 ─────────────────────────────────────

    private void ApplyAreaFire(Vector2 center)
    {
        float r   = Mathf.Max(areaHitRadius, 2f);
        int   cnt = Physics2D.OverlapCircleNonAlloc(center, r, _hitBuffer);
        float dotDmg = attackDamage * 0.15f;
        for (int i = 0; i < cnt; i++)
        {
            var e = _hitBuffer[i].GetComponentInParent<EnemyController>();
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
                int   cnt = Physics2D.OverlapCircleNonAlloc(enemy.transform.position, r, _hitBuffer);
                for (int i = 0; i < cnt; i++)
                {
                    var e = _hitBuffer[i].GetComponentInParent<EnemyController>();
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

    // ── HIT_CLONE 변형 ────────────────────────────────────────────

    /// <summary>십자 4방향으로 클론 발사.</summary>
    private void SpawnCloneCross(Vector2 targetPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        Vector2 origin = GetWeaponOrigin(targetPos);
        float range = StatusEffectConfig.Shared?.GetCloneRange(_weaponId, 6f) ?? 6f;
        var   cfg   = StatusEffectConfig.Shared;
        float ot = cfg?.GetCloneOutTime   (_weaponId, CloneOutTime)    ?? CloneOutTime;
        float pt = cfg?.GetClonePauseTime (_weaponId, ClonePauseTime)  ?? ClonePauseTime;
        float rt = cfg?.GetCloneReturnTime(_weaponId, CloneReturnTime) ?? CloneReturnTime;

        Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
        _cloneFlying      = true;
        _cloneActiveCount = dirs.Length;
        foreach (var d in dirs)
        {
            var go = SpawnCloneVisual(prefab, origin);
            StartCoroutine(CloneArcRoutine(go, origin, origin + d * range, damage * 0.5f, ot, pt, rt));
        }
    }

    /// <summary>가운데 집결 → 수직 나열 → 랜덤 순서로 하나씩 팍! 직선 발사.</summary>
    private void SpawnCloneBurst(Vector2 targetPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        Vector2 origin  = GetWeaponOrigin(targetPos);
        Vector2 forward = targetPos - origin;
        if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
        forward = forward.normalized;
        Vector2 perp       = new Vector2(-forward.y, forward.x);
        float   range      = StatusEffectConfig.Shared?.GetCloneRange(_weaponId, 7f)          ?? 7f;
        float   travelTime = StatusEffectConfig.Shared?.GetCloneOutTime(_weaponId, 0.22f)      ?? 0.22f;

        float[] slots = { -1.5f, -0.5f, 0.5f, 1.5f };
        _cloneFlying      = true;
        _cloneActiveCount = slots.Length;
        StartCoroutine(BurstSequenceRoutine(prefab, origin, forward, perp, slots, range, damage * 0.4f, travelTime));
    }

    private IEnumerator BurstSequenceRoutine(GameObject prefab, Vector2 origin, Vector2 forward,
        Vector2 perp, float[] slots, float range, float damage, float travelTime)
    {
        const float gatherDuration = 0.12f;
        const float holdDuration   = 0.06f;
        const float minFireDelay   = 0.04f;
        const float maxFireDelay   = 0.12f;

        // ① 4개 모두 origin에 생성
        var gos   = new GameObject[slots.Length];
        var poses = new Vector2[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            poses[i] = origin + perp * slots[i];
            gos[i]   = SpawnCloneVisual(prefab, origin, forward);
        }

        // ② 중앙 → 나열 위치로 펼쳐짐
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

        // ③ 랜덤 순서로 하나씩 발사
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

    private IEnumerator CloneBurstRoutine(GameObject go, Vector2 from, Vector2 dir, float range, float damage, float duration)
    {
        const int   maxHitsPerEnemy = 3;
        const float hitCooldown     = 0.15f;

        var   hitCounts  = new Dictionary<int, int>();
        var   hitTimers  = new Dictionary<int, float>();
        float t          = 0f;

        while (t < duration)
        {
            if (go == null) { DestroyClone(go); yield break; }
            t += Time.deltaTime;
            go.transform.position = from + dir * (range * Mathf.Clamp01(t / duration));

            var cols = Physics2D.OverlapCircleAll(go.transform.position, 0.35f, LayerMask.GetMask("Enemy"));
            foreach (var col in cols)
            {
                var boss = col.GetComponentInParent<BossController>();
                if (boss != null) { boss.TakeDamage(damage); continue; }

                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                int id = enemy.GetInstanceID();

                hitCounts.TryGetValue(id, out int count);
                hitTimers.TryGetValue(id, out float lastHit);
                if (count >= maxHitsPerEnemy) continue;
                if (count > 0 && Time.time - lastHit < hitCooldown) continue;

                var (ed, ec) = RollCrit(damage);
                enemy.TakeDamage(ed, ec, dir, knockbackPower);
                hitCounts[id] = count + 1;
                hitTimers[id] = Time.time;
            }
            yield return null;
        }
        DestroyClone(go);
    }

    /// <summary>타격 위치에서 8방향으로 분산 (근접 전용, 복귀 없음).</summary>
    private void SpawnCloneSpread(Vector2 hitPos, float damage)
    {
        var prefab = GetClonePrefab(); if (prefab == null) return;
        float range = StatusEffectConfig.Shared?.GetCloneRange(_weaponId, 4f) ?? 4f;
        float ot    = StatusEffectConfig.Shared?.GetCloneOutTime(_weaponId, CloneOutTime * 0.8f) ?? CloneOutTime * 0.8f;

        _cloneFlying      = true;
        _cloneActiveCount = 8;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            var   dir   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var   go    = SpawnCloneVisual(prefab, hitPos);
            StartCoroutine(CloneOneWayRoutine(go, hitPos, hitPos + dir * range, damage * 0.3f, ot));
        }
    }

    private GameObject GetClonePrefab()
    {
        var p = StatusEffectConfig.Shared?.GetClonePrefab(_weaponId) ?? projectilePrefab;
        if (p == null) Debug.LogWarning($"[HIT_CLONE_RETURN] 프리팹 없음 — weaponId='{_weaponId}'.");
        return p;
    }

    private Vector2 GetWeaponOrigin(Vector2 fallback)
        => transform.parent != null ? (Vector2)transform.parent.position : fallback;

    /// <summary>복귀 없이 목표 방향으로만 날아가는 클론 루틴.</summary>
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

            var cols = Physics2D.OverlapCircleAll(go.transform.position, 0.35f, LayerMask.GetMask("Enemy"));
            foreach (var col in cols)
            {
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

    // ── HIT_SPIKE AOE ─────────────────────────────────────────────

    /// <summary>타격 위치 주변 반경의 모든 적/보스에게 동시에 스파이크 착지.</summary>
    private void SpawnAoeSpike(Vector2 hitPos, float damage)
    {
        float r           = Mathf.Max(areaHitRadius, 2.5f);
        int   cnt         = Physics2D.OverlapCircleNonAlloc(hitPos, r, _hitBuffer);
        var   spikePrefab = StatusEffectConfig.Shared?.GetSpikePrefab(_weaponId);
        var   sprite      = _weaponSr?.sprite;
        float spikeDmg    = damage * 1.5f;

        for (int i = 0; i < cnt; i++)
        {
            var boss = _hitBuffer[i].GetComponentInParent<BossController>();
            if (boss != null) { BossSpike.Spawn(boss, sprite, spikeDmg, spikePrefab); continue; }
            var enemy = _hitBuffer[i].GetComponentInParent<EnemyController>();
            if (enemy != null) BossSpike.SpawnOnEnemy(enemy, sprite, spikeDmg, spikePrefab);
        }
    }

    // ── BUFF_* 패시브 헬퍼 ───────────────────────────────────────────

    private IEnumerator PlayBuffEquipVfx(GameObject prefab, Transform playerTransform)
    {
        const float totalDuration = 0.7f;
        const float flipPeriod    = 0.12f;
        const float riseHeight    = 2.5f;

        var go = Instantiate(prefab, playerTransform.position + Vector3.up * 1.5f, Quaternion.identity);
        Destroy(go, totalDuration + 0.15f); // 코루틴이 중단돼도 확실히 제거
        var sr = go.GetComponentInChildren<SpriteRenderer>();

        Vector3 startPos  = go.transform.position;
        float   elapsed   = 0f;
        float   flipTimer = 0f;
        int     flipDir   = 1;

        while (elapsed < totalDuration)
        {
            if (go == null) yield break;
            elapsed   += Time.deltaTime;
            flipTimer += Time.deltaTime;

            float ratio = elapsed / totalDuration;

            // 위로 부드럽게 상승
            go.transform.position = startPos + Vector3.up * (riseHeight * Mathf.SmoothStep(0f, 1f, ratio));

            // Y축 회전 연출: scaleX를 1→-1→1 반복으로 코인플립 시뮬레이션
            if (flipTimer >= flipPeriod * 0.5f)
            {
                flipDir   = -flipDir;
                flipTimer = 0f;
                var s = go.transform.localScale;
                go.transform.localScale = new Vector3(s.x * -1f, s.y, s.z);
            }

            // 후반부 페이드 아웃
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

    // ── HIT_SPIN 고스트 ───────────────────────────────────────────

    private void StartSpinGhosts()
    {
        if (_spinGhosts.Count > 0) return; // 이미 활성 중이면 스킵
        StartCoroutine(SpinGhostRoutine());
    }

    private IEnumerator SpinGhostRoutine()
    {
        if (_weaponSr == null || PlayerController.Instance == null) yield break;

        const int   GhostCount  = 6;
        const float OrbitRadius = 1.8f;
        const float RotSpeed    = 180f;  // deg/sec
        const float Alpha       = 0.45f;
        const float Duration    = 4f;

        bool permanent = StatusEffectConfig.Shared?.GetHitSpinPermanent(_weaponId) ?? false;

        var parent = PlayerController.Instance.transform;
        var ghosts = new GameObject[GhostCount];
        for (int i = 0; i < GhostCount; i++)
        {
            var go = new GameObject("SpinGhost");
            go.transform.SetParent(parent);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _weaponSr.sprite;
            sr.color        = new Color(1f, 1f, 1f, Alpha);
            sr.sortingOrder = _weaponSr.sortingOrder - 1;
            _spinGhosts.Add(go);
            ghosts[i] = go;
        }

        float elapsed = 0f;
        // permanent=true면 Duration 조건 무시하고 파괴될 때까지 공전
        while (permanent || elapsed < Duration)
        {
            if (PlayerController.Instance == null) break;
            elapsed += Time.deltaTime;
            float baseAngle = elapsed * RotSpeed;
            for (int i = 0; i < GhostCount; i++)
            {
                if (ghosts[i] == null) continue;
                float a = (baseAngle + i * (360f / GhostCount)) * Mathf.Deg2Rad;
                ghosts[i].transform.localPosition = new Vector3(Mathf.Cos(a) * OrbitRadius, Mathf.Sin(a) * OrbitRadius, 0f);
                ghosts[i].transform.rotation      = Quaternion.Euler(0f, 0f, baseAngle + i * (360f / GhostCount));
            }
            yield return null;
        }

        foreach (var go in ghosts)
        {
            _spinGhosts.Remove(go);
            if (go != null) Destroy(go);
        }
    }

    private void ApplyShockwave(Vector2 center, float damage)
    {
        float radius = Mathf.Max(areaHitRadius * 1.5f, 2f);
        int   cnt    = Physics2D.OverlapCircleNonAlloc(center, radius, _hitBuffer);
        for (int i = 0; i < cnt; i++)
        {
            var e = _hitBuffer[i].GetComponentInParent<EnemyController>();
            if (e != null)
            {
                Vector2 kDir = ((Vector2)e.transform.position - center).normalized;
                e.TakeDamage(damage, false, kDir, 8f);
                continue;
            }
            var b = _hitBuffer[i].GetComponentInParent<BossController>();
            if (b != null) b.TakeDamage(damage);
        }
        FXManager.Shared?.SpawnHitEffect(center);
    }

    private void FireExtraProjectile(Vector3 spawnPos, Vector2 dir, float dmg)
    {
        if (projectilePrefab == null) return;
        var go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p != null)
            p.Init(dmg, dir, projectileSpeed, criticalRate: criticalRate,
                criticalMultiplier: criticalMultiplier, baseDamage: dmg,
                knockbackPower: knockbackPower);
    }

    private void FireHomingProjectile(Vector3 spawnPos, Vector2 initialDir, float dmg)
    {
        var prefab = StatusEffectConfig.Shared?.GetSubProjectilePrefab(_weaponId) ?? projectilePrefab;
        if (prefab == null) return;
        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        var p  = go.GetComponent<Projectile>();
        if (p == null) return;
        p.Init(dmg, initialDir, projectileSpeed, criticalRate: criticalRate,
            criticalMultiplier: criticalMultiplier, baseDamage: dmg,
            knockbackPower: knockbackPower);
        p.SetHoming(turnSpeed: 220f);
    }

    private static Vector2 RotateVec(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(
            v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r),
            v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (pullOnReturn && weaponType == WeaponType.Melee)
        {
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.3f);
            Gizmos.DrawWireSphere(center, pullRadius);
        }

        if (weaponType == WeaponType.Spin)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, orbitRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, spinHitRadius);
        }

        if (weaponType == WeaponType.Slash)
        {
            // 무기가 바라보는 방향 기준으로 호 미리보기
            Vector2 weaponDir = (transform.position - center).sqrMagnitude > 0.001f
                ? ((Vector2)transform.position - (Vector2)center).normalized
                : Vector2.up;
            float radius      = Mathf.Max(slashRadius, (transform.position - center).magnitude);
            float centerAngle = Mathf.Atan2(weaponDir.y, weaponDir.x) * Mathf.Rad2Deg;
            float startAngle  = centerAngle - slashArcDegrees * 0.5f;
            float endAngle    = centerAngle + slashArcDegrees * 0.5f;

            // 호 선분
            int   steps    = Mathf.Max(8, Mathf.RoundToInt(slashArcDegrees / 10f));
            float stepSize = slashArcDegrees / steps;
            Gizmos.color   = new Color(0.3f, 0.8f, 1f, 0.7f);
            Vector3 prev   = center + (Vector3)(new Vector2(Mathf.Cos(startAngle * Mathf.Deg2Rad), Mathf.Sin(startAngle * Mathf.Deg2Rad)) * radius);
            for (int i = 1; i <= steps; i++)
            {
                float  a    = (startAngle + stepSize * i) * Mathf.Deg2Rad;
                Vector3 cur = center + (Vector3)(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            // VFX 스폰 지점 (중간)
            float midRad    = centerAngle * Mathf.Deg2Rad;
            Vector3 midPos  = center + (Vector3)(new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad)) * radius);
            Gizmos.color    = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(midPos, slashHitRadius);

            // 타격 판정 반경 (호 전체)
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(midPos, slashHitRadius * 2f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            UnityEditor.Handles.Label(midPos + Vector3.up * 0.2f, "VFX");
#endif
        }

    }
}
