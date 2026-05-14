using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 낙탄 살포 (Volley).
///
/// 흐름:
///   1) tAttack 트리거
///   2) 플레이어 주변 N개 지점에 마커 동시 생성
///   3) hangTime 동안 체공 (마커 펄스)
///   4) burstInterval 간격으로 순서대로 충격
///      — 충격마다: 범위 데미지 + 충격 VFX + 카메라 셰이크
/// </summary>
public class BossVolleySkill : BossSkillBase
{
    [Header("살포 패턴")]
    [SerializeField] private int   pointCount     = 6;
    [SerializeField] private float spreadRadius   = 1.8f;
    [Tooltip("랜덤 시작 각도. false면 angleOffset 고정값 사용")]
    [SerializeField] private bool  randomizeAngle = true;
    [SerializeField] private float angleOffset    = 0f;

    [Header("타이밍")]
    [SerializeField] private float preDelay       = 0.15f;  // tAttack → 마커 생성 전 딜레이
    [SerializeField] private float hangTime       = 1.0f;   // 마커 생성 후 첫 충격까지 체공 시간
    [SerializeField] private float burstInterval  = 0.12f;  // 충격 사이 간격

    [Header("충격 판정")]
    [SerializeField] private float hitRadius      = 0.8f;
    [SerializeField] private float hitDamage      = 0f;     // 0이면 ContactDamage 사용

    [Header("프리팹")]
    [Tooltip("ImpactMarker 컴포넌트가 붙은 마커 프리팹")]
    [SerializeField] private GameObject markerPrefab;
    [Tooltip("MortarShell 컴포넌트가 붙은 포탄 시각 프리팹")]
    [SerializeField] private GameObject shellPrefab;
    [SerializeField] private float      shellArcHeight = 3f;
    [Tooltip("충격 지점에 생성할 VFX 프리팹 (파티클 등)")]
    [SerializeField] private GameObject impactVfxPrefab;

    [Header("카메라 셰이크")]
    [SerializeField] private float shakeDuration  = 0.2f;
    [SerializeField] private float shakeMagnitude = 0.25f;

    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        // 1) 애니메이션 트리거
        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        if (Owner.PlayerTransform == null) yield break;

        // 2) 플레이어 위치 스냅 → N개 지점 & 마커 + 포탄 동시 생성
        Vector2 playerPos  = Owner.PlayerTransform.position;
        float   startAngle = randomizeAngle ? Random.Range(0f, 360f) : angleOffset;
        float   step       = 360f / Mathf.Max(1, pointCount);

        float finalDmg = hitDamage > 0f ? hitDamage : Owner.ContactDamage;

        Vector2[]      positions = new Vector2[pointCount];
        ImpactMarker[] markers   = new ImpactMarker[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float rad    = (startAngle + step * i) * Mathf.Deg2Rad;
            positions[i] = playerPos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spreadRadius;

            if (markerPrefab != null)
            {
                var obj = Object.Instantiate(markerPrefab, positions[i], Quaternion.identity);
                markers[i] = obj.GetComponent<ImpactMarker>();
            }

            // 포탄: 보스 위치 → 목표 지점, hangTime 동안 포물선. 착지 시 데미지 처리
            if (shellPrefab != null)
            {
                var shell = Object.Instantiate(shellPrefab, (Vector3)Owner.Rb.position, Quaternion.identity);
                shell.GetComponent<MortarShell>()?.Init(
                    Owner.Rb.position, positions[i], hangTime, shellArcHeight, finalDmg, hitRadius);
            }
        }

        // 3) 체공 대기 (포탄이 날아가는 시간)
        yield return new WaitForSeconds(hangTime);

        // 4) 순차 충격 연출 (파바박) — 데미지는 MortarShell 이 처리
        for (int i = 0; i < pointCount; i++)
        {
            if (markers[i] != null)
                markers[i].Dismiss();

            if (impactVfxPrefab != null)
                Object.Instantiate(impactVfxPrefab, positions[i], Quaternion.identity);

            CameraFollow.Shared?.Shake(shakeDuration, shakeMagnitude);

            yield return new WaitForSeconds(burstInterval);
        }

        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>DB_보스 투사체 데이터를 적용합니다.</summary>
    public void ApplyProjectileData(ProjectileData data)
    {
        if (data == null) return;
        if (data.prefab  != null) shellPrefab = data.prefab;
        if (data.damage  >  0f)  hitDamage   = data.damage;
    }
}
