using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float maxHP = 30f;
    [SerializeField] private float contactDamage = 10f;
    [SerializeField] private int scoreValue = 10;
    [SerializeField] private float damageCooldown = 1f;

    private float currentHP;
    private float damageTimer;
    private Transform playerTransform;
    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private void Awake()
    {
        currentHP = maxHP;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void Start()
    {
        playerTransform = PlayerController.Instance?.transform;
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        if (playerTransform == null)
        {
            playerTransform = PlayerController.Instance?.transform;
            return;
        }

        Vector2 dir = ((Vector2)playerTransform.position - rb.position).normalized;

        // Kinematic + MovePosition: 슬립 없이 항상 이동
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);

        if (sr != null)
        {
            if (dir.x > 0.01f) sr.flipX = false;
            else if (dir.x < -0.01f) sr.flipX = true;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;
        if (other.TryGetComponent<PlayerController>(out var p))
        {
            p.TakeDamage(contactDamage);
            damageTimer = damageCooldown;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PlayerController>(out var p))
        {
            p.TakeDamage(contactDamage);
            damageTimer = damageCooldown;
        }
    }

    private void LateUpdate()
    {
        damageTimer -= Time.deltaTime;
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        SoundManager.Shared?.Play("enemy_hit");
        FXManager.Shared?.SpawnDamageText(transform.position, damage);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        if (currentHP <= 0f)
        {
            GameManager.Shared?.AddScore(scoreValue);
            Destroy(gameObject);
        }
    }

    public void ApplyWaveScaling(int wave)
    {
        float m = 1f + (wave - 1) * 0.2f;
        moveSpeed *= m;
        maxHP *= m;
        currentHP = maxHP;
        scoreValue = Mathf.RoundToInt(scoreValue * m);
    }

    /// <summary>스폰 시 통 튀기는 bounce 스케일 효과</summary>
    public void PlaySpawnBounce()
    {
        StartCoroutine(BounceIn());
    }

    private System.Collections.IEnumerator BounceIn()
    {
        // 이동은 bounce가 끝나야 시작
        enabled = false;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;

        // 키프레임: (scale, 경과시간)
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

        // 이동 재개
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        enabled = true;
    }
}
