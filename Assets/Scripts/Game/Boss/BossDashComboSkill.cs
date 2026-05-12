using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 돌진하면서 연속 타격.
/// 플레이어 방향으로 돌진하며, 범위 안에 들어올 때마다 hitCount번 타격합니다.
/// 타격마다 애니메이션 트리거를 발동합니다.
/// </summary>
public class BossDashComboSkill : BossSkillBase
{
    [Header("돌진")]
    [SerializeField] private float   dashSpeed     = 10f;
    [SerializeField] private float   dashDuration  = 0.6f;

    [Header("연속 타격")]
    [SerializeField] private int     hitCount      = 3;
    [SerializeField] private float   hitInterval   = 0.18f;   // 타격 사이 간격
    [SerializeField] private float   hitRange      = 1.6f;
    [SerializeField] private float   hitDamage     = 0f;      // 0이면 ContactDamage 사용

    [Header("돌진 준비 시각")]
    [SerializeField] private Color   chargeColor      = Color.red;
    [SerializeField] private float   chargeScale      = 0.85f;
    [SerializeField] private float   preDashDuration  = 0.3f;
    [SerializeField] private float   ghostInterval  = 0.06f;

    [Header("경계")]
    [SerializeField] private Vector2 fallbackBoundsSize = new(10f, 18f);

    // ── 내부 상태 ────────────────────────────────────────────────
    private SpriteRenderer _sr;

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        float   speed   = dashSpeed * (Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f);
        float   dmg     = hitDamage > 0f ? hitDamage : Owner.ContactDamage;
        Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        // ── 돌진准备: 스프라이트 빨간색, 스케일 축소 ─────────────────────
        _sr = Owner.GetComponent<SpriteRenderer>();
        Color originalColor = _sr != null ? _sr.color : Color.white;
        Vector3 originalScale = Owner.transform.localScale;
        if (_sr != null)
        {
            _sr.color = chargeColor;
            Owner.transform.localScale = originalScale * chargeScale;
        }

        yield return new WaitForSeconds(preDashDuration);

        // 원상 복구
        if (_sr != null)
        {
            _sr.color = originalColor;
            Owner.transform.localScale = originalScale;
        }

        // 경계 계산
        float minX, maxX, minY, maxY;
        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max);
            minX = min.x; maxX = max.x;
            minY = min.y; maxY = max.y;
        }
        else
        {
            var     spawner = EnemySpawner.Shared;
            Vector2 ac      = spawner != null ? spawner.AreaCenter : Vector2.zero;
            Vector2 half    = (spawner != null ? spawner.AreaSize : fallbackBoundsSize) * 0.5f;
            minX = ac.x - half.x; maxX = ac.x + half.x;
            minY = ac.y - half.y; maxY = ac.y + half.y;
        }

        // ── 돌진 (경계 클램프 포함) ─────────────────────────────
        float elapsed  = 0f;
        int   hits     = 0;
        float hitTimer = 0f;
        float ghostTimer = 0f;

        while (elapsed < dashDuration && hits < hitCount)
        {
            // 고스트 트레일 생성
            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f && _sr != null)
            {
                SpawnGhostTrail();
                ghostTimer = ghostInterval;
            }

            Vector2 pos  = Owner.Rb.position;
            Vector2 next = pos + dir * speed * Time.fixedDeltaTime;

            // 경계 도달 시 클램프 후 중단
            if (next.x <= minX || next.x >= maxX || next.y <= minY || next.y >= maxY)
            {
                next = new Vector2(Mathf.Clamp(next.x, minX, maxX),
                                   Mathf.Clamp(next.y, minY, maxY));
                Owner.Rb.MovePosition(next);
                Owner.FlipToward(dir);
                break;
            }

            Owner.MoveInDirection(dir, speed);

            // 범위 안이면 타격 타이머 감소
            float dist = Owner.PlayerTransform != null
                ? Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position)
                : float.MaxValue;

            if (dist <= hitRange)
            {
                hitTimer -= Time.fixedDeltaTime;
                if (hitTimer <= 0f)
                {
                    Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                    PlayerController.Instance?.TakeDamage(dmg * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f), sourceName: Owner.gameObject.name);
                    hits++;
                    hitTimer = hitInterval;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 돌진 후 남은 타격 소진 (범위 내 정지 상태에서)
        while (hits < hitCount)
        {
            if (Owner.PlayerTransform == null) break;
            float dist = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
            if (dist > hitRange) break;

            Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
            PlayerController.Instance?.TakeDamage(dmg * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f), sourceName: Owner.gameObject.name);
            hits++;
            yield return new WaitForSeconds(hitInterval);
        }

        yield return new WaitForSeconds(0.25f);
    }

    // ── 고스트 트레일 ───────────────────────────────────────────
    private void SpawnGhostTrail()
    {
        if (_sr == null || _sr.sprite == null) return;

        var go = new GameObject("DashGhost", typeof(SpriteRenderer), typeof(CanvasGroup));
        go.transform.position = Owner.transform.position;
        go.transform.localScale = Owner.transform.localScale;

        var ghostSr = go.GetComponent<SpriteRenderer>();
        ghostSr.sprite = _sr.sprite;
        ghostSr.color = new Color(1f, 1f, 1f, 0.4f);
        ghostSr.sortingLayerID = _sr.sortingLayerID;
        ghostSr.sortingOrder = _sr.sortingOrder - 1;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0.4f;

        // 0.15초 뒤 자연 소멸
        Destroy(go, 0.15f);
    }
}
