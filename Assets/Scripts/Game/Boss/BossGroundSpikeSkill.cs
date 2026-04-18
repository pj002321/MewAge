using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 낙하 스파이크 ↔ 부채꼴 발사 교대 사이클.
///
/// 사이클:
///   1) [스파이크] 플레이어 중심 좌우로 N개 마커 표시 → 마커 제거 → 위에서 수직 낙하
///   2) [추격]     N초 추격
///   3) [부채꼴]   부채꼴 투사체 M회 발사
///   4) [추격]     N초 추격
/// </summary>
public class BossGroundSpikeSkill : BossSkillBase
{
    [Header("스파이크 투사체")]
    [Tooltip("BossSpikeProjectile 컴포넌트가 붙은 스파이크 프리팹")]
    [SerializeField] private GameObject spikeProjectilePrefab;
    [SerializeField] private float      spikeDropSpeed   = 18f;
    [SerializeField] private float      spikeHitRadius   = 0.7f;
    [SerializeField] private float      stickDuration    = 0.8f;
    [SerializeField] private float      spikeDamage      = 0f; // 0이면 ContactDamage 사용

    [Header("착지 VFX")]
    [Tooltip("스파이크 착지 시 생성할 이펙트 프리팹 (없으면 생략)")]
    [SerializeField] private GameObject spikeImpactVfx;
    [SerializeField] private float      vfxLifetime = 1f;

    [Header("부채꼴 투사체")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed  = 7f;
    [SerializeField] private float      projectileDamage = 0f;

    [Header("발사 지점 (부채꼴)")]
    [Tooltip("null이면 보스 중심")]
    [SerializeField] private Transform firePoint;

    [Header("경고 마커")]
    [Tooltip("ImpactMarker 컴포넌트가 붙은 마커 프리팹")]
    [SerializeField] private GameObject spikeMarkerPrefab;
    [Tooltip("마커 표시 시간 (초)")]
    [SerializeField] private float      spikeWarningTime = 0.9f;
    [Tooltip("마커 제거 후 스파이크 낙하 시작까지 딜레이 (초)")]
    [SerializeField] private float      postMarkerDelay  = 0.1f;

    [Header("스파이크 배치")]
    [Tooltip("스파이크 개수")]
    [SerializeField] private int        spikeCount        = 5;
    [Tooltip("스파이크 간 거리 (유닛)")]
    [SerializeField] private float      spikeDistance     = 1.2f;
    [Tooltip("스파이크 하나씩 낙하 시작 간격 (초)")]
    [SerializeField] private float      spikeStrikeDelay  = 0.15f;
    [Tooltip("스파이크 스폰 높이 — 카메라 상단 기준 추가 오프셋")]
    [SerializeField] private float      spawnHeightOffset = 3f;
    [Tooltip("스파이크 pivot → 끝부분(tip) 거리. 스프라이트 pivot이 중앙이면 절반 높이값 입력.\n" +
             "양수 = 끝이 마커보다 위에 멈춤 → 값을 늘림. 음수 = 마커 아래로 꽂힘.")]
    [SerializeField] private float      pivotToTipOffset  = 0f;

    [Header("추격")]
    [SerializeField] private float chaseAfterSpike = 2f;
    [SerializeField] private float chaseAfterFan   = 2f;
    [SerializeField] private float chaseStopDist   = 1.8f;

    [Header("부채꼴 버스트")]
    [SerializeField] private int   fanCount       = 5;
    [SerializeField] private float fanAngle       = 120f;
    [SerializeField] private int   fanRepeat      = 3;
    [SerializeField] private float fanRepeatDelay = 0.6f;

    [Header("페이즈 ON/OFF")]
    [SerializeField] private bool useSpikePhase = true;
    [SerializeField] private bool useFanPhase   = true;

    [Header("애니메이션")]
    [SerializeField] private string attackTrigger = "tAttack";

    public override void Init(BossController boss)
    {
        base.Init(boss);
        executeInPattern = false;
        StartCoroutine(CycleLoop());
    }

    protected override IEnumerator OnExecute() { yield break; }

    // ── 메인 루프 ─────────────────────────────────────────────────

    private IEnumerator CycleLoop()
    {
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
                yield return new WaitUntil(() => GameManager.Shared == null || !GameManager.Shared.IsGameOver);

            if (IsInvalid()) yield break;

            if (useSpikePhase)
            {
                yield return SpikeLinePhase();
                if (IsInvalid()) yield break;
                yield return ChasePhase(chaseAfterSpike);
                if (IsInvalid()) yield break;
            }

            if (useFanPhase)
            {
                yield return FanBurstPhase();
                if (IsInvalid()) yield break;
                yield return ChasePhase(chaseAfterFan);
                if (IsInvalid()) yield break;
            }
        }
    }

