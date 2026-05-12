using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 내려찍기 (AreaSlam).
/// 마커 표시 → 포물선 점프 → 착지 충격파 판정.
/// </summary>
public class BossSlamSkill : BossSkillBase
{
    [Header("내려찍기")]
    [SerializeField] private float slamDamage  = 30f;
    [SerializeField] private float slamRadius  = 2.5f;
    [SerializeField] private float jumpHeight  = 6f;
    [SerializeField] private float jumpDuration = 0.7f;

    [Header("마커 경고")]
    [Tooltip("점프 전 마커를 보여주는 시간 (초)")]
    [SerializeField] private float markerWarningTime = 0.8f;

    [Header("VFX")]
    [SerializeField] private GameObject slamMarkerPrefab;
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private float      vfxLifetime = 1f;

    private Animator _anim;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        _anim = boss.EnemyAnimator;
    }

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        Vector3 startPos  = Owner.transform.position;
        Vector3 slamTarget = Owner.PlayerTransform.position;
        Vector3 origScale = Owner.transform.localScale;

        // 1) 마커 표시 + 경고 대기
        GameObject marker = slamMarkerPrefab != null
            ? Instantiate(slamMarkerPrefab, slamTarget, Quaternion.identity)
            : null;

        yield return new WaitForSeconds(markerWarningTime);

        // 2) 점프 전 찌부 예비 동작
        yield return StartCoroutine(AnticipationSquish(origScale));

        // 3) 포물선 점프
        yield return StartCoroutine(ParabolicJump(startPos, slamTarget, origScale));

        // 4) 착지
        Owner.transform.position   = slamTarget;
        Owner.transform.localScale = origScale;
        if (marker != null) Destroy(marker);

        _anim?.SetTrigger(EnemyAnim.TAttack);

        if (landingVfxPrefab != null)
        {
            Vector3 vfxPos = Owner.Decal != null ? Owner.Decal.position : slamTarget;
            Destroy(Instantiate(landingVfxPrefab, vfxPos, Quaternion.identity), vfxLifetime);
        }

        yield return StartCoroutine(LandSquish(origScale));

        // 5) 범위 피해
        float dmg = slamDamage * (Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f);
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(Owner.transform.position, player.transform.position) <= slamRadius)
            player.TakeDamage(dmg, sourceName: Owner.gameObject.name);

        yield return new WaitForSeconds(0.3f);
    }

    // 점프 전 찌부→복귀
    private IEnumerator AnticipationSquish(Vector3 origScale)
    {
        const float dur = 0.18f;
        Vector3 squish = new(origScale.x * 1.35f, origScale.y * 0.5f, origScale.z);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / dur);
            float s   = pct < 0.5f ? pct * 2f : (1f - pct) * 2f;
            Owner.transform.localScale = Vector3.Lerp(origScale, squish, s);
            yield return null;
        }
        Owner.transform.localScale = origScale;
    }

    // 포물선 이동: X는 선형, Y는 sin 포물선
    private IEnumerator ParabolicJump(Vector3 from, Vector3 to, Vector3 origScale)
    {
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += jumpHeight * Mathf.Sin(Mathf.PI * t); // 포물선

            Owner.transform.position = pos;

            // 상승 중엔 Y 스트레치, 하강 중엔 복귀
            float stretchT = t < 0.5f ? t * 2f : (1f - t) * 2f;
            Owner.transform.localScale = new Vector3(
                origScale.x * Mathf.Lerp(1f, 0.7f, stretchT),
                origScale.y * Mathf.Lerp(1f, 1.4f, stretchT),
                origScale.z);

            yield return null;
        }
    }

    // 착지 스쿼시
    private IEnumerator LandSquish(Vector3 origScale)
    {
        const float squishDur  = 0.08f;
        const float restoreDur = 0.18f;
        Vector3 squish = new(origScale.x * 1.45f, origScale.y * 0.45f, origScale.z);

        float t = 0f;
        while (t < squishDur)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(origScale, squish, Mathf.Clamp01(t / squishDur));
            yield return null;
        }
        t = 0f;
        while (t < restoreDur)
        {
            t += Time.deltaTime;
            Owner.transform.localScale = Vector3.Lerp(squish, origScale, Mathf.Clamp01(t / restoreDur));
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
