using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부메랑 투사체.
/// 발사 → 첫 타겟 추적 → 타격 시 maxBounces 횟수만큼 다음 가장 가까운 적으로 튕김
/// → 더 이상 튕길 적이 없거나 횟수 소진 시 플레이어에게 귀환 → 도달하면 소멸.
/// </summary>
public class BoomerangProjectile : MonoBehaviour
{
    [Header("기본 설정 (Init()으로 덮어씀)")]
    [SerializeField] private float spinSpeed   = 720f;  // deg/s
    [SerializeField] private float returnRange = 0.35f; // 플레이어와 이 거리 내 도달 시 소멸

    private float   damage;
    private float   speed;
    private int     bouncesLeft;
    private float   bounceRange;
    private bool    initialized;
    private bool    isCritical;

    private enum State { Flying, Returning }
    private State state = State.Flying;

    private EnemyController                   currentTarget;
    private Transform                         playerTransform;
    private readonly HashSet<EnemyController> hitEnemies = new();

    // 타겟이 없을 때 계속 날아갈 방향
    private Vector2 flyDir;

    public void Init(float damage, float speed, int maxBounces, float bounceRange,
                     EnemyController firstTarget, Transform playerTf, bool isCritical = false)
    {
        this.damage          = damage;
        this.isCritical      = isCritical;
        this.speed           = speed;
        this.bouncesLeft     = maxBounces;
        this.bounceRange     = bounceRange;
        this.playerTransform = playerTf;
        this.initialized     = true;

        // Rigidbody2D가 있으면 물리 간섭 차단
        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.bodyType        = RigidbodyType2D.Kinematic;
            rb.gravityScale    = 0f;
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        currentTarget = firstTarget;

        flyDir = firstTarget != null
            ? ((Vector2)firstTarget.transform.position - (Vector2)transform.position).normalized
            : Vector2.up;

        Destroy(gameObject, 10f);
    }

    private void Update()
    {
        if (!initialized) return;

        // 시각적 스핀
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        switch (state)
        {
            case State.Flying:    UpdateFlying();    break;
            case State.Returning: UpdateReturning(); break;
        }
    }

    private void UpdateFlying()
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            MoveToward(currentTarget.transform.position);
        }
        else
        {
            // 타겟이 사라졌으면 계속 직진하다가 귀환
            currentTarget = null;
            transform.Translate(flyDir * speed * Time.deltaTime, Space.World);
        }
    }

    private void UpdateReturning()
    {
        if (playerTransform == null) { Destroy(gameObject); return; }

        MoveToward(playerTransform.position);

        if (Vector2.Distance(transform.position, playerTransform.position) <= returnRange)
            Destroy(gameObject);
    }

    private void MoveToward(Vector3 target)
    {
        Vector2 dir = ((Vector2)target - (Vector2)transform.position).normalized;
        flyDir = dir;
        transform.Translate(dir * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state == State.Returning) return; // 귀환 중엔 적에 반응하지 않음

        var enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null || hitEnemies.Contains(enemy)) return;

        hitEnemies.Add(enemy);
        Vector2 hitDir = (enemy.transform.position - transform.position).normalized;
        enemy.TakeDamage(damage, isCritical, hitDir, 6f);
        bouncesLeft--;

        if (bouncesLeft <= 0)
        {
            BeginReturn();
            return;
        }

        // 다음 튕길 적 탐색
        EnemyController next = FindNextEnemy(enemy.transform.position);
        if (next != null)
            currentTarget = next;
        else
            BeginReturn();
    }

    private void BeginReturn()
    {
        state         = State.Returning;
        currentTarget = null;
    }

    private EnemyController FindNextEnemy(Vector2 fromPos)
    {
        EnemyController nearest = null;
        float minDist = bounceRange;

        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (hitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist) { minDist = d; nearest = e; }
        }
        return nearest;
    }
}
