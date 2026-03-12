using UnityEngine;

/// <summary>
/// 화면 어느 곳이든 터치/클릭하면 해당 방향으로 캐릭터가 이동합니다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Stats")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float currentHP;

    [Header("Bounds")]
    [SerializeField] private float boundPadding = 0.5f;

    private Camera mainCamera;
    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
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
        ClampPosition();
    }

    private void HandleInput()
    {
        moveDirection = Vector2.zero;

        // 터치 입력 (모바일)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                Vector2 worldPos = ScreenToWorld(touch.position);
                Vector2 dir = worldPos - (Vector2)transform.position;
                if (dir.magnitude > 0.05f)
                    moveDirection = dir.normalized;
            }
            return;
        }

        // 마우스 입력 (에디터/PC)
        if (Input.GetMouseButton(0))
        {
            Vector2 worldPos = ScreenToWorld(Input.mousePosition);
            Vector2 dir = worldPos - (Vector2)transform.position;
            if (dir.magnitude > 0.05f)
                moveDirection = dir.normalized;
        }
    }

    /// <summary>
    /// 스크린 좌표를 월드 좌표로 변환합니다. (Z 깊이 포함)
    /// </summary>
    private Vector2 ScreenToWorld(Vector3 screenPos)
    {
        screenPos.z = Mathf.Abs(mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    private void FlipSprite()
    {
        if (spriteRenderer == null) return;
        if (moveDirection.x > 0.05f) spriteRenderer.flipX = false;
        else if (moveDirection.x < -0.05f) spriteRenderer.flipX = true;
    }

    private void ClampPosition()
    {
        Vector2 min = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, Mathf.Abs(mainCamera.transform.position.z)));
        Vector2 max = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, Mathf.Abs(mainCamera.transform.position.z)));

        Vector2 pos = rb.position;
        pos.x = Mathf.Clamp(pos.x, min.x + boundPadding, max.x - boundPadding);
        pos.y = Mathf.Clamp(pos.y, min.y + boundPadding, max.y - boundPadding);
        rb.position = pos;
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
