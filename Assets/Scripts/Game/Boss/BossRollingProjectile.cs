using UnityEngine;

/// <summary>
/// 보스 전용 굴러가는 투사체.
/// 지정 방향으로 이동하면서 Z 축 회전(굴림 연출)을 적용합니다.
/// 플레이어에 닿으면 데미지를 주고 소멸합니다.
/// </summary>
public class BossRollingProjectile : MonoBehaviour
{
    private float           _damage;
    private Vector2         _direction;
    private float           _speed;
    private float           _rotateSpeed; // 초당 회전각 (도)
    private bool            _rotate;
    private BossController  _ownerBoss;

    [Header("연출")]
    [Tooltip("벽에 튕길 때마다 스프라이트 좌우 반전")]
    [SerializeField] private bool flipOnBounce = true;

    private int             _maxBounces;
    private int             _currentBounces;
    private bool            _isDestroying;
    private SpriteRenderer  _spriteRenderer;

    /// <summary>
    /// 초기화.
    /// </summary>
    /// <param name="damage">플레이어에게 줄 데미지</param>
    /// <param name="direction">이동 방향 (정규화 불필요)</param>
    /// <param name="speed">이동 속도 (units/s)</param>
    /// <param name="lifetime">자동 소멸까지 시간 (초)</param>
    /// <param name="rotateSpeed">굴림 회전 속도 (도/s). 양수=시계방향</param>
    public void Init(float damage, Vector2 direction, float speed, float lifetime = 6f, float rotateSpeed = 360f, int maxBounces = 0, bool rotate = true)
    {
        _damage         = damage;
        _direction      = direction.normalized;
        _speed          = speed;
        _rotateSpeed    = rotateSpeed;
        _rotate         = rotate;
        _maxBounces     = maxBounces;
        _currentBounces = 0;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        this.DLog($"Initialized with speed: {speed}, maxBounces: {maxBounces}, lifetime: {lifetime}");
        
        // Use a coroutine for lifetime to handle the flash effect before actual destruction
        StartCoroutine(LifeTimeRoutine(lifetime));
    }

    private System.Collections.IEnumerator LifeTimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        if (!_isDestroying)
        {
            this.DLog("Lifetime expired.");
            StartCoroutine(DestroyWithFlashRoutine());
        }
    }

    public void SetOwnerBoss(BossController boss) => _ownerBoss = boss;

    private void Update()
    {
        if (_isDestroying) return;

        HandleMovement();
        if (_rotate)
            transform.Rotate(0f, 0f, _rotateSpeed * Time.deltaTime);
    }

    private void HandleMovement()
    {
        Vector2 currentPos = transform.position;
        Vector2 nextPos = currentPos + _direction * _speed * Time.deltaTime;

        if (MovementBounds.Shared != null && !MovementBounds.Shared.IsInside(nextPos))
        {
            if (_maxBounces > 0 && _currentBounces < _maxBounces)
            {
                // Find the normal of the boundary edge
                Vector2 hitNormal = GetBoundaryNormal(currentPos);
                _direction = Vector2.Reflect(_direction, hitNormal).normalized;
                _currentBounces++;

                if (flipOnBounce)
                {
                    var s = transform.localScale;
                    s.x = -s.x;
                    transform.localScale = s;
                }

                this.DLog($"Bounced! ({_currentBounces}/{_maxBounces}) Normal: {hitNormal}");

                // Recalculate next position after reflection to avoid getting stuck
                nextPos = currentPos + _direction * _speed * Time.deltaTime;
                
                if (_currentBounces >= _maxBounces)
                {
                    this.DLog("Max bounces reached.");
                    StartCoroutine(DestroyWithFlashRoutine());
                    return;
                }
            }
            else
            {
                // No more bounces allowed, or hit boundary with 0 maxBounces
                StartCoroutine(DestroyWithFlashRoutine());
                return;
            }
        }

        transform.position = nextPos;
    }

    private Vector2 GetBoundaryNormal(Vector2 worldPos)
    {
        if (MovementBounds.Shared == null) return Vector2.up;

        int count = MovementBounds.Shared.PointCount;
        float minDistSq = float.MaxValue;
        Vector2 bestNormal = Vector2.up;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = MovementBounds.Shared.GetWorld(j);
            Vector2 b = MovementBounds.Shared.GetWorld(i);
            
            // Edge direction
            Vector2 ab = b - a;
            // Normal (perpendicular) - assuming points are CCW, inward normal is (-dy, dx)
            // But let's check distance to edge segment to find the correct edge
            Vector2 p = worldPos;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector2 projection = a + t * ab;
            float distSq = Vector2.SqrMagnitude(p - projection);

            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                // Calculate inward normal
                // (-(b.y - a.y), (b.x - a.x)) is one perpendicular
                Vector2 normal = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
                
                // Ensure normal points inward by checking against a point slightly inside the polygon
                // Actually, the polygon center would be a safe bet, or just check the dot product with (center - projection)
                bestNormal = normal;
            }
        }

        return bestNormal;
    }

    private System.Collections.IEnumerator DestroyWithFlashRoutine()
    {
        if (_isDestroying) yield break;
        _isDestroying = true;

        this.DLog("Destroying with flash effect.");

        if (_spriteRenderer != null)
        {
            // Simple flash effect: swap color or use a material if available
            // Since we don't know the shader, we'll try a quick color tint
            Color originalColor = _spriteRenderer.color;
            _spriteRenderer.color = Color.white;
            
            // If the project has a special "Flash" material, it would be better,
            // but for now, we'll just use solid white and a small wait.
            yield return new WaitForSeconds(0.05f);
            _spriteRenderer.color = Color.gray; // Fade out slightly
            yield return new WaitForSeconds(0.05f);
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        player.TakeDamage(_damage, sourceName: gameObject.name);
        Destroy(gameObject);
    }
}
