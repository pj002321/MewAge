using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 내려찍기 (AreaSlam).
/// 위로 점프 → 목표 위치에 마커 표시 → 내려꽂기 → 범위 피해 판정.
/// </summary>
public class BossSlamSkill : BossSkillBase
{
    [Header("내려찍기")]
    [SerializeField] private float      slamDamage       = 30f;
    [SerializeField] private float      slamRadius       = 2.5f;
    [SerializeField] private float      slamJumpHeight   = 6f;
    [SerializeField] private float      slamRiseTime     = 0.4f;
    [SerializeField] private float      slamHangTime     = 0.8f;
    [SerializeField] private float      slamFallSpeed    = 25f;
    [Tooltip("낙하 최대 시간(초) — 거리와 무관하게 이 시간 안에 내려찍습니다.")]
    [SerializeField] private float      slamFallDuration = 0.5f;
    [SerializeField] private GameObject slamMarkerPrefab;
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private float      vfxLifetime      = 1f;
    [SerializeField] private string     slamLandTrigger  = "SlamLand";

    private Animator anim;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        anim = boss.EnemyAnimator;
    }

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

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
            float squishT = pct < 0.5f ? pct * 2f : (1f - pct) * 2f;
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

        Vector3 aboveTarget = new(slamTarget.x, apexPos.y, apexPos.z);
        Owner.transform.position = aboveTarget;

        yield return new WaitForSeconds(slamHangTime);

        // 4) 수직 낙하 — 세로 스트레치하며 내려꽂기
        float fallElapsed = 0f;
        while (fallElapsed < slamFallDuration)
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
            fallElapsed += Time.deltaTime;
            if (Vector3.Distance(Owner.transform.position, slamTarget) <= 0.05f) break;
            yield return null;
        }
        Owner.transform.position   = slamTarget;
        Owner.transform.localScale = origScale;

        if (marker != null) Destroy(marker);

        if (anim != null && !string.IsNullOrEmpty(slamLandTrigger))
            anim.SetTrigger(slamLandTrigger);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Owner.Decal != null ? Owner.Decal.position : slamTarget;
            Destroy(Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        // 5) 착지 스쿼시 → 원래 스케일 복귀
        yield return StartCoroutine(LandSquish(origScale));

        // 6) 범위 피해 판정
        float dmg = slamDamage * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f);
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(dmg);

        yield return new WaitForSeconds(0.3f);
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
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
