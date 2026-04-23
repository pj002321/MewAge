using UnityEngine;

/// <summary>
/// 돌진형 — 플레이어를 향해 접근하다가 주기적으로 고속 돌진합니다.
/// 접촉 피해는 EnemyController의 OnTrigger에서 처리됩니다.
/// </summary>
public class ChargeBehaviour : EnemyBehaviourBase
{
    [Header("돌진 설정")]
    [SerializeField] private float chargeInterval        = 3f;
    [SerializeField] private float chargeDuration        = 0.6f;
    [SerializeField] private float chargeSpeedMultiplier = 3.5f;

    [Header("돌진 준비 시각")]
    [SerializeField] private Color   chargeColor      = Color.red;
    [SerializeField] private float   chargeScale      = 0.85f;
    [SerializeField] private float   preChargeDuration = 0.25f;
    [SerializeField] private float   ghostInterval    = 0.06f;

    private float   chargeTimer;
    private bool    isCharging;
    private float   chargeRemaining;
    private Vector2 chargeDir;
    private float   ghostTimer;
    private Vector3 originalScale;  // 원본 스케일 저장

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        chargeTimer = chargeInterval;
    }

    public override void OnAttackUpdate()
    {
        if (isCharging || Owner.PlayerTransform == null) return;

        chargeTimer -= Time.deltaTime;
        if (chargeTimer <= 0f)
        {
            chargeTimer     = chargeInterval;
            chargeDir       = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;
            chargeRemaining = chargeDuration;
            isCharging      = true;
            ghostTimer     = 0f;
            originalScale = Owner.transform.localScale;  // 원본 스케일 저장

            Owner.EnemyAnimator.SetTrigger("tCharge");

            // 돌진 준비: 스프라이트 빨간색, 스케일 축소
            var sr = Owner.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = chargeColor;
                Owner.transform.localScale = originalScale * chargeScale;
            }
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (isCharging)
        {
            // 준비 시간后就 원상 복구 (1회만)
            if (chargeRemaining <= chargeDuration - preChargeDuration)
            {
                var sr = Owner.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = Color.white;
                    Owner.transform.localScale = originalScale;
                }
                chargeRemaining = chargeDuration + 1f;  // 중복 복구 방지
            }

            // 고스트 트레일
            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f)
            {
                SpawnGhostTrail();
                ghostTimer = ghostInterval;
            }

            Owner.MoveInDirection(chargeDir, Owner.MoveSpeed * chargeSpeedMultiplier);
            chargeRemaining -= Time.fixedDeltaTime;

            // 맵 경계 도달 시 돌진 중단 + 위치 클램프
            if (IsOutOfBounds(Owner.transform.position))
            {
                ClampToBounds();
                isCharging = false;
                var sr = Owner.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.color = Color.white; Owner.transform.localScale = originalScale; }
            }

            if (chargeRemaining <= 0f)
            {
                isCharging = false;
                var sr = Owner.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.color = Color.white; Owner.transform.localScale = originalScale; }
            }
        }
        else
        {
            Owner.MoveTowardPlayer(Owner.MoveSpeed);
        }
    }

    private bool IsOutOfBounds(Vector2 pos)
    {
        var spawner = EnemySpawner.Shared;
        if (spawner == null) return false;

        Vector2 half = spawner.AreaSize * 0.5f;
        Vector2 c    = spawner.AreaCenter;
        return pos.x < c.x - half.x || pos.x > c.x + half.x ||
               pos.y < c.y - half.y || pos.y > c.y + half.y;
    }

    private void ClampToBounds()
    {
        var spawner = EnemySpawner.Shared;
        if (spawner == null) return;

        Vector2 half = spawner.AreaSize * 0.5f;
        Vector2 c    = spawner.AreaCenter;
        Vector3 p    = Owner.transform.position;
        Owner.transform.position = new Vector3(
            Mathf.Clamp(p.x, c.x - half.x, c.x + half.x),
            Mathf.Clamp(p.y, c.y - half.y, c.y + half.y),
            p.z);
    }

    // ── 고스트 트레일 ───────────────────────────────────────────
    private void SpawnGhostTrail()
    {
        var sr = Owner.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        var sprite = sr.sprite;
        if (sprite == null) return;

        var go = new GameObject("ChargeGhost", typeof(SpriteRenderer));
        go.transform.position = Owner.transform.position;
        go.transform.localScale = Owner.transform.localScale;

        var ghostSr = go.GetComponent<SpriteRenderer>();
        ghostSr.sprite = sprite;
        ghostSr.color = new Color(1f, 1f, 1f, 0.35f);
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder = sr.sortingOrder - 1;

        Destroy(go, 0.12f);
    }
}
