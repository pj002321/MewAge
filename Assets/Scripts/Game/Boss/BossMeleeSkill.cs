using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 근접 공격 3종.
/// MeleeType으로 동작을 선택합니다.
///
///   ChaseAndSlash — 플레이어 범위 진입까지 추격 후 연속 베기 (BossComboSlashSkill 흡수)
///   DashCombo     — 플레이어 방향으로 돌진하며 연속 타격 (BossDashComboSkill 흡수)
///   FastChase     — 고속 추격 + 범위 내 자동 공격 (BossFastChaseSkill 흡수)
/// </summary>
[System.Serializable]
public class BossMeleeSkill : BossSkillBase
{
    public enum MeleeType { ChaseAndSlash, DashCombo, FastChase }

    [SerializeField] private MeleeType meleeType = MeleeType.ChaseAndSlash;

    // ── ChaseAndSlash ──────────────────────────────────────────────
    [Header("ChaseAndSlash")]
    [Tooltip("슬래시 범위 안에 들어올 때까지 추격하는 최대 시간 (초)")]
    [SerializeField] private float chaseTimeout  = 3f;
    [SerializeField] private int   hitCount      = 2;
    [SerializeField] private float slashDamage   = 12f;
    [Tooltip("보스 콜라이더 표면으로부터 추가 판정 거리")]
    [SerializeField] private float slashRange    = 0.3f;
    [SerializeField] private float preDelay      = 0.2f;
    [SerializeField] private float hitDelay      = 0.3f;
    [SerializeField] private float betweenDelay  = 0.45f;
    [SerializeField] private GameObject slashVfxPrefab;

    // ── DashCombo ─────────────────────────────────────────────────
    [Header("DashCombo")]
    [SerializeField] private float dashSpeed       = 10f;
    [SerializeField] private float dashDuration    = 0.6f;
    [SerializeField] private int   dashHitCount    = 3;
    [SerializeField] private float dashHitInterval = 0.18f;
    [SerializeField] private float dashHitRange    = 1.6f;
    [SerializeField] private float dashHitDamage   = 0f;   // 0 = ContactDamage
    [SerializeField] private float preDashDuration = 0.3f;
    [SerializeField] private float dashGhostInterval = 0.06f;

    // ── FastChase ─────────────────────────────────────────────────
    [Header("FastChase")]
    [SerializeField] private float chaseSpeed       = 16f;
    [SerializeField] private float chaseDuration    = 2.0f;
    [SerializeField] private float stopDistance     = 1.0f;
    [SerializeField] private float attackCooldown   = 0.6f;
    [SerializeField] private float animEventTimeout = 0.5f;
    [SerializeField] private GameObject attackVfx;
    [SerializeField] private float      attackVfxDuration = 0.3f;

