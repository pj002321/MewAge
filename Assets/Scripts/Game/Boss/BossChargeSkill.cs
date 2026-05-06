using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진 (Charge).
///
/// bounceCount = 0 : 기존처럼 한 번 돌진 후 벽에 닿으면 정지.
/// bounceCount > 0 : 벽에 닿을 때마다 방향을 반사(리플렉트)해서
///                   최대 N번 연속으로 벽을 튕기며 돌진합니다.
/// </summary>
public class BossChargeSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float chargeSpeed    = 18f;
    [SerializeField] private float chargeDuration = 0.4f;

    [Header("연속 벽 튕기기")]
    [Tooltip("0이면 기존처럼 한 번만 돌진. N이면 벽에 튕긴 뒤 N번 더 돌진합니다.")]
    [SerializeField] private int   bounceCount = 0;
    [Tooltip("튕긴 뒤 다음 돌진까지 잠깐 멈추는 시간 (0이면 즉시)")]
    [SerializeField] private float bouncePause = 0.1f;

    [Header("경고 라인")]
    [SerializeField] private float    warningDuration   = 0.5f;
    [SerializeField] private float    warningLineLength = 12f;
    [SerializeField] private float    warningLineWidth  = 0.08f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

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

        Vector2 origin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Vector2 dir    = ((Vector2)Owner.PlayerTransform.position - origin).normalized;

        // 첫 돌진 + 반사 반복
        int totalCharges = 1 + bounceCount;
        for (int charge = 0; charge < totalCharges; charge++)
        {
            if (IsInvalid()) yield break;

            // 경고 라인
            yield return WarnPhase(dir, speed);
            if (IsInvalid()) yield break;

            Owner.EnemyAnimator?.SetTrigger("tCharge");

            // 돌진
            bool hitWall = false;
            float elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
                if (IsInvalid()) yield break;

                Vector2 next = Owner.Rb.position + dir * speed * Time.fixedDeltaTime;

                var mb = MovementBounds.Shared;
                if (mb != null && !mb.IsInside(next))
                {
                    // 벽 충돌 위치로 이동 후 방향 반사
                    Vector2 clamped = mb.ClosestPointOnBoundary(next);
                    Owner.Rb.MovePosition(clamped);
                    dir     = Reflect(dir, next, clamped);
                    hitWall = true;
                    break;
                }

                Owner.MoveInDirection(dir, speed);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // 마지막 돌진이거나 벽에 안 닿았으면 종료
            if (!hitWall || charge == totalCharges - 1) yield break;

            Owner.FlipToward(dir);

            // 반사 후 잠깐 대기
            if (bouncePause > 0f)
                yield return new WaitForSeconds(bouncePause);
        }
    }

    // ── 경고 라인 ──────────────────────────────────────────────

    private IEnumerator WarnPhase(Vector2 dir, float speed)
    {
        Vector2 origin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

        var lr = SpawnWarningLine(origin);
        _warnLine = lr;

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (IsInvalid()) { CleanupWarnLine(); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);

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
        lr.startColor        = warningColor;
        lr.endColor          = warningColor;
        lr.sortingOrder      = 10;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    // ── 벽 반사 방향 계산 ──────────────────────────────────────

    /// <summary>
    /// 벽을 초과한 위치(overshot)와 클램프된 위치(clamped)의 차이로
    /// 어느 축이 벽에 닿았는지 판별해 방향 벡터를 반사합니다.
    /// </summary>
    private static Vector2 Reflect(Vector2 dir, Vector2 overshot, Vector2 clamped)
    {
        Vector2 diff = overshot - clamped;
        // 더 많이 초과된 축이 실제로 부딪힌 벽
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            dir.x = -dir.x;
        else
            dir.y = -dir.y;
        return dir.normalized;
    }

    private bool IsInvalid()
        => Owner == null || Owner.PlayerTransform == null;
}
