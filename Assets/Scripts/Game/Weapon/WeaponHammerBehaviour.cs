using System.Collections;
using UnityEngine;

/// <summary>해머 행동. 맵 중앙 상공 상승 → 회전 하강 → 범위 충격.</summary>
[System.Serializable]
public class WeaponHammerBehaviour : WeaponBehaviourBase
{
    [SerializeField] private float      hammerRiseHeight     = 4f;
    [SerializeField] private float      hammerRiseTime       = 0.55f;
    [SerializeField] private float      hammerSpinSpeed      = 900f;
    [SerializeField] private float      hammerSlamSpeed      = 28f;
    [SerializeField] private Vector2    hammerImpactWorldPos = Vector2.zero;
    [SerializeField] private float      hammerImpactRadius   = 1.8f;
    [SerializeField] private GameObject hammerImpactVfxPrefab;

    private Coroutine _sequenceCoroutine;
    private bool      _isInSequence;

    public override bool OverridesRotation => _isInSequence;

    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    public override void OnUpdate()
    {
        if (_isInSequence) return;
        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;

        var best = Host.FindBestTarget();
        if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = StartCoroutine(HammerSequenceRoutine(best.Enemy, best.Boss));
    }

    private IEnumerator HammerSequenceRoutine(EnemyController targetEnemy, BossController targetBoss)
    {
        _isInSequence = true;
        Vector3 startLocal    = Host.transform.localPosition;
        Vector3 startWorldPos = Host.transform.position;

        try
        {
            // 착지 위치 결정
            Vector3 impactWorld;
            if      (targetEnemy != null) impactWorld = targetEnemy.transform.position;
            else if (targetBoss  != null) impactWorld = targetBoss.transform.position;
            else                          impactWorld = new Vector3(hammerImpactWorldPos.x, hammerImpactWorldPos.y, 0f);
            impactWorld.z = 0f;

            Vector3    peakWorld = impactWorld + Vector3.up * hammerRiseHeight;
            Vector2    slamDir   = ((Vector2)impactWorld - (Vector2)peakWorld).normalized;
            float      landAngle = Mathf.Atan2(slamDir.y, slamDir.x) * Mathf.Rad2Deg - 90f;
            Quaternion landRot   = Quaternion.Euler(0f, 0f, landAngle);
            float      spinAngle = 0f;

            // 1. 상승
            for (float t = 0f; t < hammerRiseTime; t += Time.deltaTime)
            {
                float ease = (t / hammerRiseTime) * (t / hammerRiseTime);
                Host.transform.position = Vector3.Lerp(startWorldPos, peakWorld, ease);
                spinAngle += hammerSpinSpeed * Time.deltaTime;
                Host.transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
                yield return null;
            }
            Host.transform.position = peakWorld;

            // 2. 정점 정지
            yield return new WaitForSeconds(0.12f);

            // 3. 하강
            Quaternion slamStartRot = Host.transform.rotation;
            float      totalDist    = Vector3.Distance(peakWorld, impactWorld);
            while ((Host.transform.position - impactWorld).sqrMagnitude > 0.04f)
            {
                Host.transform.position = Vector3.MoveTowards(
                    Host.transform.position, impactWorld, hammerSlamSpeed * Time.deltaTime);
                float remaining = Vector3.Distance(Host.transform.position, impactWorld);
                float aligned   = 1f - Mathf.Clamp01(remaining / totalDist);
                Host.transform.rotation = Quaternion.Slerp(slamStartRot, landRot, aligned);
                yield return null;
            }
            Host.transform.position = impactWorld;
            Host.transform.rotation = landRot;

            // 4. 착지 충격 — 범위 데미지
            Vector2 impactCenter = (Vector2)impactWorld;
            int     hitCount     = Physics2D.OverlapCircleNonAlloc(impactCenter, hammerImpactRadius, WeaponPoint.SharedHitBuffer);
            BossController hammerHitBoss = null;
            bool anyHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                var col = WeaponPoint.SharedHitBuffer[i];
                if (hammerHitBoss == null)
                {
                    var b = col.GetComponentInParent<BossController>();
                    if (b != null)
                    {
                        var (bDmg, bCrit) = Host.RollCrit(Host.attackDamage);
                        Vector2 bKnock    = ((Vector2)b.transform.position - impactCenter);
                        b.TakeDamage(bDmg, bCrit,
                            bKnock.sqrMagnitude > 0.001f ? bKnock.normalized : Vector2.up, Host.knockbackPower);
                        Host.ApplyLifeSteal(bDmg);
                        hammerHitBoss = b; anyHit = true;
                        continue;
                    }
                }
                var e = col.GetComponentInParent<EnemyController>();
                if (e == null) continue;
                var (eDmg, eCrit) = Host.RollCrit(Host.attackDamage);
                Vector2 eKnock    = ((Vector2)e.transform.position - impactCenter);
                e.TakeDamage(eDmg, eCrit,
                    eKnock.sqrMagnitude > 0.001f ? eKnock.normalized : Vector2.up, Host.knockbackPower);
                Host.ApplyLifeSteal(eDmg);
                if (Host.dotDamage > 0f) e.ApplyBleed(Host.dotDamage);
                anyHit = true;
            }

            if (anyHit)
            {
                SoundManager.Shared?.PlayWeaponHit(Host.WeaponId);
                Host.ApplyGradeProc(hitCount, Vector2.down, hammerHitBoss, (Vector3)impactCenter);
            }

            if (hammerImpactVfxPrefab != null)
                Object.Destroy(Object.Instantiate(hammerImpactVfxPrefab, (Vector3)impactCenter, Quaternion.identity), 2f);

            // 5. 솟구치기 → 귀환
            Vector3 returnPeak   = impactWorld + Vector3.up * hammerRiseHeight;
            float   riseTime     = hammerRiseTime * 0.35f;
            Vector3 riseStart    = Host.transform.position;

            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                float f    = t / riseTime;
                float ease = f * (2f - f);
                Host.transform.position = Vector3.Lerp(riseStart, returnPeak, ease);
                yield return null;
            }
            Host.transform.position = returnPeak;

            float returnSlamSpeed = hammerSlamSpeed * 1.5f;
            while (true)
            {
                Vector3 returnTarget = Host.transform.parent.TransformPoint(startLocal);
                Host.transform.position = Vector3.MoveTowards(
                    Host.transform.position, returnTarget, returnSlamSpeed * Time.deltaTime);
                if ((Host.transform.position - returnTarget).sqrMagnitude <= 0.01f) break;
                yield return null;
            }
        }
        finally
        {
            Host.transform.localPosition = startLocal;
            Host.transform.rotation      = Quaternion.identity;
            Host.cooldownTimer           = Host.attackCooldown;
            _isInSequence                = false;
            _sequenceCoroutine           = null;
        }
    }
}
