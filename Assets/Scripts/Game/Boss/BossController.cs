using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
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
    [BoxGroup("기본 스탯")]
    [Tooltip("이동 속도. DB에서 로드되면 덮어씌워집니다.")]
    [SerializeField] private float moveSpeed     = 3f;
    private float maxHP         = 500f;
    private float contactDamage = 10f;

    [BoxGroup("분신 설정")]
    [Tooltip("true면 HUD·GameManager 알림·드롭을 건너뜁니다. 분신 프리팹에서 체크.")]
    [SerializeField] private bool _isClone;

    // ── 그로기 ─────────────────────────────────────────────────────
    private static readonly float[] GrogyThresholds = { 0.70f, 0.35f };
    [BoxGroup("그로기")]
    [Tooltip("그로기 지속 시간 (초)")]
    [SerializeField] private float   grogyDuration    = 5f;
    [BoxGroup("그로기")]
    [Tooltip("그로기 진입 시 보스 머리 위에 스폰할 UI 프리팹")]
    [SerializeField] private GameObject grogyOverheadPrefab;
    [BoxGroup("그로기")]
    [Tooltip("grogyOverheadPrefab의 부모 Transform")]
    [SerializeField] private Transform  grogyOverheadAnchor;

    private static readonly Color GrogyTint        = new(0.45f, 0.45f, 0.45f, 1f);
    private const           float GrogyAtkSpeedMult = 8f;

    // ── HitFlash ──────────────────────────────────────────────────
    private HitFlash _hitFlash;

    // ── 드롭 (DB에서 로드) ────────────────────────────────────────
    private string bossName      = "BOSS";
    private int    bossGoldCount = 20;
    private int    bossExpCount  = 20;

    [BoxGroup("행동")]
    [Tooltip("플레이어와 이 거리 이내면 추격을 멈춥니다")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [BoxGroup("행동")]
    [Tooltip("한 사이클당 추격 지속 시간 (초)")]
    [SerializeField] private float chaseDuration     = 2.5f;
    [BoxGroup("행동")]
    [Tooltip("추격 전 대기 시간 (초)")]
    [SerializeField] private float idleDuration      = 0.6f;
    [BoxGroup("행동")]
    [Tooltip("데칼 Transform (그림자 등 바닥 이펙트)")]
    [SerializeField] private Transform _decal;

    [SerializeReference, HideInInspector]
    private System.Collections.Generic.List<BossSkillBase> _skills = new();

    // ── 공개 프로퍼티 ──────────────────────────────────────────────
    public float  HPRatio        => maxHP > 0f ? currentHP / maxHP : 0f;
    public float  MaxHPValue    => maxHP;
    public float  CurrentHPValue => currentHP;
    public string BossName       => bossName;
    public float  MoveSpeed      => moveSpeed;
    public float  ContactDamage  => contactDamage;

    // 스킬에서 필요한 공통 참조 (IEnemyHost 구현 포함)
    public Transform   PlayerTransform { get; private set; }
    public Rigidbody2D Rb              { get; private set; }
    public Animator    EnemyAnimator   { get; private set; }
    public Transform   SelfTransform   => transform;   // IEnemyHost
    public Transform   Decal           => _decal;

    public event System.Action OnDeath;
    public event System.Action OnGrogyStart;
    public event System.Action OnGrogyEnd;
    /// <summary>HP가 변경될 때마다 발행됩니다. (current, max)</summary>
    public event System.Action<float, float> OnHPChanged;

    public bool  IsGrogy        { get; private set; }
    public bool  IsMoveBound    { get; private set; }
    public float GrogyRemaining { get; private set; }
    public float GrogyDuration  => grogyDuration;

    public void ApplyBleed(float damage) { }

    // ── 입장 연출 ─────────────────────────────────────────────────────
    [BoxGroup("입장 연출")]
    [Tooltip("true면 스케일 팝인 연출 후 패턴을 시작합니다")]
    [SerializeField] private bool  entryDrop            = true;
    [BoxGroup("입장 연출")]
    [Tooltip("팝인 완료 시간 (초). OutBack 커브로 오버슈트 바운스")]
    [SerializeField] private float entryScaleUpDuration = 0.35f;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private float            currentHP;
    private SpriteRenderer[] srs;
    private Coroutine        knockbackCoroutine;
    private bool             isDead;
    private bool             _dropping;
    private bool             _lastFlipX;

    // 그로기: 이미 발동된 구간 인덱스 추적
    private readonly HashSet<int> _triggeredGrogy = new();
    private Coroutine  _grogyFlickerCoroutine;
    private GameObject _grogyOverheadInstance;
    private float      _grogyReentryTimer; // 그로기 해제 후 재발동 방지 쿨다운

    // UniTask CancellationTokenSource
    private CancellationTokenSource _patternCts;

    /// <summary>현재 보스가 왼쪽을 바라보고 있으면 true.</summary>
    public bool IsFacingLeft => _lastFlipX;

    private BossSkillBase[] _patternSkills = System.Array.Empty<BossSkillBase>();
    public System.Collections.Generic.IReadOnlyList<BossSkillBase> Skills => _skills;
    private int             _skillIndex;
    private float[]         _lastSkillTime = System.Array.Empty<float>();

    public bool IsDead => isDead;

    public bool IsDropping    => _dropping;
    /// <summary>투사체 발사 페이즈 중임을 표시 — ChaseRoutine이 이동을 중단합니다.</summary>
    public bool IsFiringSkill { get; set; }

    // ── 분신 소환용 static 플래그 (BossCloneSkill → Awake 통신) ──────
    public static bool             NextIsClone;
    public static System.Type[]    NextCloneSkillTypes;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current             = null;
        NextIsClone         = false;
        NextCloneSkillTypes = null;
    }

    // ── Unity 생명주기 ─────────────────────────────────────────────

    private void Awake()
    {
        if (NextIsClone) _isClone = true;
        var cloneSkillTypes = NextCloneSkillTypes;
        NextIsClone         = false;
        NextCloneSkillTypes = null;

        if (!_isClone) Current = this;
        Rb            = GetComponent<Rigidbody2D>();
        EnemyAnimator = GetComponentInChildren<Animator>();
        srs           = GetComponentsInChildren<SpriteRenderer>(true);
        _hitFlash     = GetComponentInChildren<HitFlash>(true);

        var relay = GetComponentInChildren<MeleeAnimEventRelay>();
        if (relay != null) relay.OnMeleeHit += OnBossMeleeHit;

        if (_isClone)
        {
            var allowed = cloneSkillTypes != null
                ? new System.Collections.Generic.HashSet<System.Type>(cloneSkillTypes)
                : null;
            _skills.RemoveAll(s => s == null || s is BossCloneSkill
                               || (allowed != null && !allowed.Contains(s.GetType())));
        }

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

        foreach (var s in _skills) s?.Init(this);
        RebuildPatternSkills();
    }

    private void RebuildPatternSkills()
    {
        var list = new List<BossSkillBase>();
        foreach (var s in _skills)
            if (s != null && s.executeInPattern) list.Add(s);
        _patternSkills = list.ToArray();
        _skillIndex    = 0;

        _lastSkillTime = new float[_patternSkills.Length];
        for (int i = 0; i < _lastSkillTime.Length; i++)
            _lastSkillTime[i] = float.NegativeInfinity;
    }

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
            StartPatternLoop();
    }

    // ── 패턴 루프 관리 ────────────────────────────────────────────

    private void CancelPattern()
    {
        _patternCts?.Cancel();
        _patternCts?.Dispose();
        _patternCts = new CancellationTokenSource();

        // 스킬 내부 상태 정리 (IsFiringSkill 등 플래그 고착 방지)
        if (_skills != null)
            foreach (var s in _skills)
                s?.OnInterrupted();
    }

    private void StartPatternLoop()
    {
        CancelPattern();
        PatternLoopAsync(_patternCts.Token).Forget();
    }

    // ── 입장 연출 ──────────────────────────────────────────────────
    private IEnumerator EntryDropRoutine()
    {
        _dropping = true;

        Vector3 targetScale = transform.localScale;
        if (targetScale.sqrMagnitude < 0.001f) targetScale = Vector3.one;
        Vector3 spawnPos = transform.position;

        transform.localScale = Vector3.zero;
        FXManager.Shared?.SpawnBossEntryLandingVfx(spawnPos);

        transform.DOScale(targetScale, entryScaleUpDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        // WaitForCompletion 대신 직접 대기 — tween 실패 시 무한 대기 방지
        float elapsed = 0f;
        float timeout = entryScaleUpDuration + 0.5f;
        while (elapsed < timeout && transform.localScale != targetScale)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localScale = targetScale;

        _dropping = false;
        StartPatternLoop();
    }

    // ── 패턴 루프 ──────────────────────────────────────────────────
    // 구조: Idle → Chase → Skill → 반복
    // 그로기 진입 시 ct 취소로 루프 전체 종료 → GrogyAsync 종료 후 StartPatternLoop()으로 재시작

    private async UniTaskVoid PatternLoopAsync(CancellationToken ct)
    {
        // 오브젝트 파괴 토큰과 합성 → Destroy 시에도 확실히 종료
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            ct, this.GetCancellationTokenOnDestroy());
        var token = linked.Token;

        try
        {
            while (!isDead)
            {
                // ── Idle + Chase 페이즈 ─────────────────────────────────
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(idleDuration), cancellationToken: token);
                await ChaseForAsync(chaseDuration, chaseStopDistance, token);

                if (isDead) break;

                // ── 스킬 실행 페이즈 ────────────────────────────────────
                if (_patternSkills.Length > 0)
                {
                    // cooldown이 지난 첫 번째 스킬 선택 (순환 순서 유지)
                    BossSkillBase chosenSkill = null;
                    int           chosenIdx   = -1;
                    for (int i = 0; i < _patternSkills.Length; i++)
                    {
                        int idx = (_skillIndex + i) % _patternSkills.Length;
                        if (Time.time - _lastSkillTime[idx] >= _patternSkills[idx].cooldown)
                        {
                            chosenSkill = _patternSkills[idx];
                            chosenIdx   = idx;
                            break;
                        }
                    }

                    if (chosenSkill != null)
                    {
                        _skillIndex              = chosenIdx + 1;
                        _lastSkillTime[chosenIdx] = Time.time;
                        await chosenSkill.ExecuteAsync(token);
                    }
                    else
                    {
                        _skillIndex++;
                    }
                }
            }
        }
        catch (System.OperationCanceledException) { }
    }

    /// <summary>duration 동안 플레이어를 추격합니다. 스킬에서 직접 호출할 수 있습니다.</summary>
    public async UniTask ChaseForAsync(float duration, float stopDistance, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;

            if (!IsFiringSkill && PlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerTransform.position);
                if (dist > stopDistance) MoveTowardPlayer(MoveSpeed);
                else                    EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
            }
            else
            {
                EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
            }

            await UniTask.WaitForFixedUpdate(cancellationToken: ct);
        }
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isDead || _dropping) return;

        if (_grogyReentryTimer > 0f) _grogyReentryTimer -= Time.deltaTime;

        if (PlayerTransform == null)
            PlayerTransform = PlayerController.Instance?.transform;

    }

    // ── 이동 API (스킬에서 사용) ────────────────────────────────────

    public void TriggerGroggy(float duration)
    {
        if (_isClone || IsGrogy || isDead || _grogyReentryTimer > 0f) return;
        GrogyAsync(duration).Forget();
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
        if (Rb != null) Rb.MovePosition(Rb.position + delta);
        else            transform.position += (Vector3)delta;
        FlipToward(dir);
    }

    public void FlipToward(Vector2 dir)
    {
        if      (dir.x > 0.1f)  _lastFlipX = false;
        else if (dir.x < -0.1f) _lastFlipX = true;
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
            float   t    = elapsed / duration;
            Vector2 next = Rb.position + dir * force * (1f - t) * Time.fixedDeltaTime;
            var     mb   = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
                next = mb.ClosestPointOnBoundary(next);
            Rb?.MovePosition(next);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    void IEnemyHost.TakeDamage(float damage, bool isCritical) => TakeDamage(damage, isCritical);

    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        if (isDead) return;
        if (TestMonsterSpawner.EnemyInvincible) return;

        float bossAtkBonus = RelicManager.Shared?.BossAtkBonusPct ?? 0f;
        if (bossAtkBonus > 0f) damage *= (1f + bossAtkBonus / 100f);

        currentHP -= damage;

        CharacterSkillManager.Shared?.TryAddPointFromHit(transform.position);

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

        Vector3 textPos = srs != null && srs.Length > 0
            ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        Vector3 hitCenter = srs != null && srs.Length > 0 ? srs[0].bounds.center : transform.position;
        FXManager.Shared?.SpawnHitEffect(hitCenter);

        _hitFlash?.Flash();

        OnHPChanged?.Invoke(Mathf.Max(0f, currentHP), maxHP);

        if (currentHP <= 0f) StartCoroutine(DieRoutine());
        else                 CheckGrogyThresholds();
    }

