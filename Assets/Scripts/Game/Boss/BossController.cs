using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 보스 전용 올인원 컴포넌트.
/// EnemyController · HitFlash · EnemyFXConfig · BossBehaviour 기능을 모두 포함합니다.
/// 프리팹 구성: BossController + 스킬 컴포넌트(들) 만으로 동작합니다.
/// </summary>
public class BossController : MonoBehaviour, IEnemyHost
{
    public static BossController Current { get; private set; }

    // ── 기본 스탯 (DB에서 로드, DB 없을 때 아래 기본값 사용) ────────
    [Header("기본 스탯 (DB 로드 전 폴백)")]
    [SerializeField] private float damageCooldown = 0.5f;
    [Tooltip("이동 속도. DB에서 로드되면 덮어씌워집니다.")]
    [SerializeField] private float moveSpeed     = 3f;
    private float maxHP         = 500f;
    private float contactDamage = 10f;

    [Header("분신 설정")]
    [Tooltip("true면 HUD·GameManager 알림·드롭을 건너뜁니다. 분신 프리팹에서 체크.")]
    [SerializeField] private bool _isClone;

    // ── 분노 ──────────────────────────────────────────────────────
    [Header("분노")]
    [SerializeField] private float enrageThreshold  = 0.5f;
    [SerializeField] private float enrageSpeedMult  = 1.5f;
    [SerializeField] private float enrageDamageMult = 1.5f;

    // ── 그로기 ─────────────────────────────────────────────────────
    [Header("그로기")]
    [Tooltip("HP 비율 기준 그로기 진입 구간 (내림차순 권장: 0.70, 0.35)")]
    [SerializeField] private float[] grogyThresholds  = { 0.70f, 0.35f };
    [SerializeField] private float   grogyDuration    = 5f;
    [SerializeField] private GameObject grogyOverheadPrefab;
    [Tooltip("할당하면 해당 Transform 위치에 스폰. 없으면 YOffset 사용.")]
    [SerializeField] private Transform  grogyOverheadAnchor;
    [SerializeField] private float      grogyOverheadYOffset = 1.5f;

    [Tooltip("그로기 중 단색 실루엣 머티리얼. Inspector에서 할당. 없으면 sr.color만 변경.")]
    [SerializeField] private Material grogyMaterial;

    private static readonly Color GrogyTint        = new(0.45f, 0.45f, 0.45f, 1f);
    private const           float GrogyAtkSpeedMult = 8f;

    // ── HitFlash ──────────────────────────────────────────────────
    private HitFlash _hitFlash;

    // ── FX ────────────────────────────────────────────────────────
    [Header("이펙트")]
    [SerializeField] private EnemyEffectEntry onDeathFX = new();

    // ── 드롭 (DB에서 로드) ────────────────────────────────────────
    private string bossName      = "BOSS";
    private int    bossGoldCount = 20;
    private int    bossExpCount  = 20;

