using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// 적의 핵심 컴포넌트 — 스탯, HP, 이동 헬퍼, 피격/사망을 담당합니다.
/// 공격 / 이동 세부 로직은 같은 GameObject에 부착된 <see cref="EnemyBehaviourBase"/>
/// 서브클래스(MeleeBehaviour, RangedBehaviour 등)에서 처리합니다.
/// </summary>
public class EnemyController : MonoBehaviour
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
    [SerializeField] private float damageCooldown = 1f;
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

    // ── 런타임 상태 ─────────────────────────────────────────────
    private float          currentHP;
    private float          damageTimer;
    private Transform      playerTransform;
    private Rigidbody2D    rb;
    private SpriteRenderer[] srs;
    private Collider2D[]   _cachedColliders;
    private HitFlash       hitFlash;
    private Animator       animator;
    private EnemyBehaviourBase behaviour;
    private bool           _lastFlipX;  // FlipToward 중복 갱신 방지

    private bool      isStunned;
    private Vector2   _pendingMove;
    private Coroutine knockbackCoroutine;
    private Coroutine bleedCoroutine;
    private float     baseSpeed; // 넉백/스턴 전 원본 속도
    private Vector2   _deathDir;  // 사망 시 날아갈 방향
    private bool      isMoving;

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

    public event System.Action OnDeath;

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

        behaviour?.Init(this);
    }

    private void Start()
    {
        All.Add(this);   // 최초 생성 시 등록 (풀 재사용은 WakeFromPool에서)
        playerTransform = PlayerController.Instance?.transform;
    }

    // ── 업데이트 ────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;
        if (behaviour != null && behaviour.enabled) behaviour.OnAttackUpdate();
        animator?.SetBool("bMove", isMoving);
        CheckPlayerContact();
    }

    /// <summary>
    /// 거리 기반 플레이어 접촉 데미지.
    /// OnTriggerStay2D는 콜라이더 트리거 설정에 따라 발동하지 않을 수 있으므로
    /// 프리팹 설정과 무관하게 작동하는 폴백으로 사용합니다.
    /// </summary>
    private void CheckPlayerContact()
    {
        if (damageTimer > 0f) return;
        if (playerTransform == null) return;
        if (((Vector2)transform.position - (Vector2)playerTransform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        var player = PlayerController.Instance;
        if (player == null) return;
        player.TakeDamage(contactDamage, isFromElite: _eliteMarker != null);
        damageTimer = damageCooldown;
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

        if (behaviour != null && behaviour.enabled) behaviour.OnMoveFixedUpdate();
        ApplySeparation();

        if (_pendingMove != Vector2.zero && rb != null)
            rb.MovePosition(rb.position + _pendingMove);
        isMoving = _pendingMove != Vector2.zero;
        _pendingMove = Vector2.zero;
    }

    // ── 분리: Physics 쿼리 없이 전체 목록에서 직접 계산 ─────────
    public static readonly List<EnemyController> All = new List<EnemyController>();
    public static int ActiveCount => All.Count;

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

    private void OnDestroy()
    {
        AllRemoveSwapBack(this); // 진짜 파괴될 때 안전망
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
        enabled    = true;
        damageTimer = 0f;
        isStunned  = false;
        moveSpeed  = baseSpeed;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 재질 복구
        if (srs != null && _originalMats != null)
            for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
                if (_originalMats[i] != null) srs[i].material = _originalMats[i];

        foreach (var sr in srs)  sr.enabled = true;
        if (_cachedColliders != null)
            foreach (var col in _cachedColliders) col.enabled = true;

        playerTransform = PlayerController.Instance?.transform;
        _shield?.ResetShield();
        All.Add(this);
    }

    /// <summary>EnemyPool이 풀로 반환하기 전 호출 — 렌더러·콜라이더 비활성화 + All 제거.</summary>
    public void SleepToPool()
    {
        StopAllCoroutines();
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 재질 복구 (사망 실루엣 해제)
        if (srs != null && _originalMats != null)
            for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
                if (_originalMats[i] != null) srs[i].material = _originalMats[i];

        foreach (var sr in srs)  sr.enabled = false;
        if (_cachedColliders != null)
            foreach (var col in _cachedColliders) col.enabled = false;

        AllRemoveSwapBack(this);
    }

    private void LateUpdate()
    {
        damageTimer -= Time.deltaTime;

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
        bool newFlipX;
        if      (dir.x > 0.01f)  newFlipX = false;
        else if (dir.x < -0.01f) newFlipX = true;
        else return;

        _lastFlipX = newFlipX;
    }


    // ── 충돌 접촉 피해 ──────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        
        // 플레이어 본체(PlayerController가 달린 오브젝트)가 아닌 자식의 투명 트리거(자석 등) 무시
        if (other.gameObject != p.gameObject) return;
        
        if (((Vector2)transform.position - (Vector2)p.transform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;

        // 플레이어 본체(PlayerController가 달린 오브젝트)가 아닌 자식의 투명 트리거(자석 등) 무시
        if (other.gameObject != p.gameObject) return;
        if (((Vector2)transform.position - (Vector2)p.transform.position).sqrMagnitude > contactMaxDist * contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
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
    public void Stun(float seconds)
    {
        StopCoroutine(nameof(StunRoutine));
        StartCoroutine(nameof(StunRoutine), seconds);
    }

    private IEnumerator StunRoutine(float seconds)
    {
        isStunned = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetTint(new Color(1f, 0.92f, 0f, 1f)); // 노란색

        yield return new WaitForSeconds(seconds);

        isStunned = false;
        SetTint(Color.white);
    }

    public void Freeze(float seconds, float damageOnThaw, Color freezeColor)
    {
        StopCoroutine(nameof(FreezeRoutine));
        StopCoroutine(nameof(StunRoutine));
        StartCoroutine(FreezeRoutine(seconds, damageOnThaw, freezeColor));
    }

    private IEnumerator FreezeRoutine(float seconds, float damageOnThaw, Color freezeColor)
    {
        isStunned = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetTint(freezeColor);

        var beh = GetComponent<EnemyBehaviourBase>();
        bool wasOn = beh != null && beh.enabled;
        if (beh) beh.enabled = false;

        if (seconds <= 0) seconds = 1f; // 최소 1초 빙결
        yield return new WaitForSeconds(seconds);

        isStunned = false;
        SetTint(Color.white);
        if (beh && wasOn) beh.enabled = true;
        // 빙결 해제 시 피해 제거
    }

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
            float t = elapsed / duration;
            // _pendingMove에 누적 → FixedUpdate에서 한 번만 MovePosition 호출
            _pendingMove += dir * force * (1f - t) * Time.fixedDeltaTime;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        knockbackCoroutine = null;
    }

    // ── 피격 / 사망 ──────────────────────────────────────────────
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
            SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

            Vector3 textPos = srs != null && srs.Length > 0
                ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
                : transform.position;
            FXManager.Shared?.SpawnDamageText(textPos, damage, false, true);

            if (currentHP <= 0f)
            {
                _deathDir = Vector2.up;
                OnDeath?.Invoke();
                CharacterSkillManager.Shared?.AddPoint(1);
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
            SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

            // 데미지 텍스트 표시 (빨간색, - 기호)
            Vector3 textPos = srs != null && srs.Length > 0
                ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
                : transform.position;
            FXManager.Shared?.SpawnDamageText(textPos, damage, false, true); // isBleed = true로 가정

            if (currentHP <= 0f)
            {
                _deathDir = Vector2.up; // 출혈 사망 시 위쪽으로
                OnDeath?.Invoke();
                CharacterSkillManager.Shared?.AddPoint(1);
                StartCoroutine(DieRoutine());
            }
        }
        bleedCoroutine = null;
    }

    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        // 방어막이 있으면 먼저 흡수 — 잔여 피해만 HP에 적용
        if (_shield != null) damage = _shield.TryAbsorbDamage(damage);
        if (damage <= 0f) return;

        currentHP -= damage;

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        // 스프라이트 상단 기준으로 스폰 (몸집이 커도 텍스트가 가려지지 않음)
        Vector3 textPos = srs != null && srs.Length > 0
            ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        hitFlash?.Flash();

        if (!knockbackImmune && knockbackDir != Vector2.zero && knockbackForce > 0f)
            Knockback(knockbackDir, knockbackForce);

        if (currentHP <= 0f)
        {
            _deathDir = knockbackDir.sqrMagnitude > 0.001f ? knockbackDir.normalized : Vector2.up;
            OnDeath?.Invoke();
            CharacterSkillManager.Shared?.AddPoint(1);
            if (_eliteMarker != null)
                ItemDropManager.Shared?.SpawnEliteDrops(transform.position, goldDrop, expDrop);
            else
                ItemDropManager.Shared?.SpawnDrops(transform.position, goldDrop, expDrop);
            StartCoroutine(DieRoutine());
        }
    }

    /// <summary>웨이브 종료 시 드롭/포인트 없이 사망 연출만 재생합니다.</summary>
    public void ForceDie()
    {
        if (!gameObject.activeInHierarchy) return;
        _deathDir = new Vector2(Random.Range(-1f, 1f), 1f).normalized;
        StopAllCoroutines();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        // 이동·공격·충돌 데미지 비활성화
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 사망 위치에 파편 스폰 (FXManager 없으면 인라인 실행)
        var debrisPos = transform.position;
        if (FXManager.Shared != null)
            FXManager.Shared.SpawnDeathDebris(debrisPos, _deathDir);
        else
            SpawnDebrisFallback(debrisPos);

        // SpriteSilhouette 쉐이더로 재질 교체 → 흰색 실루엣
        var flashMat = GetDeathFlashMat();
        if (srs != null && flashMat != null)
            foreach(var sr in srs) sr.material = flashMat;

        float duration  = 0.22f;
        float elapsed   = 0f;
        Vector3 initScale = transform.localScale;
        float spinSpeed = 1200f; // deg/s
        float flySpeed  = 14f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            transform.position += (Vector3)(_deathDir * flySpeed * (1f - t) * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 풀이 있으면 반환, 없으면 파괴
        if (EnemyPool.Shared != null) EnemyPool.Shared.Return(this);
        else                          Destroy(gameObject);
    }

    // ── 웨이브 구간 스탯 적용 ────────────────────────────────────
    /// <summary>
    /// 스폰 시 EnemySpawner에서 호출. ApplyUnitData() 이후에 호출해야 합니다.
    /// globalWave      = (currentStage-1)*30 + currentWave
    /// designGlobalWave = (designStage-1)*30 + designWave
    /// </summary>
    public void InitForWave(int globalWave, int designGlobalWave = 0, int wavesPerStage = 30)
    {
        // DB 비율 기반 스케일링 (ApplyUnitData 이후에 호출돼야 ActiveUnitData가 있음)
        if (ActiveUnitData == null) return;

        int totalDelta  = Mathf.Max(0, globalWave - designGlobalWave);
        if (totalDelta == 0) return;

        // 스테이지 단위 차이
        int currentStage = (globalWave        - 1) / wavesPerStage + 1;
        int designStage  = (designGlobalWave   - 1) / wavesPerStage + 1;
        int stageDelta   = Mathf.Max(0, currentStage - designStage);

        // 스테이지 점프 후 남은 웨이브 차이 (같은 스테이지 내 진행)
        int waveOnlyDelta = Mathf.Max(0, totalDelta - stageDelta * wavesPerStage);

        // 최초기준 체력 + 최초기준 * stageRate * stageDelta
        //                  + 최초기준 * (stageRate / wavesPerStage) * waveOnlyDelta
        float hpMult  = 1f + ActiveUnitData.hpGrowthRate  * stageDelta
                           + ActiveUnitData.hpGrowthRate  / wavesPerStage * waveOnlyDelta;
        float dmgMult = 1f + ActiveUnitData.dmgGrowthRate * stageDelta
                           + ActiveUnitData.dmgGrowthRate / wavesPerStage * waveOnlyDelta;

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
        maxHP         = data.hp;
        currentHP     = maxHP;
        contactDamage = data.contactDamage;
        moveSpeed     = data.moveSpeed;
        baseSpeed     = data.moveSpeed;
        goldDrop      = data.goldDrop;
        expDrop       = data.expDrop;

        // RANGED 타입 투사체 데이터 전달
        if (data.projectile != null)
            GetComponent<RangedBehaviour>()?.ApplyProjectileData(data.projectile);
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

        (float scale, float time)[] keys =
        {
            (0.0f, 0.00f), (1.3f, 0.20f), (0.85f, 0.32f), (1.1f, 0.40f), (1.0f, 0.48f),
        };

        for (int i = 0; i < keys.Length - 1; i++)
        {
            float fromScale = keys[i].scale;
            float toScale   = keys[i + 1].scale;
            float duration  = keys[i + 1].time - keys[i].time;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = SpawnScale * Mathf.Lerp(fromScale, toScale, t / duration);
                yield return null;
            }
        }

        transform.localScale = SpawnScale;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
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
