using System.Collections;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 보스 행동 패턴 상태 머신.
/// 패턴 순서: Idle → Chase → AreaSlam → Idle → Chase → Charge → (반복)
/// 내려찍기: 위로 점프 → 마커 표시 → 목표 위치로 내려꽂기
/// </summary>
public class BossBehaviour : EnemyBehaviourBase
{
    // ── Chase ──────────────────────────────────────────────────
    [Header("추격")]
    [SerializeField] private float chaseStopDistance = 1.8f;  // 이 거리 이하면 멈춤
    [SerializeField] private float chaseDuration     = 2.5f;  // 추격 최대 시간

    // ── AreaSlam ────────────────────────────────────────────────
    [Header("내려찍기")]
    [SerializeField] private float slamDamage       = 30f;
    [SerializeField] private float slamRadius       = 2.5f;
    [SerializeField] private float slamJumpHeight   = 3.5f;   // 점프 높이
    [SerializeField] private float slamRiseTime     = 0.4f;   // 올라가는 시간
    [SerializeField] private float slamHangTime     = 0.8f;   // 정점 유지 (마커 보여주는 시간)
    [SerializeField] private float slamFallSpeed    = 25f;    // 내려오는 속도
    [SerializeField] private GameObject slamMarkerPrefab;

    // ── Charge ──────────────────────────────────────────────────
    [Header("돌진")]
    [SerializeField] private float   chargeSpeed      = 12f;
    [SerializeField] private float   chargeDuration   = 0.55f;
    [Tooltip("돌진 허용 범위 중심 (0,0 = 월드 원점)")]
    [SerializeField] private Vector2 chargeBoundsCenter = Vector2.zero;
    [Tooltip("돌진 허용 범위 크기 — EnemySpawner 스폰 영역과 맞추세요")]
    [SerializeField] private Vector2 chargeBoundsSize   = new(10f, 18f);

    // ── Idle ────────────────────────────────────────────────────
    [Header("대기")]
    [SerializeField] private float idleDuration = 0.6f;

    // ── 런타임 ──────────────────────────────────────────────────
    private BossController boss;
    private bool           isRunning;
    private int            patternStep; // 0=Slam, 1=Charge, 번갈아가며