    [Header("행동 (Chase)")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [SerializeField] private float chaseDuration     = 2.5f;
    [SerializeField] private float idleDuration      = 0.6f;

    [Header("스킬 목록")]
    [Tooltip("비워두면 시작 시 컴포넌트를 자동 탐색합니다.")]
    [SerializeField] private BossSkillBase[] skills;

    // ── 공개 프로퍼티 ──────────────────────────────────────────────
    public bool   IsEnraged      { get; private set; }
    public float  HPRatio        => maxHP > 0f ? currentHP / maxHP : 0f;
    public float  MaxHPValue    => maxHP;
    public float  CurrentHPValue => currentHP;
    public string BossName       => bossName;
    public float  EnrageSpeedMult  => enrageSpeedMult;
    public float  EnrageDamageMult => enrageDamageMult;
    public float  MoveSpeed        => moveSpeed * (IsEnraged ? enrageSpeedMult : 1f);
    public float  ContactDamage    => contactDamage;

    // 스킬에서 필요한 공통 참조 (IEnemyHost 구현 포함)
    public Transform   PlayerTransform { get; private set; }
    public Rigidbody2D Rb              { get; private set; }
    public Animator    EnemyAnimator   { get; private set; }
    public Transform   SelfTransform   => transform;   // IEnemyHost
    public Transform   Decal           => _decal;
    [SerializeField] private Transform _decal;

    public event System.Action OnEnraged;
    public event System.Action OnDeath;
    public event System.Action<float> OnSkillGaugeChanged;
    public event System.Action OnGrogyStart;
    public event System.Action OnGrogyEnd;

    public float SkillGauge { get; private set; }
    public float MaxSkillGauge => 100f;

    public bool  IsGrogy        { get; private set; }
    public bool  IsMoveBound    { get; private set; }
    public float GrogyRemaining { get; private set; }
    public float GrogyDuration  => grogyDuration;

    public void ApplyBleed(float damage) { } // Placeholder for compatibility

    // ── 입장 연출 ─────────────────────────────────────────────────────
    [Header("입장 연출")]
    [SerializeField] private bool  entryDrop            = true;
    [Tooltip("팝인 완료 시간 (초). OutBack 커브로 오버슈트 바운스")]
    [SerializeField] private float entryScaleUpDuration = 0.35f;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private float            currentHP;
    private float            damageTimer;
    private SpriteRenderer[] srs;
    private Coroutine        knockbackCoroutine;
    private Coroutine        _patternCoroutine;
    private bool             isDead;
    private bool             _dropping;
    private bool             _skillExecuting;
    private bool             _lastFlipX;  // FlipToward 중복 갱신 방지

    // 그로기: 이미 발동된 구간 인덱스 추적
    private readonly System.Collections.Generic.HashSet<int> _triggeredGrogy = new();
    private Coroutine  _gaugeSkillCoroutine;
    private Coroutine  _grogyFlickerCoroutine;
    private GameObject _grogyOverheadInstance;
    private Color[]    _originalSrColors;
    private Material[] _originalMaterials;

    /// <summary>현재 보스가 왼쪽을 바라보고 있으면 true.</summary>
    public bool IsFacingLeft => _lastFlipX;

    private BossSkillBase[] _patternSkills = System.Array.Empty<BossSkillBase>();
    private int             _skillIndex;

    public bool IsDropping    => _dropping;
    /// <summary>투사체 발사 페이즈 중임을 표시 — ChaseRoutine이 이동을 중단합니다.</summary>
    public bool IsFiringSkill { get; set; }

    // ── 분신 소환용 static 플래그 (BossCloneSkill → Awake 통신) ──────
    /// <summary>다음 Instantiate 될 BossController 를 분신으로 표시합니다.</summary>
    public static bool             NextIsClone;
    /// <summary>분신이 유지할 스킬 타입 목록. null이면 모든 스킬 유지.</summary>
    public static System.Type[]    NextCloneSkillTypes;

    // ── Unity 생명주기 ─────────────────────────────────────────────

    private void Awake()
    {
        // static 플래그로 분신 여부 설정 (BossCloneSkill → Awake 통신)
        if (NextIsClone) _isClone = true;
        var cloneSkillTypes = NextCloneSkillTypes;
        NextIsClone         = false;
        NextCloneSkillTypes = null;

        if (!_isClone) Current = this;
        Rb            = GetComponent<Rigidbody2D>();
        EnemyAnimator = GetComponentInChildren<Animator>();
        srs      = GetComponentsInChildren<SpriteRenderer>(true);
        _hitFlash = GetComponentInChildren<HitFlash>(true);

        if (skills == null || skills.Length == 0)
            skills = GetComponents<BossSkillBase>();

        // 분신: BossCloneSkill 항상 제거 + 허용 타입 필터 적용
        if (_isClone)
        {
            var allowed  = cloneSkillTypes != null
                ? new System.Collections.Generic.HashSet<System.Type>(cloneSkillTypes)
                : null;
            var filtered = new System.Collections.Generic.List<BossSkillBase>();
            foreach (var s in skills)
            {
                if (s == null) continue;
                bool isCloneSkill = s is BossCloneSkill;
                bool notAllowed   = allowed != null && !allowed.Contains(s.GetType());
                if (isCloneSkill || notAllowed)
                    DestroyImmediate(s);
                else
                    filtered.Add(s);
            }
            skills = filtered.ToArray();
        }

        // 클론: 원본이 피격 중일 때 복제되면 flash 색상이 그대로 복사됨 → 초기화
        if (_isClone && srs != null)
            foreach (var sr in srs) sr.color = Color.white;

        if (Rb != null)
        {
            Rb.bodyType      = RigidbodyType2D.Kinematic;
            Rb.sleepMode     = RigidbodySleepMode2D.NeverSleep;
            Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            Rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        }

        currentHP = maxHP;

        foreach (var s in skills)
            s.Init(this);

        RebuildPatternSkills();
    }

    private void RebuildPatternSkills()
    {
        var list = new System.Collections.Generic.List<BossSkillBase>();
        foreach (var s in skills)
            if (s != null && s.executeInPattern) list.Add(s);
        _patternSkills = list.ToArray();
        _skillIndex    = 0;
    }

    /// <summary>패턴 스킬 목록에서 다음 스킬을 순차 반환합니다.</summary>
    public BossSkillBase NextPatternSkill()
    {
        if (_patternSkills.Length == 0) return null;
        var skill = _patternSkills[_skillIndex % _patternSkills.Length];
        _skillIndex++;
        return skill;
    }

    private void Start()
    {
        PlayerTransform = PlayerController.Instance?.transform;
        if (!_isClone)
        {
            GameManager.Shared?.StartBossWave();
            BossHUDPanel.Shared?.Show(this);
        }

        if (entryDrop && !_isClone)
            StartCoroutine(EntryDropRoutine());
        else
            _patternCoroutine = StartCoroutine(PatternRoutine());
    }

    // ── 입장 연출 ──────────────────────────────────────────────────
    private IEnumerator EntryDropRoutine()
    {
        _dropping = true;

        Vector3 originalScale = transform.localScale;
        Vector3 spawnPos      = transform.position;

        // 보스 숨김
        transform.localScale = Vector3.zero;

        // 착지 VFX
        FXManager.Shared?.SpawnBossEntryLandingVfx(spawnPos);

        // 스케일 0 → 원본, OutBack으로 오버슈트 바운스 (뿅!)
        yield return transform.DOScale(originalScale, entryScaleUpDuration)
            .SetEase(DG.Tweening.Ease.OutBack)
            .WaitForCompletion();

        _dropping = false;
        _patternCoroutine = StartCoroutine(PatternRoutine());
    }

    // ── 패턴 루프 ──────────────────────────────────────────────────

    private IEnumerator PatternRoutine()
    {
        while (!isDead)
        {
            // 그로기 중에는 대기
            yield return new WaitUntil(() => !IsGrogy || isDead);
            if (isDead) break;

            // 1) 대기
            yield return new WaitForSeconds(IdleTime());
            if (isDead) break;

            // 2) 추격
            yield return ChaseRoutine();
            if (isDead) break;

            // 3) 스킬 실행 — 순차 플레이리스트 방식
            if (!_skillExecuting && _patternSkills.Length > 0)
            {
                var skill = _patternSkills[_skillIndex % _patternSkills.Length];
                _skillIndex++;
                _skillExecuting = true;
                yield return skill.Execute();
                _skillExecuting = false;

                // 스킬별 쿨타임 (분노 시 0.6배)
                float cd = skill.cooldown * (IsEnraged ? 0.6f : 1f);
                if (cd > 0f) yield return new WaitForSeconds(cd);
            }
        }
    }

    private float IdleTime() => IsEnraged ? idleDuration * 0.6f : idleDuration;

    private IEnumerator ChaseRoutine()
    {
        float elapsed = 0f;
        while (elapsed < chaseDuration)
        {
            // 그로기 돌입 시 즉시 이동 중단 후 루틴 종료 (PatternRoutine 상단 WaitUntil로 복귀)
            if (IsGrogy)
            {
                EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
                yield break;
            }

            elapsed += Time.fixedDeltaTime;

            if (!IsFiringSkill && PlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerTransform.position);
                if (dist > chaseStopDistance)
                    MoveTowardPlayer(MoveSpeed);
                else
                    EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
            }
            else
            {
                EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
            }

            yield return new WaitForFixedUpdate();
        }
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isDead || _dropping) return;

