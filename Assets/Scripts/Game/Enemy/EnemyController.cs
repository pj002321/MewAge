using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 적의 핵심 컴포넌트 — 스탯, HP, 이동 헬퍼, 피격/사망을 담당합니다.
/// 공격 / 이동 세부 로직은 같은 GameObject에 부착된 <see cref="EnemyBehaviourBase"/>
/// 서브클래스(MeleeBehaviour, RangedBehaviour 등)에서 처리합니다.
/// </summary>
public class EnemyController : MonoBehaviour, IEnemyHost
{
    // ── 인스펙터 ────────────────────────────────────────────────
    // DB_유닛에서 로드 (ApplyUnitData) — 인스펙터 편의 노출
    [SerializeField] private float moveSpeed     = 2f;
    [SerializeField] private float maxHP         = 30f;
    [SerializeField] private float contactDamage = 10f;
    [SerializeField] private int   goldDrop      = 5;
    [SerializeField] private int   expDrop       = 10;

    [Header("전투")]
    [SerializeField] private int   scoreValue     = 10;
    [Tooltip("OnTrigger 접촉 데미지 최대 거리 — 콜라이더가 스프라이트보다 클 때 보정.")]
    [SerializeField] private float contactMaxDist = 0.8f;

    [Header("넉백 면역")]
    [SerializeField] private bool knockbackImmune = false;
    public bool KnockbackImmune { get => knockbackImmune; set => knockbackImmune = value; }

    [Header("출혈")]
    [SerializeField] private float bleedDuration = 5f; // 출혈 지속 시간
    [SerializeField] private float bleedInterval = 1f; // 출혈 간격

    [Header("분리 (간격 유지)")]
    [Tooltip("주변 적과 이 거리 이내면 밀어냅니다. 0 = 비활성")]
    [SerializeField] private float separationRadius   = 1.2f;
    [Tooltip("밀어내는 힘의 강도")]
    [SerializeField] public float separationStrength = 8f;



    // ── 공개 프로퍼티 (EnemyBehaviourBase에서 사용) ────────────
    public float      MoveSpeed       => moveSpeed;
    public float      ContactDamage   => contactDamage;
    public Transform  PlayerTransform => playerTransform;
    public Rigidbody2D Rb             => rb;
    public Animator   EnemyAnimator   => animator;
    public float      HPRatio         => maxHP > 0f ? currentHP / maxHP : 0f;
    public float      MaxHP           => maxHP;
    public Transform  SelfTransform   => transform;   // IEnemyHost
    public Transform  Decal           => _decal;
    [SerializeField] private Transform _decal;

    // ── 런타임 상태 ─────────────────────────────────────────────
    private float          currentHP;
    private Transform      playerTransform;
    private Rigidbody2D    rb;
    private SpriteRenderer[] srs;
    private Collider2D[]   _cachedColliders;
    private HitFlash       hitFlash;
    private Animator       animator;
    private EnemyBehaviourBase behaviour;
    private bool           _lastFlipX;  // FlipToward 중복 갱신 방지

    private bool      isStunned;
    private bool      _isDying;
    public  bool      IsPooled { get; private set; }
    private Vector2   _pendingMove;
    private bool      _isKnockedBack;
    private Coroutine knockbackCoroutine;
    private Coroutine bleedCoroutine;
    private Coroutine _fireCoroutine;
    private float     baseSpeed; // 넉백/스턴 전 원본 속도
    private Vector2   _deathDir;  // 사망 시 날아갈 방향
    private bool      isMoving;
    private bool      _lastIsMoving = true; // 초기값 다르게 설정해 첫 프레임 강제 동기화
    private float     _contactTimer;
    private bool      _halfHpGoldDropped;

    // ── 각도 슬롯 (포위 형성) ────────────────────────────────
    public float SlotAngle         { get; private set; }
    public bool  HasSlotAssignment { get; private set; }

    // 상태효과 VFX 인스턴스 (Resources/VFX/SE_Freeze, SE_Stun, SE_Fire)
    private GameObject _freezeVfxInst;
    private GameObject _stunVfxInst;
    private GameObject _fireVfxInst;

    private Coroutine _stunCoroutine;
    private Coroutine _freezeCoroutine;
    private Coroutine _purpleDotCoroutine;

    // 사망 흰색 실루엣 재질 (SpriteSilhouette 쉐이더) — 씬 전체 공유
    private static Material _deathFlashMat;
    private static Material GetDeathFlashMat()
    {
        if (_deathFlashMat != null) return _deathFlashMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _deathFlashMat = new Material(shader);
        _deathFlashMat.SetColor("_Color", Color.white);
        return _deathFlashMat;
    }

    // 듀크냥 스킬 보라색 실루엣 재질
    private static Material _purpleDotMat;
    private static Material GetPurpleDotMat()
    {
        if (_purpleDotMat != null) return _purpleDotMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _purpleDotMat = new Material(shader);
        _purpleDotMat.SetColor("_Color", new Color(0.6f, 0f, 1f, 1f));
        return _purpleDotMat;
    }

    public event System.Action OnDeath;
    public event System.Action OnHit;

    // 원본 재질 캐시 (풀 반환 시 복구용)
    private Material[] _originalMats;

    private EliteMarker _eliteMarker;
    private TankShield  _shield;

