using System.Collections;
using UnityEngine;

/// <summary>
/// 일반 적 행동: 내려찍기 (SlamBehaviour).
/// 플레이어를 추적하다가 사거리 내에 들어오면 점프 → 낙하 → 범위 피해.
/// </summary>
public class SlamBehaviour : EnemyBehaviourBase
{
    [Header("추적")]
    [SerializeField] private float chaseSpeed = 2.5f;

    [Header("내려찍기")]
    [SerializeField] private float slamRange      = 2f;    // 슬램 발동 거리
    [SerializeField] private float slamDamage     = 15f;
    [SerializeField] private float slamRadius     = 1.8f;  // 착지 피해 범위
    [SerializeField] private float slamCooldown   = 4f;
    [SerializeField] private float slamJumpHeight = 4f;
    [SerializeField] private float slamRiseTime   = 0.3f;
    [SerializeField] private float slamHangTime   = 0.5f;
    [SerializeField] private float slamFallSpeed  = 20f;

    [Header("VFX / 애니메이션")]
    [SerializeField] private GameObject slamMarkerPrefab;
    [SerializeField] private string     slamLandTrigger = "SlamLand";

    private float cooldownTimer;
    private bool  isSlamming;
    private Animator anim;
    private WaitForSeconds _waitHangTime;
    private WaitForSeconds _waitAfterLand;

    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        anim           = controller.GetComponentInChildren<Animator>();
        _waitHangTime  = new WaitForSeconds(slamHangTime);
        _waitAfterLand = new WaitForSeconds(0.3f);
    }

    public override void ApplyWaveScaling(float multiplier)
    {
        slamDamage *= multiplier;
    }

    // ── OnAttackUpdate: 쿨다운 체크 + 슬램 발동 ─────────────────
    public override void OnAttackUpdate()
    {
        if (isSlamming) return;
        if (Owner.PlayerTransform == null) return;

        cooldownTimer -= Time.deltaTime;

        float dist = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        if (cooldownTimer <= 0f && dist <= slamRange)
        {
            cooldownTimer = slamCooldown;
            Owner.StartCoroutine(SlamRoutine());
        }
    }

    // ── OnMoveFixedUpdate: 슬램 중 이동 정지, 평상시 추적 ───────
    public override void OnMoveFixedUpdate()
    {
        if (isSlamming) return;
        Owner.MoveTowardPlayer(chaseSpeed);
    }

    // ── 슬램 코루틴 ──────────────────────────────────────────────
    private IEnumerator SlamRoutine()
    {
        isSlamming = true;

        if (Owner.PlayerTransform == null)
        {
            isSlamming = false;
            yield break;
        }

        Vector3 slamTarget = Owner.PlayerTransform.position;
        Vector3 startPos   = Owner.transform.position;
        Vector3 apexPos    = new(startPos.x, startPos.y + slamJumpHeight, startPos.z);

        // 1) 올라가기
        float elapsed = 0f;
        while (elapsed < slamRiseTime)
        {
            elapsed += Time.deltaTime;
            Owner.transform.position = Vector3.Lerp(startPos, apexPos, elapsed / slamRiseTime);
            yield return null;
        }
        Owner.transform.position = apexPos;

        // 2) 정점 유지 — 마커 표시
        GameObject marker = null;
        if (slamMarkerPrefab != null)
            marker = Instantiate(slamMarkerPrefab, slamTarget, Quaternion.identity);

        yield return _waitHangTime;

        // 3) 내려꽂기
        while (Vector3.Distance(Owner.transform.position, slamTarget) > 0.05f)
        {
            Owner.transform.position = Vector3.MoveTowards(
                Owner.transform.position, slamTarget, slamFallSpeed * Time.deltaTime);
            yield return null;
        }
        Owner.transform.position = slamTarget;

        if (marker != null) Destroy(marker);

        // 착지 애니메이션
        if (anim != null && !string.IsNullOrEmpty(slamLandTrigger))
            anim.SetTrigger(slamLandTrigger);

        // 4) 범위 피해 판정
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(slamDamage);

        yield return _waitAfterLand;
        isSlamming = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, slamRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
