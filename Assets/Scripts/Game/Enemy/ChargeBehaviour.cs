using UnityEngine;

/// <summary>
/// 돌진형 — 2초 이동 → 2초 정지(Idle) → 돌진 → 반복
/// 접촉 피해는 EnemyController의 OnTrigger에서 처리됩니다.
/// </summary>
public class ChargeBehaviour : EnemyBehaviourBase
{
    [Header("돌진 설정")]
    [SerializeField] private float chargeDuration        = 0.6f;
    [SerializeField] private float chargeSpeedMultiplier = 3.5f;

    [Header("이동/정지 사이클")]
    [SerializeField] private float moveDuration = 2f;
    [SerializeField] private float idleDuration = 2f;

    [Header("돌진 준비 시각")]
    [SerializeField] private Color chargeColor       = Color.red;
    [SerializeField] private float chargeScale       = 0.85f;
    [SerializeField] private float preChargeDuration = 0.25f;
    [SerializeField] private float ghostInterval     = 0.06f;

    private enum Phase { Moving, Idle, Charging }
    private Phase   _phase;
    private float   _phaseTimer;

    private float           chargeRemaining;
    private Vector2         chargeDir;
    private float           ghostTimer;
    private Vector3         originalScale;
    private SpriteRenderer  _cachedSr;

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        _phase      = Phase.Moving;
        _phaseTimer = moveDuration;
        _cachedSr   = Owner.GetComponent<SpriteRenderer>();
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null || _phase == Phase.Charging) return;

        _phaseTimer -= Time.deltaTime;

        if (_phase == Phase.Moving && _phaseTimer <= 0f)
        {
            _phase      = Phase.Idle;
            _phaseTimer = idleDuration;
        }
        else if (_phase == Phase.Idle && _phaseTimer <= 0f)
        {
            BeginCharge();
        }
    }

    private void BeginCharge()
    {
        _phase          = Phase.Charging;
        chargeDir       = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;
        chargeRemaining = chargeDuration;
        ghostTimer      = 0f;
        originalScale   = Owner.transform.localScale;

        Owner.EnemyAnimator?.SetTrigger("tCharge");

        var sr = _cachedSr;
        if (sr != null)
        {
            sr.color = chargeColor;
            // X는 LateUpdate의 flip이 제어하므로 Y만 조정
            var s = Owner.transform.localScale;
            s.y = originalScale.y * chargeScale;
            Owner.transform.localScale = s;
        }
    }

    private void EndCharge()
    {
        var sr = _cachedSr;
        if (sr != null)
        {
            sr.color = Color.white;
            var s = Owner.transform.localScale;
            s.y = originalScale.y;
            Owner.transform.localScale = s;
        }
        _phase      = Phase.Moving;
        _phaseTimer = moveDuration;
    }

    public override void OnMoveFixedUpdate()
    {
        if (_phase == Phase.Charging)
        {
            // 준비 시간 후 원상 복구 (1회만)
            if (chargeRemaining <= chargeDuration - preChargeDuration)
            {
                var sr = _cachedSr;
                if (sr != null)
                {
                    sr.color = Color.white;
                    var s = Owner.transform.localScale;
                    s.y = originalScale.y;
                    Owner.transform.localScale = s;
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
                EndCharge();
            }
            else if (chargeRemaining <= 0f)
            {
                EndCharge();
            }
        }
        else if (_phase == Phase.Moving)
        {
            Owner.MoveTowardPlayer(Owner.MoveSpeed);
        }
        // Phase.Idle: 이동하지 않음
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
        var sr = _cachedSr;
        if (sr == null) return;

        var sprite = sr.sprite;
        if (sprite == null) return;

        var go = new GameObject("ChargeGhost", typeof(SpriteRenderer));
        go.transform.position   = Owner.transform.position;
        go.transform.localScale = Owner.transform.localScale;

        var ghostSr = go.GetComponent<SpriteRenderer>();
        ghostSr.sprite         = sprite;
        ghostSr.color          = new Color(1f, 1f, 1f, 0.35f);
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder - 1;

        Destroy(go, 0.12f);
    }
}