    // 분리 스로틀 (20회/초 — FixedUpdate 50회/초 대비 O(N²) 부하 60% 감소)
    private float _sepTimer;
    private const float SepInterval = 0.05f;

    // ── 초기화 ──────────────────────────────────────────────────
    private void Awake()
    {
        currentHP    = maxHP;
        baseSpeed    = moveSpeed;
        isMoving     = false;
        rb                = GetComponent<Rigidbody2D>();
        srs               = GetComponentsInChildren<SpriteRenderer>(true);
        _cachedColliders  = GetComponentsInChildren<Collider2D>(true);
        hitFlash          = GetComponentInChildren<HitFlash>(true);
        animator          = GetComponentInChildren<Animator>();
        behaviour         = GetComponent<EnemyBehaviourBase>();
        _eliteMarker      = GetComponent<EliteMarker>();
        _shield           = GetComponent<TankShield>();
        if (_eliteMarker != null) knockbackImmune = true;

        // 디버그: SpriteRenderer 확인
        if (srs == null || srs.Length == 0)
        {
            Debug.LogWarning($"[EnemyController] {gameObject.name}: SpriteRenderer가 없습니다! 프리팹에 SpriteRenderer가 있는지 확인하세요.");
        }
        else
        {
            foreach (var sr in srs)
            {
                if (sr.sprite == null)
                {
                    Debug.LogWarning($"[EnemyController] {gameObject.name}: SpriteRenderer의 sprite가 null입니다. sprite를 할당하세요.");
                }
            }
        }

        // 원본 재질 캐시 (사망 연출 후 복구용)
        if (srs != null)
        {
            _originalMats = new Material[srs.Length];
            for (int i = 0; i < srs.Length; i++)
                _originalMats[i] = srs[i].material;
        }

        if (rb != null)
        {
            rb.bodyType      = RigidbodyType2D.Kinematic;
            rb.sleepMode     = RigidbodySleepMode2D.NeverSleep;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        }

        All.Add(this);
        behaviour?.Init(this);
    }

    private void Start()
    {
        playerTransform = PlayerController.Instance?.transform;
    }

