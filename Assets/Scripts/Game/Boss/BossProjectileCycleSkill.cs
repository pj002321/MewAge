using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 스킬: 데이터-드리븐 투사체 사이클 패턴.
///
/// Inspector의 phases 리스트에 페이즈를 원하는 순서로 추가하면
/// 그 순서대로 무한 반복합니다. 새 발사 패턴이 필요해도 클래스를
/// 추가할 필요 없이 PhaseConfig 하나만 추가하면 됩니다.
///
/// 지원 패턴 타입:
///   AimedRepeat — 플레이어 조준 N회 연사
///   Radial      — N방향 방사형 버스트 (회전 오프셋 지원)
///   Fan         — 플레이어 방향 부채꼴 버스트
///   Chase       — 발사 없이 플레이어 추격
/// </summary>
public class BossProjectileCycleSkill : BossSkillBase
{
    public enum PatternType
    {
        조준연사,   // 플레이어 방향으로 N회 한 발씩 발사
        방사형,     // N방향 동시 방사형 버스트
        부채꼴,     // 플레이어 방향 기준 부채꼴 버스트
        추격,       // 발사 없이 플레이어를 N초 추격
    }

    [System.Serializable]
    public class PhaseConfig
    {
        public string      label = "페이즈";
        public PatternType type  = PatternType.조준연사;

        [Header("경고")]
        [Tooltip("투사체 발사 전 경고 라인이 표시되는 시간 (초)")]
        public float 경고시간 = 0.4f;

        [Header("조준연사 설정 (조준연사 타입만 사용)")]
        [Tooltip("플레이어 방향으로 총 몇 발 쏠지")]
        public int   연사횟수   = 1;
        [Tooltip("각 발사 사이 대기 시간 (초)")]
        public float 발사간격   = 0.5f;

        [Header("방사형 · 부채꼴 설정 (해당 타입만 사용)")]
        [Tooltip("동시에 발사할 방향 수. 방사형=8이면 45° 간격 8방향, 12이면 30° 간격 12방향")]
        public int   방향수     = 8;
        [Tooltip("이 버스트 세트를 몇 번 반복할지 (1이면 한 번만)")]
        public int   반복횟수   = 1;
        [Tooltip("버스트 반복 사이 대기 시간 (초)")]
        public float 반복간격   = 0.6f;

        [Header("방사형 전용")]
        [Tooltip("첫 번째 방향의 각도 오프셋 (도). 0이면 오른쪽(→) 부터 시작")]
        public float 각도오프셋 = 0f;

        [Header("부채꼴 전용")]
        [Tooltip("부채꼴 전체 각도 범위 (도). 120이면 플레이어 방향 기준 ±60°")]
        public float 부채꼴각도 = 120f;

        [Header("추격 설정 (추격 타입만 사용)")]
        [Tooltip("플레이어를 추격할 시간 (초)")]
        public float 추격시간   = 2f;
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
    [SerializeField] private float    warningLineLength = 10f;
    [SerializeField] private float    warningLineWidth  = 0.06f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("null이면 Sprites/Default 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("추격 설정")]
    [SerializeField] private float chaseStopDistance = 1.8f;

    [Header("애니메이션")]
    [SerializeField] private string attackTrigger = "tAttack";

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

    protected override IEnumerator OnExecute() { yield break; }

    // ── 메인 루프 ────────────────────────────────────────────────

    private IEnumerator CycleLoop()
    {
        yield return null;
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
                yield return new WaitUntil(() => GameManager.Shared == null || !GameManager.Shared.IsGameOver);

            if (IsInvalid()) yield break;

            foreach (var phase in phases)
            {
                if (IsInvalid()) yield break;
                yield return RunPhase(phase);
            }
        }
    }

    // ── 페이즈 디스패치 ──────────────────────────────────────────

    private IEnumerator RunPhase(PhaseConfig cfg)
    {
        switch (cfg.type)
        {
            case PatternType.추격:   yield return ChasePhase(cfg.추격시간); break;
            case PatternType.조준연사: yield return AimedRepeatPhase(cfg);       break;
            case PatternType.방사형:
            case PatternType.부채꼴: yield return BurstPhase(cfg);               break;
        }
    }

    // ── AimedRepeat ──────────────────────────────────────────────

    private IEnumerator AimedRepeatPhase(PhaseConfig cfg)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < cfg.연사횟수; i++)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                Vector2 dir = Owner.PlayerTransform != null
                    ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
                    : Vector2.right;

                yield return WarnThenFire(spawnPos, new[] { dir }, cfg.경고시간);
                if (IsInvalid()) yield break;

                if (i < cfg.연사횟수 - 1)
                    yield return new WaitForSeconds(cfg.발사간격);
            }
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    // ── Radial / Fan 버스트 ──────────────────────────────────────

    private IEnumerator BurstPhase(PhaseConfig cfg)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int r = 0; r < cfg.반복횟수; r++)
            {
                if (IsInvalid()) yield break;

                Vector2 spawnPos = GetSpawnPos();
                Vector2[] dirs = cfg.type == PatternType.방사형
                    ? GetRadialDirs(cfg.방향수, cfg.각도오프셋)
                    : GetFanDirs(spawnPos, cfg.방향수, cfg.부채꼴각도);

                yield return WarnThenFire(spawnPos, dirs, cfg.경고시간);
                if (IsInvalid()) yield break;

                if (r < cfg.반복횟수 - 1)
                    yield return new WaitForSeconds(cfg.반복간격);
            }
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    // ── Chase ────────────────────────────────────────────────────

    private IEnumerator ChasePhase(float duration)
    {
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
                Owner.FlipToward(((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 경고 라인 → 발사 ─────────────────────────────────────────

    private IEnumerator WarnThenFire(Vector2 spawnPos, Vector2[] dirs, float warnDuration)
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

        Owner.EnemyAnimator?.SetTrigger(attackTrigger);
        Vector2 firePos = GetSpawnPos();
        foreach (var dir in dirs)
            SpawnProjectile(firePos, dir);
    }

    private LineRenderer SpawnWarningLine(Vector2 start)
    {
        if (!WarningLineSettings.Enabled) return null;
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

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private Vector2 GetSpawnPos()
        => firePoint != null
            ? (Vector2)firePoint.position
            : Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

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

    private bool IsInvalid() => Owner == null;

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
