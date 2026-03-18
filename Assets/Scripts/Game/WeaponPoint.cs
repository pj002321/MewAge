using UnityEngine;

/// <summary>
/// Player 자식 오브젝트(weaponPoint)에 부착합니다.
/// 공격 흐름: Idle → Pullback(당김) → Striking(확! 돌진) → Overshooting(팍!) → Returning
/// 회전 우선순위: 적 존재 → 적 방향 / 없으면 → 이동 방향 / 없으면 → 위쪽
/// </summary>
public class WeaponPoint : MonoBehaviour
{
    [Header("무기 타입")]
    [SerializeField] private WeaponType weaponType = WeaponType.Melee;

    [Header("무기 스탯")]
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("탐지 범위 (이 안의 적에게 날아감)")]
    [SerializeField] private float attackRange = 3f;

    [Header("근거리 이동 속도")]
    [SerializeField] private float pullbackSpeed = 12f;
    [SerializeField] private float strikeSpeed   = 40f;
    [SerializeField] private float returnSpeed   = 25f;

    [Header("근거리 타격 판정")]
    [SerializeField] private float hitDistance = 0.25f;

    [Header("근거리 풀백")]
    [SerializeField] private float pullbackDistance = 0.4f;

    [Header("오버슈트 (미사용)")]
    [SerializeField] private float overshootDistance = 0.35f;
    [SerializeField] private float overshootSpeed    = 30f;

    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 8f;

    [Header("부메랑 설정")]
    [SerializeField] private int   boomerangMaxBounces = 3;
    [SerializeField] private float boomerangBounceRange = 5f;

    private enum State { Idle, Pullback, Striking, Returning }
    private State state = State.Idle;

    private Vector3 originLocalPos;
    private EnemyController targetEnemy;
    private Vector3 pullbackTarget;
    private Vector3 overshootTarget;
    private float cooldownTimer;

    private void Start()
    {
        originLocalPos = transform.localPosition;
        cooldownTimer = attackCooldown; // 시작할 때 쿨타임 한번 대기 (선택)
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        cooldownTimer -= Time.deltaTime;

        UpdateRotation();

        switch (state)
        {
            case State.Idle:         UpdateIdle();         break;
            case State.Pullback:     UpdatePullback();     break;
            case State.Striking:     UpdateStriking();     break;
            case State.Returning:    UpdateReturning();    break;
        }
    }

    // ─────────────────────────────────────────
    // Idle: 쿨타임 후 미예약 적 탐색 → Pullback 시작
    // ─────────────────────────────────────────
    private void UpdateIdle()
    {
        if (cooldownTimer > 0f) return;

        EnemyController nearest = FindNearestUnclaimed();
        if (nearest == null) return;

        if (weaponType == WeaponType.Ranged)
        {
            ShootAt(nearest);
            cooldownTimer = attackCooldown;
            return;
        }

        if (weaponType == WeaponType.Boomerang)
        {
            ShootBoomerang(nearest);
            cooldownTimer = attackCooldown;
            return;
        }

        if (WeaponManager.Instance.TryClaim(nearest))
        {
            targetEnemy = nearest;
            BeginPullback();
        }
    }

    private void ShootAt(EnemyController target)
    {
        if (projectilePrefab == null) return;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        Vector2 dir = (target.transform.position - transform.position).normalized;
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        go.GetComponent<Projectile>()?.Init(attackDamage, dir, projectileSpeed);
    }

    private void ShootBoomerang(EnemyController firstTarget)
    {
        if (projectilePrefab == null) return;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<BoomerangProjectile>(out var bm))
            bm.Init(attackDamage, projectileSpeed, boomerangMaxBounces, boomerangBounceRange,
                    firstTarget, transform.parent);
    }

    // ─────────────────────────────────────────
    // Pullback: 원점(부모 위치)에서 타겟 반대 방향으로 살짝 당겨짐 (장전 느낌)
    // ─────────────────────────────────────────
    private void BeginPullback()
    {
        Vector3 originWorld = transform.parent.position;
        Vector3 pullbackDir = Vector3.down; // 기본값

        if (targetEnemy != null)
        {
            // 적의 반대 방향으로 당김
            pullbackDir = (originWorld - targetEnemy.transform.position).normalized;
        }

        pullbackTarget = originWorld + pullbackDir * pullbackDistance;
        state = State.Pullback;
    }

    private void UpdatePullback()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            BeginReturn();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, pullbackTarget, pullbackSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, pullbackTarget) <= 0.05f)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            state = State.Striking;
        }
    }

    // ─────────────────────────────────────────
    // Striking: 확! 빠르게 돌진 → 타격 → (즉시 복귀)
    // ─────────────────────────────────────────
    private void UpdateStriking()
    {
        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            BeginReturn();
            return;
        }

        Vector3 targetPos = targetEnemy.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            targetEnemy.TakeDamage(attackDamage);

            // 타격 직후 즉시 예약 해제 후 바로 복귀
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            BeginReturn();
        }
    }

    // ─────────────────────────────────────────
    // Returning: 원위치(부모 위치)로 복귀
    // ─────────────────────────────────────────
    private void UpdateReturning()
    {
        // 부모(앵커) 기준으로 (0,0,0) 위치로 되돌아갑니다.
        // 플레이어가 아무리 빠르게 이동해도 localPosition 기준이므로 빗나가지 않습니다.
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, Vector3.zero, returnSpeed * Time.deltaTime);

        if (transform.localPosition.sqrMagnitude <= 0.001f)
        {
            transform.localPosition = Vector3.zero; // 완벽히 안착
            cooldownTimer = attackCooldown;
            state = State.Idle;
        }
    }

    private void BeginReturn() => state = State.Returning;

    // ─────────────────────────────────────────
    // 회전: 적 > 이동방향 > 위쪽 우선순위
    // ─────────────────────────────────────────
    private void UpdateRotation()
    {
        Vector2 dir = GetLookDirection();
        if (dir.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetLookDirection()
    {
        // Striking 중: 타겟 적 방향 고정
        if (state == State.Striking && targetEnemy != null)
            return (targetEnemy.transform.position - transform.position).normalized;

        // 적이 있으면 가장 가까운 적 방향
        EnemyController nearest = FindNearestAny();
        if (nearest != null)
            return (nearest.transform.position - transform.position).normalized;

        // 적 없으면 플레이어 이동 방향
        if (PlayerController.Instance != null)
        {
            Vector2 moveDir = PlayerController.Instance.MoveDirection;
            if (moveDir.sqrMagnitude > 0.001f)
                return moveDir;
        }

        // 이동도 없으면 위쪽
        return Vector2.up;
    }

    // ─────────────────────────────────────────
    // 적 탐색 유틸
    // ─────────────────────────────────────────
    private EnemyController FindNearestUnclaimed()
    {
        EnemyController nearest = null;
        float minDist = attackRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.parent.position, attackRange);
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<EnemyController>(out var enemy)) continue;
            if (WeaponManager.Instance.IsClaimed(enemy)) continue;

            float dist = Vector2.Distance(transform.parent.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy; }
        }
        return nearest;
    }

    // 거리 제한 없이 씬 전체에서 가장 가까운 적 (회전용)
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

    /// <summary>WeaponManager가 장착 시 호출 — 타입과 데미지 설정.</summary>
    public void Init(WeaponData data)
    {
        weaponType    = data.weaponType;
        // attackDamage = 무기 인스펙터 기본값 + 플레이어 공격력 보너스
        attackDamage += PlayerController.Instance?.Attack ?? 0f;
        // projectilePrefab은 인스펙터에서 설정한 값을 유지합니다.
        // Ranged 무기 프리팹의 WeaponPoint 컴포넌트에 projectilePrefab을 직접 지정하세요.
    }

    /// <summary>공격속도를 multiplier배 빠르게 합니다 (쿨타임 단축).</summary>
    public void MultiplyAttackSpeed(float multiplier)
    {
        attackCooldown = Mathf.Max(0.1f, attackCooldown / multiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
