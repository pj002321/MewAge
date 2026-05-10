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
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private float      vfxLifetime     = 1f;

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
        if (Owner.PlayerTransform == null) { isSlamming = false; yield break; }

        Vector3 origScale = Owner.transform.localScale;
        Vector3 startPos  = Owner.transform.position;

        // 1) 점프 준비 — Y 찌부 후 원래 스케일로 복귀
        const float anticipationDur = 0.18f;
        Vector3 crouchScale = new(origScale.x * 1.35f, origScale.y * 0.5f, origScale.z);
        float t = 0f;
        while (t < anticipationDur)
        {
            t += Time.deltaTime;
            float pct     = Mathf.Clamp01(t / anticipationDur);
            float squishT = pct < 0.5f ? pct * 2f : (1f - pct) * 2f; // 찌부 → 복귀
            Owner.transform.localScale = Vector3.Lerp(origScale, crouchScale, squishT);
            yield return null;
        }
        Owner.transform.localScale = origScale;

        // 2) 빠른 상승 — 세로 스트레치하며 화면 밖으로
        Vector3 apexPos = new(startPos.x, startPos.y + slamJumpHeight, startPos.z);
        t = 0f;
        while (t < slamRiseTime)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / slamRiseTime);
            Owner.transform.position   = Vector3.Lerp(startPos, apexPos, pct);
            Owner.transform.localScale = new Vector3(
                origScale.x * Mathf.Lerp(1f, 0.7f, pct),
                origScale.y * Mathf.Lerp(1f, 1.4f, pct),
                origScale.z);
            yield return null;
        }
        Owner.transform.position   = apexPos;
        Owner.transform.localScale = origScale;

        // 3) 공중 대기 — 플레이어 위치 위로 수평 이동 + 마커 표시
        Vector3 slamTarget = Owner.PlayerTransform != null
            ? Owner.PlayerTransform.position
            : startPos;
        GameObject marker = null;
        if (slamMarkerPrefab != null)
            marker = Instantiate(slamMarkerPrefab, slamTarget, Quaternion.identity);

        // 플레이어 바로 위로 수평 이동 (hang 중에 은밀히)
        Vector3 aboveTarget = new(slamTarget.x, apexPos.y, apexPos.z);
        Owner.transform.position = aboveTarget;

        yield return _waitHangTime;

        // 4) 수직 낙하 — 세로 스트레치하며 내려꽂기
        while (Vector3.Distance(Owner.transform.position, slamTarget) > 0.05f)
        {
            float totalFall = Vector3.Distance(aboveTarget, slamTarget);
            float remaining = Vector3.Distance(Owner.transform.position, slamTarget);
            float fallPct   = totalFall > 0f ? 1f - Mathf.Clamp01(remaining / totalFall) : 1f;
            Owner.transform.localScale = new Vector3(
                origScale.x * Mathf.Lerp(0.7f, 1f, fallPct),
                origScale.y * Mathf.Lerp(1.4f, 1f, fallPct),
                origScale.z);
            Owner.transform.position = Vector3.MoveTowards(
                Owner.transform.position, slamTarget, slamFallSpeed * Time.deltaTime);
            yield return null;
        }
        Owner.transform.position   = slamTarget;
        Owner.transform.localScale = origScale;

        if (marker != null) Destroy(marker);

        anim?.SetTrigger(EnemyAnim.TAttack);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Owner.Decal != null ? Owner.Decal.position : slamTarget;
            Destroy(Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        // 5) 착지 스쿼시 → 원래 스케일 복귀
        yield return Owner.StartCoroutine(LandSquish(origScale));

        // 6) 범위 피해 판정
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(slamDamage);

        yield return _waitAfterLand;
        isSlamming = false;
    }

    private IEnumerator LandSquish(Vector3 origScale)
    {
        const float squishDur  = 0.08f;
        const float restoreDur = 0.18f;
        Vector3 squishScale = new(origScale.x * 1.45f, origScale.y * 0.45f, origScale.z);

        float t = 0f;
        while (t < squishDur)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(origScale, squishScale, Mathf.Clamp01(t / squishDur));
            yield return null;
        }
        t = 0f;
        while (t < restoreDur)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(squishScale, origScale, Mathf.Clamp01(t / restoreDur));
            yield return null;
        }
        Owner.transform.localScale = origScale;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, slamRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
