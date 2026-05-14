using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 보스 스킬: 데이터-드리븐 투사체 사이클 패턴.
///
/// Inspector의 phases 리스트에 페이즈를 원하는 순서로 추가하면
/// 그 순서대로 무한 반복합니다. 각 페이즈 후 추격시간 &gt; 0이면
/// 발사 후 해당 시간만큼 플레이어를 추격한 뒤 다음 페이즈로 넘어갑니다.
///
/// 지원 패턴 타입:
///   조준연사 — 플레이어 조준 N회 연사
///   방사형   — N방향 동시 방사형 버스트 (회전 오프셋 지원)
///   부채꼴   — 플레이어 방향 기준 부채꼴 버스트
/// </summary>
public class BossProjectileCycleSkill : BossSkillBase
{
    public enum PatternType
    {
        조준연사,   // 플레이어 방향으로 N회 한 발씩 발사
        방사형,     // N방향 동시 방사형 버스트
        부채꼴,     // 플레이어 방향 기준 부채꼴 버스트
        호밍,       // 포물선 곡선으로 플레이어 위치에 꽂히는 타입
    }

    [System.Serializable]
    public class PhaseConfig
    {
        public string      label = "페이즈";
        public PatternType type  = PatternType.조준연사;

        [Tooltip("투사체 발사 전 경고 라인이 표시되는 시간 (초)")]
        public float 경고시간 = 0.4f;

        // ── 공통 ─────────────────────────────────────────────────
        [Tooltip("발사(반복) 횟수")]
        public int   횟수 = 1;

        [Tooltip("발사 사이 대기 시간 (초)")]
        public float 간격 = 0.5f;

        // ── 방사형 · 부채꼴 공통 ─────────────────────────────────
        [ShowIf("@type == PatternType.방사형 || type == PatternType.부채꼴")]
        [Tooltip("동시에 발사할 방향 수. 방사형=8이면 45° 간격 8방향, 12이면 30° 간격 12방향")]
        public int   방향수 = 8;

        // ── 방사형 전용 ───────────────────────────────────────────
        [ShowIf("type", PatternType.방사형)]
        [Tooltip("첫 번째 방향의 각도 오프셋 (도). 0이면 오른쪽(→) 부터 시작")]
        public float 각도오프셋 = 0f;

        // ── 부채꼴 전용 ───────────────────────────────────────────
        [ShowIf("type", PatternType.부채꼴)]
        [Tooltip("부채꼴 전체 각도 범위 (도). 120이면 플레이어 방향 기준 ±60°")]
        public float 부채꼴각도 = 120f;

        // ── 호밍 전용 ─────────────────────────────────────────────
        [ShowIf("type", PatternType.호밍)]
        [Tooltip("포물선 호 높이. 양수=위로 볼록, 음수=아래로 볼록")]
        public float 아크높이 = 3f;

        [ShowIf("type", PatternType.호밍)]
        [Tooltip("호밍 투사체 이동 속도")]
        public float 호밍속도 = 9f;

        // ── 가속 (비호밍 전용) ────────────────────────────────────
        [HideIf("type", PatternType.호밍)]
        [Tooltip("0이면 일반 발사. 값을 넣으면 느리게 출발해 팍 꽂히는 느낌으로 가속됩니다.\n최고속도에 도달하는 시간 (초). 예: 0.4 → 0.4초 동안 가속 후 최고속도")]
        public float 가속시간 = 0f;

        [ShowIf("@가속시간 > 0f && type != PatternType.호밍")]
        [Range(0.01f, 0.5f)]
        [Tooltip("발사 시작 속도 비율 (0.05 = 최고속도의 5%에서 출발)")]
        public float 초기속도비율 = 0.05f;

        // ── 페이즈 후 추격 ────────────────────────────────────────
        [Tooltip("발사 후 플레이어를 추격할 시간 (초). 0이면 즉시 다음 페이즈")]
        public float 추격시간 = 0f;
    }

