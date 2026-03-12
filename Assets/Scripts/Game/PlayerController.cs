using UnityEngine;

/// <summary>
/// VirtualJoystick으로부터 방향을 받아 캐릭터를 이동합니다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Stats")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP;

    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHP = maxHP;
    }

    private void Start()
    {
        GameManager.Shared?.UpdateHP(1f);
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

        rb.linearVelocity = moveDirection * moveSpeed;
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
        currentHP = Mathf.Max(0f, currentHP);
        GameManager.Shared?.UpdateHP(currentHP / maxHP);

        if (currentHP <= 0f)
            GameManager.Shared?.TriggerGameOver();
    }

    public Vector2 Position => transform.position;
    public Vector2 MoveDirection => moveDirection;
}
