using UnityEngine;

/// <summary>
/// 원거리 적이 발사하는 투사체.
/// Init() 후 지정 방향으로 이동하며, 플레이어 충돌 시 데미지를 입히고 파괴됩니다.
/// </summary>
public class Projectile : MonoBehaviour
{
    private float   damage;
    private Vector2 direction;
    private float   speed;

    public void Init(float damage, Vector2 direction, float speed = 6f, float lifetime = 5f)
    {
        this.damage    = damage;
        this.direction = direction.normalized;
        this.speed     = speed;
        Destroy(gameObject, lifetime);

        // 투사체 방향으로 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
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
