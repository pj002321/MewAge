using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 투사체 사이클 패턴.
///
/// 반복 사이클:
///   1) [직선 연사] N초 동안 x간격으로 플레이어 방향 투사체 발사
///   2) [추격]      N초 동안 플레이어 추격
///   3) [부채꼴]    120도 부채꼴 N개 투사체를 N회 발사 (회당 딜레이)
///   4) [추격]      N초 동안 플레이어 추격
///   → 1 로 반복
/// </summary>
public class BossProjectileCycleSkill : BossSkillBase
{
    [Header("투사체 프리팹")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed  = 7f;
    [SerializeField] private float      projectileDamage = 0f; // 0이면 ContactDamage 사용

    [Header("발사 지점")]
    [Tooltip("null이면 보스 중심에서 발사")]
    [SerializeField] private Transform firePoint;

    [Header("1단계 — 직선 연사")]
    [SerializeField] private float linearDuration  = 4f;  // 연사 지속 시간 (초)
    [SerializeField] private float linearInterval  = 0.4f; // 발사 간격 (초)

    [Header("2단계 — 추격 (직선 연사 후)")]
    [SerializeField] private float chaseAfterLinear = 2f;

    [Header("3단계 — 부채꼴 버스트")]
    [SerializeField] private int   fanCount       = 5;    // 1회 발사당 투사체 수
    [SerializeField] private float fanAngle       = 120f; // 부채꼴 각도 (도)
    [SerializeField] private int   fanRepeat      = 3;    // 반복 횟수
    [SerializeField] private float fanRepeatDelay = 0.6f; // 회 사이 간격 (초)

    [Header("4단계 — 추격 (부채꼴 후)")]
    [SerializeField] private float chaseAfterFan  = 2f;

    [Header("추격 설정")]
    [SerializeField] private float chaseStopDistance = 1.8f;

    [Header("애니메이션")]
    [SerializeField] private string attackTrigger = "tAttack";

    private void Start()
    {
        // BossController.Awake에서 Init이 이미 호출되므로 바로 루프 시작
        StartCoroutine(CycleLoop());
    }

    protected override IEnumerator OnExecute()
    {
        // BossPatternBehaviour와 함께 쓸 경우를 위한 폴백 (Start에서 이미 시작됨)
        yield break;
    }

    private IEnumerator CycleLoop()
    {
        while (true)
        {
            if (IsInvalid()) yield break;

            // 1) 직선 연사
            yield return LinearFirePhase();

            if (IsInvalid()) yield break;

            // 2) 추격
            yield return ChasePhase(chaseAfterLinear);

            if (IsInvalid()) yield break;

            // 3) 부채꼴 버스트
            yield return FanBurstPhase();

            if (IsInvalid()) yield break;

            // 4) 추격
            yield return ChasePhase(chaseAfterFan);
        }
    }

    // ── 페이즈 ────────────────────────────────────────────────────

    private IEnumerator LinearFirePhase()
    {
        float elapsed = 0f;
        float timer   = 0f;

        while (elapsed < linearDuration)
        {
            if (IsInvalid()) yield break;

            timer   -= Time.deltaTime;
            elapsed += Time.deltaTime;

            if (timer <= 0f)
            {
                timer = linearInterval;
                FireTowardPlayer();
            }

            yield return null;
        }
    }

    private IEnumerator FanBurstPhase()
    {
        for (int i = 0; i < fanRepeat; i++)
        {
            if (IsInvalid()) yield break;

            FireFan();

            if (i < fanRepeat - 1)
                yield return new WaitForSeconds(fanRepeatDelay);
        }
    }

    private IEnumerator ChasePhase(float duration)
    {
        float elapsed   = 0f;
        float speedMult = Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f;

        while (elapsed < duration)
        {
            if (IsInvalid()) yield break;

            if (Owner.PlayerTransform != null)
            {
                float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
                if (dist > chaseStopDistance)
                    Owner.MoveTowardPlayer(Owner.MoveSpeed * speedMult);

                Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
                Owner.FlipToward(toPlayer);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 발사 ─────────────────────────────────────────────────────

    private void FireTowardPlayer()
    {
        if (projectilePrefab == null || Owner.PlayerTransform == null) return;

        Owner.EnemyAnimator?.SetTrigger(attackTrigger);

        Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        Vector2 dir      = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;

        SpawnProjectile(spawnPos, dir);
    }

    private void FireFan()
    {
        if (projectilePrefab == null || Owner.PlayerTransform == null) return;

        Owner.EnemyAnimator?.SetTrigger(attackTrigger);

        Vector2 spawnPos  = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        Vector2 toPlayer  = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;
        float   baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

        int   count     = Mathf.Max(1, fanCount);
        float halfSpan  = fanAngle * 0.5f;
        float step      = count > 1 ? fanAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle - halfSpan + step * i;
            float rad   = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            SpawnProjectile(spawnPos, dir);
        }
    }

    private void SpawnProjectile(Vector2 pos, Vector2 dir)
    {
        float dmgMult  = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
        float dmg      = (projectileDamage > 0f ? projectileDamage : Owner.ContactDamage) * dmgMult;

        var go = Instantiate(projectilePrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var p))
            p.Init(dmg, dir, projectileSpeed);
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private bool IsInvalid() =>
        Owner == null ||
        (GameManager.Shared != null && GameManager.Shared.IsGameOver);
}