    // ── 업데이트 ────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;
        if (behaviour != null && behaviour.enabled) behaviour.OnAttackUpdate();
        if (isMoving != _lastIsMoving)
        {
            _lastIsMoving = isMoving;
            animator?.SetBool(EnemyAnim.BMove, isMoving);
        }
        CheckPlayerContact();
    }

    /// <summary>
    /// 거리 기반 플레이어 접촉 데미지.
    /// OnTriggerStay2D는 콜라이더 트리거 설정에 따라 발동하지 않을 수 있으므로
    /// 프리팹 설정과 무관하게 작동하는 폴백으로 사용합니다.
    /// </summary>
    private void CheckPlayerContact()
    {
        if (_contactTimer > 0f) { _contactTimer -= Time.deltaTime; return; }
        if (playerTransform == null) return;
        if (((Vector2)transform.position - (Vector2)playerTransform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        var player = PlayerController.Instance;
        if (player == null) return;
        _contactTimer = 0.5f;
        player.TakeDamage(contactDamage, isFromElite: _eliteMarker != null, sourceName: gameObject.name);
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned)
        {
            _pendingMove = Vector2.zero;
            return;
        }

        if (playerTransform == null)
        {
            playerTransform = PlayerController.Instance?.transform;
            return;
        }

        if (!_isKnockedBack && behaviour != null && behaviour.enabled) behaviour.OnMoveFixedUpdate();
        ApplySeparation();

        // 각도 슬롯 갱신 — 목록 첫 번째 적만 담당 (정적 타이머 1회 실행)
        if (All.Count > 0 && All[0] == this && playerTransform != null)
            TryRefreshSlots(playerTransform);

        if (_pendingMove != Vector2.zero && rb != null)
        {
            Vector2 next = rb.position + _pendingMove;
            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
                next = mb.ClosestPointOnBoundary(next);
            rb.MovePosition(next);
        }
        isMoving = _pendingMove != Vector2.zero;
        _pendingMove = Vector2.zero;
    }

    // ── 분리: Physics 쿼리 없이 전체 목록에서 직접 계산 ─────────
    public static readonly List<EnemyController> All = new List<EnemyController>();
    public static int ActiveCount => All.Count;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { All.Clear(); _slotTimer = 0f; }

    private void ApplySeparation()
    {
        if (separationRadius <= 0f || rb == null) return;

        _sepTimer -= Time.fixedDeltaTime;
        if (_sepTimer > 0f) return;
        _sepTimer = SepInterval;

        float   r2   = separationRadius * separationRadius;
        Vector2 push = Vector2.zero;

        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == this || other.rb == null) continue;
            Vector2 diff   = rb.position - other.rb.position;
            float   sqDist = diff.sqrMagnitude;
            if (sqDist > 0.000001f && sqDist < r2)
            {
                float dist = Mathf.Sqrt(sqDist);
                push += (diff / dist) * (1f - dist / separationRadius);
            }
        }

        if (push != Vector2.zero)
            _pendingMove += push * separationStrength * SepInterval;
    }

    // ── 각도 슬롯 갱신 ──────────────────────────────────────────
    private static float _slotTimer;

    private static void TryRefreshSlots(Transform playerTr)
    {
        _slotTimer -= Time.fixedDeltaTime;
        if (_slotTimer > 0f) return;
        _slotTimer = 0.4f;
        RefreshAngularSlots((Vector2)playerTr.position);
    }

    private static void RefreshAngularSlots(Vector2 playerPos)
    {
        int n = All.Count;
        if (n == 0) return;

        var sorted = new (float angle, EnemyController ec)[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = All[i].rb != null ? All[i].rb.position - playerPos : Vector2.right;
            sorted[i] = (Mathf.Atan2(d.y, d.x), All[i]);
        }
        System.Array.Sort(sorted, (a, b) => a.angle.CompareTo(b.angle));

        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            sorted[i].ec.SlotAngle         = i * step;
            sorted[i].ec.HasSlotAssignment = true;
        }
    }

    private void OnDestroy()
    {
        AllRemoveSwapBack(this);
    }

    // O(1) 리스트 제거 — 마지막 요소와 교환 후 RemoveAt
    static void AllRemoveSwapBack(EnemyController e)
    {
        int idx = All.IndexOf(e);
        if (idx < 0) return;
        int last = All.Count - 1;
        All[idx] = All[last];
        All.RemoveAt(last);
    }

    /// <summary>EnemyPool이 풀에서 꺼낼 때 호출 — 렌더러·콜라이더 활성화 + All 등록.</summary>
    public void WakeFromPool()
    {
        StopAllCoroutines();
        _stunCoroutine     = null;
        _freezeCoroutine   = null;
        bleedCoroutine     = null;
        _fireCoroutine     = null;
        _slowCoroutine     = null;
        knockbackCoroutine = null;
        _isKnockedBack     = false;
        _fireTintCoroutine = null;
        _purpleDotCoroutine = null;

        // 이전 풀 사용에서 남은 상태 VFX 제거
        if (_freezeVfxInst != null) { Destroy(_freezeVfxInst); _freezeVfxInst = null; }
        if (_stunVfxInst   != null) { Destroy(_stunVfxInst);   _stunVfxInst   = null; }
        if (_fireVfxInst   != null) { Destroy(_fireVfxInst);   _fireVfxInst   = null; }

        IsPooled           = false;
        enabled            = true;
        isStunned          = false;
        _isDying           = false;
        _contactTimer      = 0f;
        _lastIsMoving      = true;
        _halfHpGoldDropped = false;
        HasSlotAssignment  = false;
        moveSpeed  = baseSpeed;
        if (animator != null) animator.speed = 1f;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.constraints    = RigidbodyConstraints2D.FreezeRotation;
        }
        if (behaviour != null)
        {
            behaviour.StopAllCoroutines();
            behaviour.enabled = true;
            behaviour.Init(this);
        }

        // 재질·색상 복구
        if (srs != null && _originalMats != null)
            for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
                if (_originalMats[i] != null && srs[i] != null) srs[i].material = _originalMats[i];
        SetTint(Color.white);

        foreach (var sr in srs) if (sr != null) sr.enabled = true;
        if (_cachedColliders != null)
            foreach (var col in _cachedColliders) col.enabled = true;

        playerTransform = PlayerController.Instance?.transform;
        GetComponent<SlimeBobAnimation>()?.ResetOrigin();
        _shield?.ResetShield();
        All.Add(this);
    }

    /// <summary>EnemyPool이 풀로 반환하기 전 호출 — 렌더러·콜라이더 비활성화 + All 제거.</summary>
    public void SleepToPool()
    {
        IsPooled = true;
        StopAllCoroutines();
        DG.Tweening.DOTween.Kill(transform); // BounceIn DOTween이 다음 웨이브 재사용 시 충돌하지 않도록 즉시 제거
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // behaviour 코루틴도 중단 (EnemyController.StopAllCoroutines는 다른 MonoBehaviour에 영향 없음)
        if (behaviour != null)
        {
            behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }

        // 재질 복구 (사망 실루엣 해제)
        if (srs != null && _originalMats != null)
            for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
                if (_originalMats[i] != null && srs[i] != null) srs[i].material = _originalMats[i];

        foreach (var sr in srs) if (sr != null) sr.enabled = false;
        if (_cachedColliders != null)
            foreach (var col in _cachedColliders) col.enabled = false;

        AllRemoveSwapBack(this);
    }

    private void LateUpdate()
    {
        var s = transform.localScale;
        s.x = _lastFlipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        transform.localScale = s;
    }

    // ── 이동 헬퍼 (EnemyBehaviourBase에서 호출) ─────────────────
    /// <summary>플레이어 방향으로 speed만큼 이동합니다.</summary>
    public void MoveTowardPlayer(float speed)
    {
        if (playerTransform == null || rb == null) return;
        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        MoveInDirection(toPlayer.normalized, speed);
    }

    /// <summary>지정 방향으로 speed만큼 이동하고 스프라이트를 뒤집습니다.</summary>
    public void MoveInDirection(Vector2 dir, float speed)
    {
        if (rb == null) return;
        _pendingMove += dir.normalized * speed * Time.fixedDeltaTime;
        FlipToward(dir);
    }

    /// <summary>방향 벡터에 따라 좌우 반전 플래그를 갱신합니다. 실제 scale 적용은 LateUpdate.</summary>
    public void FlipToward(Vector2 dir)
    {
        if      (dir.x > 0.1f)  _lastFlipX = false;
        else if (dir.x < -0.1f) _lastFlipX = true;
        // dir.x ≈ 0 → 현재 방향 유지 (무한 플립 방지)
    }


    // ── 충돌 접촉 피해 ──────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (_contactTimer > 0f) return;
        var p = PlayerController.Instance;
        if (p == null || other.gameObject != p.gameObject) return;
        if (((Vector2)transform.position - (Vector2)p.transform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        _contactTimer = 0.5f;
        p.TakeDamage(contactDamage, sourceName: gameObject.name);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_contactTimer > 0f) return;
        var p = PlayerController.Instance;
        if (p == null || other.gameObject != p.gameObject) return;
        if (((Vector2)transform.position - (Vector2)p.transform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        _contactTimer = 0.5f;
        p.TakeDamage(contactDamage, sourceName: gameObject.name);
    }

    public void SetTint(Color color)
    {
        if (srs != null) { foreach(var sr in srs) sr.color = color; }
        if (hitFlash != null) hitFlash.BaseColor = color;
    }

    // ── 상태 효과 ────────────────────────────────────────────────
    /// <summary>지정 시간 동안 이동 불가 상태를 부여합니다.</summary>
    public void Paralyze(float seconds)
    {
        StopCoroutine(nameof(ParalyzeRoutine));
        StartCoroutine(nameof(ParalyzeRoutine), seconds);
    }

    private IEnumerator ParalyzeRoutine(float seconds)
    {
        moveSpeed = 0f;
        yield return new WaitForSeconds(seconds);
        moveSpeed = baseSpeed;
    }

    /// <summary>지정 시간 동안 이동·공격을 모두 멈추고 스프라이트를 노란색으로 변경합니다.</summary>
    public void Stun(float seconds, int weaponChapter = 1, string weaponId = null, string traitId = "HIT_STUN")
    {
        if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutineVfx(seconds, weaponChapter, weaponId, traitId));
    }

    private IEnumerator StunRoutineVfx(float seconds, int weaponChapter, string weaponId, string traitId)
    {
        isStunned = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetTint(new Color(1f, 0.92f, 0f, 1f));
        if (animator != null) animator.speed = 0f;

        if (_stunVfxInst != null) Destroy(_stunVfxInst);
        var cfg = StatusEffectConfig.Shared;
        var pfb = (!string.IsNullOrEmpty(weaponId) ? cfg?.GetHitEffectVfx(weaponId, weaponChapter, traitId) : null)
               ?? cfg?.GetStun(weaponChapter);
        if (pfb != null)
        {
            _stunVfxInst = Instantiate(pfb, transform);
            var sr = GetComponentInChildren<SpriteRenderer>();
            float yOff = sr != null ? sr.bounds.min.y - transform.position.y : 0f;
            _stunVfxInst.transform.localPosition = new Vector3(0f, yOff, 0f);
        }

        yield return new WaitForSeconds(seconds);

        isStunned = false;
        if (animator != null) animator.speed = 1f;
        SetTint(Color.white);
        if (_stunVfxInst != null) { Destroy(_stunVfxInst); _stunVfxInst = null; }
        _stunCoroutine = null;
    }

    private Coroutine _slowCoroutine;

    /// <summary>지정 시간 동안 이동 속도를 factor 배로 줄입니다. (0.4 = 40% 속도)</summary>
    public void ApplySlow(float duration, float factor = 0.4f)
    {
        if (isStunned) return; // 스턴/빙결 중엔 슬로우 적용 불필요
        if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
        _slowCoroutine = StartCoroutine(SlowRoutine(duration, factor));
    }

    private IEnumerator SlowRoutine(float duration, float factor)
    {
        moveSpeed = baseSpeed * factor;
        SetTint(new Color(0.5f, 0.8f, 1f, 1f)); // 연파란 틴트
        yield return new WaitForSeconds(duration);
        if (!isStunned) // 슬로우 끝날 때 스턴 중이면 스턴이 색 관리
        {
            moveSpeed = baseSpeed;
            SetTint(Color.white);
        }
        _slowCoroutine = null;
    }

    public void Freeze(float seconds, float damageOnThaw, Color freezeColor, int weaponChapter = 1)
    {
        if (_freezeCoroutine != null) StopCoroutine(_freezeCoroutine);
        _freezeCoroutine = StartCoroutine(FreezeRoutine(seconds, damageOnThaw, freezeColor, weaponChapter));
    }

    private IEnumerator FreezeRoutine(float seconds, float damageOnThaw, Color freezeColor, int weaponChapter)
    {
        isStunned = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetTint(freezeColor);
        if (animator != null) animator.speed = 0f;

        if (_freezeVfxInst != null) Destroy(_freezeVfxInst);
        var pfb = StatusEffectConfig.Shared?.GetFreeze(weaponChapter);
        if (pfb != null) { _freezeVfxInst = Instantiate(pfb, transform); _freezeVfxInst.transform.localPosition = Vector3.zero; }

        var beh = GetComponent<EnemyBehaviourBase>();
        bool wasOn = beh != null && beh.enabled;
        if (beh) beh.enabled = false;

        if (seconds <= 0) seconds = 1f;
        yield return new WaitForSeconds(seconds);

        isStunned = false;
        if (animator != null) animator.speed = 1f;
        SetTint(Color.white);
        if (beh && wasOn) beh.enabled = true;
        if (_freezeVfxInst != null) { Destroy(_freezeVfxInst); _freezeVfxInst = null; }
        _freezeCoroutine = null;
    }

    /// <summary>지속 시간 동안 DoT 화염 피해를 입힙니다. 적 객체에 VFX 프리팹을 부착합니다.</summary>
    public void ApplyFire(float dotPerTick, float duration, float tickInterval = 0.5f, int weaponChapter = 1)
    {
        if (_fireCoroutine != null) StopCoroutine(_fireCoroutine);
        _fireCoroutine = StartCoroutine(FireRoutine(dotPerTick, duration, tickInterval, weaponChapter));
    }

    private IEnumerator FireRoutine(float dotPerTick, float duration, float tickInterval, int weaponChapter)
    {
        if (_fireVfxInst != null) Destroy(_fireVfxInst);
        var pfb = StatusEffectConfig.Shared?.GetFire(weaponChapter);
        if (pfb != null) { _fireVfxInst = Instantiate(pfb, transform); _fireVfxInst.transform.localPosition = Vector3.zero; }

        SetTint(new Color(1f, 0.5f, 0f, 1f));

        float elapsed = 0f;
        float timer   = 0f;
        while (elapsed < duration && !_isDying)
        {
            elapsed += Time.deltaTime;
            timer   += Time.deltaTime;
            if (timer >= tickInterval)
            {
                timer -= tickInterval;
                TakeDamage(dotPerTick);
            }
            yield return null;
        }

        if (!isStunned) SetTint(Color.white);
        if (_fireVfxInst != null) { Destroy(_fireVfxInst); _fireVfxInst = null; }
        _fireCoroutine = null;
    }

    /// <summary>듀크냥 스킬 — 보라색 실루엣으로 변경하고 지속 피해를 입힙니다.</summary>
    public void ApplyPurpleDot(float dotPerTick, float duration, float tickInterval = 1f)
    {
        if (_purpleDotCoroutine != null)
        {
            // StopCoroutine은 코루틴 내 cleanup 코드를 실행하지 않으므로 먼저 정리
            hitFlash?.ExitPersistentState();
            StopCoroutine(_purpleDotCoroutine);
        }
        _purpleDotCoroutine = StartCoroutine(PurpleDotRoutine(dotPerTick, duration, tickInterval));
    }

    private static readonly Color PurpleDoTColor = new Color(0.6f, 0f, 1f, 1f);

    private IEnumerator PurpleDotRoutine(float dotPerTick, float duration, float tickInterval)
    {
        if (hitFlash != null)
            hitFlash.EnterPersistentStateImmediate(PurpleDoTColor);
        else
        {
            var purpleMat = GetPurpleDotMat();
            if (srs != null && purpleMat != null)
                foreach (var sr in srs) if (sr != null) sr.material = purpleMat;
        }

        float elapsed = 0f;
        float timer   = 0f;
        while (elapsed < duration && !_isDying)
        {
            elapsed += Time.deltaTime;
            timer   += Time.deltaTime;
            if (timer >= tickInterval)
            {
                timer -= tickInterval;
                TakeDamage(dotPerTick);
                if (UnityEngine.Random.value < 0.5f)
                    PlayerController.Instance?.HealHP(dotPerTick);
            }
            yield return null;
        }

        if (hitFlash != null)
            hitFlash.ExitPersistentState();
        else if (srs != null && _originalMats != null)
            for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
                if (_originalMats[i] != null && srs[i] != null) srs[i].material = _originalMats[i];

        _purpleDotCoroutine = null;
    }

    /// <summary>지정 방향으로 순간적으로 밀어냅니다.</summary>
    public void Knockback(Vector2 dir, float force)
    {
        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(dir.normalized, force));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float force)
    {
        _isKnockedBack = true;
        float elapsed  = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            _pendingMove += dir * force * (1f - t) * Time.fixedDeltaTime;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _isKnockedBack     = false;
        knockbackCoroutine = null;
    }

    // ── 피격 / 사망 ──────────────────────────────────────────────

    /// <summary>체력이 최초로 50% 이하가 됐을 때 골드를 드롭합니다.</summary>
    private void CheckHalfHpDrop()
    {
        if (_halfHpGoldDropped || currentHP > maxHP * 0.5f || currentHP <= 0f) return;
        _halfHpGoldDropped = true;
        ItemDropManager.Shared?.SpawnGoldDrop(transform.position, goldDrop);
    }

    /// <summary>출혈 적용 (WeaponPoint에서 호출)</summary>
    public void ApplyBleed(float damage)
    {
        if (damage <= 0f || bleedCoroutine != null) return;
        bleedCoroutine = StartCoroutine(BleedRoutine(damage));
    }

    private Coroutine _fireTintCoroutine;

    /// <summary>
    /// 화염 출혈 적용 — 스프라이트를 빨간색으로 물들이고
    /// duration 동안 tickInterval마다 damage 피해를 입힌 뒤 원래 색으로 복구합니다.
    /// 이미 적용 중이면 타이머를 갱신합니다.
    /// </summary>
    public void ApplyFireBleed(float damage, float duration, float tickInterval)
    {
        if (damage <= 0f) return;
        if (_fireTintCoroutine != null) StopCoroutine(_fireTintCoroutine);
        _fireTintCoroutine = StartCoroutine(FireBleedRoutine(damage, duration, tickInterval));
    }

    private IEnumerator FireBleedRoutine(float damage, float duration, float tickInterval)
    {
        SetTint(new Color(1f, 0.2f, 0.2f, 1f)); // 빨간색

        float elapsed  = 0f;
        var   wait     = new WaitForSeconds(tickInterval);
        while (elapsed < duration)
        {
            yield return wait;
            elapsed += tickInterval;
            if (currentHP <= 0f) break;

            currentHP -= damage;
            CheckHalfHpDrop();
            SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

            Vector3 textPos = srs != null && srs.Length > 0
                ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
                : transform.position;
            FXManager.Shared?.SpawnDamageText(textPos, damage, false, true);

            if (currentHP <= 0f)
            {
                _deathDir = Vector2.up;
                OnDeath?.Invoke();
                StartCoroutine(DieRoutine());
                break;
            }
        }

        SetTint(Color.white);
        _fireTintCoroutine = null;
    }

    private IEnumerator BleedRoutine(float damage)
    {
        float elapsed = 0f;
        while (elapsed < bleedDuration)
        {
            yield return new WaitForSeconds(bleedInterval);
            elapsed += bleedInterval;
            if (currentHP <= 0f) break; // 사망 시 중지

            // 출혈 데미지 적용
            currentHP -= damage;
            CheckHalfHpDrop();
            SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

            // 데미지 텍스트 표시 (빨간색, - 기호)
            Vector3 textPos = srs != null && srs.Length > 0
                ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
                : transform.position;
            FXManager.Shared?.SpawnDamageText(textPos, damage, false, true); // isBleed = true로 가정

            if (currentHP <= 0f)
            {
                _deathDir = Vector2.up;
                OnDeath?.Invoke();
                StartCoroutine(DieRoutine());
            }
        }
        bleedCoroutine = null;
    }

    void IEnemyHost.TakeDamage(float damage, bool isCritical) => TakeDamage(damage, isCritical);

    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        if (_isDying) return;
        if (TestMonsterSpawner.EnemyInvincible) return;
        // 방어막이 있으면 먼저 흡수 — 잔여 피해만 HP에 적용
        if (_shield != null) damage = _shield.TryAbsorbDamage(damage);
        if (damage <= 0f) return;

        currentHP -= damage;
        CheckHalfHpDrop();

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        // 스프라이트 상단 기준으로 스폰 (몸집이 커도 텍스트가 가려지지 않음)
        Vector3 textPos = srs != null && srs.Length > 0
            ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        hitFlash?.Flash();
        OnHit?.Invoke();

        if (!knockbackImmune && knockbackDir != Vector2.zero && knockbackForce > 0f)
            Knockback(knockbackDir, knockbackForce);

        CharacterSkillManager.Shared?.TryAddPointFromHit(transform.position);

        if (currentHP <= 0f)
        {
            _isDying  = true;
            _deathDir = knockbackDir.sqrMagnitude > 0.001f ? knockbackDir.normalized : Vector2.up;
            OnDeath?.Invoke();
            if (_eliteMarker != null)
                ItemDropManager.Shared?.SpawnBossDrops(transform.position, goldDrop, 1, expDrop, 1);
            else
                ItemDropManager.Shared?.SpawnDrops(transform.position, goldDrop, expDrop);
            StartCoroutine(DieRoutine());
        }
    }

    /// <summary>웨이브 종료 시 드롭/포인트 없이 사망 연출만 재생합니다.</summary>
    public void ForceDie()
    {
        if (!gameObject.activeInHierarchy) return;
        _isDying  = true;   // TakeDamage 중복 진입 차단
        _deathDir = new Vector2(Random.Range(-1f, 1f), 1f).normalized;
        StopAllCoroutines();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        SpriteRenderer mainSr = null;
        if (srs != null)
            foreach (var sr in srs)
                if (sr != null && sr.sprite != null) { mainSr = sr; break; }

        var debrisPos = transform.position;
        if (FXManager.Shared != null)
            FXManager.Shared.SpawnDeathDebris(debrisPos, _deathDir);
        else
            SpawnDebrisFallback(debrisPos);

        if (mainSr != null)
        {
            Vector2 flyDir = _deathDir;
            if (PlayerController.Instance != null)
            {
                var toSelf = (Vector2)transform.position - (Vector2)PlayerController.Instance.transform.position;
                if (toSelf.sqrMagnitude > 0.001f) flyDir = toSelf.normalized;
            }
            SpriteSliceFX.Play(mainSr, flyDir, 2, 2);
            foreach (var sr in srs) if (sr != null) sr.enabled = false;
        }

        yield return null;
        Destroy(gameObject);
    }

    // ── 웨이브 스케일링 커브 ─────────────────────────────────────
    // DB_웨이브에서 f_웨이브 > 0인 행을 로드해 동적으로 구성됩니다.
    private static int[]   s_curveWaves;
    private static float[] s_curveHp;
    private static float[] s_curveDmg;
    private static int[]   s_curveTimes;

    private static void EnsureCurveLoaded()
    {
        if (s_curveWaves != null) return;

        var rows = new List<DB_웨이브>();
        DB_웨이브.ForEachEntity(e => { if (e.f_웨이브 > 0) rows.Add(e); });
        rows.Sort((a, b) => a.f_웨이브.CompareTo(b.f_웨이브));

        int n = rows.Count;
        s_curveWaves = new int[n];
        s_curveHp    = new float[n];
        s_curveDmg   = new float[n];
        s_curveTimes = new int[n];
        for (int i = 0; i < n; i++)
        {
            s_curveWaves[i] = rows[i].f_웨이브;
            s_curveHp[i]    = rows[i].f_체력배율;
            s_curveDmg[i]   = rows[i].f_피해배율;
            s_curveTimes[i] = rows[i].f_시간;
        }
    }

    /// <summary>웨이브 번호에 해당하는 기본 시간(초) 반환. 키프레임 이하 중 가장 가까운 값.</summary>
    public static int GetWaveTimeSecs(int wave)
    {
        EnsureCurveLoaded();
        int result = s_curveTimes[0];
        for (int i = 0; i < s_curveWaves.Length; i++)
        {
            if (s_curveWaves[i] <= wave) result = s_curveTimes[i];
            else break;
        }
        return result;
    }

    // wave(스테이지 내 1~N)에서의 배율을 키프레임 사이 선형 보간으로 반환
    private static float EvalScaleCurve(int wave, float[] keys)
    {
        // s_curveWaves는 EnsureCurveLoaded() 호출 후 보장됨
        if (wave <= s_curveWaves[0]) return keys[0];

        int last = s_curveWaves.Length - 1;
        if (wave >= s_curveWaves[last])
        {
            // 마지막 구간 기울기로 외삽
            float slope = (keys[last] - keys[last - 1])
                        / (s_curveWaves[last] - s_curveWaves[last - 1]);
            return keys[last] + slope * (wave - s_curveWaves[last]);
        }

        for (int i = 1; i <= last; i++)
        {
            if (wave <= s_curveWaves[i])
            {
                float t = (float)(wave - s_curveWaves[i - 1])
                        / (s_curveWaves[i] - s_curveWaves[i - 1]);
                return Mathf.Lerp(keys[i - 1], keys[i], t);
            }
        }
        return keys[last];
    }

    // ── 웨이브 구간 스탯 적용 ────────────────────────────────────
    /// <summary>
    /// 스폰 시 EnemySpawner에서 호출. ApplyUnitData() 이후에 호출해야 합니다.
    /// globalWave      = (currentStage-1)*wavesPerStage + currentWave
    /// designGlobalWave = (designStage-1)*wavesPerStage + designWave
    /// </summary>
    public void InitForWave(int globalWave, int designGlobalWave = 0, int wavesPerStage = 30)
    {
        if (ActiveUnitData == null) return;

        int totalDelta = Mathf.Max(0, globalWave - designGlobalWave);
        if (totalDelta == 0) return;

        EnsureCurveLoaded();

        // 스테이지 차이 (스테이지가 다르면 hpGrowthRate 기반 추가 배율 적용)
        int currentStage = (globalWave        - 1) / wavesPerStage + 1;
        int designStage  = (designGlobalWave   - 1) / wavesPerStage + 1;
        int stageDelta   = Mathf.Max(0, currentStage - designStage);

        // 스테이지 내 웨이브 (1 ~ wavesPerStage)
        int curWave = ((globalWave      - 1) % wavesPerStage) + 1;
        int desWave = ((designGlobalWave - 1) % wavesPerStage) + 1;

        // 커브 상대 비율: curve(현재웨이브) / curve(설계웨이브)
        // → W1 적이 W15에 나오면 5.5/1.0 = 5.5x, W10 적이 W15에 나오면 5.5/3.2 = 1.72x
        float hpMult  = Mathf.Max(1f, EvalScaleCurve(curWave, s_curveHp)
                                    / EvalScaleCurve(desWave, s_curveHp));
        float dmgMult = Mathf.Max(1f, EvalScaleCurve(curWave, s_curveDmg)
                                    / EvalScaleCurve(desWave, s_curveDmg));

        // 스테이지 간 추가 배율 (기존 hpGrowthRate 활용)
        if (stageDelta > 0)
        {
            hpMult  *= 1f + ActiveUnitData.hpGrowthRate  * stageDelta;
            dmgMult *= 1f + ActiveUnitData.dmgGrowthRate * stageDelta;
        }

        maxHP         *= hpMult;
        currentHP      = maxHP;
        contactDamage *= dmgMult;
        behaviour?.ApplyWaveScaling(dmgMult);
    }

    // ── DB 데이터 적용 ───────────────────────────────────────────
    public UnitData ActiveUnitData { get; private set; }

    /// <summary>스폰 직후 UnitData 스탯을 적용합니다.</summary>
    public void ApplyUnitData(UnitData data)
    {
        ActiveUnitData = data;
        maxHP              = data.hp;
        currentHP          = maxHP;
        _halfHpGoldDropped = false;
        contactDamage      = data.contactDamage;
        moveSpeed     = data.moveSpeed;
        baseSpeed     = data.moveSpeed;
        goldDrop      = data.goldDrop;
        expDrop       = data.expDrop;

        // RANGED 타입 투사체 데이터 전달
        if (data.projectile != null)
            GetComponent<RangedBehaviour>()?.ApplyProjectileData(data.projectile);
    }

    /// <summary>소환 미니언 스탯을 덮어씁니다 (BossSummonMinion.Init 에서 호출).</summary>
    public void ApplySummonStats(float hp, float contactDmg, float speed)
    {
        maxHP         = hp;
        currentHP     = hp;
        contactDamage = contactDmg;
        moveSpeed     = speed;
        baseSpeed     = speed;
        goldDrop      = 0;
        expDrop       = 0;
    }

    /// <summary>보스 스탯을 적용합니다 (BossController.ApplyBossData 에서 호출).</summary>
    public void ApplyBossStats(int hp, int contactDmg, float speed)
    {
        maxHP         = hp;
        currentHP     = hp;
        contactDamage = contactDmg;
        moveSpeed     = speed;
        baseSpeed     = speed;
        goldDrop      = 0;  // 보스는 BossController에서 별도로 드롭 처리함
        expDrop       = 0;
    }

    // ── 스폰 연출 ────────────────────────────────────────────────
    public Vector3 SpawnScale { get; private set; } = Vector3.one;

    public void PlaySpawnBounce()
    {
        SpawnScale = transform.localScale; // BounceIn이 스케일을 바꾸기 전에 저장
        StartCoroutine(BounceIn());
    }

    private IEnumerator BounceIn()
    {
        enabled = false;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;

        Vector3 groundPos = transform.position;
        Vector2 toPlayer  = playerTransform != null
            ? ((Vector2)playerTransform.position - (Vector2)groundPos).normalized
            : Vector2.up;

        // ── 포물선 점프 ───────────────────────────────────────────
        // startPos: 착지 지점 뒤(플레이어 반대)에서 출발, Y는 지면과 동일
        const float jumpDist   = 0.9f;  // 수평 도약 거리
        const float jumpHeight = 1.3f;  // 호 최고점 높이
        const float jumpDur    = 0.32f;

        Vector3 startPos = groundPos - new Vector3(toPlayer.x * jumpDist, toPlayer.y * jumpDist * 0.4f, 0f);
        transform.position = startPos;

        float elapsed = 0f;
        while (elapsed < jumpDur)
        {
            float t = elapsed / jumpDur;

            // X/Y: 시작→착지 선형 이동 + Y 포물선 호 (4t(1-t) = t=0.5에서 최대)
            float x   = Mathf.Lerp(startPos.x, groundPos.x, t);
            float arc = jumpHeight * 4f * t * (1f - t);
            float y   = Mathf.Lerp(startPos.y, groundPos.y, t) + arc;
            transform.position = new Vector3(x, y, groundPos.z);

            // 스케일: 공중엔 세로 늘어남 → 착지 직전 납작하게 전환
            float squishT = Mathf.Clamp01((t - 0.60f) / 0.40f);
            squishT = squishT * squishT; // ease-in
            Vector3 airScale  = new Vector3(SpawnScale.x * 0.78f, SpawnScale.y * 1.35f, SpawnScale.z);
            Vector3 landScale = new Vector3(SpawnScale.x * 1.38f, SpawnScale.y * 0.52f, SpawnScale.z);
            transform.localScale = Vector3.Lerp(airScale, landScale, squishT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── 착지 반동 ─────────────────────────────────────────────
        transform.position   = groundPos;
        transform.localScale = new Vector3(SpawnScale.x * 1.38f, SpawnScale.y * 0.52f, SpawnScale.z);
        yield return EnemyFX.LandingSquish(transform, SpawnScale).WaitForCompletion();

        transform.position = groundPos;
        if (rb != null)
        {
            rb.position    = new Vector2(groundPos.x, groundPos.y);
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        enabled = true;
    }

    // ── FXManager 없을 때 파편 인라인 스폰 ──────────────────────
    private static void SpawnDebrisFallback(Vector3 pos)
    {
        // 임시 호스트 GO에서 코루틴 실행 (적 오브젝트 파괴 후에도 유지)
        var host = new GameObject("__DebrisHost");
        host.AddComponent<DebrisHostComponent>().Run(pos);
    }

    private class DebrisHostComponent : MonoBehaviour
    {
        public void Run(Vector3 pos) => StartCoroutine(Routine(pos));

        private static Sprite _spr;
        private static Sprite GetSprite()
        {
            if (_spr != null) return _spr;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(160, 160, 160, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            _spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 16f);
            return _spr;
        }

        private IEnumerator Routine(Vector3 pos)
        {
            const int count = 8;
            var gos  = new GameObject[count];
            var srs  = new SpriteRenderer[count];
            var vels = new Vector2[count];
            var spr  = GetSprite();

            for (int i = 0; i < count; i++)
            {
                gos[i] = new GameObject("Debris");
                gos[i].transform.position   = pos;
                gos[i].transform.localScale = Vector3.one * Random.Range(0.06f, 0.18f);
                srs[i] = gos[i].AddComponent<SpriteRenderer>();
                srs[i].sprite       = spr;
                srs[i].color        = new Color(0.55f, 0.55f, 0.55f, 1f);
                srs[i].sortingOrder = 5;
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(2.5f, 6f);
                vels[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

            float duration = 0.5f, elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                for (int i = 0; i < count; i++)
                {
                    if (gos[i] == null) continue;
                    gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                    vels[i] *= 1f - 5f * Time.deltaTime;
                    if (srs[i]) srs[i].color = new Color(0.55f, 0.55f, 0.55f, 1f - t);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (gos[i] != null) Destroy(gos[i]);
            Destroy(gameObject);
        }
    }
}
