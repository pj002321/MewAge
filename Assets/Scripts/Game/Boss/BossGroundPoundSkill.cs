using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 그라운드 파운드.
/// 보스 소점프 → 쿵 착지(카메라 흔들) → 플레이어 공중 부유(이동 불가) → 보스 돌진.
/// </summary>
public class BossGroundPoundSkill : BossSkillBase
{
    [Header("점프")]
    [SerializeField] private float hopHeight      = 2.0f;
    [SerializeField] private float hopRiseTime    = 0.25f;
    [SerializeField] private float hopFallTime    = 0.18f;

    [Header("착지 충격")]
    [SerializeField] private float  shakeDuration  = 0.45f;
    [SerializeField] private float  shakeMagnitude = 0.35f;
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private float      vfxLifetime = 1f;

    [Header("플레이어 부유")]
    [SerializeField] private float floatHeight    = 2.8f;
    [SerializeField] private float floatRiseTime  = 0.35f;
    [SerializeField] private float floatHangTime  = 0.25f;
    [SerializeField] private float floatFallTime  = 0.55f;

    [Header("돌진")]
    [SerializeField] private float preChargeDelay = 0.3f;
    [SerializeField] private float chargeSpeed    = 20f;
    [SerializeField] private float chargeMaxTime  = 3f;

    [Header("돌진 경고 라인")]
    [SerializeField] private float    chargeWarnDuration = 0.5f;
    [SerializeField] private float    warningLineLength  = 12f;
    [SerializeField] private float    warningLineWidth   = 0.08f;
    [SerializeField] private Color    warningLineColor   = Color.red;
    [Tooltip("null이면 Sprites/Default 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("돌진 고스트 트레일")]
    [SerializeField] private Color ghostColor    = new Color(1f, 0.15f, 0.15f, 0.45f);
    [SerializeField] private float ghostInterval = 0.04f;
    [SerializeField] private float ghostLifetime = 0.12f;

    private LineRenderer _warnLine;

    private void OnDisable()
    {
        if (_warnLine != null) { Destroy(_warnLine.gameObject); _warnLine = null; }
    }

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

        float speed = chargeSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);

        // ── 1. 보스 소점프 ──────────────────────────────────────
        Vector3 origScale  = Owner.transform.localScale;
        Vector3 startPos   = Owner.transform.position;
        Vector3 apexPos    = startPos + Vector3.up * hopHeight;

        // 찌부 예비동작
        yield return StartCoroutine(Squish(origScale, 0.14f, 1.3f, 0.55f));

        // 상승
        yield return StartCoroutine(LerpPos(startPos, apexPos, hopRiseTime,
            origScale, stretchX: 0.75f, stretchY: 1.35f));

        // 하강 (빠르게)
        yield return StartCoroutine(LerpPos(apexPos, startPos, hopFallTime,
            origScale, stretchX: 1.35f, stretchY: 0.6f));

        Owner.transform.position   = startPos;
        Owner.transform.localScale = origScale;

        // ── 2. 착지 충격 ───────────────────────────────────────
        yield return StartCoroutine(Squish(origScale, 0.07f, 1.5f, 0.4f));

        CameraFollow.Shared?.Shake(shakeDuration, shakeMagnitude);

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Owner.Decal != null ? Owner.Decal.position : startPos;
            Destroy(Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        // ── 3. 플레이어 부유 (완전히 끝날 때까지 대기) ────────────
        var player = PlayerController.Instance;
        yield return StartCoroutine(FloatPlayer(player));

        // ── 4. 돌진 전 대기 ────────────────────────────────────
        if (preChargeDelay > 0f)
            yield return new WaitForSeconds(preChargeDelay);

        // 돌진 방향 계산 (경고 라인과 실제 돌진 공유)
        Vector2 chargeOrigin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Vector2 chargeDir    = player != null
            ? ((Vector2)player.transform.position - chargeOrigin).normalized
            : Vector2.right;
        if (chargeDir == Vector2.zero) chargeDir = Vector2.right;

        // ── 5. 돌진 경고 라인 ──────────────────────────────────
        yield return StartCoroutine(WarnPhase(chargeDir));
        if (Owner == null) yield break;

        // ── 6. 보스 돌진 ───────────────────────────────────────
        yield return StartCoroutine(ChargeAtPlayer(player, speed, chargeDir));
    }

    // ── 보스 돌진 ──────────────────────────────────────────────────

    private IEnumerator ChargeAtPlayer(PlayerController player, float speed, Vector2 dir)
    {
        Owner.FlipToward(dir);
        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TCharge);

        var   sr         = Owner.GetComponentInChildren<SpriteRenderer>();
        float ghostTimer = 0f;
        float elapsed    = 0f;
        Vector2 pos = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

        while (elapsed < chargeMaxTime)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
            if (Owner == null) yield break;

            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f) { SpawnGhost(sr); ghostTimer = ghostInterval; }

            Vector2 next = pos + dir * speed * Time.fixedDeltaTime;

            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
            {
                Vector2 clamped = mb.ClosestPointOnBoundary(next);
                Owner.Rb.MovePosition(clamped);
                yield break;
            }

