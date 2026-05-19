using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 보스 스킬: 데이터 드리븐 투사체 패턴 (통합판).
///
/// phases 리스트에 페이즈를 순서대로 추가하면 한 번의 스킬 발동에서 모두 실행됩니다.
/// BossController의 PatternLoop가 다음 스킬 사이클을 담당합니다.
///
/// 지원 타입:
///   Aimed   — 플레이어 조준 연사
///   Radial  — N방향 동시 방사형 버스트
///   Fan     — 플레이어 방향 기준 부채꼴 버스트
///   Homing  — 포물선 곡선으로 플레이어 위치에 꽂히는 호밍탄
///   Volley  — 낙탄 살포 (마커 + MortarShell 포물선) — BossVolleySkill 흡수
///   Boulder — 반사하는 큰 돌 굴리기 — BossRollingBoulderSkill 흡수
/// </summary>
[System.Serializable]
public class BossProjectileSkill : BossSkillBase
{
    public enum ProjectileType { Aimed, Radial, Fan, Homing, Volley, Boulder }

    [System.Serializable]
    public class PhaseConfig
    {
        public ProjectileType type = ProjectileType.Aimed;

        [Tooltip("경고 라인/마커가 표시되는 시간 (초)")]
        public float warningTime = 0.4f;

        // ── 공통 (조준/방사형/부채꼴/호밍/바위) ─────────────────────
        [Tooltip("발사 횟수")]
        public int   count    = 1;
        [HideIf("type", ProjectileType.Fan)]
        [Tooltip("발사 사이 간격 (초). Fan은 동시 발사이므로 미사용")]
        public float interval = 0.5f;

        // ── 방사형·부채꼴 공통 ────────────────────────────────────
        [ShowIf("@type == ProjectileType.Radial || type == ProjectileType.Fan")]
        [Tooltip("동시 발사 방향 수")]
        public int dirCount = 8;

        // ── 방사형 전용 ───────────────────────────────────────────
        [ShowIf("type", ProjectileType.Radial)]
        [Tooltip("첫 방향 각도 오프셋 (도)")]
        public float radialAngleOffset = 0f;

        // ── 부채꼴 전용 ───────────────────────────────────────────
        [ShowIf("type", ProjectileType.Fan)]
        [Tooltip("부채꼴 전체 각도 범위 (도)")]
        public float fanAngle = 120f;

        // ── 호밍 전용 ─────────────────────────────────────────────
        [ShowIf("type", ProjectileType.Homing)]
        [Tooltip("포물선 호 높이. 양수=위로 볼록")]
        public float arcHeight = 3f;
        [ShowIf("type", ProjectileType.Homing)]
        public float homingSpeed = 9f;

        // ── 가속 (Aimed/Radial/Fan) ────────────────────────────
        [HideIf("@type == ProjectileType.Homing || type == ProjectileType.Volley || type == ProjectileType.Boulder")]
        [Tooltip("0=즉시. 양수=해당 초 동안 최고속도까지 가속")]
        public float accelTime = 0f;
        [ShowIf("@accelTime > 0f && type != ProjectileType.Homing && type != ProjectileType.Volley && type != ProjectileType.Boulder")]
        [Range(0.01f, 0.5f)]
        public float initialSpeedRatio = 0.05f;

        // ── Volley 전용 ───────────────────────────────────────────
        [ShowIf("type", ProjectileType.Volley)]
        public int   volleyPointCount   = 6;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleySpreadRadius = 1.8f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyHangTime     = 1.0f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyBurstInterval = 0.12f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyHitRadius    = 0.8f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyHitDamage    = 0f;  // 0 = ContactDamage
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyShellArcHeight = 3f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyShakeDuration  = 0.2f;
        [ShowIf("type", ProjectileType.Volley)]
        public float volleyShakeMagnitude = 0.25f;
        [ShowIf("type", ProjectileType.Volley)]
        public GameObject volleyMarkerPrefab;
        [ShowIf("type", ProjectileType.Volley)]
        public GameObject volleyShellPrefab;
        [ShowIf("type", ProjectileType.Volley)]
        public GameObject volleyImpactVfx;

        // ── Boulder 전용 ──────────────────────────────────────────
        [ShowIf("type", ProjectileType.Boulder)]
        public float boulderSpeed    = 3.5f;
        [ShowIf("type", ProjectileType.Boulder)]
        public float boulderLifetime = 6f;
        [ShowIf("type", ProjectileType.Boulder)]
        public int   boulderMaxBounces = 3;
        [ShowIf("type", ProjectileType.Boulder)]
        public float boulderRotateSpeed = 270f;
        [ShowIf("type", ProjectileType.Boulder)]
        public float boulderRollInterval = 0.8f;
    }

