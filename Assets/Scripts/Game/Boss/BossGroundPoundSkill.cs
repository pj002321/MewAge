using System.Collections;
using DG.Tweening;
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

    // 현재 스킬 실행 중 원본 스케일 — OnInterrupted에서 복구
    private Vector3             _origScale;
    private LineRenderer        _warnLine;
    private PlayerController    _activeFloatingPlayer;
    private SlimeBobAnimation   _activeFloatingBob;

    private void OnDisable()
    {
        if (_warnLine != null) { Destroy(_warnLine.gameObject); _warnLine = null; }
    }

    public override void OnInterrupted()
    {
        base.OnInterrupted(); // StopAllCoroutines
        // DOTween이 transform 스케일을 건드리고 있을 수 있으므로 즉시 Kill
        if (Owner != null)
        {
            DOTween.Kill(Owner.transform, complete: false);
            Owner.transform.localScale = _origScale;
        }
        RestoreFloatingPlayer();
        CleanupWarnLine();
    }

    private void RestoreFloatingPlayer()
    {
        if (_activeFloatingPlayer == null) return;
        _activeFloatingPlayer.InputLocked = false;
        if (_activeFloatingBob != null) { _activeFloatingBob.ResetOrigin(); _activeFloatingBob.enabled = true; }
        _activeFloatingPlayer = null;
        _activeFloatingBob    = null;
    }

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

        float speed = chargeSpeed;

        // 스케일 기준값 저장 — OnInterrupted에서 복구
        _origScale = Owner.transform.localScale;

        // ── 1. 보스 소점프 ──────────────────────────────────────
        Vector2 startPos = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Vector2 apexPos  = startPos + Vector2.up * hopHeight;

        // 찌부 예비동작
        yield return StartCoroutine(Squish(_origScale, 0.14f, 1.3f, 0.55f));

        // 상승 (Rigidbody 기반, FixedUpdate 타이밍)
        yield return StartCoroutine(LerpPos(startPos, apexPos, hopRiseTime,
            _origScale, stretchX: 0.75f, stretchY: 1.35f));

        // 하강
        yield return StartCoroutine(LerpPos(apexPos, startPos, hopFallTime,
            _origScale, stretchX: 1.35f, stretchY: 0.6f));

        // 착지 확정: Rigidbody 위치와 transform 위치 동기화
        if (Owner.Rb != null) Owner.Rb.position = startPos;
        Owner.transform.position   = startPos;
        Owner.transform.localScale = _origScale;

        // ── 2. 착지 충격 ───────────────────────────────────────
        yield return StartCoroutine(Squish(_origScale, 0.07f, 1.5f, 0.4f));

        CameraFollow.Shared?.Shake(shakeDuration, shakeMagnitude);
        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Owner.Decal != null ? Owner.Decal.position : (Vector3)startPos;
            Destroy(Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        // ── 3. 플레이어 부유 ────────────────────────────────────
        var player = PlayerController.Instance;
        yield return StartCoroutine(FloatPlayer(player));

        // ── 4. 돌진 전 대기 ────────────────────────────────────
        if (preChargeDelay > 0f)
            yield return new WaitForSeconds(preChargeDelay);

        // 돌진 방향 계산
        Vector2 chargeOrigin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Vector2 chargeDir    = player != null
            ? ((Vector2)player.transform.position - chargeOrigin).normalized
            : Vector2.right;
        if (chargeDir == Vector2.zero) chargeDir = Vector2.right;

        // ── 5. 돌진 경고 라인 ──────────────────────────────────
        yield return StartCoroutine(WarnPhase(chargeDir));
        if (Owner == null) yield break;

        // ── 6. 보스 돌진 ───────────────────────────────────────
        yield return StartCoroutine(ChargeAtPlayer(speed, chargeDir));
    }

    // ── 보스 돌진 ──────────────────────────────────────────────────

    private IEnumerator ChargeAtPlayer(float speed, Vector2 dir)
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
                Owner.Rb.MovePosition(mb.ClosestPointOnBoundary(next));
                yield break;
            }

            // SpawnZone AABB 폴백 (보스 전용 배경처럼 MovementBounds가 없는 경우)
            if (mb == null && SpawnZone.Shared != null)
            {
                Vector2 c = SpawnZone.Shared.BoundsCenter;
                Vector2 s = SpawnZone.Shared.BoundsSize * 0.5f;
                if (next.x < c.x - s.x || next.x > c.x + s.x ||
                    next.y < c.y - s.y || next.y > c.y + s.y)
                {
                    next.x = Mathf.Clamp(next.x, c.x - s.x, c.x + s.x);
                    next.y = Mathf.Clamp(next.y, c.y - s.y, c.y + s.y);
                    Owner.Rb.MovePosition(next);
                    yield break;
                }
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
        _activeFloatingPlayer = player;
        _activeFloatingBob    = bob;
        player.InputLocked = true;
        if (bob != null) bob.enabled = false;

        Vector2 originWorld = playerRb != null ? playerRb.position : (Vector2)player.transform.position;

        var visualSlot  = player.VisualSlot;
        Vector3 lockedScale = visualSlot != null ? visualSlot.localScale : Vector3.one;

        try
        {
            // 상승 (ease-out) — FixedUpdate 타이밍으로 물리 이동
            float t = 0f;
            while (t < floatRiseTime)
            {
                t += Time.fixedDeltaTime;
                float pct  = Mathf.Clamp01(t / floatRiseTime);
                float ease = 1f - (1f - pct) * (1f - pct);
                Vector2 target = originWorld + Vector2.up * (floatHeight * ease);
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                yield return new WaitForFixedUpdate();
            }

            Vector2 apex = originWorld + Vector2.up * floatHeight;
            if (playerRb != null) playerRb.MovePosition(apex);
            else player.transform.position = apex;

            // 체공
            yield return new WaitForSeconds(floatHangTime);

            // 하강 (ease-in) — FixedUpdate 타이밍
            t = 0f;
            while (t < floatFallTime)
            {
                t += Time.fixedDeltaTime;
                float pct  = Mathf.Clamp01(t / floatFallTime);
                float ease = pct * pct;
                Vector2 target = originWorld + Vector2.up * (floatHeight * (1f - ease));
                if (playerRb != null) playerRb.MovePosition(target);
                else player.transform.position = target;
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            if (playerRb != null) playerRb.MovePosition(originWorld);
            else if (player != null) player.transform.position = originWorld;
            if (visualSlot != null) visualSlot.localScale = lockedScale;
            if (bob != null) { bob.ResetOrigin(); bob.enabled = true; }
            if (player != null) player.InputLocked = false;
            _activeFloatingPlayer = null;
            _activeFloatingBob    = null;
        }
    }

    // ── 보조 스쿼시/스트레치 헬퍼 ───────────────────────────────────

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

    /// <summary>
    /// Rigidbody 기반 위치 보간 + 스트레치 연출.
    /// WaitForFixedUpdate 타이밍을 사용해 Rigidbody 위치와 transform 위치 불일치를 방지합니다.
    /// </summary>
    private IEnumerator LerpPos(Vector2 from, Vector2 to, float dur,
        Vector3 origScale, float stretchX, float stretchY)
    {
        Vector3 stretchedScale = new(origScale.x * stretchX, origScale.y * stretchY, origScale.z);
        float t = 0f;
        while (t < dur)
        {
            t += Time.fixedDeltaTime;
            float pct  = Mathf.Clamp01(t / dur);
            Vector2 pos = Vector2.Lerp(from, to, pct);
            if (Owner.Rb != null) Owner.Rb.MovePosition(pos);
            else Owner.transform.position = pos;
            Owner.transform.localScale = Vector3.Lerp(origScale, stretchedScale, pct);
            yield return new WaitForFixedUpdate();
        }
        // 끝에서 위치 확정 (스케일은 OnExecute에서 origScale로 복구)
        if (Owner.Rb != null) Owner.Rb.MovePosition(to);
        else Owner.transform.position = to;
    }
}