    // ── 초기화 ──────────────────────────────────────────────────
    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        boss = controller.GetComponent<BossController>();
        if (boss != null) boss.OnEnraged += OnBossEnraged;
    }

    // ── 이동은 코루틴에서 처리 ──────────────────────────────────
    public override void OnMoveFixedUpdate() { }   // 직접 처리

    // ── 패턴 트리거 ─────────────────────────────────────────────
    public override void OnAttackUpdate()
    {
        if (isRunning) return;
        Owner.StartCoroutine(PatternRoutine());
    }

    // ── 패턴 루프 ───────────────────────────────────────────────
    private IEnumerator PatternRoutine()
    {
        isRunning = true;

        // 1) Idle
        yield return new WaitForSeconds(IdleTime());

        // 2) Chase
        yield return ChaseRoutine();

        // 3) 패턴 번갈아: Slam / Charge
        if (patternStep % 2 == 0)
            yield return SlamRoutine();
        else
            yield return ChargeRoutine();

        patternStep++;
        isRunning = false;
    }

    // ── Chase: 플레이어를 향해 이격 거리 유지하며 접근 ──────────
    private IEnumerator ChaseRoutine()
    {
        float elapsed = 0f;
        while (elapsed < chaseDuration)
        {
            elapsed += Time.fixedDeltaTime;

            if (Owner.PlayerTransform == null) { yield return new WaitForFixedUpdate(); continue; }

            float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
            if (dist > chaseStopDistance)
                Owner.MoveTowardPlayer(Owner.MoveSpeed * (boss != null && boss.IsEnraged ? boss.EnrageSpeedMult : 1f));

            yield return new WaitForFixedUpdate();
        }
    }

    // ── AreaSlam: 점프 → 마커 → 내려꽂기 ──────────────────────
    private IEnumerator SlamRoutine()
    {
        if (Owner.PlayerTransform == null) yield break;

        // 목표 위치 = 발동 시점의 플레이어 위치
        Vector3 slamTarget = Owner.PlayerTransform.position;
        Vector3 startPos   = Owner.transform.position;
        Vector3 apexPos    = new Vector3(slamTarget.x, slamTarget.y + slamJumpHeight, startPos.z);

        // 1) 위로 올라가기
        float elapsed = 0f;
        while (elapsed < slamRiseTime)
        {
            elapsed += Time.deltaTime;
            Owner.transform.position = Vector3.Lerp(startPos, apexPos, elapsed / slamRiseTime);
            yield return null;
        }
        Owner.transform.position = apexPos;

        // 2) 정점 유지 (마커 표시)
        GameObject marker = null;
        if (slamMarkerPrefab != null)
            marker = Object.Instantiate(slamMarkerPrefab, slamTarget, Quaternion.identity);

        yield return new WaitForSeconds(slamHangTime);

        // 3) 내려꽂기
        while (Vector3.Distance(Owner.transform.position, slamTarget) > 0.05f)
        {
            Owner.transform.position = Vector3.MoveTowards(
                Owner.transform.position, slamTarget, slamFallSpeed * Time.deltaTime);
            yield return null;
        }
        Owner.transform.position = slamTarget;

        if (marker != null) Object.Destroy(marker);

        // 4) 피해 판정
        float dmg = slamDamage * (boss != null && boss.IsEnraged ? boss.EnrageDamageMult : 1f);
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
        {
            player.TakeDamage(dmg);
        }

        // 착지 후 짧은 경직
        yield return new WaitForSeconds(0.3f);
    }

    // ── Charge: 플레이어 방향으로 빠르게 돌진 ──────────────────
    private IEnumerator ChargeRoutine()
    {
        if (Owner.PlayerTransform == null) yield break;

        float speed   = chargeSpeed * (boss != null && boss.IsEnraged ? boss.EnrageSpeedMult : 1f);
        Vector2 dir   = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
        float elapsed = 0f;

        // EnemySpawner 영역을 경계로 사용 (없으면 자체 필드로 폴백)
        var     spawner  = EnemySpawner.Shared;
        Vector2 aCenter  = spawner != null ? spawner.AreaCenter : chargeBoundsCenter;
        Vector2 aSize    = spawner != null ? spawner.AreaSize   : chargeBoundsSize;
        Vector2 halfSize = aSize * 0.5f;
        float   minX = aCenter.x - halfSize.x, maxX = aCenter.x + halfSize.x;
        float   minY = aCenter.y - halfSize.y, maxY = aCenter.y + halfSize.y;

        while (elapsed < chargeDuration)
        {
            Vector2 pos = Owner.transform.position;

            // 경계 도달 시 돌진 즉시 중단
            if (pos.x <= minX || pos.x >= maxX || pos.y <= minY || pos.y >= maxY)
            {
                Owner.transform.position = new Vector3(
                    Mathf.Clamp(pos.x, minX, maxX),
                    Mathf.Clamp(pos.y, minY, maxY),
                    Owner.transform.position.z);
                break;
            }

            Owner.MoveInDirection(dir, speed);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 돌진 후 짧은 경직
        yield return new WaitForSeconds(0.2f);
    }

    // ── 분노 이벤트 ─────────────────────────────────────────────
    private void OnBossEnraged()
    {
        idleDuration = Mathf.Max(0.2f, idleDuration * 0.5f);
    }

    // ── 수치 헬퍼 ───────────────────────────────────────────────
    private float IdleTime() => boss != null && boss.IsEnraged ? idleDuration * 0.6f : idleDuration;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, chaseStopDistance);
    }
}
