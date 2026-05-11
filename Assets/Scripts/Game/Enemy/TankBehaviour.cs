using System.Collections;
using UnityEngine;

/// <summary>
/// 탱커형 — 느리지만 고체력으로 플레이어에게 정면으로 돌진합니다.
/// </summary>
public class TankBehaviour : EnemyBehaviourBase
{
    [Header("탱커 설정")]
    [SerializeField] private float attackRange      = 1.5f;
    [SerializeField] private float attackInterval   = 2.0f;
    [SerializeField] private float damageMultiplier = 1.5f;

    [Header("VFX")]
    [SerializeField] private GameObject shieldVfx;
    [SerializeField] private float      shieldVfxDuration = 0.2f;

    private static readonly int OutlineProperty = Shader.PropertyToID("_SolidOutline");
    private static readonly Color ShieldOutlineColor = Color.yellow;
    private const float ShieldHPThreshold = 0.4f;

    private float                 attackTimer;
    private SpriteRenderer        _sr;
    private MaterialPropertyBlock _mpb;

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        attackTimer = attackInterval;
        shieldVfx?.SetActive(false);
        var ec = EC;
        if (ec != null) ec.KnockbackImmune = true;

        _sr  = Owner.SelfTransform.GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        SetOutline(ShieldOutlineColor);

        if (ec != null) ec.OnHit += OnHit;
    }

    private void OnDisable()
    {
        if (EC != null) EC.OnHit -= OnHit;
    }

    private void OnHit()
    {
        if (EC?.HPRatio <= ShieldHPThreshold)
        {
            SetOutline(Color.clear); // 외곽선 제거
            return;
        }

        if (shieldVfx != null)
            StartCoroutine(ShowVfx(shieldVfx, shieldVfxDuration));
    }

    private void SetOutline(Color color)
    {
        if (_sr == null || _mpb == null) return;
        _mpb.SetColor(OutlineProperty, color);
        _sr.SetPropertyBlock(_mpb);
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        attackTimer = attackInterval;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
        {
            Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
        }
    }

    private IEnumerator ShowVfx(GameObject vfx, float duration)
    {
        vfx.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (vfx != null) vfx.SetActive(false);
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist > attackRange)
            Owner.MoveTowardPlayer(Owner.MoveSpeed);
        else
            Owner.FlipToward((Vector2)Owner.PlayerTransform.position - Owner.Rb.position);
    }

    /// <summary>Animation Event — 강화 근접 타격.</summary>
    public void OnTankAttackHit()
    {
        if (Owner == null || Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage * damageMultiplier);
    }
}
