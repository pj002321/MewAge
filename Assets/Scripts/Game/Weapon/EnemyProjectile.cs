using UnityEngine;

/// <summary>
/// 원거리 적(EnemyType.Ranged)이 발사하는 투사체.
/// EnemyController에서 Instantiate 후 Init()으로 파라미터를 주입합니다.
/// 발사 시점의 플레이어 방향을 유지하며 직선 이동합니다.
/// 경계 밖으로 나가거나 플레이어에 닿으면 즉시 소멸합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    private float       damage;
    private float       speed;
    private Vector2     direction;
    private Rigidbody2D rb;

    public void Init(float damage, float speed, Vector2 direction)
    {
        this.damage    = damage;
        this.speed     = speed;
        this.direction = direction.normalized;

        rb              = GetComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 스프라이트가 방향에 맞게 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, 10f);
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        Vector2 nextPos = rb.position + direction * speed * Time.fixedDeltaTime;

        // 경계 밖이면 제거
        if (MovementBounds.Shared != null && !MovementBounds.Shared.IsInside(nextPos))
        {
            Destroy(gameObject);
            return;
        }

        rb.MovePosition(nextPos);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
