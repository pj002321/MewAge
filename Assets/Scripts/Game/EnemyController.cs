using UnityEngine;
using Mewage.DB;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float moveSpeed    = 2f;
    [SerializeField] private float maxHP        = 30f;
    [SerializeField] private float contactDamage = 10f;
    [SerializeField] private int   scoreValue   = 10;
    [SerializeField] private float damageCooldown = 1f;

    [Header("드롭")]
    [SerializeField] private int goldDrop = 5;
    [SerializeField] private int expDrop  = 10;

    [Header("유닛 타입")]
    [SerializeField] private UnitType unitType = UnitType.근거리;

    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float preferredRange  = 4f;
    [SerializeField] private float shootInterval   = 2f;
    [SerializeField] private float rangedDamage    = 0f;  // 0이면 contactDamage 사용

    [Header("근거리 설정")]
    [SerializeField] private float      attackRange    = 1.2f;
    [SerializeField] private float      attackInterval = 1.0f;
    [SerializeField] private GameObject attackVfxPrefab;

    [Header("돌진 설정")]
    [SerializeField] private float chargeInterval        = 3f;
    [SerializeField] private float chargeDuration        = 0.6f;
    [SerializeField] private float chargeSpeedMultiplier = 3.5f;

    // ── 런타임 상태 ─────────────────────────────────────────────
    private float     currentHP;
    private float     damageTimer;
    private Transform playerTransform;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private HitFlash  hitFlash;
    private Animator  animator;

    private float   attackTimer;
    private float   shootTimer;
    private float   chargeTimer;
    private bool    isCharging;
    private float   chargeRemaining;
    private Vector2 chargeDir;

    // ── 초기화 ───────────────────────────────────────────────────
    private void Awake()
    {
        currentHP = maxHP;
        rb        = GetComponent<Rigidbody2D>();
        sr        = GetComponent<SpriteRenderer>();
        hitFlash  = GetComponent<HitFlash>();
        animator  = GetComponent<Animator>();

        if (rb != null)
        {
            rb.bodyType      = RigidbodyType2D.Kinematic;
            rb.sleepMode     = RigidbodySleepMode2D.NeverSleep;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        }

        chargeTimer = chargeInterval;
        attackTimer = attackInterval;
    }

    private void Start()
    {
        playerTransform = PlayerController.Instance?.transform;
    }

    /// <summary>EnemySpawner가 DB 데이터로 초기화할 때 호출합니다.</summary>
    public void InitFromDB(DB_유닛 unit, int wave)
    {
        unitType       = unit.f_타입;
        maxHP          = unit.f_체력;
        currentHP      = maxHP;
        contactDamage  = unit.f_데미지;
        expDrop        = unit.f_경험치;
        goldDrop       = unit.f_골드;
        preferredRange = unit.f_공격범위;
        shootInterval  = unit.f_쿨타임 > 0f ? unit.f_쿨타임 : shootInterval;
        rangedDamage   = unit.f_원거리데미지;

        if (unit.f_투사체프리펩 != null)
            projectilePrefab = unit.f_투사체프리펩;

        ApplyWaveScaling(wave);
        PlaySpawnBounce();
    }

    // ── 매 프레임 ────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        switch (unitType)
        {
            case UnitType.근거리: UpdateMeleeAttack(); break;
            case UnitType.원거리: UpdateShootTimer();  break;
            case UnitType.돌진:   UpdateChargeTimer(); break;
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        if (playerTransform == null)
        {
            playerTransform = PlayerController.Instance?.transform;
            return;
        }

        switch (unitType)
        {
            case UnitType.근거리: MoveTowardPlayer(moveSpeed); break;
            case UnitType.원거리: UpdateRangedMove();          break;
            case UnitType.돌진:   UpdateChargeMove();          break;
        }
    }

    private void LateUpdate()
    {
        damageTimer -= Time.deltaTime;
    }

    // ── 근거리 ───────────────────────────────────────────────────
    private void MoveTowardPlayer(float speed)
    {
        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer / Mathf.Max(dist, 0.001f);

        if (dist > attackRange)
            rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);

        FlipSprite(dir);
    }

    private void UpdateMeleeAttack()
    {
        if (playerTransform == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= attackRange)
        {
            attackTimer = attackInterval;
            animator?.SetTrigger("tAttack");
            SpawnAttackVfx();
            PlayerController.Instance?.TakeDamage(contactDamage);
        }
        else
        {
            // 사정거리 밖이면 타이머를 리셋해서 진입 즉시 공격하지 않고 인터벌 후 공격
            attackTimer = attackInterval;
        }
    }

    // ── 원거리 ───────────────────────────────────────────────────
    private void UpdateRangedMove()
    {
        float   dist     = Vector2.Distance(rb.position, playerTransform.position);
        Vector2 toPlayer = ((Vector2)playerTransform.position - rb.position).normalized;

        Vector2 moveDir;
        if (dist < preferredRange * 0.75f)
            moveDir = -toPlayer;              // 너무 가까우면 후퇴
        else if (dist > preferredRange * 1.3f)
            moveDir = toPlayer;               // 너무 멀면 접근
        else
            moveDir = Vector2.zero;           // 적정 거리 유지

        if (moveDir != Vector2.zero)
            rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);

        FlipSprite(toPlayer);
    }

    private void UpdateShootTimer()
    {
        if (playerTransform == null) return;
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            shootTimer = shootInterval;
            ShootProjectile();
        }
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null) return;
        animator?.SetTrigger("tAttack");
        SpawnAttackVfx();
        Vector2 dir = ((Vector2)playerTransform.position - rb.position).normalized;
        GameObject proj = Instantiate(projectilePrefab, rb.position, Quaternion.identity);
        if (proj.TryGetComponent<Projectile>(out var p))
            p.Init(rangedDamage > 0f ? rangedDamage : contactDamage, dir);
    }

    // ── 돌진 ─────────────────────────────────────────────────────
    private void UpdateChargeMove()
    {
        if (isCharging)
        {
            rb.MovePosition(rb.position + chargeDir * moveSpeed * chargeSpeedMultiplier * Time.fixedDeltaTime);
            FlipSprite(chargeDir);

            chargeRemaining -= Time.fixedDeltaTime;
            if (chargeRemaining <= 0f)
                isCharging = false;
        }
        else
        {
            // 돌진 유형은 멈추지 않고 플레이어 방향으로 계속 접근
            Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
            Vector2 dir = toPlayer.normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
            FlipSprite(dir);
        }
    }

    private void UpdateChargeTimer()
    {
        if (isCharging || playerTransform == null) return;
        chargeTimer -= Time.deltaTime;
        if (chargeTimer <= 0f)
        {
            chargeTimer     = chargeInterval;
            chargeDir       = ((Vector2)playerTransform.position - rb.position).normalized;
            chargeRemaining = chargeDuration;
            isCharging      = true;
        }
    }

    // ── 충돌 데미지 ──────────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p != null)
        {
            p.TakeDamage(contactDamage);
            damageTimer = damageCooldown;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p != null)
        {
            p.TakeDamage(contactDamage);
            damageTimer = damageCooldown;
        }
    }

    // ── 피격 / 사망 ──────────────────────────────────────────────
    public void Paralyze(float seconds)
    {
        StopCoroutine(nameof(ParalyzeRoutine));
        StartCoroutine(nameof(ParalyzeRoutine), seconds);
    }

    private System.Collections.IEnumerator ParalyzeRoutine(float seconds)
    {
        float saved = moveSpeed;
        moveSpeed = 0f;
        yield return new WaitForSeconds(seconds);
        moveSpeed = saved;
    }

    public void TakeDamage(float damage)
    {
        float actualDamage = Mathf.Min(damage, currentHP);
        currentHP -= damage;
        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        FXManager.Shared?.SpawnDamageText(transform.position, actualDamage);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        hitFlash?.Flash();
        if (currentHP <= 0f)
        {
            ItemDropManager.Shared?.SpawnDrops(transform.position, goldDrop, expDrop);
            Destroy(gameObject);
        }
    }

    // ── 웨이브 스케일링 & 스폰 ───────────────────────────────────
    public void ApplyWaveScaling(int wave)
    {
        float m = 1f + (wave - 1) * 0.2f;
        moveSpeed     *= m;
        maxHP         *= m;
        currentHP      = maxHP;
        contactDamage *= m;
        if (rangedDamage > 0f) rangedDamage *= m;
        scoreValue     = Mathf.RoundToInt(scoreValue * m);
    }

    public void PlaySpawnBounce()
    {
        StartCoroutine(BounceIn());
    }

    private System.Collections.IEnumerator BounceIn()
    {
        enabled = false;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;

        (float scale, float time)[] keys =
        {
            (0.0f, 0.00f),
            (1.3f, 0.20f),
            (0.85f, 0.32f),
            (1.1f, 0.40f),
            (1.0f, 0.48f),
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
                float s = Mathf.Lerp(fromScale, toScale, t / duration);
                transform.localScale = Vector3.one * s;
                yield return null;
            }
        }

        transform.localScale = Vector3.one;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        enabled = true;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    private void SpawnAttackVfx()
    {
        if (attackVfxPrefab == null) return;
        Instantiate(attackVfxPrefab, transform.position, Quaternion.identity);
    }

    private void FlipSprite(Vector2 dir)
    {
        if (sr == null) return;
        if (dir.x > 0.01f)       sr.flipX = false;
        else if (dir.x < -0.01f) sr.flipX = true;
    }
}
