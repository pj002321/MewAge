using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 전용 올인원 컴포넌트.
/// EnemyController · HitFlash · EnemyFXConfig · BossBehaviour 기능을 모두 포함합니다.
/// 프리팹 구성: BossController + 스킬 컴포넌트(들) 만으로 동작합니다.
/// </summary>
public class BossController : MonoBehaviour
{
    public static BossController Current { get; private set; }

    // ── 기본 스탯 (DB에서 로드) ───────────────────────────────────
    [SerializeField] private float damageCooldown = 0.5f;
    private float maxHP         = 500f;
    private float contactDamage = 10f;
    private float moveSpeed     = 3f;

    // ── 분노 ──────────────────────────────────────────────────────
    [Header("분노")]
    [SerializeField] private float enrageThreshold  = 0.5f;
    [SerializeField] private float enrageSpeedMult  = 1.5f;
    [SerializeField] private float enrageDamageMult = 1.5f;

    // ── HitFlash ──────────────────────────────────────────────────
    [Header("피격 플래시")]
    [SerializeField] private Color flashColor    = new(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float flashDuration = 0.12f;

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

    // 스킬에서 필요한 공통 참조
    public Transform   PlayerTransform { get; private set; }
    public Rigidbody2D Rb              { get; private set; }
    public Animator    EnemyAnimator   { get; private set; }

    public event System.Action OnEnraged;
    public event System.Action OnDeath;
    public event System.Action<float> OnSkillGaugeChanged;

    public float SkillGauge { get; private set; }
    public float MaxSkillGauge => 100f;

    public void ApplyBleed(float damage) { } // Placeholder for compatibility

    // ── 입장 낙하 연출 ────────────────────────────────────────────────
    [Header("입장 낙하 연출")]
    [SerializeField] private bool  entryDrop       = true;
    [SerializeField] private float dropSpeed       = 22f;   // 낙하 속도 (units/s)
    [SerializeField] private float squashDuration  = 0.08f; // 찌부 시간
    [SerializeField] private float stretchDuration = 0.10f; // 튀어오름 시간
    [SerializeField] private float settleDuration  = 0.12f; // 정착 시간

    // ── 내부 상태 ──────────────────────────────────────────────────
    private float            currentHP;
    private float            damageTimer;
    private SpriteRenderer[] srs;
    private Coroutine        flashCoroutine;
    private Coroutine        knockbackCoroutine;
    private bool             isDead;
    private bool             _dropping;
    private bool             _skillExecuting;
    private bool             _lastFlipX;  // FlipToward 중복 갱신 방지

    private readonly List<BossSkillBase> _readySkills = new();

    public bool IsDropping    => _dropping;
    /// <summary>투사체 발사 페이즈 중임을 표시 — ChaseRoutine이 이동을 중단합니다.</summary>
    public bool IsFiringSkill { get; set; }

    // ── Unity 생명주기 ─────────────────────────────────────────────

    private void Awake()
    {
        Current      = this;
        Rb           = GetComponent<Rigidbody2D>();
        EnemyAnimator = GetComponentInChildren<Animator>();
        srs          = GetComponentsInChildren<SpriteRenderer>(true);
        
        if (skills == null || skills.Length == 0)
            skills = GetComponents<BossSkillBase>();

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
    }

    private void Start()
    {
        PlayerTransform = PlayerController.Instance?.transform;
        GameManager.Shared?.StartBossWave();
        BossHUDPanel.Shared?.Show(this);

        if (entryDrop)
            StartCoroutine(EntryDropRoutine());
        else
            StartCoroutine(PatternRoutine());
    }

    // ── 입장 낙하 연출 ─────────────────────────────────────────────
    private IEnumerator EntryDropRoutine()
    {
        _dropping = true;

        // 원본 스케일 저장
        Vector3 originalScale = transform.localScale;

        // 착지 목표 = 현재(스폰) 위치
        Vector3 landPos = transform.position;

        // 화면 상단 위로 이동 (카메라 없으면 +12 오프셋)
        var cam = Camera.main;
        float topY = cam != null
            ? cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, cam.nearClipPlane)).y + 2f
            : landPos.y + 12f;
        transform.position = new Vector3(landPos.x, topY, landPos.z);

