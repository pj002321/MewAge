using System.Collections;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 적의 핵심 컴포넌트 — 스탯, HP, 이동 헬퍼, 피격/사망을 담당합니다.
/// 공격 / 이동 세부 로직은 같은 GameObject에 부착된 <see cref="EnemyBehaviourBase"/>
/// 서브클래스(MeleeBehaviour, RangedBehaviour 등)에서 처리합니다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    // ── 인스펙터 ────────────────────────────────────────────────
    [Header("기본 스탯")]
    [SerializeField] private float moveSpeed      = 2f;
    [SerializeField] private float maxHP          = 30f;
    [SerializeField] private float contactDamage  = 10f;
    [SerializeField] private int   scoreValue     = 10;
    [SerializeField] private float damageCooldown = 1f;
    [Tooltip("OnTrigger 접촉 데미지 최대 거리 — 콜라이더가 스프라이트보다 클 때 보정.")]
    [SerializeField] private float contactMaxDist = 0.8f;

    [Header("드롭")]
    [SerializeField] private int goldDrop = 5;
    [SerializeField] private int expDrop  = 10;

    // ── 공개 프로퍼티 (EnemyBehaviourBase에서 사용) ────────────
    public float      MoveSpeed       => moveSpeed;
    public float      ContactDamage   => contactDamage;
    public Transform  PlayerTransform => playerTransform;
    public Rigidbody2D Rb             => rb;
    public Animator   EnemyAnimator   => animator;
    public float      HPRatio         => maxHP > 0f ? currentHP / maxHP : 0f;

    // ── 런타임 상태 ─────────────────────────────────────────────
    private float          currentHP;
    private float          damageTimer;
    private Transform      playerTransform;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private HitFlash       hitFlash;
    private Animator       animator;
    private EnemyBehaviourBase behaviour;

    private bool isStunned;

    public event System.Action OnDeath;

    // ── 초기화 ──────────────────────────────────────────────────
    private void Awake()
    {
        currentHP = maxHP;
        rb        = GetComponent<Rigidbody2D>();
        sr        = GetComponent<SpriteRenderer>();
        hitFlash  = GetComponent<HitFlash>();
        animator  = GetComponent<Animator>();
        behaviour = GetComponent<EnemyBehaviourBase>();

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
        playerTransform = PlayerController.Instance?.transform;
    }

    /// <summary>EnemySpawner가 DB 데이터로 초기화할 때 호출합니다.</summary>
    public void InitFromDB(DB_유닛 unit, int wave)
    {
        maxHP         = unit.f_체력;
        currentHP     = maxHP;
        contactDamage = unit.f_데미지;
        expDrop       = unit.f_경험치;
        goldDrop      = unit.f_골드;
        if (unit.f_이동속도 > 0f) moveSpeed = unit.f_이동속도;

        behaviour?.ApplyDBData(unit);
        ApplyWaveScaling(wave);
        PlaySpawnBounce();
    }

    // ── 업데이트 ────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;
        behaviour?.OnAttackUpdate();
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;

        if (playerTransform == null)
        {
            playerTransform = PlayerController.Instance?.transform;
            return;
        }

        behaviour?.OnMoveFixedUpdate();
    }

    private void LateUpdate()
    {
        damageTimer -= Time.deltaTime;
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
        rb.MovePosition(rb.position + dir.normalized * speed * Time.fixedDeltaTime);
        FlipToward(dir);
    }

    /// <summary>방향 벡터에 따라 스프라이트를 좌우 반전합니다.</summary>
    public void FlipToward(Vector2 dir)
    {
        if (sr == null) return;
        if (dir.x > 0.01f)       sr.flipX = false;
        else if (dir.x < -0.01f) sr.flipX = true;
    }

    // ── 충돌 접촉 피해 ──────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (Vector2.Distance(transform.position, p.transform.position) > contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (Vector2.Distance(transform.position, p.transform.position) > contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
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
        float saved = moveSpeed;
        moveSpeed = 0f;
        yield return new WaitForSeconds(seconds);
        moveSpeed = saved;
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
        Color originalColor = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = new Color(1f, 0.92f, 0f, 1f); // 노란색

        yield return new WaitForSeconds(seconds);

        isStunned = false;
        if (sr != null) sr.color = originalColor;
    }

    /// <summary>지정 방향으로 순간적으로 밀어냅니다.</summary>
    public void Knockback(Vector2 dir, float force)
    {
        StartCoroutine(KnockbackRoutine(dir.normalized, force));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float force)
    {
        float savedSpeed = moveSpeed;
        moveSpeed = 0f;

        float elapsed  = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            rb?.MovePosition(rb.position + dir * force * (1f - t) * Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        moveSpeed = savedSpeed;
    }

    // ── 피격 / 사망 ──────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        float actual = Mathf.Min(damage, currentHP);
        currentHP -= damage;

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        // 스프라이트 상단 기준으로 스폰 (몸집이 커도 텍스트가 가려지지 않음)
        Vector3 textPos = sr != null
            ? new Vector3(transform.position.x, sr.bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, actual);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        hitFlash?.Flash();

        if (currentHP <= 0f)
        {
            OnDeath?.Invoke();
            ItemDropManager.Shared?.SpawnDrops(transform.position, goldDrop, expDrop);
            StartCoroutine(DieRoutine());
        }
    }

    private IEnumerator DieRoutine()
    {
        // 이동·공격·충돌 데미지 비활성화
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        float duration   = 0.35f;
        float elapsed    = 0f;
        Vector3 initScale = transform.localScale;
        float   spinSpeed = 520f; // deg/s

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale    = Vector3.Lerp(initScale, Vector3.zero, t * t);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── 웨이브 스케일링 ──────────────────────────────────────────
    public void ApplyWaveScaling(int wave)
    {
        float m = 1f + (wave - 1) * 0.2f;
        moveSpeed     *= m;
        maxHP         *= m;
        currentHP      = maxHP;
        contactDamage *= m;
        scoreValue     = Mathf.RoundToInt(scoreValue * m);

        behaviour?.ApplyWaveScaling(m);
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
                transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t / duration);
                yield return null;
            }
        }

        transform.localScale = Vector3.one;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        enabled = true;
    }
}
