using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 보스 스킬: 내려찍기 2종.
///
///   BasicSlam   — 마커 표시 → 포물선 점프 → 착지 충격파 (기존 BossSlamSkill)
///   GroundPound — 소점프 쿵 착지 → 플레이어 부유 → 보스 돌진 (BossGroundPoundSkill 흡수)
/// </summary>
[System.Serializable]
public class BossSlamSkill : BossSkillBase
{
    public enum SlamType { BasicSlam, GroundPound }

    [SerializeField] private SlamType slamType = SlamType.BasicSlam;

    // ── BasicSlam ─────────────────────────────────────────────────
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private float slamDamage        = 30f;
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private float slamRadius        = 2.5f;
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private float jumpHeight        = 6f;
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private float jumpDuration      = 0.7f;
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private float markerWarningTime = 0.8f;
    [ShowIf("@slamType == SlamType.BasicSlam")]
    [SerializeField] private GameObject slamMarkerPrefab;
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private float      vfxLifetime = 1f;

    // ── GroundPound ───────────────────────────────────────────────
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float hopHeight      = 2.0f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float hopRiseTime    = 0.25f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float hopFallTime    = 0.18f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float shakeDuration  = 0.45f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float shakeMagnitude = 0.35f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float floatHeight      = 2.8f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float floatRiseTime    = 0.35f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float floatHangTime    = 0.25f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float floatFallTime    = 0.55f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float preChargeDelay   = 0.3f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float poundChargeSpeed = 20f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float chargeWarnDuration   = 0.5f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float chargeWarningLength  = 12f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float chargeWarningWidth   = 0.08f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private Color chargeWarningColor   = Color.red;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private Material chargeWarningMat;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private Color ghostColor       = new Color(1f, 0.15f, 0.15f, 0.45f);
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float ghostInterval    = 0.04f;
    [ShowIf("@slamType == SlamType.GroundPound")]
    [SerializeField] private float ghostLifetime    = 0.12f;

    // ── 런타임 상태 (GroundPound) ─────────────────────────────────
    private Vector3           _origScale;
    private bool              _isExecuting;
    private LineRenderer      _warnLine;
    private PlayerController  _floatingPlayer;
    private SlimeBobAnimation _floatingBob;

    protected override UniTask OnExecuteAsync(CancellationToken ct) => slamType switch
    {
        SlamType.BasicSlam   => BasicSlamAsync(ct),
        SlamType.GroundPound => GroundPoundAsync(ct),
        _                    => UniTask.CompletedTask
    };

    public override void OnInterrupted()
    {
        if (_isExecuting && Boss != null)
        {
            DG.Tweening.DOTween.Kill(Boss.transform, complete: false);
            Boss.transform.localScale = _origScale;
        }
        _isExecuting = false;
        RestoreFloatingPlayer();
        CleanupWarnLine();
    }

    // ── BasicSlam ─────────────────────────────────────────────────

    private async UniTask BasicSlamAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;

        Vector3 startPos   = Boss.transform.position;
        Vector3 slamTarget = PlayerT.position;
        Vector3 origScale  = Boss.transform.localScale;

        // 1) 마커 + 경고 대기
        var marker = slamMarkerPrefab != null
            ? Object.Instantiate(slamMarkerPrefab, slamTarget, Quaternion.identity) : null;

        await UniTask.Delay(System.TimeSpan.FromSeconds(markerWarningTime), cancellationToken: ct);

        // 2) 예비 찌부
        await SquishAsync(origScale, 0.18f, 1.35f, 0.5f, ct);

        // 3) 포물선 점프
        await ParabolicJumpAsync(startPos, slamTarget, origScale, ct);

        // 4) 착지
        Boss.transform.position   = slamTarget;
        Boss.transform.localScale = origScale;
        if (marker != null) Object.Destroy(marker);

        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Boss.Decal != null ? Boss.Decal.position : slamTarget;
            Object.Destroy(Object.Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        await LandSquishAsync(origScale, ct);

        // 5) 범위 피해
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Boss.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(slamDamage, sourceName: Boss.gameObject.name);

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
    }