    // ── 페이즈 ────────────────────────────────────────────────────

    private IEnumerator SpikeLinePhase()
    {
        if (Owner.PlayerTransform == null) yield break;

        var positions     = new Vector2[spikeCount];
        var markers       = new ImpactMarker[spikeCount];
        var markerObjects = new GameObject[spikeCount];

        Boss.IsFiringSkill = true;
        try
        {
            // 플레이어 위치 중심, 보스→플레이어 수직 방향으로 균등 배치
            Vector2 playerPos  = Owner.PlayerTransform.position;
            Vector2 toPlayer   = ((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized;
            Vector2 spreadDir  = new Vector2(-toPlayer.y, toPlayer.x); // 수직 방향

            float totalSpan = spikeDistance * (spikeCount - 1);
            Vector2 startPos = playerPos - spreadDir * (totalSpan * 0.5f);

            // 마커 일괄 생성
            for (int i = 0; i < spikeCount; i++)
            {
                positions[i] = startPos + spreadDir * spikeDistance * i;
                if (spikeMarkerPrefab != null)
                {
                    var obj = Instantiate(spikeMarkerPrefab, positions[i], Quaternion.identity);
                    markerObjects[i] = obj;
                    markers[i] = obj.GetComponent<ImpactMarker>();
                }
            }

            // ① 경고 대기
            yield return new WaitForSeconds(spikeWarningTime);
            if (IsInvalid()) yield break;

            // ② 마커 전체 제거
            for (int i = 0; i < spikeCount; i++)
            {
                if (markers[i] != null) markers[i].Dismiss();
                else if (markerObjects[i] != null) Destroy(markerObjects[i]);
                markers[i] = null; markerObjects[i] = null;
            }

            yield return new WaitForSeconds(postMarkerDelay);
            if (IsInvalid()) yield break;

            Owner.EnemyAnimator?.SetTrigger(attackTrigger);

            // ③ 스파이크 수직 낙하 — 코루틴이 직접 이동 제어 (컴포넌트 무관)
            float spawnY  = GetSpawnY();
            float dmgMult = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
            float dmg     = (spikeDamage > 0f ? spikeDamage : Owner.ContactDamage) * dmgMult;

            for (int i = 0; i < spikeCount; i++)
            {
                if (IsInvalid()) break;

                if (spikeProjectilePrefab != null)
                {
                    var spawnPos = new Vector3(positions[i].x, spawnY, 0f);
                    var go = Instantiate(spikeProjectilePrefab, spawnPos, Quaternion.identity);
                    float targetY = positions[i].y + pivotToTipOffset;
                    StartCoroutine(MoveSpikeDown(go, targetY, dmg));
                }

                if (i < spikeCount - 1)
                    yield return new WaitForSeconds(spikeStrikeDelay);
            }

            // ④ 마지막 스파이크가 착지 + 박힘 시간까지 대기
            float fallHeight = spawnY - positions[0].y;
            yield return new WaitForSeconds(fallHeight / spikeDropSpeed + stickDuration + 0.2f);
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null) markers[i].Dismiss();
                else if (markerObjects[i] != null) Destroy(markerObjects[i]);
                markers[i] = null; markerObjects[i] = null;
            }
        }
    }

