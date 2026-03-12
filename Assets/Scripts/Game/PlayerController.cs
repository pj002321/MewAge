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

    [Header("플레이어 위 HP 슬라이더 (World Space)")]
    [SerializeField] private Slider playerHpSlider;

    private Vector2        moveDirection;
    private Rigidbody2D    rb;
    private SpriteRenderer spriteRenderer;
    private HitFlash       hitFlash;

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
        currentHP -= damage;
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

    /// <summary>GameManager.Revive()에서 호출 — HP를 가득 채우고 UI 동기화</summary>
    public void Revive()
    {
        currentHP = maxHP;
        SyncHPUI();
    }

    public Vector2 Position => transform.position;
    public Vector2 MoveDirection => moveDirection;
}