public void ForceDie()
    {
        if (!isDead) StartCoroutine(DieRoutine());
    }

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
        CancelPattern(); // OnInterrupted()까지 내부 처리
        StopGrogyVisuals();
        OnDeath?.Invoke();

        if (!_isClone)
        {
            foreach (var clone in BossClone.All.ToArray())
                if (clone != null) Destroy(clone.gameObject);

            ItemDropManager.Shared?.SpawnBossDrops(transform.position, bossGoldCount, 1, bossExpCount, 1);
            BossHUDPanel.Shared?.Hide();
        }

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

        gameObject.SetActive(false); // Destroy는 프레임 말 처리이므로 즉시 숨김
        if (!_isClone)
            GameManager.Shared?.OnBossDefeated();
        Destroy(gameObject);
    }

    // ── 그로기 ────────────────────────────────────────────────────

    private void CheckGrogyThresholds()
    {
        if (IsGrogy || isDead) return;
        for (int i = 0; i < GrogyThresholds.Length; i++)
        {
            if (_triggeredGrogy.Contains(i)) continue;
            if (HPRatio <= GrogyThresholds[i])
            {
                _triggeredGrogy.Add(i);
                GrogyAsync(grogyDuration).Forget();
                break;
            }
        }
    }

    private async UniTaskVoid GrogyAsync(float duration)
    {
        // CancelPattern() 내부에서 OnInterrupted()까지 처리
        CancelPattern();

        IsGrogy        = true;
        GrogyRemaining = duration;
        EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        OnGrogyStart?.Invoke();
        StartGrogyVisuals();
        ApplyGrogyAtkSpeed(GrogyAtkSpeedMult);

        try
        {
            var ct = this.GetCancellationTokenOnDestroy();
            while (GrogyRemaining > 0f && !isDead)
            {
                GrogyRemaining -= Time.deltaTime;
                await UniTask.Yield(ct);
            }
        }
        catch (System.OperationCanceledException)
        {
            // 보스 파괴 시에도 플레이어 공격속도 반드시 복구
            ApplyGrogyAtkSpeed(1f);
            StopGrogyVisuals();
            IsGrogy = false;
            return;
        }

        GrogyRemaining = 0f;
        ApplyGrogyAtkSpeed(1f / GrogyAtkSpeedMult);
        StopGrogyVisuals();
        IsGrogy = false;
        _grogyReentryTimer = idleDuration + chaseDuration; // 최소 1사이클(대기+추격) 동안 재발동 방지
        OnGrogyEnd?.Invoke();
        if (!isDead) StartPatternLoop();
    }

    // ── 그로기 비주얼 ─────────────────────────────────────────────────

    private void StartGrogyVisuals()
    {
        _hitFlash?.EnterPersistentState(GrogyTint);

        if (_grogyFlickerCoroutine != null) StopCoroutine(_grogyFlickerCoroutine);
        _grogyFlickerCoroutine = StartCoroutine(GrogyEntryHighlight());

        if (grogyOverheadPrefab != null && grogyOverheadAnchor != null)
        {
            if (_grogyOverheadInstance != null) Destroy(_grogyOverheadInstance);
            _grogyOverheadInstance = Instantiate(grogyOverheadPrefab, grogyOverheadAnchor);
            _grogyOverheadInstance.transform.localPosition = Vector3.zero;
        }
    }

    private void StopGrogyVisuals()
    {
        if (_grogyFlickerCoroutine != null) { StopCoroutine(_grogyFlickerCoroutine); _grogyFlickerCoroutine = null; }
        _hitFlash?.ExitPersistentState();
        if (_grogyOverheadInstance != null) { Destroy(_grogyOverheadInstance); _grogyOverheadInstance = null; }
    }

    private IEnumerator GrogyEntryHighlight()
    {
        transform.DOPunchScale(Vector3.one * 0.3f, 0.6f, 8, 0.4f);
        StartCoroutine(GrogyWaveEffect());

        float totalTime = 0.6f;
        float t = 0f;
        while (t < totalTime)
        {
            t += Time.deltaTime;
            float ratio   = t / totalTime;
            float flicker = Mathf.Abs(Mathf.Sin(ratio * Mathf.PI * 4f)) * (1f - ratio) * 0.5f;
            float white   = Mathf.Clamp01(1f - ratio + flicker);
            Color c = Color.Lerp(GrogyTint, Color.white, white);
            _hitFlash?.UpdatePersistentColor(c);
            yield return null;
        }

        _hitFlash?.UpdatePersistentColor(GrogyTint);
        _grogyFlickerCoroutine = null;
    }

    private IEnumerator GrogyWaveEffect()
    {
        SpriteRenderer sr0 = null;
        if (srs != null)
            foreach (var sr in srs)
                if (sr && sr.sprite != null) { sr0 = sr; break; }
        if (sr0 == null) yield break;

        var go = new GameObject("GrogyWave");
        go.transform.position = sr0.bounds.center;

        var waveSR = go.AddComponent<SpriteRenderer>();
        waveSR.sprite         = sr0.sprite;
        waveSR.sortingLayerID = sr0.sortingLayerID;
        waveSR.sortingOrder   = sr0.sortingOrder + 1;
        waveSR.color = new Color(1f, 1f, 1f, 0.9f);

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
            wp.SetGrogySpeedMult(mult);
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
        _patternCts?.Cancel();
        _patternCts?.Dispose();
    }

    // ── 애니메이션 이벤트 포워딩 (순수 C# 스킬은 직접 수신 불가) ────────────

    /// <summary>텔레포트 애니메이션 클립 마지막 프레임에서 호출됩니다.</summary>
    public void OnTeleportAnimEnd(string clipName)
    {
        foreach (var s in _skills)
            if (s is BossTeleportSkill ts) { ts.NotifyTeleportAnimEnd(clipName); return; }
    }

    private void OnBossMeleeHit()
    {
        foreach (var s in _skills)
            if (s is BossMeleeSkill ms) { ms.NotifyFastChaseHit(); return; }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (other.gameObject != p.gameObject) return;
        p.TakeDamage(contactDamage, isFromBoss: true, sourceName: gameObject.name);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        var p = collision.collider.GetComponentInParent<PlayerController>();
        if (p == null) return;
        p.TakeDamage(contactDamage, sourceName: gameObject.name);
    }
}