    private IEnumerator FanBurstPhase()
    {
        Boss.IsFiringSkill = true;
        try
        {
            for (int i = 0; i < fanRepeat; i++)
            {
                if (IsInvalid()) yield break;
                FireFan();
                if (i < fanRepeat - 1)
                    yield return new WaitForSeconds(fanRepeatDelay);
            }
            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    private IEnumerator ChasePhase(float duration)
    {
        float elapsed   = 0f;
        float speedMult = Boss != null && Boss.IsEnraged ? Boss.EnrageSpeedMult : 1f;

        while (elapsed < duration)
        {
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver) yield break;
            if (IsInvalid()) yield break;

            if (Owner.PlayerTransform != null)
            {
                float dist = Vector2.Distance(Owner.transform.position, Owner.PlayerTransform.position);
                if (dist > chaseStopDist)
                    Owner.MoveTowardPlayer(Owner.MoveSpeed * speedMult);
                Owner.FlipToward(((Vector2)Owner.PlayerTransform.position - (Vector2)Owner.transform.position).normalized);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 부채꼴 발사 ───────────────────────────────────────────────

    private void FireFan()
    {
        if (projectilePrefab == null || Owner.PlayerTransform == null) return;

        Owner.EnemyAnimator?.SetTrigger(attackTrigger);

        Vector2 spawnPos  = firePoint != null ? (Vector2)firePoint.position
                          : Owner.Rb != null  ? Owner.Rb.position
                          : (Vector2)Owner.transform.position;
        Vector2 toPlayer  = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;
        float   baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

        int   count    = Mathf.Max(1, fanCount);
        float halfSpan = fanAngle * 0.5f;
        float step     = count > 1 ? fanAngle / (count - 1) : 0f;

        float dmgMult = Boss != null && Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;
        float dmg     = (projectileDamage > 0f ? projectileDamage : Owner.ContactDamage) * dmgMult;

        for (int i = 0; i < count; i++)
        {
            float   angle = baseAngle - halfSpan + step * i;
            Vector2 dir   = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<Projectile>(out var p)) { p.Init(dmg, dir, projectileSpeed); p.SetOwnerBoss(Boss); }
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private float GetSpawnY()
    {
        var cam = Camera.main;
        return cam != null
            ? cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, cam.nearClipPlane)).y + spawnHeightOffset
            : Owner.transform.position.y + 15f;
    }

    /// <summary>
    /// 스파이크 오브젝트를 직접 targetY까지 이동시키고 멈춥니다.
    /// 프리팹에 어떤 컴포넌트가 붙어있든 이 코루틴이 이동을 제어합니다.
    /// </summary>
    private IEnumerator MoveSpikeDown(GameObject spike, float targetY, float damage)
    {
        if (spike == null) yield break;

        // 프리팹에 Rigidbody2D가 있으면 물리가 간섭하지 않도록 Kinematic으로 설정
        if (spike.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // targetY까지 수직 낙하
        while (spike != null)
        {
            Vector3 pos = spike.transform.position;
            pos.y -= spikeDropSpeed * Time.deltaTime;

            if (pos.y <= targetY)
            {
                pos.y = targetY;
                spike.transform.position = pos;
                break;
            }

            spike.transform.position = pos;
            yield return null;
        }

        if (spike == null) yield break;

        // 착지 — VFX (프리팹 자식에 "VfxPoint" Transform이 있으면 그 위치 사용)
        if (spikeImpactVfx != null)
        {
            var vfxPoint = spike.transform.Find("VfxPoint");
            Vector3 vfxPos = vfxPoint != null ? vfxPoint.position : spike.transform.position;
            var vfxGo = Instantiate(spikeImpactVfx, vfxPos, Quaternion.identity);
            Destroy(vfxGo, vfxLifetime);
        }

        // 착지 — 데미지 판정
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(spike.transform.position, player.transform.position) <= spikeHitRadius)
            player.TakeDamage(damage);

        // stickDuration 동안 박힌 채 유지
        yield return new WaitForSeconds(stickDuration);
        if (spike != null) Destroy(spike);
    }

    private bool IsInvalid() => Owner == null;
}
