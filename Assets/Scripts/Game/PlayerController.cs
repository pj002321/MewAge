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

    public float Attack  => attack;
    public float Defense => defense;

    [Header("플레이어 위 HP 슬라이더 (World Space)")]
    [SerializeField] private Slider playerHpSlider;

    private Vector2        moveDirection;
    private Rigidbody2D    rb;
    private SpriteRenderer spriteRenderer;
    private HitFlash       hitFlash;
    private bool           isInvincible;

    private void Awake()
    {
        Instance      = this;
        rb            = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitFlash      = GetComponent<HitFlash>();
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

        float reduced = Mathf.Max(0f, damage - defense);
        currentHP -= reduced;
        currentHP  = Mathf.Max(0f, currentHP);
        SyncHPUI();
        hitFlash?.Flash();

        if (currentHP <= 0f)
            GameManager.Shared?.TriggerGameOver();
    }

    private void SyncHPUI()
    {
        float norm = currentHP / maxHP;
        GameManager.Shared?.UpdateHP(norm);
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
        maxHP += amount;
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
