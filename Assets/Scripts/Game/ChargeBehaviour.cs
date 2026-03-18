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
            if (chargeRemaining <= 0f) isCharging = false;
        }
        else
        {
            Owner.MoveTowardPlayer(Owner.MoveSpeed);
        }
    }
}