    private async UniTask SquishAsync(Vector3 orig, float dur, float squishX, float squishY, CancellationToken ct)
    {
        Vector3 squished = new(orig.x * squishX, orig.y * squishY, orig.z);
        float half = dur * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            Boss.transform.localScale = Vector3.Lerp(orig, squished, Mathf.Clamp01(t / half));
            await UniTask.Yield(ct);
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            Boss.transform.localScale = Vector3.Lerp(squished, orig, Mathf.Clamp01(t / half));
            await UniTask.Yield(ct);
        }
        Boss.transform.localScale = orig;
    }

    private async UniTask ParabolicJumpAsync(Vector3 from, Vector3 to, Vector3 origScale, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += jumpHeight * Mathf.Sin(Mathf.PI * t);
            Boss.transform.position = pos;

            float stretchT = t < 0.5f ? t * 2f : (1f - t) * 2f;
            Boss.transform.localScale = new Vector3(
                origScale.x * Mathf.Lerp(1f, 0.7f, stretchT),
                origScale.y * Mathf.Lerp(1f, 1.4f, stretchT),
                origScale.z);

            await UniTask.Yield(ct);
        }
    }

    private async UniTask LandSquishAsync(Vector3 origScale, CancellationToken ct)
    {
        const float squishDur  = 0.08f;
        const float restoreDur = 0.18f;
        Vector3 squish = new(origScale.x * 1.45f, origScale.y * 0.45f, origScale.z);

        float t = 0f;
        while (t < squishDur)
        {
            t += Time.deltaTime;
            Boss.transform.localScale = Vector3.Lerp(origScale, squish, Mathf.Clamp01(t / squishDur));
            await UniTask.Yield(ct);
        }
        t = 0f;
        while (t < restoreDur)
        {
            t += Time.deltaTime;
            Boss.transform.localScale = Vector3.Lerp(squish, origScale, Mathf.Clamp01(t / restoreDur));
            await UniTask.Yield(ct);
        }
        Boss.transform.localScale = origScale;
    }

    // ── GroundPound ───────────────────────────────────────────────

    private async UniTask GroundPoundAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        _origScale   = Boss.transform.localScale;
        _isExecuting = true;

        try
        {
            Vector2 startPos = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
            Vector2 apexPos  = startPos + Vector2.up * hopHeight;

            // 1) 소점프
            await SquishAsync(_origScale, 0.14f, 1.3f, 0.55f, ct);
            await LerpPosAsync(startPos, apexPos, hopRiseTime, _origScale, 0.75f, 1.35f, ct);
            await LerpPosAsync(apexPos, startPos, hopFallTime, _origScale, 1.35f, 0.6f, ct);

            if (Boss.Rb != null) Boss.Rb.position = startPos;
            Boss.transform.position   = startPos;
            Boss.transform.localScale = _origScale;

            // 2) 착지 충격
            await SquishAsync(_origScale, 0.07f, 1.5f, 0.4f, ct);

            CameraFollow.Shared?.Shake(shakeDuration, shakeMagnitude);
            Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

            if (landingVfxPrefab != null)
            {
                Vector3 vfxPos = Boss.Decal != null ? Boss.Decal.position : (Vector3)startPos;
                Object.Destroy(Object.Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
            }

            // 3) 플레이어 부유
            var player = PlayerController.Instance;
            await FloatPlayerAsync(player, ct);

            // 4) 돌진 준비
            if (preChargeDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(preChargeDelay), cancellationToken: ct);

            Vector2 chargeOrigin = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
            Vector2 chargeDir    = player != null
                ? ((Vector2)player.transform.position - chargeOrigin).normalized
                : Vector2.right;
            if (chargeDir == Vector2.zero) chargeDir = Vector2.right;

            // 5) 경고 라인
            await WarnPhaseAsync(chargeDir, ct);
            if (Boss == null || ct.IsCancellationRequested) return;

            // 6) 돌진
            await ChargeAtPlayerAsync(poundChargeSpeed, chargeDir, ct);
        }
        finally
        {
            _isExecuting = false;
        }
    }

    private async UniTask LerpPosAsync(Vector2 from, Vector2 to, float dur,
        Vector3 origScale, float stretchX, float stretchY, CancellationToken ct)
    {
        Vector3 stretchedScale = new(origScale.x * stretchX, origScale.y * stretchY, origScale.z);
        float t = 0f;
        while (t < dur)
        {
            t += Time.fixedDeltaTime;
            float pct = Mathf.Clamp01(t / dur);
            Vector2 pos = Vector2.Lerp(from, to, pct);
            if (Boss.Rb != null) Boss.Rb.MovePosition(pos);
            else Boss.transform.position = pos;
            Boss.transform.localScale = Vector3.Lerp(origScale, stretchedScale, pct);
            await UniTask.WaitForFixedUpdate(ct);
        }
        if (Boss.Rb != null) Boss.Rb.MovePosition(to);
        else Boss.transform.position = to;
    }

    private async UniTask FloatPlayerAsync(PlayerController player, CancellationToken ct)
    {
        if (player == null) return;

        var playerRb = player.GetComponent<Rigidbody2D>();
        var bob      = player.GetComponentInChildren<SlimeBobAnimation>();
        _floatingPlayer = player;
        _floatingBob    = bob;
        player.InputLocked = true;
        if (bob != null) bob.enabled = false;

        Vector2 originWorld = playerRb != null ? playerRb.position : (Vector2)player.transform.position;
        var visualSlot = player.VisualSlot;
        Vector3 lockedScale = visualSlot != null ? visualSlot.localScale : Vector3.one;

        try
        {
            // 상승
            float t = 0f;
            while (t < floatRiseTime)
            {
                t += Time.fixedDeltaTime;
                float ease = 1f - (1f - Mathf.Clamp01(t / floatRiseTime)) *
                                  (1f - Mathf.Clamp01(t / floatRiseTime));
                Vector2 target = originWorld + Vector2.up * (floatHeight * ease);
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                await UniTask.WaitForFixedUpdate(ct);
            }

            Vector2 apex = originWorld + Vector2.up * floatHeight;
            if (playerRb != null) playerRb.MovePosition(apex);
            else player.transform.position = apex;

            // 체공
            await UniTask.Delay(System.TimeSpan.FromSeconds(floatHangTime), cancellationToken: ct);

            // 하강
            t = 0f;
            while (t < floatFallTime)
            {
                t += Time.fixedDeltaTime;
                float pct  = Mathf.Clamp01(t / floatFallTime);
                float ease = pct * pct;
                Vector2 target = originWorld + Vector2.up * (floatHeight * (1f - ease));
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                await UniTask.WaitForFixedUpdate(ct);
            }
        }
        finally
        {
            if (playerRb != null) playerRb.MovePosition(originWorld);
            else if (player != null) player.transform.position = originWorld;
            if (visualSlot != null) visualSlot.localScale = lockedScale;
            if (bob != null) { bob.ResetOrigin(); bob.enabled = true; }
            if (player != null) player.InputLocked = false;
            _floatingPlayer = null;
            _floatingBob    = null;
        }
    }

    private async UniTask WarnPhaseAsync(Vector2 dir, CancellationToken ct)
    {
        Vector2 origin = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
        Boss.FlipToward(dir);
        _warnLine = SpawnWarnLine(origin);

        float elapsed = 0f;
        while (elapsed < chargeWarnDuration)
        {
            if (Boss == null || ct.IsCancellationRequested) { CleanupWarnLine(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / chargeWarnDuration);

            if (_warnLine != null)
            {
                Vector2 cur = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
                _warnLine.transform.position = new Vector3(cur.x, cur.y, 0f);
                _warnLine.SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new(cur.x + dir.x * chargeWarningLength * t,
                                       cur.y + dir.y * chargeWarningLength * t, 0f);
                _warnLine.SetPosition(1, _warnLine.transform.InverseTransformPoint(worldEnd));
            }
            await UniTask.Yield(ct);
        }
        CleanupWarnLine();
    }

    private async UniTask ChargeAtPlayerAsync(float speed, Vector2 dir, CancellationToken ct)
    {
        Boss.FlipToward(dir);
        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TCharge);

        var   sr         = Boss.GetComponentInChildren<SpriteRenderer>();
        float ghostTimer = 0f;
        float elapsed    = 0f;
        float maxTime    = 3f;
        Vector2 pos = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;

        while (elapsed < maxTime)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
            if (Boss == null || ct.IsCancellationRequested) return;

            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f) { SpawnChargeGhost(sr); ghostTimer = ghostInterval; }

            Vector2 next = pos + dir * speed * Time.fixedDeltaTime;

            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
            {
                Boss.Rb.MovePosition(mb.ClosestPointOnBoundary(next));
                return;
            }

            if (mb == null && SpawnZone.Shared != null)
            {
                Vector2 c = SpawnZone.Shared.BoundsCenter;
                Vector2 s = SpawnZone.Shared.BoundsSize * 0.5f;
                if (next.x < c.x - s.x || next.x > c.x + s.x ||
                    next.y < c.y - s.y || next.y > c.y + s.y)
                {
                    next.x = Mathf.Clamp(next.x, c.x - s.x, c.x + s.x);
                    next.y = Mathf.Clamp(next.y, c.y - s.y, c.y + s.y);
                    Boss.Rb.MovePosition(next);
                    return;
                }
            }

            pos = next;
            Boss.Rb.MovePosition(next);
            Boss.FlipToward(dir);
            elapsed += Time.fixedDeltaTime;
            await UniTask.WaitForFixedUpdate(ct);
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    private void RestoreFloatingPlayer()
    {
        if (_floatingPlayer == null) return;
        _floatingPlayer.InputLocked = false;
        if (_floatingBob != null) { _floatingBob.ResetOrigin(); _floatingBob.enabled = true; }
        _floatingPlayer = null;
        _floatingBob    = null;
    }

    private void CleanupWarnLine()
    {
        if (_warnLine == null) return;
        Object.Destroy(_warnLine.gameObject);
        _warnLine = null;
    }

    private LineRenderer SpawnWarnLine(Vector2 start)
    {
        if (!WarningLineSettings.Enabled) return null;
        var go = new GameObject("WarnLine");
        go.transform.position = new Vector3(start.x, start.y, 0f);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = false;
        lr.positionCount     = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.zero);
        lr.startWidth        = chargeWarningWidth;
        lr.endWidth          = chargeWarningWidth;
        lr.material          = chargeWarningMat != null
            ? chargeWarningMat
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor        = chargeWarningColor;
        lr.endColor          = chargeWarningColor;
        lr.sortingOrder      = 10;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    private void SpawnChargeGhost(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return;
        var go   = new GameObject("ChargeGhost");
        go.transform.SetPositionAndRotation(sr.transform.position, sr.transform.rotation);
        go.transform.localScale = sr.transform.lossyScale;
        var ghost              = go.AddComponent<SpriteRenderer>();
        ghost.sprite           = sr.sprite;
        ghost.color            = ghostColor;
        ghost.sortingLayerID   = sr.sortingLayerID;
        ghost.sortingOrder     = sr.sortingOrder - 1;
        Object.Destroy(go, ghostLifetime);
    }
}
