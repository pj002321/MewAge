using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VirtualJoystick으로부터 방향을 받아 캐릭터를 이동합니다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Stats")]
    [SerializeField] private float  maxHP = 100f;
    [SerializeField] private float  currentHP;

    [Header("증강 스탯")]
    [SerializeField] private float attack  = 0f;  // 공격력 보너스
    [SerializeField] private float defense = 0f;  // 방어력 (받는 피해 감소)
    [SerializeField, Range(0f, 0.9f)] private float dodge = 0f;  // 회피 확률 0~0.9

    [Header("피격 무적 시간")]
    [SerializeField] private float hitInvincibleDuration = 0.3f;  // 피격 후 무적 시간(초)

    // 무기 타입별 보너스 (런타임 전용)
    private float rangedAttackBonus = 0f;
    private float meleeAttackBonus  = 0f;
    private float waveHealAmount    = 0f;
    private Coroutine knockbackRoutine;

    public float Attack            => attack;
    public float Defense           => defense;
    public float Dodge             => dodge;
    public float RangedAttackBonus => rangedAttackBonus;
    public float MeleeAttackBonus  => meleeAttackBonus;
    public float WaveHealAmount    => waveHealAmount;

    [Header("플레이어 위 HP 슬라이더 (World Space)")]
    [SerializeField] private Slider playerHpSlider;

    private Vector2        moveDirection;
    private Rigidbody2D    rb;
    private SpriteRenderer spriteRenderer;
    private HitFlash       hitFlash;
    private HitShake       hitShake;
    private bool           isInvincible;

    private void Awake()
    {
        Instance      = this;
        rb            = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitFlash      = GetComponent<HitFlash>();
        hitShake      = GetComponent<HitShake>();
        currentHP     = maxHP;
    }

    private void Start()
    {
        SyncHPUI();
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        HandleInput();
        FlipSprite();
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desired = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
        desired = ClampToBounds(desired);

        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(desired);
    }

    private Vector2 ClampToBounds(Vector2 desired)
    {
        var bounds = MovementBounds.Shared;
        if (bounds == null) return desired;
        if (bounds.IsInside(desired)) return desired;
        return bounds.ClosestPointOnBoundary(desired);
    }

    private void HandleInput()
    {
        moveDirection = VirtualJoystick.Instance != null
            ? VirtualJoystick.Instance.Direction
            : Vector2.zero;
    }

    private void FlipSprite()
    {
        if (spriteRenderer == null) return;
        if (moveDirection.x > 0.05f) spriteRenderer.flipX = false;
        else if (moveDirection.x < -0.05f) spriteRenderer.flipX = true;
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible) return;
        if (dodge > 0f && Random.value < dodge) return;

        float reduced = Mathf.Max(0f, damage - defense);
        currentHP -= reduced;
        currentHP  = Mathf.Max(0f, currentHP);
        SyncHPUI();
        SoundManager.Shared?.Play(SoundKey.Player_Hit);
        hitFlash?.Flash();
        hitShake?.Shake();
        CameraFollow.Shared?.Shake();

        // 피격 후 짧은 무적 (여러 적에 동시에 맞는 것 방지)
        if (hitInvincibleDuration > 0f)
            StartCoroutine(HitInvincibleRoutine(hitInvincibleDuration));

        if (currentHP <= 0f)
            GameManager.Shared?.TriggerGameOver();
    }

    private void SyncHPUI()
    {
        float norm = maxHP > 0f ? currentHP / maxHP : 0f;
        GameManager.Shared?.UpdateHP(currentHP, maxHP);
        if (playerHpSlider) playerHpSlider.value = norm;
    }

    // ────────────────────────────────────────────────────────────────
    // 증강 메서드 (ImprovManager에서 호출)
    // ────────────────────────────────────────────────────────────────
    public void HealHP(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        SyncHPUI();
    }

    public void IncreaseMaxHP(float amount)
    {
        maxHP     += amount;
        currentHP  = Mathf.Min(currentHP + amount, maxHP);
        SyncHPUI();
    }

    public void IncreaseAttack(float amount)
    {
        attack += amount;
    }

    public void IncreaseDefense(float amount)
    {
        defense += amount;
    }

    public void IncreaseMoveSpeed(float amount)
    {
        moveSpeed += amount;
    }

    [Header("몸집 한계")]
    [SerializeField] private float minScale = 0.4f;
    [SerializeField] private float maxScale = 3.0f;

    /// <summary>몸집을 scaleMultiplier배 키우고 최대 체력 hpBonus만큼 증가.</summary>
    public void IncreaseSizeHP(float scaleMultiplier, float hpBonus)
    {
        float newScale = Mathf.Min(transform.localScale.x * scaleMultiplier, maxScale);
        transform.localScale = Vector3.one * newScale;
        maxHP     += hpBonus;
        currentHP  = Mathf.Min(currentHP + hpBonus, maxHP);
        SyncHPUI();
    }

    /// <summary>몸집을 scaleMultiplier배 줄이고 최대 체력 hpPenalty만큼 감소.</summary>
    public void DecreaseSizeHP(float scaleMultiplier, float hpPenalty)
    {
        float newScale = Mathf.Max(transform.localScale.x / scaleMultiplier, minScale);
        transform.localScale = Vector3.one * newScale;
        maxHP     = Mathf.Max(1f, maxHP - hpPenalty);
        currentHP = Mathf.Min(currentHP, maxHP);
        SyncHPUI();
    }

    /// <summary>회피 확률 증가. 최대 0.75(75%)로 제한합니다.</summary>
    public void IncreaseDodge(float amount)
    {
        dodge = Mathf.Clamp(dodge + amount, 0f, 0.75f);
    }

    private IEnumerator HitInvincibleRoutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    /// <summary>공격속도를 amount% 증가 (쿨타임 단축).</summary>
    public void IncreaseAttackSpeed(float amount)
    {
        float multiplier = 1f + amount / 100f;
        foreach (var wp in GetComponentsInChildren<WeaponPoint>())
            wp.MultiplyAttackSpeed(multiplier);
    }

    /// <summary>원거리 무기 전용 추가 공격력.</summary>
    public void IncreaseRangedAttackBonus(float amount) => rangedAttackBonus += amount;

    /// <summary>근거리 무기 전용 추가 공격력.</summary>
    public void IncreaseMeleeAttackBonus(float amount) => meleeAttackBonus += amount;

    /// <summary>웨이브 시작 시 자동 회복량을 늘립니다. (GameManager가 웨이브 시작 시 호출)</summary>
    public void IncreaseWaveHeal(float amount) => waveHealAmount += amount;

    /// <summary>주기적으로 주변 적을 밀어냅니다. 중복 호출 시 기존 루틴을 교체합니다.</summary>
    public void StartPeriodicKnockback(float interval, float force, float radius = 4f)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(PeriodicKnockbackRoutine(interval, force, radius));
    }

    private System.Collections.IEnumerator PeriodicKnockbackRoutine(float interval, float force, float radius)
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            yield return wait;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                Vector2 dir = (enemy.transform.position - transform.position);
                if (dir.magnitude <= radius)
                    enemy.Knockback(dir.normalized, force);
            }
        }
    }

    /// <summary>GameManager.Revive()에서 호출 — HP를 가득 채우고 3초 무적 부여</summary>
    public void Revive()
    {
        currentHP = maxHP;
        SyncHPUI();
        StartCoroutine(InvincibleRoutine(3f));
    }

    private IEnumerator InvincibleRoutine(float duration)
    {
        isInvincible = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 0.15초 주기로 깜빡임
            if (spriteRenderer) spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.15f);
            elapsed += 0.15f;
        }
        if (spriteRenderer) spriteRenderer.enabled = true;
        isInvincible = false;
    }

    public Vector2 Position => transform.position;
    public Vector2 MoveDirection => moveDirection;
}
