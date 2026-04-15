using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 큰 돌 굴리기.
///
/// 흐름:
///   1) 잠깐 멈춰서 조준 (preDelay)
///   2) 큰 돌 투사체를 플레이어 방향으로 천천히 굴림
///      — 발사 중 보스 이동 정지
///   3) repeatCount 번 반복 후 종료
/// </summary>
public class BossRollingBoulderSkill : BossSkillBase
{
    [Header("바위 프리팹")]
    [Tooltip("BossRollingProjectile 컴포넌트가 붙은 바위 프리팹")]
    [SerializeField] private GameObject boulderPrefab;

    [Header("바위 데이터 (SO)")]
    [Tooltip("바위 속성 설정 데이터. null이 아니면 아래 개별 설정보다 우선합니다.")]
    [SerializeField] private BossRollingProjectileData boulderData;

    [Header("바위 개별 설정 (Data가 없을 때 사용)")]
    [Tooltip("바위 이동 속도 (느릴수록 큰 돌 느낌)")]
    [SerializeField] private float      boulderSpeed    = 3.5f;
    [Tooltip("바위 자동 소멸 시간 (초)")]
    [SerializeField] private float      boulderLifetime = 6f;
    [Tooltip("굴림 회전 속도 (도/s)")]
    [SerializeField] private float      rotateSpeed     = 270f;
    [SerializeField] private float      boulderDamage   = 0f; // 0이면 ContactDamage 사용
    [SerializeField] private int        maxBounces      = 3;

    [Header("발사 지점")]
    [Tooltip("null이면 보스 중심에서 발사")]
    [SerializeField] private Transform firePoint;

    [Header("타이밍")]
    [Tooltip("발사 전 조준 딜레이 (초)")]
    [SerializeField] private float preDelay     = 0.6f;
    [Tooltip("한 사이클에서 바위를 몇 번 굴리는지")]
    [SerializeField] private int   rollCount    = 2;
    [Tooltip("연속 발사 사이 간격 (초)")]
    [SerializeField] private float rollInterval = 0.8f;

    [Header("애니메이션")]
    [SerializeField] private string attackTrigger = "tAttack";

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        Boss.IsFiringSkill = true;
        try
        {
            // 1) 조준 딜레이
            if (preDelay > 0f)
                yield return new WaitForSeconds(preDelay);

            if (Owner.PlayerTransform == null) yield break;

            // 2) 바위 굴리기 (rollCount 회)
            for (int i = 0; i < rollCount; i++)
            {
                if (Owner.PlayerTransform == null) yield break;

                Owner.EnemyAnimator?.SetTrigger(attackTrigger);
                this.DLog($"Rolling boulder {i + 1}/{rollCount}");
                SpawnBoulder();

                if (i < rollCount - 1)
                    yield return new WaitForSeconds(rollInterval);
            }

            yield return new WaitForSeconds(0.4f);
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    private void SpawnBoulder()
    {
        if (boulderPrefab == null || Owner.PlayerTransform == null) return;

        Vector2 spawnPos = firePoint != null
            ? (Vector2)firePoint.position
            : Owner.Rb != null ? Owner.Rb.position : (Vector2)Owner.transform.position;

        Vector2 dir    = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;
        float dmgMult  = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
        float dmg      = (boulderDamage > 0f ? boulderDamage : Owner.ContactDamage) * dmgMult;

        var go = Instantiate(boulderPrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<BossRollingProjectile>(out var boulder))
        {
            float finalSpeed    = boulderData != null ? boulderData.Speed : boulderSpeed;
            float finalLifetime = boulderData != null ? boulderData.LifeTime : boulderLifetime;
            float finalRotate   = boulderData != null ? boulderData.RotateSpeed : rotateSpeed;
            float finalDmg      = boulderData != null ? boulderData.Damage : dmg;
            int   finalBounces  = boulderData != null ? boulderData.MaxBounces : maxBounces;

            boulder.Init(finalDmg, dir, finalSpeed, finalLifetime, finalRotate, finalBounces);
            boulder.SetOwnerBoss(Boss);
        }
    }
}
