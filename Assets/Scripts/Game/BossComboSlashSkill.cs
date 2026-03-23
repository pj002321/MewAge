using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 연속 근접 공격 (ComboSlash).
/// N회 앞으로 짧게 돌진하며 근접 피해를 줍니다.
/// </summary>
public class BossComboSlashSkill : BossSkillBase
{
    [Header("콤보 설정")]
    [SerializeField] private int   hitCount       = 2;
    [SerializeField] private float slashDamage    = 12f;
    [SerializeField] private float slashRange     = 1.8f;   // 피해 판정 거리
    [SerializeField] private float lungeSpeed     = 8f;     // 한 타당 이동 속도
    [SerializeField] private float lungeDuration  = 0.15f;  // 한 타 이동 시간
    [SerializeField] private float betweenDelay   = 0.2f;   // 타 사이 딜레이
    [SerializeField] private string slashTrigger  = "tAttack";

    [Header("VFX")]
    [SerializeField] private GameObject slashVfxPrefab;

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        float damageMult = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;

        for (int i = 0; i < hitCount; i++)
        {
            if (Owner.PlayerTransform == null) yield break;

            // 애니메이션
            Owner.EnemyAnimator?.SetTrigger(slashTrigger);

            // VFX
            if (slashVfxPrefab != null)
                Instantiate(slashVfxPrefab, Owner.transform.position, Quaternion.identity);

            // 플레이어 방향으로 짧게 이동
            Vector2 dir     = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
            float   elapsed = 0f;
            while (elapsed < lungeDuration)
            {
                Owner.MoveInDirection(dir, lungeSpeed);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // 피해 판정
            var player = PlayerController.Instance;
            if (player != null &&
                Vector2.Distance(Owner.transform.position, player.transform.position) <= slashRange)
                player.TakeDamage(slashDamage * damageMult);

            yield return new WaitForSeconds(betweenDelay);
        }

        // 콤보 후 짧은 경직
        yield return new WaitForSeconds(0.25f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, slashRange);
    }
}
