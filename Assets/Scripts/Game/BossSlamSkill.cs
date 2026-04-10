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

        yield return new WaitForSeconds(slamHangTime);

        // 3) 내려꽂기 — 거리 상관없이 slamFallDuration 안에 도달
        float fallElapsed = 0f;
        while (fallElapsed < slamFallDuration)
        {
            Owner.transform.position = Vector3.MoveTowards(
                Owner.transform.position, slamTarget, slamFallSpeed * Time.deltaTime);
            fallElapsed += Time.deltaTime;
            if (Vector3.Distance(Owner.transform.position, slamTarget) <= 0.05f) break;
            yield return null;
        }
        Owner.transform.position = slamTarget;

        if (marker != null) Destroy(marker);

        // 착지 애니메이션 트리거
        if (anim != null && !string.IsNullOrEmpty(slamLandTrigger))
            anim.SetTrigger(slamLandTrigger);

        // 4) 범위 피해 판정
        float dmg = slamDamage * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f);
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(dmg);

        yield return new WaitForSeconds(0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