    [Header("투사체")]
    [ProjectileID]
    [SerializeField] private string      projectileID;
    [System.NonSerialized] private GameObject _projectilePrefab;
    [SerializeField] private float       projectileSpeed = 7f;
    [Tooltip("null이면 보스 중심에서 발사")]
    [SerializeField] private Transform  firePoint;

    [Header("경고 라인")]
    [SerializeField] private float    warningLineLength = 10f;
    [SerializeField] private float    warningLineWidth  = 0.06f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("null이면 Sprites/Default 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("호밍 착탄 VFX")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float      impactVfxLifetime = 1f;

    [Header("페이즈 목록 (순서대로 실행)")]
    [SerializeField] private List<PhaseConfig> phases = new();

    private readonly List<LineRenderer> _activeWarnLines = new();

    public override void Init(BossController boss)
    {
        base.Init(boss);
        if (!string.IsNullOrEmpty(projectileID))
            _projectilePrefab = Resources.Load<GameObject>(projectileID);
    }

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        foreach (var phase in phases)
        {
            if (ct.IsCancellationRequested) return;
            await RunPhaseAsync(phase, ct);
        }
    }

    private UniTask RunPhaseAsync(PhaseConfig cfg, CancellationToken ct) => cfg.type switch
    {
        ProjectileType.Aimed  or
        ProjectileType.Radial or
        ProjectileType.Fan    => AimedBurstPhaseAsync(cfg, ct),
        ProjectileType.Homing => HomingPhaseAsync(cfg, ct),
        ProjectileType.Volley => VolleyPhaseAsync(cfg, ct),
        ProjectileType.Boulder => BoulderPhaseAsync(cfg, ct),
        _                     => UniTask.CompletedTask
    };

    // ── Aimed / Radial / Fan ─────────────────────────────────────

