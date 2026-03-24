using System.Collections.Generic;
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
    [SerializeField] private float attackDamage   = 20f;
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("크리티컬 발동 확률 (0~1, 예: 0.1 = 10%)")]
    [SerializeField] private float criticalRate       = 0f;
    [Tooltip("크리티컬 데미지 배율 (예: 2 = 2배)")]
    [SerializeField] private float criticalMultiplier = 2f;
    [Tooltip("출혈 데미지 (0 = 없음)")]
    [SerializeField] private float dotDamage   = 0f;
    [Tooltip("흡혈 (공격 시 HP 회복 %, 0 = 없음)")]
    [SerializeField] private float lifeSteal   = 0f;

    [Header("탐지 범위 (이 안의 적에게 날아감)")]
    [SerializeField] private float attackRange = 3f;

    // ── 읽기 전용 프로퍼티 (WeaponInfoPanel용) ───────────────────
    public float AttackDamage   => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float AttackRange    => attackRange;
    public float CriticalRate   => criticalRate;
    public float BleedDamage    => dotDamage;
    public float LifeSteal      => lifeSteal;

    [Header("근거리 이동 속도")]
    [SerializeField] private float pullbackSpeed = 20f;   // 빠르게 당김
    [SerializeField] private float strikeSpeed   = 50f;   // 더 빠르게 돌진
    [SerializeField] private float returnSpeed   = 25f;

    [Header("근거리 타격 판정")]
    [SerializeField] private float hitDistance = 0.25f;

    [Header("근거리 풀백 (0 = 플레이어 중심까지만, 양수 = 중심 너머 과장)")]
    [SerializeField] private float pullbackDistance = 0.15f;

    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 8f;

    [Header("부메랑 설정")]
    [SerializeField] private int   boomerangMaxBounces = 3;
    [SerializeField] private float boomerangBounceRange = 5f;

    [Header("돌리기 설정 (WeaponType.Spin)")]
    [SerializeField] private float orbitRadius     = 1.2f;
    [SerializeField] private float orbitSpeed      = 200f;   // deg/s
    [SerializeField] private float spinHitRadius   = 0.35f;  // 타격 판정 반경
    [SerializeField] private float spinHitCooldown = 0.5f;   // 동일 적 재타격 쿨타임
    [SerializeField] private float spinKnockback   = 7f;

    private enum State { Idle, Pullback, Striking, Returning }
    private State state = State.Idle;

    private Vector3 originLocalPos;
    private EnemyController targetEnemy;
    private Vector3 pullbackTarget;
    private Vector3 overshootTarget;
    private float cooldownTimer;

    // Spin 전용
    private float currentAngle;
    private readonly Dictionary<EnemyController, float> spinHitTimers = new Dictionary<EnemyController, float>();

    private void Start()
    {
        originLocalPos = transform.localPosition;
        cooldownTimer = attackCooldown; // 시작할 때 쿨타임 한번 대기 (선택)
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        if (weaponType == WeaponType.Spin)
        {
            UpdateSpin();
            return;
        }

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
    // Spin: 플레이어 주위를 공전하며 닿는 적 타격
    // ─────────────────────────────────────────
    private void UpdateSpin()
    {
        // 공전 각도 증가 → localPosition 갱신
        currentAngle += orbitSpeed * Time.deltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;

        // 무기 스프라이트를 공전 방향으로 자전
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);

        // 피타격 쿨타임 감소 (Keys 복사 후 수정)
        var keys = new List<EnemyController>(spinHitTimers.Keys);
        foreach (var k in keys)
        {
            spinHitTimers[k] -= Time.deltaTime;
            if (spinHitTimers[k] <= 0f) spinHitTimers.Remove(k);
        }

        // 타격 판정
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, spinHitRadius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || spinHitTimers.ContainsKey(enemy)) continue;

            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            var (finalDmg, isCrit) = RollCrit(attackDamage);
            Vector2 knockDir = (enemy.transform.position - transform.position).normalized;
            enemy.TakeDamage(finalDmg, isCrit, knockDir, spinKnockback);

            spinHitTimers[enemy] = spinHitCooldown;
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
        var (finalDmg, isCrit) = RollCrit(attackDamage);
        Vector2 dir = (target.transform.position - transform.position).normalized;
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        go.GetComponent<Projectile>()?.Init(finalDmg, dir, projectileSpeed, isCritical: isCrit);
    }

    private void ShootBoomerang(EnemyController firstTarget)
    {
        if (projectilePrefab == null) return;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        var (finalDmg, isCrit) = RollCrit(attackDamage);
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<BoomerangProjectile>(out var bm))
            bm.Init(finalDmg, projectileSpeed, boomerangMaxBounces, boomerangBounceRange,
                    firstTarget, transform.parent, isCrit);
    }

    // ─────────────────────────────────────────
    // Pullback: 플레이어 몸 중심으로 확 당겨짐 → 이후 적에게 돌진
    // ─────────────────────────────────────────
    private void BeginPullback()
    {
        // 플레이어 몸 중심(localPosition zero)으로 완전히 당김
        // pullbackDistance > 0이면 중심을 지나 더 반대쪽까지 당겨 과장감 추가
        Vector3 playerCenter = transform.parent.position;
        if (pullbackDistance > 0f && targetEnemy != null)
        {
            Vector3 behindDir = (playerCenter - targetEnemy.transform.position).normalized;
            pullbackTarget = playerCenter + behindDir * pullbackDistance;
        }
        else
        {
            pullbackTarget = playerCenter;
        }
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
            var (finalDmg, isCrit) = RollCrit(attackDamage);
            Vector2 hitDir = (targetEnemy.transform.position - transform.position).normalized;
            targetEnemy.TakeDamage(finalDmg, isCrit, hitDir, 8f);

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
        weaponType   = data.weaponType;
        criticalRate = 0.05f + data.criticalRate;  // 기본 5% + 성 보너스 포함 크리티컬 확률

        var player = PlayerController.Instance;
        if (player != null)
        {
            // 공통 보너스 + 무기 타입별 보너스 적용
            attackDamage += player.Attack;
            attackDamage += weaponType == WeaponType.Melee    ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Ranged   ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Boomerang ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Spin      ? player.MeleeAttackBonus  : 0f;
        }

        // 성 등급에 맞는 외곽선 적용
        GetComponentInChildren<SpriteOutline>()?.SetStar(data.star);
        // projectilePrefab은 무기 프리팹 Inspector에서 직접 설정합니다.
    }

    /// <summary>크리티컬 판정. isCritical=true이면 데미지에 multiplier를 곱한 값 반환.</summary>
    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    /// <summary>공격속도를 multiplier배 빠르게 합니다 (쿨타임 단축).</summary>
    public void MultiplyAttackSpeed(float multiplier)
    {
        attackCooldown = Mathf.Max(0.1f, attackCooldown / multiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (weaponType == WeaponType.Spin)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, orbitRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, spinHitRadius);
        }
    }
}
