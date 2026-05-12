using System.Collections;
using UnityEngine;

/// <summary>
/// 근거리 공격형 — 플레이어에게 접근 후 공격 애니메이션을 재생합니다.
/// 실제 피해는 애니메이터의 Animation Event → OnMeleeAttackHit()로 처리됩니다.
/// </summary>
public class MeleeBehaviour : EnemyBehaviourBase
{
    [Header("근거리 설정")]
    [SerializeField] private float attackRange    = 1.2f;
    [SerializeField] private float attackInterval = 1.0f;
    [SerializeField] private bool  useAnimationEvent = true; // false면 타이머에서 직접 피해

    [Header("VFX")]
    [SerializeField] private GameObject attackVfx;      // 프리팹 자식 오브젝트
    [SerializeField] private float      vfxDuration = 0.3f;
    [Tooltip("useAnimationEvent=true일 때, 이 시간 후에도 이벤트가 없으면 직접 피해 적용 (0이면 폴백 없음)")]
    [SerializeField] private float      animEventTimeout = 0.5f;

    private float     attackTimer;
    private bool      _pendingHit;
    private Coroutine _vfxCoroutine;

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        attackTimer   = attackInterval;
        _pendingHit   = false;
        _vfxCoroutine = null;

        // Inspector 미할당 시 "AttackVFX" 이름의 자식 오브젝트 자동 탐색
        if (attackVfx == null)
            attackVfx = transform.root.Find("AttackVFX")?.gameObject
                     ?? FindVfxInChildren(transform.root);

        attackVfx?.SetActive(false);
    }

    private static GameObject FindVfxInChildren(Transform root)
    {
        foreach (Transform child in root)
            if (child.name.ToLower().Contains("vfx") || child.name.ToLower().Contains("fx"))
                return child.gameObject;
        return null;
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        // 사정거리 안팎 모두 타이머 리셋 (진입 즉시 공격 방지)
        attackTimer = attackInterval;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
        {
            if (attackVfx != null)
            {
                if (_vfxCoroutine != null) StopCoroutine(_vfxCoroutine);
                _vfxCoroutine = StartCoroutine(ShowVfx());
            }

            if (useAnimationEvent)
            {
                Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
                if (animEventTimeout > 0f)
                {
                    _pendingHit = true;
                    StartCoroutine(AnimEventFallback());
                }
            }
            else
            {
                PlayerController.Instance?.TakeDamage(Owner.ContactDamage, sourceName: Owner.SelfTransform.name);
            }
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        Vector2 toPlayer = (Vector2)Owner.PlayerTransform.position - Owner.Rb.position;
        float dist = toPlayer.magnitude;

        if (dist > attackRange)
            Owner.MoveInDirection(toPlayer / Mathf.Max(dist, 0.001f), Owner.MoveSpeed);
        else
            Owner.FlipToward(toPlayer);
    }

    private IEnumerator AnimEventFallback()
    {
        yield return new WaitForSeconds(animEventTimeout);
        if (!_pendingHit) yield break; // 이미 OnMeleeAttackHit에서 처리됨
        _pendingHit = false;
        if (Owner == null || Owner.PlayerTransform == null) yield break;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage, sourceName: Owner.SelfTransform.name);
    }

    private IEnumerator ShowVfx()
    {
        attackVfx.SetActive(true);
        yield return new WaitForSeconds(vfxDuration);
        if (attackVfx != null) attackVfx.SetActive(false);
        _vfxCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var col = GetComponentInParent<Collider2D>();
        Vector3 pos = col != null ? col.bounds.center : transform.position;

        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, attackRange);
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, attackRange);
    }
#endif

    /// <summary>
    /// 공격 애니메이션의 타격 프레임에 Animation Event로 호출됩니다.
    /// 이 시점에 플레이어가 근접해 있으면 피해를 입힙니다.
    /// </summary>
    public void OnMeleeAttackHit()
    {
        _pendingHit = false; // 폴백 코루틴이 중복 적용하지 않도록 취소
        if (Owner == null || Owner.PlayerTransform == null) return;
        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist <= attackRange)
            PlayerController.Instance?.TakeDamage(Owner.ContactDamage, sourceName: Owner.SelfTransform.name);
    }
}
