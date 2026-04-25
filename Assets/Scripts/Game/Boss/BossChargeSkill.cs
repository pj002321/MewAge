using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진 (Charge).
/// 플레이어 방향으로 빠르게 돌진하며, MovementBounds 경계에 닿으면 즉시 중단합니다.
///
/// Fix: 게임오버 상태이거나 플레이어가 null이면 루프를 즉시 종료합니다.
/// </summary>
public class BossChargeSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float chargeSpeed    = 18f;
    [SerializeField] private float chargeDuration = 0.4f;

    [Header("경고 라인")]
    [SerializeField] private float    warningDuration   = 0.5f;
    [SerializeField] private float    warningLineLength = 12f;
    [SerializeField] private float    warningLineWidth  = 0.08f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

    protected override IEnumerator OnExecute()
    {
        // 플레이어 없음 또는 게임오버 → 즉시 중단
        if (Owner.PlayerTransform == null) yield break;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

        Vector2 origin = Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;
        Vector2 dir    = ((Vector2)Owner.PlayerTransform.position - origin).normalized;

        // 경고 라인 애니메이션
        var lr = SpawnWarningLine(origin, dir);
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (Owner == null || Owner.PlayerTransform == null) { if (lr != null) Destroy(lr.gameObject); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);
            if (lr != null) lr.SetPosition(1, origin + dir * (warningLineLength * t));
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);

        if (Owner == null || Owner.PlayerTransform == null) yield break;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;

        Owner.EnemyAnimator?.SetTrigger("tCharge");

        float   speed   = chargeSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);
        elapsed = 0f;

        while (elapsed < chargeDuration)
        {
            // 루프 중 게임오버 또는 PlayerTransform 소멸 시 즉시 중단
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
            if (Owner == null || Owner.PlayerTransform == null) yield break;

            Vector2 next = Owner.Rb.position + dir * speed * Time.fixedDeltaTime;

            // MovementBounds 밖으로 나가면 경계에서 멈춤
            var mb = MovementBounds.Shared;
            if (mb != null && !mb.IsInside(next))
            {
                Owner.Rb.MovePosition(mb.ClosestPointOnBoundary(next));
                Owner.FlipToward(dir);
                yield break;   // break → yield break: 완전 종료
            }

            Owner.MoveInDirection(dir, speed);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private LineRenderer SpawnWarningLine(Vector2 start, Vector2 dir)
    {
        var go = new GameObject("WarnLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, start);
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
}
