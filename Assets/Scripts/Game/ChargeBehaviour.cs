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

    private float   chargeTimer;
    private bool    isCharging;
    private float   chargeRemaining;
    private Vector2 chargeDir;

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
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (isCharging)
        {
            Owner.MoveInDirection(chargeDir, Owner.MoveSpeed * chargeSpeedMultiplier);
            chargeRemaining -= Time.fixedDeltaTime;

            // 맵 경계 도달 시 돌진 중단 + 위치 클램프
            if (IsOutOfBounds(Owner.transform.position))
            {
                ClampToBounds();
                isCharging = false;
            }

            if (chargeRemaining <= 0f) isCharging = false;
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
}
