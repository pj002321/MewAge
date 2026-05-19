using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진 (Charge).
///
/// bounceCount = 0 : 한 번 돌진 후 벽에 닿으면 정지.
/// bounceCount > 0 : 벽에 튕길 때마다 방향을 반사해 최대 N번 연속 돌진.
/// </summary>
[System.Serializable]
public class BossChargeSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float chargeSpeed    = 18f;
    [SerializeField] private float chargeDuration = 0.4f;

    [Header("연속 벽 튕기기")]
    [Tooltip("0이면 한 번만 돌진. N이면 벽에 튕긴 뒤 N번 더 돌진")]
    [SerializeField] private int   bounceCount = 0;
    [Tooltip("튕긴 뒤 다음 돌진까지 잠깐 멈추는 시간 (0이면 즉시)")]
    [SerializeField] private float bouncePause = 0.1f;

    [Header("경고 라인")]
    [SerializeField] private float    warningDuration   = 0.5f;
    [SerializeField] private float    warningLineLength = 12f;
    [SerializeField] private float    warningLineWidth  = 0.08f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("null이면 Sprites/Default 자동 생성")]
    [SerializeField] private Material warningMaterial;

    private LineRenderer _warnLine;

    public override void OnInterrupted() => CleanupWarnLine();

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        Vector2 pos = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
        Vector2 dir = ((Vector2)PlayerT.position - pos).normalized;
        if (dir == Vector2.zero) dir = Vector2.right;

        int totalCharges = 1 + bounceCount;
        for (int charge = 0; charge < totalCharges; charge++)
        {
            if (Boss == null || ct.IsCancellationRequested) return;

            // 첫 번째 돌진에만 경고 라인 표시
            if (charge == 0)
                await WarnPhaseAsync(dir, ct);

            if (Boss == null || ct.IsCancellationRequested) return;

            Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TCharge);

            pos = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
            bool  hitWall      = false;
            float elapsed      = 0f;
            bool  isFinalCharge = charge == totalCharges - 1;

            // 마지막 돌진은 chargeDuration으로 시간 제한.
            // 튕기기 전 돌진은 벽에 닿을 때까지 계속 진행 (ct가 안전장치).
            while (isFinalCharge ? elapsed < chargeDuration : !hitWall)
            {
                if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
                if (Boss == null || ct.IsCancellationRequested) return;

                Vector2 next = pos + dir * chargeSpeed * Time.fixedDeltaTime;

                var mb = MovementBounds.Shared;
                var sz = SpawnZone.Shared;
                if (mb != null && !mb.IsInside(next))
                {
                    Vector2 clamped = mb.ClosestPointOnBoundary(next);
                    Boss.Rb.MovePosition(clamped);
                    pos     = clamped;
                    dir     = ReflectDir(dir, next, clamped);
                    hitWall = true;
                    break;
                }
                else if (mb == null && sz != null)
                {
                    Vector2 c    = sz.BoundsCenter;
                    Vector2 half = sz.BoundsSize * 0.5f;
                    if (next.x < c.x - half.x || next.x > c.x + half.x ||
                        next.y < c.y - half.y || next.y > c.y + half.y)
                    {
                        Vector2 clamped = new(Mathf.Clamp(next.x, c.x - half.x, c.x + half.x),
                                              Mathf.Clamp(next.y, c.y - half.y, c.y + half.y));
                        Boss.Rb.MovePosition(clamped);
                        pos     = clamped;
                        dir     = ReflectDir(dir, next, clamped);
                        hitWall = true;
                        break;
                    }
                }

                pos = next;
                Boss.Rb.MovePosition(next);
                Boss.FlipToward(dir);
                elapsed += Time.fixedDeltaTime;
                await UniTask.WaitForFixedUpdate(ct);
            }

            if (isFinalCharge) return;

            Boss.FlipToward(dir);
            if (bouncePause > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(bouncePause), cancellationToken: ct);
        }
    }

    private async UniTask WarnPhaseAsync(Vector2 dir, CancellationToken ct)
    {
        Vector2 origin = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
        _warnLine = SpawnWarnLine(origin);

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (Boss == null || ct.IsCancellationRequested) { CleanupWarnLine(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);

            if (_warnLine != null)
            {
                Vector2 cur = Boss.Rb != null ? Boss.Rb.position : (Vector2)Boss.transform.position;
                _warnLine.transform.position = new Vector3(cur.x, cur.y, 0f);
                _warnLine.SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new(cur.x + dir.x * warningLineLength * t,
                                       cur.y + dir.y * warningLineLength * t, 0f);
                _warnLine.SetPosition(1, _warnLine.transform.InverseTransformPoint(worldEnd));
            }
            await UniTask.Yield(ct);
        }
        CleanupWarnLine();
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
        lr.startWidth        = warningLineWidth;
        lr.endWidth          = warningLineWidth;
        lr.material          = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor        = warningColor;
        lr.endColor          = warningColor;
        lr.sortingOrder      = 10;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(go, warningDuration + 0.5f);
        return lr;
    }

    /// <summary>초과 위치와 클램프 위치의 차이로 부딪힌 벽 축을 판단해 방향을 반사합니다.</summary>
    private static Vector2 ReflectDir(Vector2 dir, Vector2 overshot, Vector2 clamped)
    {
        Vector2 diff = overshot - clamped;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)) dir.x = -dir.x;
        else                                          dir.y = -dir.y;
        return dir.normalized;
    }
}
