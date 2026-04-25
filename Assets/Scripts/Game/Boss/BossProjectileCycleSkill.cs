using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 투사체 사이클 패턴.
///
/// 반복 사이클:
///   1) [직선 연사] N회 플레이어 방향 투사체 발사
///   2) [추격]      N초 동안 플레이어 추격
///   3) [부채꼴]    fanAngle도 부채꼴 N개 투사체를 N회 발사
///   4) [추격]      N초 동안 플레이어 추격
///   → 1 로 반복
/// </summary>
public class BossProjectileCycleSkill : BossSkillBase
{
    [Header("투사체")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 7f;

    [Header("발사 지점")]
    [Tooltip("null이면 보스 중심에서 발사")]
    [SerializeField] private Transform firePoint;

    [Header("1단계 — 직선 연사")]
    [Tooltip("false로 끄면 직선 연사 페이즈를 건너뜁니다")]
    [SerializeField] private bool  enableLinearFire = true;
    [SerializeField] private int   linearCount    = 8;    // 총 발사 횟수
    [SerializeField] private float linearInterval = 0.5f; // 발사 간격 (초)

    [Header("2단계 — 추격")]
    [SerializeField] private float chaseAfterLinear = 2f;

    [Header("3단계 — 부채꼴 버스트")]
    [SerializeField] private int   fanCount       = 5;
    [SerializeField] private float fanAngle       = 120f;
    [SerializeField] private int   fanRepeat      = 3;
    [SerializeField] private float fanRepeatDelay = 0.6f;

    [Header("4단계 — 추격")]
    [SerializeField] private float chaseAfterFan = 2f;

    [Header("추격 설정")]
    [SerializeField] private float chaseStopDistance = 1.8f;

    [Header("경고 라인")]
    [SerializeField] private float    warningDuration   = 0.4f;
    [SerializeField] private float    warningLineLength = 10f;
    [SerializeField] private float    warningLineWidth  = 0.06f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("애니메이션")]
    [SerializeField] private string attackTrigger = "tAttack";

    private bool isChasing;

    // ── 초기화 ────────────────────────────────────────────────────

    public override void Init(BossController boss)
    {
        base.Init(boss);
        executeInPattern = false;
        StartCoroutine(CycleLoop());
    }

    protected override IEnumerator OnExecute() { yield break; }

    // ── 메인 사이클 ───────────────────────────────────────────────