        damageTimer -= Time.deltaTime;

        // 플레이어 참조 갱신
        if (PlayerTransform == null)
            PlayerTransform = PlayerController.Instance?.transform;

        // 분노 체크
        if (!IsEnraged && HPRatio <= enrageThreshold)
            TriggerEnrage();

    }

    // ── 이동 API (스킬에서 사용) ────────────────────────────────────

    public void TriggerGroggy(float duration)
    {
        if (IsGrogy || isDead) return;
        StartCoroutine(ForcedGrogyRoutine(duration));
    }

    private IEnumerator ForcedGrogyRoutine(float duration)
    {
        if (_patternCoroutine   != null) { StopCoroutine(_patternCoroutine);   _patternCoroutine   = null; }
        if (_gaugeSkillCoroutine != null) { StopCoroutine(_gaugeSkillCoroutine); _gaugeSkillCoroutine = null; }
        _skillExecuting = false;
        IsGrogy        = true;
        GrogyRemaining = duration;
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        OnGrogyStart?.Invoke();
        StartGrogyVisuals();
        ApplyGrogyAtkSpeed(GrogyAtkSpeedMult);
        while (GrogyRemaining > 0f) { GrogyRemaining -= Time.deltaTime; yield return null; }
        GrogyRemaining = 0f;
        ApplyGrogyAtkSpeed(1f / GrogyAtkSpeedMult);
        StopGrogyVisuals();
        IsGrogy = false;
        OnGrogyEnd?.Invoke();
        if (!isDead) _patternCoroutine = StartCoroutine(PatternRoutine());
    }

    public void BindMovement(float duration)
    {
        if (IsGrogy || isDead) return;
        StartCoroutine(BindMovementRoutine(duration));
    }

    private IEnumerator BindMovementRoutine(float duration)
    {
        IsMoveBound = true;
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        yield return new WaitForSeconds(duration);
        IsMoveBound = false;
    }

    public void MoveTowardPlayer(float speed)
    {
        if (IsMoveBound || IsGrogy) return;
        if (PlayerTransform == null || Rb == null) return;
        Vector2 dir = ((Vector2)PlayerTransform.position - Rb.position).normalized;
        MoveInDirection(dir, speed);
    }

    public void MoveInDirection(Vector2 dir, float speed)
    {
        if (IsMoveBound || IsGrogy) return;
        EnemyAnimator?.SetBool(EnemyAnim.BMove, true);
        Vector2 delta = dir.normalized * speed * Time.fixedDeltaTime;
        if (Rb != null)
            Rb.MovePosition(Rb.position + delta);
        else
            transform.position += (Vector3)delta;
        FlipToward(dir);
    }

    public void FlipToward(Vector2 dir)
    {
        if      (dir.x > 0.1f)  _lastFlipX = false;
        else if (dir.x < -0.1f) _lastFlipX = true;
        // dir.x ≈ 0 (플레이어가 보스와 X가 같은 경우) → 현재 방향 유지
    }

    private void LateUpdate()
    {
        if (isDead || _dropping) return;
        var s = transform.localScale;
        float absX = Mathf.Abs(s.x);
        s.x = _lastFlipX ? -absX : absX;
        transform.localScale = s;
    }

    // ── 피격 / 사망 ─────────────────────────────────────────────────

    /// <summary>지정 방향으로 순간적으로 밀어냅니다.</summary>
    public void Knockback(Vector2 dir, float force)
    {
        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(dir.normalized, force));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float force)
    {
        float elapsed  = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t    = elapsed / duration;
            Vector2 next = Rb.position + dir * force * (1f - t) * Time.fixedDeltaTime;

            Rb?.MovePosition(next);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        if (isDead) return;
        if (TestMonsterSpawner.EnemyInvincible) return;

        float bossAtkBonus = RelicManager.Shared?.BossAtkBonusPct ?? 0f;
        if (bossAtkBonus > 0f) damage *= (1f + bossAtkBonus / 100f);

        currentHP -= damage;

        // 플레이어 스킬 게이지 증가
        CharacterSkillManager.Shared?.AddPoint(1);

        // 보스 스킬 게이지 증가
        AddSkillGauge(damage);

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

        Vector3 textPos = srs != null && srs.Length > 0
            ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        FXManager.Shared?.SpawnHitEffect(transform.position);

        _hitFlash?.Flash();

        if (currentHP <= 0f)
            StartCoroutine(DieRoutine());
        else
            CheckGrogyThresholds();
    }

    private void AddSkillGauge(float damage)
    {
        if (isDead) return;
        SkillGauge = Mathf.Min(SkillGauge + damage, MaxSkillGauge);
        OnSkillGaugeChanged?.Invoke(SkillGauge);

        // 게이지 100% 시 즉시 스킬 발동
        if (SkillGauge >= MaxSkillGauge)
        {
            if (_gaugeSkillCoroutine != null) StopCoroutine(_gaugeSkillCoroutine);
            _gaugeSkillCoroutine = StartCoroutine(TriggerSkillFromGauge());
        }
    }

    private IEnumerator TriggerSkillFromGauge()
    {
        SkillGauge = 0f;
        OnSkillGaugeChanged?.Invoke(0f);

        // 이미 스킬 실행 중이거나 사망/낙하/그로기 중이면 취소
        if (isDead || _dropping || _skillExecuting || IsGrogy) { _gaugeSkillCoroutine = null; yield break; }

        var skill = NextPatternSkill();
        if (skill != null)
        {
            _skillExecuting = true;
            yield return skill.Execute();
            _skillExecuting = false;
        }
        _gaugeSkillCoroutine = null;
    }

    public void ForceDie()
    {
        if (!isDead) StartCoroutine(DieRoutine());
    }

    /// <summary>분신 스탯 초기화. BossClone.Init() 에서 Awake 직후·Start 직전에 호출.</summary>
    public void ApplyCloneStats(float hp, float contactDmg, float speed)
    {
        maxHP         = hp;
        currentHP     = hp;
        contactDamage = contactDmg;
        moveSpeed     = speed;
    }

    private IEnumerator DieRoutine()
    {
        isDead = true;
        StopGrogyVisuals();
        OnDeath?.Invoke();

        PlayFX(onDeathFX);
        if (!_isClone)
        {
            foreach (var clone in BossClone.All.ToArray())
                if (clone != null) Destroy(clone.gameObject);

            ItemDropManager.Shared?.SpawnBossDrops(transform.position, bossGoldCount, 1, bossExpCount, 1);
            BossHUDPanel.Shared?.Hide();
            GameManager.Shared?.OnBossDefeated();
        }

        // 사망 연출
        if (srs != null)
            foreach (var sr in srs) sr.color = Color.white;

        float elapsed = 0f;
        Vector3 initScale = transform.localScale;
        while (elapsed < 0.4f)
        {
            float t = elapsed / 0.4f;
            transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── 분노 ─────────────────────────────────────────────────────

    private void TriggerEnrage()
    {
        IsEnraged = true;
        OnEnraged?.Invoke();
    }

    // ── 그로기 ────────────────────────────────────────────────────

    private void CheckGrogyThresholds()
    {
        if (IsGrogy || isDead) return;
        for (int i = 0; i < grogyThresholds.Length; i++)
        {
            if (_triggeredGrogy.Contains(i)) continue;
            if (HPRatio <= grogyThresholds[i])
            {
                _triggeredGrogy.Add(i);
                StartCoroutine(GrogyRoutine());
                break;
            }
        }
    }

    private IEnumerator GrogyRoutine()
    {
        if (_patternCoroutine   != null) { StopCoroutine(_patternCoroutine);   _patternCoroutine   = null; }
        if (_gaugeSkillCoroutine != null) { StopCoroutine(_gaugeSkillCoroutine); _gaugeSkillCoroutine = null; }
        _skillExecuting = false;
        IsGrogy        = true;
        GrogyRemaining = grogyDuration;
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        OnGrogyStart?.Invoke();
        StartGrogyVisuals();

        ApplyGrogyAtkSpeed(GrogyAtkSpeedMult);

        while (GrogyRemaining > 0f)
        {
            GrogyRemaining -= Time.deltaTime;
            yield return null;
        }
        GrogyRemaining = 0f;

        ApplyGrogyAtkSpeed(1f / GrogyAtkSpeedMult);
        StopGrogyVisuals();
        IsGrogy = false;
        OnGrogyEnd?.Invoke();
        if (!isDead) _patternCoroutine = StartCoroutine(PatternRoutine());
    }

    // ── 그로기 비주얼 ─────────────────────────────────────────────────

    private void StartGrogyVisuals()
    {
        // 피격 flash 중이면 먼저 중단해야 원본 색상을 올바르게 스냅샷할 수 있음
        _hitFlash?.StopFlash();

        if (srs != null)
        {
            _originalSrColors    = new Color[srs.Length];
            _originalMaterials   = new Material[srs.Length];
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                _originalSrColors[i]  = srs[i].color;
                _originalMaterials[i] = srs[i].material;
            }

            foreach (var sr in srs)
            {
                if (!sr) continue;
                sr.color = Color.white;
                if (grogyMaterial != null) sr.material = grogyMaterial;
            }
        }

        if (_grogyFlickerCoroutine != null) StopCoroutine(_grogyFlickerCoroutine);
        _grogyFlickerCoroutine = StartCoroutine(GrogyEntryHighlight());

        if (grogyOverheadPrefab != null)
        {
            if (_grogyOverheadInstance != null) Destroy(_grogyOverheadInstance);
            Transform parent = grogyOverheadAnchor != null ? grogyOverheadAnchor : transform;
            Vector3 localPos = grogyOverheadAnchor != null ? Vector3.zero : Vector3.up * grogyOverheadYOffset;
            _grogyOverheadInstance = Instantiate(grogyOverheadPrefab, parent);
            _grogyOverheadInstance.transform.localPosition = localPos;
        }
    }

    private void StopGrogyVisuals()
    {
        if (_grogyFlickerCoroutine != null) { StopCoroutine(_grogyFlickerCoroutine); _grogyFlickerCoroutine = null; }

        if (srs != null && _originalSrColors != null)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                if (i < _originalSrColors.Length)  srs[i].color    = _originalSrColors[i];
                if (_originalMaterials != null && i < _originalMaterials.Length && _originalMaterials[i] != null)
                    srs[i].material = _originalMaterials[i];
            }
        }
        else if (srs != null)
        {
            foreach (var sr in srs) if (sr) sr.color = Color.white;
        }

        _originalSrColors  = null;
        _originalMaterials = null;

        if (_grogyOverheadInstance != null) { Destroy(_grogyOverheadInstance); _grogyOverheadInstance = null; }
    }

    private IEnumerator GrogyEntryHighlight()
    {
        transform.DOPunchScale(Vector3.one * 0.3f, 0.6f, 8, 0.4f);
        StartCoroutine(GrogyWaveEffect());

        // 흰색에서 시작해 번쩍이며 회색으로 정착 (0.6s)
        float totalTime = 0.6f;
        float t = 0f;
        while (t < totalTime)
        {
            t += Time.deltaTime;
            float ratio   = t / totalTime;
            float flicker = Mathf.Abs(Mathf.Sin(ratio * Mathf.PI * 4f)) * (1f - ratio) * 0.5f;
            float white   = Mathf.Clamp01(1f - ratio + flicker);
            Color c = Color.Lerp(GrogyTint, Color.white, white);
            if (srs != null) foreach (var sr in srs) if (sr) sr.color = c;
            yield return null;
        }

        if (srs != null) foreach (var sr in srs) if (sr) sr.color = GrogyTint;
        _grogyFlickerCoroutine = null;
    }

    private IEnumerator GrogyWaveEffect()
    {
        // 유효한 스프라이트가 있는 SpriteRenderer 탐색 (Animator 미업데이트 시 null 가능)
        SpriteRenderer sr0 = null;
        if (srs != null)
            foreach (var sr in srs)
                if (sr && sr.sprite != null) { sr0 = sr; break; }
        if (sr0 == null) yield break;

        var go = new GameObject("GrogyWave");
        // 보스 루트 위치가 아닌 스프라이트 실제 월드 중심에 배치
        go.transform.position = sr0.bounds.center;

        var waveSR = go.AddComponent<SpriteRenderer>();
        waveSR.sprite         = sr0.sprite;
        waveSR.sortingLayerID = sr0.sortingLayerID;
        waveSR.sortingOrder   = sr0.sortingOrder + 1;

        waveSR.color = new Color(1f, 1f, 1f, 0.9f);

        // 보스 월드 스케일 기준으로 파동 시작 크기 결정
        float bossScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        go.transform.localScale = Vector3.one * bossScale;

        const float duration  = 0.5f;
        const float scaleMult = 1.8f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (go == null) yield break;
            elapsed += Time.deltaTime;
            float f    = elapsed / duration;
            float ease = 1f - (1f - f) * (1f - f);
            go.transform.localScale = Vector3.one * Mathf.LerpUnclamped(bossScale, bossScale * scaleMult, ease);
            waveSR.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, ease));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private static void ApplyGrogyAtkSpeed(float mult)
    {
        var player = PlayerController.Instance;
        if (player == null) return;
        foreach (var wp in player.GetComponentsInChildren<WeaponPoint>())
            wp.MultiplyAttackSpeed(mult);
    }


    // ── DB 데이터 적용 ────────────────────────────────────────────

    public void ApplyBossData(BossData data)
    {
        bossName      = data.bossName;
        bossGoldCount = data.goldCount;
        bossExpCount  = data.expCount;
        maxHP         = data.hp;
        currentHP     = data.hp;
        contactDamage = data.contactDamage;
        moveSpeed     = data.moveSpeed;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ── FX 유틸 ──────────────────────────────────────────────────

    private void PlayFX(EnemyEffectEntry entry)
    {
        if (entry == null) return;
        if (entry.vfxPrefab != null)
            Instantiate(entry.vfxPrefab, transform.position + (Vector3)entry.spawnOffset, Quaternion.identity);
        if (entry.sfxClip != null)
            AudioSource.PlayClipAtPoint(entry.sfxClip, transform.position, entry.volume);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;

        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;

        // 플레이어 본체(PlayerController가 있는 바로 그 오브젝트)의 콜라이더가 아니라
        // 자식 오브젝트에 달려 있는 투명한 자석(Magnet)이나 공격 반경 트리거라면 무시!
        if (other.gameObject != p.gameObject) return;

        damageTimer = damageCooldown; // TakeDamage 전에 설정 — OnCollisionStay2D 동프레임 중복 방지
        p.TakeDamage(contactDamage, isFromBoss: true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (damageTimer > 0f) return;
        var p = collision.collider.GetComponentInParent<PlayerController>();
        if (p == null) return;
        damageTimer = damageCooldown; // TakeDamage 전에 설정 — OnTriggerStay2D 동프레임 중복 방지
        p.TakeDamage(contactDamage);
    }
}
