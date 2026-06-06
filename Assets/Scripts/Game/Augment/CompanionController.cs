using UnityEngine;

/// <summary>
/// 증강으로 소환되는 동반자(식인 식물, 애완동물 등)의 AI.
/// 프리팹 Inspector에서 수치 설정 후 Init() 호출로 덮어씌울 수 있음.
/// </summary>
public class CompanionController : MonoBehaviour
{
    [SerializeField] private float attackDamage   = 10f;
    [SerializeField] private float attackInterval = 1f;
    [SerializeField] private float attackRange    = 4f;
    [SerializeField] private bool  followPlayer   = false;
    [SerializeField] private float moveSpeed      = 3f;

    private float _attackTimer;

    public void Init(float damage, float interval, float range, bool follow)
    {
        attackDamage   = damage;
        attackInterval = interval;
        attackRange    = range;
        followPlayer   = follow;
    }

    private void Update()
    {
        if (followPlayer)
        {
            var player = PlayerController.Instance;
            if (player != null)
            {
                Vector2 dir = (Vector2)player.transform.position - (Vector2)transform.position;
                if (dir.magnitude > 1.2f)
                    transform.position += (Vector3)(dir.normalized * moveSpeed * Time.deltaTime);
            }
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer > 0f) return;
        _attackTimer = attackInterval;

        EnemyController nearest = null;
        float minDist = attackRange;
        foreach (var e in EnemyController.All)
        {
            if (e == null) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < minDist) { minDist = d; nearest = e; }
        }
        nearest?.TakeDamage(attackDamage);
    }
}