        // 낙하
        while (transform.position.y > landPos.y)
        {
            float step = dropSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, landPos, step);
            yield return null;
        }
        transform.position = landPos;

        // Y만 찌부 — X/Z 원본 유지
        yield return StartCoroutine(ScaleYTo(originalScale, 0.55f, squashDuration));
        yield return StartCoroutine(ScaleYTo(originalScale, 1.3f,  stretchDuration));
        yield return StartCoroutine(ScaleYTo(originalScale, 1f,    settleDuration));
        FXManager.Shared?.SpawnLandingVFX(transform.position);

        _dropping = false;
        StartCoroutine(PatternRoutine());
    }

    /// <summary>X/Z는 originalScale 그대로, Y만 targetYMult(배율) 로 보간합니다.</summary>
    private IEnumerator ScaleYTo(Vector3 originalScale, float targetYMult, float duration)
    {
        Vector3 startScale  = transform.localScale;
        Vector3 targetScale = new Vector3(originalScale.x, originalScale.y * targetYMult, originalScale.z);
        float   elapsed     = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }
        transform.localScale = targetScale;
    }

    // ── 패턴 루프 ──────────────────────────────────────────────────

    private IEnumerator PatternRoutine()
    {
        while (!isDead)
        {
            // 1) 대기
            yield return new WaitForSeconds(IdleTime());
            if (isDead) break;

            // 2) 추격
            yield return ChaseRoutine();
            if (isDead) break;

            // 3) 스킬 실행 (executeInPattern=false 인 자율 스킬 제외, 중복 실행 방지)
            if (!_skillExecuting)
            {
                var skill = RandomReadySkill();
                if (skill != null)
                {
                    _skillExecuting = true;
                    yield return skill.Execute();
                    _skillExecuting = false;
                }
            }
        }
    }

    private float IdleTime() => IsEnraged ? idleDuration * 0.6f : idleDuration;

    private IEnumerator ChaseRoutine()
    {
        float elapsed = 0f;
        while (elapsed < chaseDuration)
        {
            elapsed += Time.fixedDeltaTime;

            if (!IsFiringSkill && PlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerTransform.position);
                if (dist > chaseStopDistance)
                    MoveTowardPlayer(MoveSpeed); // 내부에서 bMove=true
                else
                    EnemyAnimator?.SetBool("bMove", false);
            }
            else
            {
                EnemyAnimator?.SetBool("bMove", false);
            }

            yield return new WaitForFixedUpdate();
        }
        EnemyAnimator?.SetBool("bMove", false);
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

        // 스킬 쿨다운 틱
        foreach (var s in skills)
            s.TickCooldown(Time.deltaTime);
    }

    // ── 이동 API (스킬에서 사용) ────────────────────────────────────

    public void MoveTowardPlayer(float speed)
    {
        if (PlayerTransform == null || Rb == null) return;
        Vector2 dir = ((Vector2)PlayerTransform.position - Rb.position).normalized;
        MoveInDirection(dir, speed);
    }

    public void MoveInDirection(Vector2 dir, float speed)
    {
        EnemyAnimator?.SetBool("bMove", true);
        Vector2 delta = dir.normalized * speed * Time.fixedDeltaTime;
        if (Rb != null)
            Rb.MovePosition(Rb.position + delta);
        else
            transform.position += (Vector3)delta;
        FlipToward(dir);
    }

    public void FlipToward(Vector2 dir)
    {
        if (srs == null) return;
        bool newFlipX;
        if      (dir.x > 0.01f)  newFlipX = false;
        else if (dir.x < -0.01f) newFlipX = true;
        else return;

        if (newFlipX == _lastFlipX) return;
        _lastFlipX = newFlipX;
        foreach (var sr in srs) sr.flipX = newFlipX;
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

        HitFlash();

        if (currentHP <= 0f)
            StartCoroutine(DieRoutine());
    }

    private void AddSkillGauge(float damage)
    {
        if (isDead) return;
        SkillGauge = Mathf.Min(SkillGauge + damage, MaxSkillGauge);
        OnSkillGaugeChanged?.Invoke(SkillGauge);

        // 게이지 100% 시 즉시 스킬 발동
        if (SkillGauge >= MaxSkillGauge)
        {
            StartCoroutine(TriggerSkillFromGauge());
        }
    }

    private IEnumerator TriggerSkillFromGauge()
    {
        SkillGauge = 0f;
        OnSkillGaugeChanged?.Invoke(0f);

        // 이미 스킬 실행 중이거나 사망/낙하 중이면 취소
        if (isDead || _dropping || _skillExecuting) yield break;

        var skill = RandomReadySkill();
        if (skill != null)
        {
            _skillExecuting = true;
            yield return skill.Execute();
            _skillExecuting = false;
        }
    }

    private IEnumerator DieRoutine()
    {
        isDead = true;
        OnDeath?.Invoke();

        PlayFX(onDeathFX);
        ItemDropManager.Shared?.SpawnBossDrops(transform.position, bossGoldCount, 1, bossExpCount, 1);
        BossHUDPanel.Shared?.Hide();
        GameManager.Shared?.OnBossDefeated();

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

    // ── HitFlash ─────────────────────────────────────────────────

    private void HitFlash()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (srs == null) yield break;
        foreach (var sr in srs) sr.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        foreach (var sr in srs) sr.color = Color.white;
    }

    // ── 분노 ─────────────────────────────────────────────────────

    private void TriggerEnrage()
    {
        IsEnraged = true;
        OnEnraged?.Invoke();
    }

    // ── 스킬 선택 ─────────────────────────────────────────────────

    public BossSkillBase RandomReadySkill()
    {
        if (skills == null || skills.Length == 0) return null;

        float dist = PlayerTransform != null
            ? Vector2.Distance(transform.position, PlayerTransform.position)
            : 0f;

        _readySkills.Clear();
        foreach (var s in skills)
            if (s.executeInPattern && s.CanUse(dist)) _readySkills.Add(s);

        if (_readySkills.Count == 0) return null;
        return _readySkills[Random.Range(0, _readySkills.Count)];
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
        p.TakeDamage(contactDamage);
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
