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
    private float   lifeSteal;
    private float   dotDamage;
    private float   areaHitRadius;
    private float   areaHitDamageRatio;
    private float   criticalRate;
    private float   criticalMultiplier;
    private float   baseDamage;

    private enum State { Flying, Returning }
    private State state = State.Flying;

    private EnemyController                   currentTarget;
    private Transform                         playerTransform;
    private readonly HashSet<EnemyController> hitEnemies = new();

    // 타겟이 없을 때 계속 날아갈 방향
    private Vector2 flyDir;

    // Physics NonAlloc 버퍼
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    public void Init(float damage, float speed, int maxBounces, float bounceRange,
                     EnemyController firstTarget, Transform playerTf, bool isCritical = false, float lifeSteal = 0f,
                     float dotDamage = 0f, float areaHitRadius = 0f, float areaHitDamageRatio = 0f,
                     float criticalRate = 0f, float criticalMultiplier = 2f, float baseDamage = 0f)
    {
        this.damage             = damage;
        this.isCritical         = isCritical;
        this.lifeSteal          = lifeSteal;
        this.dotDamage          = dotDamage;
        this.areaHitRadius      = areaHitRadius;
        this.areaHitDamageRatio = areaHitDamageRatio;
        this.criticalRate       = criticalRate;
        this.criticalMultiplier = criticalMultiplier;
        this.baseDamage         = baseDamage;
        this.speed              = speed;
        this.bouncesLeft        = maxBounces;
        this.bounceRange        = bounceRange;
        this.playerTransform    = playerTf;
        this.initialized        = true;

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

    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
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

        // 범위 데미지 적용
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, areaHitRadius, _hitBuffer);
        for (int i = 0; i < colCount; i++)
        {
            if (!_hitBuffer[i].TryGetComponent<EnemyController>(out var areaEnemy)) continue;
            bool isPrimary = areaEnemy == enemy;
            float dmg = isPrimary ? damage : baseDamage * areaHitDamageRatio;
            bool isCrit = isPrimary ? isCritical : RollCrit(dmg).isCritical;
            if (!isPrimary)
            {
                var rolled = RollCrit(baseDamage * areaHitDamageRatio);
                dmg = rolled.finalDamage;
                isCrit = rolled.isCritical;
            }
            Vector2 knockDir = ((Vector2)areaEnemy.transform.position - (Vector2)transform.position);
            if (knockDir.sqrMagnitude < 0.001f) knockDir = (enemy.transform.position - transform.position).normalized;
            areaEnemy.TakeDamage(dmg, isCrit, knockDir.normalized, 6f);
            if (lifeSteal > 0f) PlayerController.Instance?.HealHP(dmg * lifeSteal);
            if (dotDamage > 0f) areaEnemy.ApplyBleed(dotDamage);
        }

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

        foreach (var e in EnemyController.All)
        {
            if (hitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist) { minDist = d; nearest = e; }
        }
        return nearest;
    }
}