            pos = next;
            Owner.Rb.MovePosition(next);
            Owner.FlipToward(dir);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 경고 라인 ──────────────────────────────────────────────────

    private IEnumerator WarnPhase(Vector2 dir)
    {
        Vector2 origin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Owner.FlipToward(dir);

        var lr = SpawnWarningLine(origin);
        _warnLine = lr;

        float elapsed = 0f;
        while (elapsed < chargeWarnDuration)
        {
            if (Owner == null) { CleanupWarnLine(); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / chargeWarnDuration);

            if (lr != null)
            {
                Vector2 cur = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
                var tf = lr.transform;
                tf.position = new Vector3(cur.x, cur.y, 0f);
                lr.SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new Vector3(
                    cur.x + dir.x * warningLineLength * t,
                    cur.y + dir.y * warningLineLength * t, 0f);
                lr.SetPosition(1, tf.InverseTransformPoint(worldEnd));
            }
            yield return null;
        }

        CleanupWarnLine();
    }

    private void CleanupWarnLine()
    {
        if (_warnLine == null) return;
        Destroy(_warnLine.gameObject);
        _warnLine = null;
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
        lr.startColor        = warningLineColor;
        lr.endColor          = warningLineColor;
        lr.sortingOrder      = 10;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    // ── 고스트 트레일 ──────────────────────────────────────────────

    private void SpawnGhost(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return;
        var go    = new GameObject("ChargeGhost");
        go.transform.SetPositionAndRotation(sr.transform.position, sr.transform.rotation);
        go.transform.localScale = sr.transform.lossyScale;
        var ghost            = go.AddComponent<SpriteRenderer>();
        ghost.sprite         = sr.sprite;
        ghost.color          = ghostColor;
        ghost.sortingLayerID = sr.sortingLayerID;
        ghost.sortingOrder   = sr.sortingOrder - 1;
        ghost.flipX          = sr.flipX;
        ghost.flipY          = sr.flipY;
        Destroy(go, ghostLifetime);
    }

    // ── 플레이어 공중 부유 ──────────────────────────────────────────

    private IEnumerator FloatPlayer(PlayerController player)
    {
        if (player == null) yield break;

        var playerRb  = player.GetComponent<Rigidbody2D>();
        var bob       = player.GetComponentInChildren<SlimeBobAnimation>();
        player.InputLocked = true;
        if (bob != null) bob.enabled = false;

        Vector2 originWorld = playerRb != null ? playerRb.position : (Vector2)player.transform.position;

        // 비주얼 스케일 고정 (bob 비활성 전 이미 변형된 경우 대비)
        var visualSlot = player.VisualSlot;
        Vector3 lockedScale = visualSlot != null ? visualSlot.localScale : Vector3.one;

        try
        {
            // 상승 (ease-out)
            float t = 0f;
            while (t < floatRiseTime)
            {
                t += Time.deltaTime;
                float pct  = Mathf.Clamp01(t / floatRiseTime);
                float ease = 1f - (1f - pct) * (1f - pct);
                Vector2 target = originWorld + Vector2.up * (floatHeight * ease);
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                yield return null;
            }

            Vector2 apex = originWorld + Vector2.up * floatHeight;
            if (playerRb != null) playerRb.MovePosition(apex);
            else player.transform.position = apex;

            // 체공
            yield return new WaitForSeconds(floatHangTime);

            // 하강 (ease-in)
            t = 0f;
            while (t < floatFallTime)
            {
                t += Time.deltaTime;
                float pct  = Mathf.Clamp01(t / floatFallTime);
                float ease = pct * pct;
                Vector2 target = originWorld + Vector2.up * (floatHeight * (1f - ease));
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                yield return null;
            }
        }
        finally
        {
            if (playerRb != null) playerRb.MovePosition(originWorld);
            else if (player != null) player.transform.position = originWorld;
            if (visualSlot != null) visualSlot.localScale = lockedScale;
            if (bob != null) { bob.ResetOrigin(); bob.enabled = true; }
            player.InputLocked = false;
        }
    }

    // ── 보조 스쿼시/스트레치 헬퍼 ───────────────────────────────────

    /// <summary>찌부(squishX > 1, squishY < 1) 후 원래 스케일로 복귀.</summary>
    private IEnumerator Squish(Vector3 orig, float dur, float squishX, float squishY)
    {
        Vector3 squished = new(orig.x * squishX, orig.y * squishY, orig.z);
        float half = dur * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(orig, squished, Mathf.Clamp01(t / half));
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(squished, orig, Mathf.Clamp01(t / half));
            yield return null;
        }
        Owner.transform.localScale = orig;
    }

    /// <summary>위치 보간 + 스트레치 연출.</summary>
    private IEnumerator LerpPos(Vector3 from, Vector3 to, float dur,
        Vector3 origScale, float stretchX, float stretchY)
    {
        Vector3 stretchedScale = new(origScale.x * stretchX, origScale.y * stretchY, origScale.z);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / dur);
            Owner.transform.position   = Vector3.Lerp(from, to, pct);
            Owner.transform.localScale = Vector3.Lerp(origScale, stretchedScale, pct);
            yield return null;
        }
    }
}