    [Header("투사체")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 7f;

    [Header("발사 지점")]
    [Tooltip("null이면 보스 중심에서 발사")]
    [SerializeField] private Transform firePoint;

    [Header("페이즈 목록 (순서대로 반복)")]
    [SerializeField] private List<PhaseConfig> phases = new();

    [Header("경고 라인 공통")]
    [SerializeField] private bool     useWarningLines   = true;
    [SerializeField] private float    warningLineLength = 10f;
    [SerializeField] private float    warningLineWidth  = 0.06f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("null이면 Sprites/Default 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("호밍 착탄 VFX")]
    [Tooltip("호밍 투사체가 착탄(충돌/도착)할 때 스폰할 VFX 프리팹")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float      impactVfxLifetime = 1f;

    [Header("추격 설정")]
    [SerializeField] private float chaseStopDistance = 1.8f;
    [SerializeField] private float animWaitTimeout    = 2f;

    private readonly List<LineRenderer> _activeWarnLines = new();

    // ── 생명주기 ─────────────────────────────────────────────────

    private void OnDisable()
    {
        foreach (var lr in _activeWarnLines)
            if (lr != null) Destroy(lr.gameObject);
        _activeWarnLines.Clear();
    }

    public override void Init(BossController boss)
    {
        base.Init(boss);
        executeInPattern = false;
        StartCoroutine(CycleLoop());
    }

    /// <summary>CycleLoop은 그로기를 WaitUntil로 자체 처리하므로 중단하지 않습니다.</summary>
    public override void OnInterrupted() { }

    protected override IEnumerator OnExecute() { yield break; }

    // ── 메인 루프 ────────────────────────────────────────────────

    private IEnumerator CycleLoop()
    {
        yield return null;
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            if (Boss == null || Boss.IsDead) yield break;
            if (phases == null || phases.Count == 0) { yield return new WaitForSeconds(1f); continue; }

            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
                yield return new WaitUntil(() => GameManager.Shared == null || !GameManager.Shared.IsGameOver);

            if (Boss != null && Boss.IsGrogy)
                yield return new WaitUntil(() => Boss == null || !Boss.IsGrogy);

            if (IsInvalid()) yield break;

            foreach (var phase in phases)
            {
                if (IsInvalid()) yield break;
                yield return RunPhase(phase);
                if (IsInvalid()) yield break;

                if (phase.추격시간 > 0f)
                    yield return ChasePhase(phase.추격시간);
            }
        }
    }

    // ── 페이즈 디스패치 ──────────────────────────────────────────

    private IEnumerator RunPhase(PhaseConfig cfg)
    {
        switch (cfg.type)
        {
            case PatternType.조준연사:                yield return AimedRepeatPhase(cfg); break;
            case PatternType.방사형:
            case PatternType.부채꼴:                  yield return BurstPhase(cfg);       break;
            case PatternType.호밍:                   yield return HomingPhase(cfg);      break;
        }
    }

    // ── AimedRepeat ──────────────────────────────────────────────

    private IEnumerator AimedRepeatPhase(PhaseConfig cfg)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < cfg.횟수; i++)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
                if (Boss != null && Boss.IsGrogy) yield break;
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                Vector2 dir = Owner.PlayerTransform != null
                    ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
                    : Vector2.right;

                yield return WarnThenFire(spawnPos, new[] { dir }, cfg.경고시간, cfg.가속시간, cfg.초기속도비율);
                if (IsInvalid()) yield break;

                if (i < cfg.횟수 - 1)
                    yield return new WaitForSeconds(cfg.간격);
            }
            yield return WaitForAttackAnimEnd();
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    // ── Radial / Fan 버스트 ──────────────────────────────────────