    private async UniTask AimedBurstPhaseAsync(PhaseConfig cfg, CancellationToken ct)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < cfg.count; i++)
            {
                if (ct.IsCancellationRequested) return;

                Vector2   spawnPos = GetSpawnPos();
                Vector2[] dirs     = GetDirs(cfg, spawnPos);

                await WarnThenFireAsync(spawnPos, dirs, cfg.warningTime, cfg.accelTime, cfg.initialSpeedRatio, ct);
                if (ct.IsCancellationRequested) return;

                if (i < cfg.count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.interval), cancellationToken: ct);
            }
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    private Vector2[] GetDirs(PhaseConfig cfg, Vector2 spawnPos)
    {
        return cfg.type switch
        {
            ProjectileType.Radial => GetRadialDirs(cfg.dirCount, cfg.radialAngleOffset),
            ProjectileType.Fan    => GetFanDirs(spawnPos, cfg.dirCount, cfg.fanAngle),
            _                     => new[] { GetAimedDir(spawnPos) }
        };
    }

    private Vector2 GetAimedDir(Vector2 spawnPos)
    {
        Vector2 dir = PlayerT != null
            ? ((Vector2)PlayerT.position - spawnPos).normalized
            : Vector2.right;
        return dir == Vector2.zero ? Vector2.right : dir;
    }

    // ── Homing ───────────────────────────────────────────────────

    private async UniTask HomingPhaseAsync(PhaseConfig cfg, CancellationToken ct)
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < cfg.count; i++)
            {
                if (ct.IsCancellationRequested) return;

                Vector2 spawnPos = GetSpawnPos();

                if (cfg.warningTime > 0f)
                {
                    var lr = SpawnWarningLine(spawnPos);
                    if (lr != null) _activeWarnLines.Add(lr);

                    Vector2 initDir = GetAimedDir(spawnPos);
                    Boss.FlipToward(initDir);

                    float elapsed = 0f;
                    while (elapsed < cfg.warningTime)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            if (lr != null) { Object.Destroy(lr.gameObject); _activeWarnLines.Remove(lr); }
                            return;
                        }
                        elapsed += Time.deltaTime;
                        UpdateHomingWarnLine(lr, spawnPos);
                        await UniTask.Yield(ct);
                    }
                    if (lr != null) { Object.Destroy(lr.gameObject); _activeWarnLines.Remove(lr); }
                }

                if (ct.IsCancellationRequested || PlayerT == null) return;

                Vector3 lockTarget = PlayerT.position;
                Vector2 fireDir    = ((Vector2)(lockTarget - (Vector3)GetSpawnPos())).normalized;
                Boss.FlipToward(fireDir);
                Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

                var go = Object.Instantiate(_projectilePrefab, GetSpawnPos(), Quaternion.identity);
                if (go.TryGetComponent<Projectile>(out var p))
                {
                    p.InitParabolic(Boss.ContactDamage, lockTarget, cfg.homingSpeed, cfg.arcHeight);
                    p.SetOwnerBoss(Boss);
                    if (impactVfxPrefab != null) p.SetImpactVfx(impactVfxPrefab, impactVfxLifetime);
                }

                if (i < cfg.count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.interval), cancellationToken: ct);
            }
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    private void UpdateHomingWarnLine(LineRenderer lr, Vector2 origin)
    {
        if (lr == null || PlayerT == null) return;
        Vector2 cur      = GetSpawnPos();
        Vector2 toPlayer = ((Vector2)PlayerT.position - cur).normalized;
        lr.transform.position = new Vector3(cur.x, cur.y, 0f);
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, lr.transform.InverseTransformPoint(PlayerT.position));
    }

    // ── Volley ───────────────────────────────────────────────────

    private async UniTask VolleyPhaseAsync(PhaseConfig cfg, CancellationToken ct)
    {
        if (PlayerT == null) return;

        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (cfg.warningTime > 0f)
            await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.warningTime), cancellationToken: ct);

        if (ct.IsCancellationRequested || PlayerT == null) return;

        Vector2 playerPos  = PlayerT.position;
        float   startAngle = Random.Range(0f, 360f);
        float   step       = 360f / Mathf.Max(1, cfg.volleyPointCount);
        float   finalDmg   = cfg.volleyHitDamage > 0f ? cfg.volleyHitDamage : Boss.ContactDamage;

        var positions = new Vector2[cfg.volleyPointCount];
        var markers   = new ImpactMarker[cfg.volleyPointCount];

        for (int i = 0; i < cfg.volleyPointCount; i++)
        {
            float rad    = (startAngle + step * i) * Mathf.Deg2Rad;
            positions[i] = playerPos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * cfg.volleySpreadRadius;

            if (cfg.volleyMarkerPrefab != null)
            {
                var obj = Object.Instantiate(cfg.volleyMarkerPrefab, positions[i], Quaternion.identity);
                markers[i] = obj.GetComponent<ImpactMarker>();
            }

            if (cfg.volleyShellPrefab != null && Boss.Rb != null)
            {
                var shell = Object.Instantiate(cfg.volleyShellPrefab,
                    (Vector3)Boss.Rb.position, Quaternion.identity);
                shell.GetComponent<MortarShell>()?.Init(
                    Boss.Rb.position, positions[i],
                    cfg.volleyHangTime, cfg.volleyShellArcHeight, finalDmg, cfg.volleyHitRadius);
            }
        }

        // 포탄이 날아가는 동안 대기
        await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.volleyHangTime), cancellationToken: ct);

        // 순차 충격 연출
        for (int i = 0; i < cfg.volleyPointCount; i++)
        {
            if (ct.IsCancellationRequested) break;
            if (markers[i] != null) markers[i].Dismiss();
            if (cfg.volleyImpactVfx != null) Object.Instantiate(cfg.volleyImpactVfx, positions[i], Quaternion.identity);
            CameraFollow.Shared?.Shake(cfg.volleyShakeDuration, cfg.volleyShakeMagnitude);
            await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.volleyBurstInterval), cancellationToken: ct);
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f), cancellationToken: ct);
    }

    // ── Boulder ───────────────────────────────────────────────────

    private async UniTask BoulderPhaseAsync(PhaseConfig cfg, CancellationToken ct)
    {
        if (PlayerT == null || _projectilePrefab == null) return;

        Boss.IsFiringSkill = true;
        try
        {
            Vector2 spawnPos = GetSpawnPos();
            // 보스가 이동 폴리곤 밖에 있으면 경계 안쪽으로 클램프
            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(spawnPos))
                spawnPos = mb.ClosestPointOnBoundary(spawnPos);
            Vector2 dir = GetAimedDir(spawnPos);

            // 경고 라인
            if (cfg.warningTime > 0f)
            {
                var lr = SpawnWarningLine(spawnPos);
                if (lr != null) _activeWarnLines.Add(lr);

                float elapsed = 0f;
                while (elapsed < cfg.warningTime)
                {
                    if (ct.IsCancellationRequested)
                    {
                        if (lr != null) { Object.Destroy(lr.gameObject); _activeWarnLines.Remove(lr); }
                        return;
                    }
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / cfg.warningTime);
                    if (lr != null)
                    {
                        Vector2 cur = GetSpawnPos();
                        lr.transform.position = new Vector3(cur.x, cur.y, 0f);
                        lr.SetPosition(0, Vector3.zero);
                        Vector3 worldEnd = new(cur.x + dir.x * warningLineLength * t,
                                               cur.y + dir.y * warningLineLength * t, 0f);
                        lr.SetPosition(1, lr.transform.InverseTransformPoint(worldEnd));
                    }
                    await UniTask.Yield(ct);
                }
                if (lr != null) { Object.Destroy(lr.gameObject); _activeWarnLines.Remove(lr); }
            }

            if (ct.IsCancellationRequested) return;

            // 바위 굴리기 (rollCount = count)
            for (int i = 0; i < cfg.count; i++)
            {
                if (ct.IsCancellationRequested) return;

                Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                SpawnBoulder(cfg, dir, spawnPos);

                if (i < cfg.count - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(cfg.boulderRollInterval), cancellationToken: ct);
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f), cancellationToken: ct);
        }
        finally { if (Boss != null) Boss.IsFiringSkill = false; }
    }

    private void SpawnBoulder(PhaseConfig cfg, Vector2 dir, Vector2 spawnPos)
    {
        var go = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        if (!go.TryGetComponent<Projectile>(out var p)) return;

        p.Init(Boss.ContactDamage, dir, cfg.boulderSpeed, cfg.boulderLifetime);
        p.SetBounce(cfg.boulderMaxBounces, cfg.boulderRotateSpeed);
        p.SetOwnerBoss(Boss);
    }

    // ── 경고 라인 → 발사 ────────────────────────────────────────

    private async UniTask WarnThenFireAsync(Vector2 spawnPos, Vector2[] dirs, float warnDuration,
        float accelTime, float initialSpeedRatio, CancellationToken ct)
    {
        var lines = new LineRenderer[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            lines[i] = SpawnWarningLine(spawnPos);
            if (lines[i] != null) _activeWarnLines.Add(lines[i]);
        }

        float elapsed = 0f;
        while (elapsed < warnDuration)
        {
            if (ct.IsCancellationRequested) { CleanupLines(lines); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warnDuration);
            Vector2 cur = GetSpawnPos();
            Boss.FlipToward(dirs[0]);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                var tf = lines[i].transform;
                tf.position = new Vector3(cur.x, cur.y, 0f);
                lines[i].SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new(cur.x + dirs[i].x * warningLineLength * t,
                                       cur.y + dirs[i].y * warningLineLength * t, 0f);
                lines[i].SetPosition(1, tf.InverseTransformPoint(worldEnd));
            }
            await UniTask.Yield(ct);
        }

        CleanupLines(lines);
        if (ct.IsCancellationRequested) return;

        Boss.FlipToward(dirs[0]);
        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
        Vector2 firePos = GetSpawnPos();
        foreach (var dir in dirs)
            SpawnProjectile(firePos, dir, accelTime, initialSpeedRatio);
    }

    private void CleanupLines(LineRenderer[] lines)
    {
        foreach (var lr in lines)
        {
            if (lr == null) continue;
            Object.Destroy(lr.gameObject);
            _activeWarnLines.Remove(lr);
        }
    }

    // ── 방향 계산 ────────────────────────────────────────────────

    private static Vector2[] GetRadialDirs(int count, float offsetDeg)
    {
        count = Mathf.Max(1, count);
        float step = 360f / count;
        var dirs   = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float rad = (offsetDeg + step * i) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        return dirs;
    }

    private Vector2[] GetFanDirs(Vector2 spawnPos, int count, float fanAngle)
    {
        Vector2 toPlayer  = GetAimedDir(spawnPos);
        float   baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
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
            : Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;

    private void SpawnProjectile(Vector2 pos, Vector2 dir, float accel = 0f, float initialRatio = 0.1f)
    {
        var go = Object.Instantiate(_projectilePrefab, pos, Quaternion.identity);
        if (!go.TryGetComponent<Projectile>(out var p)) return;
        p.Init(Boss.ContactDamage, dir, projectileSpeed, 5f,
               acceleration: accel, initialSpeedRatio: initialRatio);
        p.SetOwnerBoss(Boss);
    }

    private LineRenderer SpawnWarningLine(Vector2 start)
    {
        if (!WarningLineSettings.Enabled) return null;
        var go = new GameObject("WarnLine");
        go.transform.position = new Vector3(start.x, start.y, 0f);
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

    public override void OnInterrupted()
    {
        foreach (var lr in _activeWarnLines)
            if (lr != null) Object.Destroy(lr.gameObject);
        _activeWarnLines.Clear();
        if (Boss != null) Boss.IsFiringSkill = false;
    }
}