    private IEnumerator CycleLoop()
    {
        // 1프레임 대기 → IsDropping이 설정된 뒤 착지 완료까지 대기
        yield return null;
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
                yield return new WaitUntil(() => GameManager.Shared == null || !GameManager.Shared.IsGameOver);

            if (IsInvalid()) yield break;

            yield return LinearFirePhase();
            if (IsInvalid()) yield break;

            yield return ChasePhase(chaseAfterLinear);
            if (IsInvalid()) yield break;

            yield return FanBurstPhase();
            if (IsInvalid()) yield break;

            yield return ChasePhase(chaseAfterFan);
        }
    }

    // ── 페이즈 ────────────────────────────────────────────────────

    private IEnumerator LinearFirePhase()
    {
        if (!enableLinearFire) yield break;

        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < linearCount; i++)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                float   dist     = Owner.PlayerTransform != null
                    ? Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position)
                    : float.MaxValue;

                if (dist > chaseStopDistance)
                {
                    Vector2 dir = Owner.PlayerTransform != null
                        ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
                        : Vector2.right;

                    yield return WarnThenFire(spawnPos, new[] { dir });
                    if (IsInvalid()) yield break;
                }

                if (i < linearCount - 1)
                    yield return new WaitForSeconds(linearInterval);
            }
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    private IEnumerator FanBurstPhase()
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < fanRepeat; i++)
            {
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                float   dist     = Owner.PlayerTransform != null
                    ? Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position)
                    : float.MaxValue;

                if (dist > chaseStopDistance)
                {
                    yield return WarnThenFire(spawnPos, GetFanDirs(spawnPos));
                    if (IsInvalid()) yield break;
                }

                if (i < fanRepeat - 1)
                    yield return new WaitForSeconds(fanRepeatDelay);
            }
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    private IEnumerator ChasePhase(float duration)
    {
        isChasing = true;
        float elapsed   = 0f;
        float speedMult = Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f;

        while (elapsed < duration)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
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

        isChasing = false;
    }

    // ── 경고 라인 → 발사 ─────────────────────────────────────────

    private IEnumerator WarnThenFire(Vector2 spawnPos, Vector2[] dirs)
    {
        var lines = new LineRenderer[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
            lines[i] = SpawnWarningLine(spawnPos, dirs[i]);

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (lines[i] == null) continue;
                lines[i].SetPosition(1, spawnPos + dirs[i] * (warningLineLength * t));
            }
            yield return null;
        }

        foreach (var lr in lines)
            if (lr != null) Destroy(lr.gameObject);

        if (IsInvalid()) yield break;

        Owner.EnemyAnimator?.SetTrigger(attackTrigger);
        foreach (var dir in dirs)
            SpawnProjectile(spawnPos, dir);
    }

    private LineRenderer SpawnWarningLine(Vector2 start, Vector2 dir)
    {
        var go = new GameObject("WarnLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, start);   // 길이 0에서 시작, WarnThenFire에서 애니메이션
        lr.startWidth   = warningLineWidth;
        lr.endWidth     = warningLineWidth;
        lr.material     = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor   = warningColor;
        lr.endColor     = warningColor;
        lr.sortingOrder = 10;
        return lr;
    }

    // ── 발사 헬퍼 ────────────────────────────────────────────────

    private Vector2 GetSpawnPos()
        => firePoint != null
            ? (Vector2)firePoint.position
            : Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

    private Vector2[] GetFanDirs(Vector2 spawnPos)
    {
        Vector2 toPlayer  = Owner.PlayerTransform != null
            ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
            : Vector2.right;
        float   baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        int     count     = Mathf.Max(1, fanCount);
        float   halfSpan  = fanAngle * 0.5f;
        float   step      = count > 1 ? fanAngle / (count - 1) : 0f;

        var dirs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle - halfSpan + step * i;
            float rad   = angle * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        return dirs;
    }

    private void SpawnProjectile(Vector2 pos, Vector2 dir)
    {
        float dmgMult = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
        float dmg     = Owner.ContactDamage * dmgMult;

        var go = Instantiate(projectilePrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var p))
        {
            p.Init(dmg, dir, projectileSpeed);
            p.SetOwnerBoss(Boss);
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private bool IsInvalid()
    {
        if (Owner == null) { isChasing = false; return true; }
        return false;
    }

    // ── 씬뷰 기즈모 (에디터 전용) ─────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin   = firePoint != null ? firePoint.position : transform.position;
        int     count    = Mathf.Max(1, fanCount);
        float   halfSpan = fanAngle * 0.5f;
        float   step     = count > 1 ? fanAngle / (count - 1) : 0f;

        // 부채꼴 반투명 채우기
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidArc(
            origin, Vector3.forward,
            Quaternion.Euler(0f, 0f, -halfSpan) * Vector3.right,
            fanAngle, warningLineLength);

        // 외곽선
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.7f);
        UnityEditor.Handles.DrawWireArc(
            origin, Vector3.forward,
            Quaternion.Euler(0f, 0f, -halfSpan) * Vector3.right,
            fanAngle, warningLineLength);

        // 각 투사체 방향 라인
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.9f);
        for (int i = 0; i < count; i++)
        {
            float   angle = -halfSpan + step * i;
            float   rad   = angle * Mathf.Deg2Rad;
            Vector3 dir   = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Gizmos.DrawRay(origin, dir * warningLineLength);
        }
    }
#endif
}
