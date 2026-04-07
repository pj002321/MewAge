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

    // ── 아웃라인 ──────────────────────────────────────────────────
    [Header("아웃라인")]
    [SerializeField] private string outlineColorProperty = "_OutlineColorBase";
    [SerializeField] private Color  normalOutlineColor   = Color.white;
    [SerializeField] private Color  enrageOutlineColor   = Color.red;

    // ── HitFlash ──────────────────────────────────────────────────
    [Header("피격 플래시")]
    [SerializeField] private Color flashColor    = new(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float flashDuration = 0.12f;

    // ── FX ────────────────────────────────────────────────────────
    [Header("이펙트")]
    [SerializeField] private EnemyEffectEntry onHitFX   = new();
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

    public void ApplyBleed(float damage) { } // Placeholder for compatibility

    // ── 내부 상태 ──────────────────────────────────────────────────
    private float            currentHP;
    private float            damageTimer;
    private SpriteRenderer[] srs;
    private Coroutine        flashCoroutine;
    private bool             isDead;

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
        SetOutlineColor(normalOutlineColor);
        GameManager.Shared?.StartBossWave();
        BossHUDPanel.Shared?.Show(this);

        StartCoroutine(PatternRoutine());
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

            // 3) 스킬 실행
            var skill = RandomReadySkill();
            if (skill != null)
            {
                yield return skill.Execute();
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

            if (PlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerTransform.position);
                if (dist > chaseStopDistance)
                {
                    MoveTowardPlayer(MoveSpeed);
                }
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isDead) return;

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
        if (dir.x > 0.01f)       foreach (var sr in srs) sr.flipX = false;
        else if (dir.x < -0.01f) foreach (var sr in srs) sr.flipX = true;
    }

    // ── 피격 / 사망 ─────────────────────────────────────────────────

    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        if (isDead) return;

        currentHP -= damage;

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);

        Vector3 textPos = srs != null && srs.Length > 0
            ? new Vector3(transform.position.x, srs[0].bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        FXManager.Shared?.SpawnHitEffect(transform.position);

        PlayFX(onHitFX);
        HitFlash();

        if (currentHP <= 0f)
            StartCoroutine(DieRoutine());
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
        SetOutlineColor(enrageOutlineColor);
        OnEnraged?.Invoke();
    }

    // ── 스킬 선택 ─────────────────────────────────────────────────

    public BossSkillBase RandomReadySkill()
    {
        if (skills == null || skills.Length == 0) return null;

        float dist = PlayerTransform != null
            ? Vector2.Distance(transform.position, PlayerTransform.position)
            : 0f;

        var ready = new List<BossSkillBase>();
        foreach (var s in skills)
            if (s.CanUse(dist)) ready.Add(s);

        if (ready.Count == 0) return null;
        return ready[Random.Range(0, ready.Count)];
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

    // ── 아웃라인 ─────────────────────────────────────────────────

    private void SetOutlineColor(Color color)
    {
        if (srs == null) return;
        foreach (var sr in srs)
            if (sr != null && sr.material != null)
                sr.material.SetColor(outlineColorProperty, color);
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

        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (damageTimer > 0f) return;
        var p = collision.collider.GetComponentInParent<PlayerController>();
        if (p == null) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }
}