    private IEnumerator BurstPhase(PhaseConfig cfg)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int r = 0; r < cfg.횟수; r++)
            {
                if (Boss != null && Boss.IsGrogy) yield break;
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                Vector2[] dirs = cfg.type == PatternType.방사형
                    ? GetRadialDirs(cfg.방향수, cfg.각도오프셋)
                    : GetFanDirs(spawnPos, cfg.방향수, cfg.부채꼴각도);

                yield return WarnThenFire(spawnPos, dirs, cfg.경고시간, cfg.가속시간, cfg.초기속도비율);
                if (IsInvalid()) yield break;

                if (r < cfg.횟수 - 1)
                    yield return new WaitForSeconds(cfg.간격);
            }
            yield return WaitForAttackAnimEnd();
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    // ── Chase ────────────────────────────────────────────────────

    private IEnumerator ChasePhase(float duration)
    {
        const float xAvoidDist = 2.5f;

        float elapsed = 0f;

        // 페이즈 시작 시 어느 쪽으로 비킬지 결정 (0이면 아직 미결정)
        float xSide = 0f;

        while (elapsed < duration)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
            if (IsInvalid()) yield break;

            if (Owner.PlayerTransform != null)
            {
                Vector2 myPos     = Owner.transform.position;
                Vector2 playerPos = Owner.PlayerTransform.position;
                float   dx        = myPos.x - playerPos.x;

                // x가 너무 가까우면 옆으로 비킬 방향 결정 후 유지
                if (Mathf.Abs(dx) < xAvoidDist)
                {
                    if (xSide == 0f) xSide = dx >= 0f ? 1f : -1f;
                }
                else
                {
                    xSide = 0f; // 충분히 벗어나면 리셋
                }

                Vector2 targetPos = xSide != 0f
                    ? new Vector2(playerPos.x + xSide * xAvoidDist, playerPos.y)
                    : playerPos;

                Vector2 toTarget = targetPos - myPos;
                if (toTarget.magnitude > chaseStopDistance && !(Boss != null && Boss.IsFiringSkill))
                    Owner.MoveInDirection(toTarget.normalized, Owner.MoveSpeed);

                Owner.FlipToward((playerPos - myPos).normalized);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 경고 라인 → 발사 ─────────────────────────────────────────

    private IEnumerator WarnThenFire(Vector2 spawnPos, Vector2[] dirs, float warnDuration, float accelDuration = 0f, float initialSpeedRatio = 0.05f)
    {
        var lines = new LineRenderer[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            lines[i] = SpawnWarningLine(spawnPos);
            _activeWarnLines.Add(lines[i]);
        }

        float elapsed = 0f;
        while (elapsed < warnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warnDuration);
            Vector2 cur = GetSpawnPos();
            Owner.FlipToward(dirs[0]);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                var tf = lines[i].transform;
                tf.position = new Vector3(cur.x, cur.y, 0f);
                lines[i].SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new Vector3(
                    cur.x + dirs[i].x * warningLineLength * t,
                    cur.y + dirs[i].y * warningLineLength * t, 0f);
                lines[i].SetPosition(1, tf.InverseTransformPoint(worldEnd));
            }
            yield return null;
        }

        foreach (var lr in lines)
        {
            if (lr != null) Destroy(lr.gameObject);
            _activeWarnLines.Remove(lr);
        }

        if (IsInvalid()) yield break;

        Owner.FlipToward(dirs[0]);
        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
        Vector2 firePos = GetSpawnPos();
        foreach (var dir in dirs)
            SpawnProjectile(firePos, dir, accelDuration, initialSpeedRatio);
    }

    private LineRenderer SpawnWarningLine(Vector2 start)
    {
        if (!useWarningLines || !WarningLineSettings.Enabled) return null;
        var go = new GameObject("WarnLine");
        go.transform.position = new Vector3(start.x, start.y, 0f);
        go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = false;
        lr.positionCount     = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.zero);
        lr.startWidth        = warningLineWidth;
        lr.endWidth          = warningLineWidth;
        lr.material          = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor        = warningColor;
        lr.endColor          = warningColor;
        lr.sortingOrder      = 10;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    // ── 방향 계산 ────────────────────────────────────────────────

    private static Vector2[] GetRadialDirs(int count, float offsetDeg)
    {
        count = Mathf.Max(1, count);
        float step = 360f / count;
        var dirs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float rad = (offsetDeg + step * i) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        return dirs;
    }

    private Vector2[] GetFanDirs(Vector2 spawnPos, int count, float fanAngle)
    {
        Vector2 toPlayer = Owner.PlayerTransform != null
            ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
            : Vector2.right;
        float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        count = Mathf.Max(1, count);
        float halfSpan = fanAngle * 0.5f;
        float step     = count > 1 ? fanAngle / (count - 1) : 0f;

        var dirs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float rad = (baseAngle - halfSpan + step * i) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        return dirs;
    }

    // ── Homing ───────────────────────────────────────────────────

    private IEnumerator HomingPhase(PhaseConfig cfg)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < cfg.횟수; i++)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
                if (IsInvalid()) yield break;

                // 경고: 라인이 플레이어를 실시간 추적
                if (cfg.경고시간 > 0f)
                {
                    Vector2 warnOrigin = GetSpawnPos();
                    var lr = SpawnWarningLine(warnOrigin);
                    if (lr != null) _activeWarnLines.Add(lr);

                    // 경고 시작 시점에 한 번만 플립 (매 프레임 플립하면 무한 진동)
                    {
                        Vector2 initDir = Owner.PlayerTransform != null
                            ? ((Vector2)Owner.PlayerTransform.position - GetSpawnPos()).normalized
                            : Vector2.right;
                        Owner.FlipToward(initDir);
                    }

                    float elapsed = 0f;
                    while (elapsed < cfg.경고시간)
                    {
                        if (IsInvalid()) { if (lr != null) { Destroy(lr.gameObject); _activeWarnLines.Remove(lr); } yield break; }
                        elapsed += Time.deltaTime;

                        Vector2 cur      = GetSpawnPos();
                        Vector2 toPlayer = Owner.PlayerTransform != null
                            ? ((Vector2)Owner.PlayerTransform.position - cur).normalized
                            : Vector2.right;

                        if (lr != null)
                        {
                            var tf = lr.transform;
                            tf.position = new Vector3(cur.x, cur.y, 0f);
                            lr.SetPosition(0, Vector3.zero);
                            // 라인 끝을 플레이어 위치로 직접 지정
                            Vector3 playerPos = Owner.PlayerTransform != null
                                ? Owner.PlayerTransform.position
                                : (Vector3)cur + (Vector3)(toPlayer * warningLineLength);
                            lr.SetPosition(1, tf.InverseTransformPoint(playerPos));
                        }
                        yield return null;
                    }

                    if (lr != null) { Destroy(lr.gameObject); _activeWarnLines.Remove(lr); }
                    if (IsInvalid()) yield break;
                }

                // 발사 시점의 플레이어 위치 고정
                Vector3 lockTarget = Owner.PlayerTransform != null
                    ? Owner.PlayerTransform.position
                    : (Vector3)GetSpawnPos();

                Vector2 fireDir = ((Vector2)(lockTarget - (Vector3)GetSpawnPos())).normalized;
                Owner.FlipToward(fireDir);
                Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

                SpawnHomingProjectile(GetSpawnPos(), lockTarget, cfg.아크높이, cfg.호밍속도);

                if (i < cfg.횟수 - 1)
                    yield return new WaitForSeconds(cfg.간격);
            }
            yield return WaitForAttackAnimEnd();
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    private void SpawnHomingProjectile(Vector2 spawnPos, Vector3 targetPos, float arcHeight, float speed)
    {
        float dmg = Owner.ContactDamage;
        var go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (!go.TryGetComponent<Projectile>(out var p)) return;

        p.InitParabolic(dmg, targetPos, speed, arcHeight);
        p.SetOwnerBoss(Boss);
        if (impactVfxPrefab != null)
            p.SetImpactVfx(impactVfxPrefab, impactVfxLifetime);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private Vector2 GetSpawnPos()
        => firePoint != null
            ? (Vector2)firePoint.position
            : Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

    private void SpawnProjectile(Vector2 pos, Vector2 dir, float accel = 0f, float initialSpeedRatio = 0.1f)
    {
        float dmg = Owner.ContactDamage;
        var go = Instantiate(projectilePrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<Projectile>(out var p))
        {
            // 이름 있는 인자 대신 전체 파라미터를 명시적으로 전달 (바인딩 오류 방지)
            p.Init(dmg, dir, projectileSpeed, 5f, false, 0f, 0f, 0f, 0f, 0f, 2f, 0f, 16f, accel, initialSpeedRatio);
            p.SetOwnerBoss(Boss);
        }
    }

    private bool IsInvalid() => Owner == null;

    private IEnumerator WaitForAttackAnimEnd()
    {
        var anim = Owner.EnemyAnimator;
        if (anim == null) yield break;

        int prevHash = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
        yield return null;

        // 전환이 시작되지 않았으면 TAttack 애니가 없는 것으로 간주하고 즉시 종료
        if (!anim.IsInTransition(0) &&
            anim.GetCurrentAnimatorStateInfo(0).fullPathHash == prevHash)
            yield break;

        float elapsed = 0f;
        while (elapsed < animWaitTimeout)
        {
            if (!anim.IsInTransition(0) &&
                anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ── 에디터 기즈모 ────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        foreach (var phase in phases)
        {
            if (phase.type != PatternType.부채꼴) continue;
            float halfSpan = phase.부채꼴각도 * 0.5f;
            UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.15f);
            UnityEditor.Handles.DrawSolidArc(origin, Vector3.forward,
                Quaternion.Euler(0f, 0f, -halfSpan) * Vector3.right,
                phase.부채꼴각도, warningLineLength);
            UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.7f);
            UnityEditor.Handles.DrawWireArc(origin, Vector3.forward,
                Quaternion.Euler(0f, 0f, -halfSpan) * Vector3.right,
                phase.부채꼴각도, warningLineLength);
        }
    }
#endif
}
