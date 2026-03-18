using UnityEngine;

/// <summary>
/// 무기 프리팹에 부착합니다. 근거리/원거리 동작을 하나의 컴포넌트로 처리합니다.
/// ▶ 근거리(Melee) : Idle → Pullback → Striking → Returning 상태 머신
/// ▶ 원거리(Ranged) : 범위 내 가장 가까운 적에게 Projectile 발사
/// ▶ WeaponPoint는 하위 호환을 위한 빈 서브클래스로 남겨 둡니다.
/// </summary>
public class WeaponInfo : MonoBehaviour
{
    // ── 타입 ─────────────────────────────────────────────────
    [Header("무기 타입")]
    [SerializeField] private WeaponType weaponType = WeaponType.Melee;

    // ── 공통 스탯 ─────────────────────────────────────────────
    [Header("공통 스탯")]
    [SerializeField] private float attackDamage   = 20f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackRange    = 3f;

    // ── 근거리 전용 ───────────────────────────────────────────
    [Header("근거리 전용 (Melee)")]
    [SerializeField] private float pullbackSpeed    = 12f;
    [SerializeField] private float strikeSpeed      = 40f;
    [SerializeField] private float returnSpeed      = 25f;
    [SerializeField] private float hitDistance      = 0.25f;
    [SerializeField] private float pullbackDistance = 0.4f;

    // ── 원거리 전용 ───────────────────────────────────────────
    [Header("원거리 전용 (Ranged / Magic)")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed      = 10f;
    [SerializeField] private float      projectileSize       = 1f;
    [SerializeField] private bool       projectileBounce     = false;
    [SerializeField] private int        projectileMaxBounces = 3;

    // ── 에디터가 타입 읽기용 ──────────────────────────────────
    public WeaponType WeaponTypeValue => weaponType;

    // ── 근거리 상태 머신 ──────────────────────────────────────
    private enum State { Idle, Pullback, Striking, Returning }
    private State state = State.Idle;

    private EnemyController targetEnemy;
    private Vector3         pullbackTarget;
    private float           cooldownTimer;

    // ─────────────────────────────────────────────────────────
    private void Start()
    {
        cooldownTimer = attackCooldown;
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        cooldownTimer -= Time.deltaTime;
        UpdateRotation();

        switch (weaponType)
        {
            case WeaponType.Melee:
                UpdateMelee();
                break;
            case WeaponType.Ranged:
            case WeaponType.Magic:
                UpdateRanged();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    // 근거리
    // ══════════════════════════════════════════════════════════
    private void UpdateMelee()
    {
        switch (state)
        {
            case State.Idle:      UpdateIdle();      break;
            case State.Pullback:  UpdatePullback();  break;
            case State.Striking:  UpdateStriking();  break;
            case State.Returning: UpdateReturning(); break;
        }
    }

    private void UpdateIdle()
    {
        if (cooldownTimer > 0f) return;

        EnemyController nearest = FindNearestUnclaimed();
        if (nearest == null) return;

        if (WeaponManager.Instance.TryClaim(nearest))
        {
            targetEnemy = nearest;
            BeginPullback();
        }
    }

    private void BeginPullback()
    {
        Vector3 originWorld = transform.parent.position;
        Vector3 pullbackDir = targetEnemy != null
            ? (originWorld - targetEnemy.transform.position).normalized
            : Vector3.down;

        pullbackTarget = originWorld + pullbackDir * pullbackDistance;
        state = State.Pullback;
    }

    private void UpdatePullback()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            state = State.Returning;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, pullbackTarget, pullbackSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, pullbackTarget) <= 0.05f)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            state = State.Striking;
        }
    }

    private void UpdateStriking()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            state = State.Returning;
            return;
        }

        Vector3 targetPos = targetEnemy.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            var pc  = PlayerController.Instance;
            float dmg = attackDamage + (pc?.Attack ?? 0f);
            targetEnemy.TakeDamage(dmg);

            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            state = State.Returning;
        }
    }

    private void UpdateReturning()
    {
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, Vector3.zero, returnSpeed * Time.deltaTime);

        if (transform.localPosition.sqrMagnitude <= 0.001f)
        {
            transform.localPosition = Vector3.zero;
            cooldownTimer = EffectiveCooldown;
            state = State.Idle;
        }
    }

    private float EffectiveCooldown => attackCooldown;

    // ══════════════════════════════════════════════════════════
    // 원거리
    // ══════════════════════════════════════════════════════════
    private void UpdateRanged()
    {
        if (cooldownTimer > 0f) return;
        if (projectilePrefab == null) return;

        EnemyController nearest = FindNearestUnclaimed();
        if (nearest == null) return;

        if (WeaponManager.Instance.TryClaim(nearest))
        {
            FireAt(nearest.transform.position);
            WeaponManager.Instance.Release(nearest);
            cooldownTimer = EffectiveCooldown;
        }
    }

    private void FireAt(Vector3 targetWorldPos)
    {
        var pc  = PlayerController.Instance;
        Vector2 dir = (targetWorldPos - transform.position).normalized;

        float dmg = attackDamage + (pc?.Attack ?? 0f);

        GameObject go = Object.Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var proj))
            proj.Init(dmg, dir, projectileSpeed);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
    }

    // ══════════════════════════════════════════════════════════
    // 회전 & 탐색 공통
    // ══════════════════════════════════════════════════════════
    private void UpdateRotation()
    {
        Vector2 dir = GetLookDirection();
        if (dir.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetLookDirection()
    {
        // Striking 중에는 타겟 고정
        if (state == State.Striking && targetEnemy != null)
            return (targetEnemy.transform.position - transform.position).normalized;

        EnemyController nearest = FindNearestAny();
        if (nearest != null)
            return (nearest.transform.position - transform.position).normalized;

        if (PlayerController.Instance != null)
        {
            Vector2 moveDir = PlayerController.Instance.MoveDirection;
            if (moveDir.sqrMagnitude > 0.001f) return moveDir;
        }

        return Vector2.up;
    }

    private EnemyController FindNearestUnclaimed()
    {
        EnemyController nearest = null;
        float minDist = attackRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.parent.position, attackRange);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            if (WeaponManager.Instance.IsClaimed(enemy)) continue;

            float dist = Vector2.Distance(transform.parent.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy; }
        }
        return nearest;
    }

    private EnemyController FindNearestAny()
    {
        EnemyController nearest = null;
        float minDist = float.MaxValue;

        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            float dist = Vector2.Distance(transform.parent.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy; }
        }
        return nearest;
    }

    /// <summary>PlayerController.IncreaseAttackSpeed에서 호출 — 쿨타임을 multiplier로 나눔.</summary>
    public void MultiplyAttackSpeed(float multiplier)
    {
        if (multiplier <= 0f) return;
        attackCooldown /= multiplier;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