    // ── 런타임 상태 ────────────────────────────────────────────────
    private Collider2D _col;
    private bool       _pendingHit;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        _col = boss.GetComponent<Collider2D>();
    }

    protected override UniTask OnExecuteAsync(CancellationToken ct) => meleeType switch
    {
        MeleeType.ChaseAndSlash => ChaseAndSlashAsync(ct),
        MeleeType.DashCombo     => DashComboAsync(ct),
        MeleeType.FastChase     => FastChaseAsync(ct),
        _                       => UniTask.CompletedTask
    };

    // ── ChaseAndSlash ──────────────────────────────────────────────

    private async UniTask ChaseAndSlashAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;

        await ChaseUntilInRangeAsync(ct);
        if (PlayerT == null || ct.IsCancellationRequested) return;

        Boss.EnemyAnimator?.SetBool(EnemyAnim.BMove, false);
        Boss.IsFiringSkill = true;
        try
        {
            if (preDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(preDelay), cancellationToken: ct);

            for (int i = 0; i < hitCount; i++)
            {
                if (PlayerT == null || ct.IsCancellationRequested) return;

                Vector2 toPlayer = (Vector2)PlayerT.position - (Vector2)Boss.transform.position;
                Boss.FlipToward(toPlayer);
                Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

                if (slashVfxPrefab != null)
                    Object.Instantiate(slashVfxPrefab, Boss.transform.position, Quaternion.identity);

                await UniTask.Delay(System.TimeSpan.FromSeconds(hitDelay), cancellationToken: ct);

                var player = PlayerController.Instance;
                if (player != null && SurfaceDistance((Vector2)player.transform.position) <= slashRange)
                    player.TakeDamage(slashDamage, sourceName: Boss.gameObject.name);

                if (i < hitCount - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(betweenDelay), cancellationToken: ct);
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    private async UniTask ChaseUntilInRangeAsync(CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < chaseTimeout)
        {
            if (ct.IsCancellationRequested || PlayerT == null) return;
            if (SurfaceDistance((Vector2)PlayerT.position) <= slashRange) return;

            Vector2 dir = ((Vector2)PlayerT.position - (Vector2)Boss.transform.position).normalized;
            Boss.MoveInDirection(dir, Boss.MoveSpeed);

            elapsed += Time.fixedDeltaTime;
            await UniTask.WaitForFixedUpdate(ct);
        }
    }

    private float SurfaceDistance(Vector2 playerPos)
    {
        return _col != null
            ? Vector2.Distance(_col.ClosestPoint(playerPos), playerPos)
            : Vector2.Distance((Vector2)Boss.transform.position, playerPos);
    }

    // ── DashCombo ─────────────────────────────────────────────────

    private async UniTask DashComboAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;

        float   dmg = dashHitDamage > 0f ? dashHitDamage : Boss.ContactDamage;
        Vector2 dir = (PlayerT.position - Boss.transform.position).normalized;

        // 돌진 준비: 빨간색 플래시
        var sr            = Boss.GetComponentInChildren<SpriteRenderer>();
        Color origColor   = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = Color.red;

        await UniTask.Delay(System.TimeSpan.FromSeconds(preDashDuration), cancellationToken: ct);

        if (sr != null) sr.color = origColor;
        if (ct.IsCancellationRequested) return;

        // 경계 계산
        Vector2 boundsMin, boundsMax;
        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out boundsMin, out boundsMax);
        }
        else
        {
            var spawner = EnemySpawner.Shared;
            Vector2 center = spawner != null ? spawner.AreaCenter : Vector2.zero;
            Vector2 half   = (spawner != null ? spawner.AreaSize : new Vector2(10f, 18f)) * 0.5f;
            boundsMin = center - half;
            boundsMax = center + half;
        }

        // 돌진
        float elapsed   = 0f;
        int   hits      = 0;
        float hitTimer  = 0f;
        float ghostTimer = 0f;

        while (elapsed < dashDuration && hits < dashHitCount)
        {
            if (ct.IsCancellationRequested) return;

            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f && sr != null) { SpawnGhost(sr); ghostTimer = dashGhostInterval; }

            Vector2 pos  = Boss.Rb.position;
            Vector2 next = pos + dir * dashSpeed * Time.fixedDeltaTime;

            if (next.x <= boundsMin.x || next.x >= boundsMax.x ||
                next.y <= boundsMin.y || next.y >= boundsMax.y)
            {
                next = new Vector2(Mathf.Clamp(next.x, boundsMin.x, boundsMax.x),
                                   Mathf.Clamp(next.y, boundsMin.y, boundsMax.y));
                Boss.Rb.MovePosition(next);
                Boss.FlipToward(dir);
                break;
            }

            Boss.MoveInDirection(dir, dashSpeed);

            float dist = PlayerT != null
                ? Vector2.Distance(Boss.Rb.position, PlayerT.position) : float.MaxValue;

            if (dist <= dashHitRange)
            {
                hitTimer -= Time.fixedDeltaTime;
                if (hitTimer <= 0f)
                {
                    Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                    PlayerController.Instance?.TakeDamage(dmg, sourceName: Boss.gameObject.name);
                    hits++;
                    hitTimer = dashHitInterval;
                }
            }

            elapsed += Time.fixedDeltaTime;
            await UniTask.WaitForFixedUpdate(ct);
        }

        // 남은 타격 소진
        while (hits < dashHitCount)
        {
            if (ct.IsCancellationRequested || PlayerT == null) break;
            if (Vector2.Distance(Boss.Rb.position, PlayerT.position) > dashHitRange) break;

            Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
            PlayerController.Instance?.TakeDamage(dmg, sourceName: Boss.gameObject.name);
            hits++;

            await UniTask.Delay(System.TimeSpan.FromSeconds(dashHitInterval), cancellationToken: ct);
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
    }

    private void SpawnGhost(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return;
        var go = new GameObject("DashGhost");
        go.transform.SetPositionAndRotation(Boss.transform.position, Boss.transform.rotation);
        go.transform.localScale = Boss.transform.localScale;
        var ghost              = go.AddComponent<SpriteRenderer>();
        ghost.sprite           = sr.sprite;
        ghost.color            = new Color(1f, 1f, 1f, 0.4f);
        ghost.sortingLayerID   = sr.sortingLayerID;
        ghost.sortingOrder     = sr.sortingOrder - 1;
        Object.Destroy(go, 0.15f);
    }

    // ── FastChase ─────────────────────────────────────────────────

    private async UniTask FastChaseAsync(CancellationToken ct)
    {
        float elapsed     = 0f;
        float attackTimer = 0f;
        _pendingHit = false;

        while (elapsed < chaseDuration)
        {
            if (ct.IsCancellationRequested) return;

            if (PlayerT != null)
            {
                float dist = Vector2.Distance(Boss.transform.position, PlayerT.position);

                if (dist > stopDistance && !Boss.IsFiringSkill)
                {
                    Vector2 dir = ((Vector2)PlayerT.position - (Vector2)Boss.transform.position).normalized;
                    Boss.MoveInDirection(dir, chaseSpeed);
                }
                else
                {
                    attackTimer -= Time.fixedDeltaTime;
                    if (attackTimer <= 0f)
                    {
                        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                        if (attackVfx != null) ShowAttackVfxAsync().Forget();

                        _pendingHit = true;
                        // 애니메이션 이벤트 미수신 대비 폴백
                        if (animEventTimeout > 0f) FastChaseHitFallbackAsync(ct).Forget();

                        attackTimer = attackCooldown;
                    }
                }
            }

            elapsed += Time.fixedDeltaTime;
            await UniTask.WaitForFixedUpdate(ct);
        }
    }

    /// <summary>BossController에서 OnFastChaseHit 애니메이션 이벤트를 포워딩합니다.</summary>
    internal void NotifyFastChaseHit()
    {
        if (!_pendingHit || Boss == null || PlayerT == null) return;
        _pendingHit = false;
        if (Vector2.Distance(Boss.transform.position, PlayerT.position) <= stopDistance)
            PlayerController.Instance?.TakeDamage(Boss.ContactDamage, sourceName: Boss.gameObject.name);
    }

    private async UniTaskVoid FastChaseHitFallbackAsync(CancellationToken ct)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(animEventTimeout), cancellationToken: ct)
                     .SuppressCancellationThrow();
        if (!_pendingHit || Boss == null || PlayerT == null) return;
        _pendingHit = false;
        if (Vector2.Distance(Boss.transform.position, PlayerT.position) <= stopDistance)
            PlayerController.Instance?.TakeDamage(Boss.ContactDamage, sourceName: Boss.gameObject.name);
    }

    private async UniTaskVoid ShowAttackVfxAsync()
    {
        if (attackVfx == null) return;
        attackVfx.SetActive(true);
        await UniTask.Delay(System.TimeSpan.FromSeconds(attackVfxDuration));
        if (attackVfx != null) attackVfx.SetActive(false);
    }
}
