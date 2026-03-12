using UnityEngine;

/// <summary>
/// Player 자식 오브젝트(weaponPoint)에 부착합니다.
/// 공격 흐름: Idle → Pullback(당김) → Striking(확! 돌진) → Overshooting(팍!) → Returning
/// 회전 우선순위: 적 존재 → 적 방향 / 없으면 → 이동 방향 / 없으면 → 위쪽
/// </summary>
public class WeaponPoint : MonoBehaviour
{
    [Header("무기 스탯")]
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("탐지 범위 (이 안의 적에게 날아감)")]
    [SerializeField] private float attackRange = 3f;

    [Header("이동 속도")]
    [SerializeField] private float pullbackSpeed = 12f;  // 뒤로 당겨지는 속도
    [SerializeField] private float strikeSpeed   = 40f;  // 확! 들어가는 속도
    [SerializeField] private float returnSpeed   = 25f;  // 복귀 속도

    [Header("타격 충돌 판정 거리 (보통 0.1~0.3)")]
    [SerializeField] private float hitDistance = 0.25f;

    [Header("풀백 (공격 전 플레이어쪽으로 당김)")]
    [SerializeField] private float pullbackDistance = 0.4f;

    [Header("오버슈트 (팍! 느낌)")]
    [SerializeField] private float overshootDistance = 0.35f;
    [SerializeField] private float overshootSpeed    = 30f;

    private enum State { Idle, Pullback, Striking, Overshooting, Returning }
    private State state = State.Idle;

    private Vector3 originLocalPos;
    private EnemyController targetEnemy;
    private Vector3 pullbackTarget;
    private Vector3 overshootTarget;
    private float cooldownTimer;

    private void Start()
    {
        originLocalPos = transform.localPosition;
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
            case State.Overshooting: UpdateOvershooting(); break;
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

        if (WeaponManager.Instance.TryClaim(nearest))
        {
            targetEnemy = nearest;
            BeginPullback();
        }
    }

    // ─────────────────────────────────────────
    // Pullback: 원점에서 플레이어 방향으로 살짝 당겨짐
    // ─────────────────────────────────────────
    private void BeginPullback()
    {
        Vector3 originWorld  = transform.parent.TransformPoint(originLocalPos);
        Vector3 towardPlayer = (transform.parent.position - originWorld).normalized;
        pullbackTarget = originWorld + towardPlayer * pullbackDistance;
        state = State.Pullback;
    }

    private void UpdatePullback()
    {
        if (targetEnemy == null)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            BeginReturn();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, pullbackTarget, pullbackSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, pullbackTarget) <= 0.05f)
            state = State.Striking;
    }

    // ─────────────────────────────────────────
    // Striking: 확! 빠르게 돌진 → 타격 → Overshooting
    // ─────────────────────────────────────────
    private void UpdateStriking()
    {
        if (targetEnemy == null)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            BeginReturn();
            return;
        }

        Vector3 targetPos = targetEnemy.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            targetEnemy.TakeDamage(attackDamage);

            Vector3 strikeDir = (targetPos - transform.parent.position).normalized;
            overshootTarget = targetPos + strikeDir * overshootDistance;

            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            state = State.Overshooting;
        }
    }

    // ─────────────────────────────────────────
    // Overshooting: 관성으로 살짝 더 나아감 (팍!)
    // ─────────────────────────────────────────
    private void UpdateOvershooting()
    {
        transform.position = Vector3.MoveTowards(transform.position, overshootTarget, overshootSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, overshootTarget) <= 0.05f)
            BeginReturn();
    }

    // ─────────────────────────────────────────
    // Returning: 원위치로 복귀
    // ─────────────────────────────────────────
    private void UpdateReturning()
    {
        Vector3 worldOrigin = transform.parent.TransformPoint(originLocalPos);
        transform.position = Vector3.MoveTowards(transform.position, worldOrigin, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, worldOrigin) <= 0.01f)
        {
            transform.localPosition = originLocalPos;
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

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetLookDirection()
    {
        // Striking 중: 타겟 적 방향 고정
        if (state == State.Striking && targetEnemy != null)
            return (targetEnemy.transform.position - transform.position).normalized;

        // Overshooting 중: 날아가는 방향 유지
        if (state == State.Overshooting)
            return (overshootTarget - transform.position).normalized;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Vector3 center = Application.isPlaying ? transform.parent.position : transform.position;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
