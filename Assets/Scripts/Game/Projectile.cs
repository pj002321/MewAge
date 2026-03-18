using UnityEngine;

/// <summary>
/// 플레이어 또는 적이 발사하는 투사체.
/// 레이어 충돌 매트릭스로 대상을 구분합니다 (PlayerProjectile / EnemyProjectile 레이어).
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer; // 인스펙터에서 설정 (Enemy 또는 Player)

    private float   damage;
    private Vector2 direction;
    private float   speed;

    public void Init(float damage, Vector2 direction, float speed = 6f, float lifetime = 5f)
    {
        this.damage    = damage;
        this.direction = direction.normalized;
        this.speed     = speed;
        Destroy(gameObject, lifetime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;

        var enemy  = other.GetComponentInParent<EnemyController>();
        var player = other.GetComponentInParent<PlayerController>();

        if (enemy != null)
            enemy.TakeDamage(damage);
        else if (player != null)
            player.TakeDamage(damage);
        else
            return;

        Destroy(gameObject);
    }
}
