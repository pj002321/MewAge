using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>슬래시(호 베기) 행동. 텔레포트 → 반원 호 베기 → 귀환 코루틴.</summary>
[System.Serializable]
public class WeaponSlashBehaviour : WeaponBehaviourBase
{
    [SerializeField] private float      slashRadius          = 1.2f;
    [SerializeField] private float      slashArcDegrees      = 180f;
    [SerializeField] private float      slashSweepSpeed      = 480f;
    [SerializeField] private float      slashHitRadius       = 0.35f;
    [SerializeField] private GameObject slashVfxPrefab;
    [SerializeField] private float      slashVfxDuration     = 1f;
    [SerializeField] private float      slashTeleportMaxDist = 3.0f;
    [SerializeField] private float      slashFadeTime        = 0.12f;
    [SerializeField] private GameObject projectilePrefab;   // 타격 시 스폰할 이펙트 프리팹(선택)

    public event System.Action<Vector3, Vector2> OnSlashVfxSpawned;

    private Coroutine _sequenceCoroutine;
    private bool      _isInSequence;

    private float _currentAngle;
    private float _centerAngle;
    private float _endAngle;
    private float _activeRadius;
    private bool  _vfxSpawned;
    private readonly HashSet<EnemyController> _slashHitEnemies = new();
    private bool _slashHitBoss;

    public override bool OverridesRotation => _isInSequence;

    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    public override void OnUpdate()
    {
        if (_isInSequence) return;

        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;

        var best = Host.FindBestTarget();
        if (best.IsNone) return;

        if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = StartCoroutine(SlashSequenceRoutine(best.Enemy, best.Boss));
    }

    // ── 슬래시 시퀀스 ────────────────────────────────────────────────────
    private IEnumerator SlashSequenceRoutine(EnemyController enemy, BossController boss)
    {
        _isInSequence      = true;
        Host.cooldownTimer = Host.attackCooldown;

        Vector2 playerPos = Host.transform.parent != null
            ? (Vector2)Host.transform.parent.position : (Vector2)Host.transform.position;

        try
        {
            // 1. 페이드 아웃
            Host.StopTrail();
            yield return StartCoroutine(FadeWeapon(1f, 0f, slashFadeTime));

            // 2. 적 방향 텔레포트 (최대 거리 제한)
            Vector2 targetWorldPos;
            if      (enemy != null) targetWorldPos = enemy.transform.position;
            else if (boss  != null) targetWorldPos = boss.transform.position;
            else yield break;

            Vector2 toTarget    = targetWorldPos - playerPos;
            float   clampedDist = Mathf.Min(toTarget.magnitude * 0.85f, slashTeleportMaxDist);
            Vector2 localOffset = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized * clampedDist : Vector2.up * clampedDist;

            Host.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            float faceAngle = Mathf.Atan2(localOffset.y, localOffset.x) * Mathf.Rad2Deg - 90f;
            Host.transform.rotation = Quaternion.Euler(0f, 0f, faceAngle);

            // 3. 페이드 인
            yield return StartCoroutine(FadeWeapon(0f, 1f, slashFadeTime));

            // 4. 슬래시 호 초기화
            Vector2 weaponFromPlayer = (Vector2)Host.transform.localPosition;
            _activeRadius  = Mathf.Max(weaponFromPlayer.magnitude, slashRadius);
            _centerAngle   = Mathf.Atan2(weaponFromPlayer.y, weaponFromPlayer.x) * Mathf.Rad2Deg;
            _currentAngle  = _centerAngle + slashArcDegrees * 0.5f;
            _endAngle      = _centerAngle - slashArcDegrees * 0.5f;
            _slashHitEnemies.Clear();
            _slashHitBoss  = false;
            _vfxSpawned    = false;

            float startRad = _currentAngle * Mathf.Deg2Rad;
            Host.transform.localPosition = new Vector3(
                Mathf.Cos(startRad), Mathf.Sin(startRad), 0f) * _activeRadius;

            // 5. 호를 그리며 베기
            while (_currentAngle > _endAngle)
            {
                _currentAngle -= slashSweepSpeed * Time.deltaTime;
                float rad = _currentAngle * Mathf.Deg2Rad;
                Host.transform.localPosition = new Vector3(
                    Mathf.Cos(rad), Mathf.Sin(rad), 0f) * _activeRadius;
                Host.transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle - 90f);

                if (!_vfxSpawned && _currentAngle <= _centerAngle)
                {
                    _vfxSpawned = true;
                    SpawnSlashVfx(playerPos);
                }

                // 베기 판정 (OverlapCircle로 직접 처리)
                PerformSlashHit();
                yield return null;
            }

            // 6. 베기 후 페이드 아웃
            yield return StartCoroutine(FadeWeapon(1f, 0f, slashFadeTime));

            // 7. 원위치 복귀 후 페이드 인
            Host.transform.localPosition = Vector3.zero;
            yield return StartCoroutine(FadeWeapon(0f, 1f, slashFadeTime));
        }
        finally
        {
            RestoreWeapon();
        }
    }

    private void PerformSlashHit()
    {
        int colCount = Physics2D.OverlapCircleNonAlloc(Host.transform.position, slashHitRadius, WeaponPoint.SharedHitBuffer);

        for (int i = 0; i < colCount; i++)
        {
            var col = WeaponPoint.SharedHitBuffer[i];

            if (!_slashHitBoss)
            {
                var boss = col.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    _slashHitBoss = true;
                    Vector2 bDir = ((Vector2)boss.transform.position - (Vector2)Host.transform.position);
                    if (bDir.sqrMagnitude < 0.001f) bDir = Vector2.right;
                    else bDir = bDir.normalized;
                    var (bDmg, bCrit) = Host.RollCrit(Host.attackDamage);
                    boss.TakeDamage(bDmg, bCrit, bDir, Host.knockbackPower);
                    Host.ApplyLifeSteal(bDmg);
                    Host.TrySpecialEffectBoss(boss, bDmg);
                    SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
                    continue;
                }
            }

            var enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null || _slashHitEnemies.Contains(enemy)) continue;
            _slashHitEnemies.Add(enemy);

            Vector2 eDir = ((Vector2)enemy.transform.position - (Vector2)Host.transform.position);
            if (eDir.sqrMagnitude < 0.001f) eDir = Vector2.right;
            else eDir = eDir.normalized;
            var (eDmg, eCrit) = Host.RollCrit(Host.attackDamage);
            enemy.TakeDamage(eDmg, eCrit, eDir, Host.knockbackPower);
            Host.ApplyLifeSteal(eDmg);
            if (Host.dotDamage > 0f) enemy.ApplyBleed(Host.dotDamage);
            Host.TrySpecialEffectEnemy(enemy, eDmg, eDir);
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);

            if (projectilePrefab != null)
            {
                float angle = Mathf.Atan2(eDir.y, eDir.x) * Mathf.Rad2Deg - 90f;
                Object.Instantiate(projectilePrefab, Host.transform.position, Quaternion.Euler(0, 0, angle));
            }
        }
    }

    private void SpawnSlashVfx(Vector2 playerPos)
    {
        if (slashVfxPrefab == null) return;
        float   midRad      = _centerAngle * Mathf.Deg2Rad;
        Vector2 midDir      = new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad));
        Vector3 vfxWorldPos = (Vector3)playerPos + (Vector3)(midDir * _activeRadius);
        var     vfx         = Object.Instantiate(slashVfxPrefab, vfxWorldPos, Quaternion.identity);
        vfx.GetComponent<SlashVFX>()?.SetDirection(midDir);
        vfx.AddComponent<SlashVfxBehaviour>().Init(
            slashVfxDuration, 0.25f, midDir,
            Host.attackDamage, Host.criticalRate, Host.criticalMultiplier,
            Host.knockbackPower, Host.dotDamage, Host.lifeSteal);
        Object.Destroy(vfx, slashVfxDuration);
        if (Host.Grade >= WeaponGrade.Legendary) OnSlashVfxSpawned?.Invoke(vfxWorldPos, midDir);
    }

#if UNITY_EDITOR
    public override void DrawGizmos(Vector3 playerCenter, Vector3 weaponPos)
    {
        // 슬래시 궤도 반경
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Handles.DrawWireDisc(playerCenter, Vector3.forward, slashRadius);

        // 최대 텔레포트 거리
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.2f);
        Handles.DrawWireDisc(playerCenter, Vector3.forward, slashTeleportMaxDist);

        // 타격 판정 반경 (궤도 위 대표 지점: 위쪽)
        Vector3 samplePos = playerCenter + Vector3.up * slashRadius;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(samplePos, slashHitRadius);

        // VFX 스폰 지점 (대표 방향: 위쪽, slashRadius 거리)
        Gizmos.color = new Color(1f, 0.3f, 1f, 0.8f);
        Gizmos.DrawWireSphere(samplePos, 0.12f);
        Handles.color = new Color(1f, 0.3f, 1f, 0.5f);
        Handles.DrawDottedLine(playerCenter, samplePos, 4f);
        Handles.Label(samplePos + Vector3.up * 0.2f, "VFX");
    }
#endif

    private IEnumerator FadeWeapon(float from, float to, float duration)
    {
        var sr = Host.WeaponSr;
        if (sr == null) yield break;
        Color c = sr.color;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(from, to, t / duration);
            sr.color = c;
            yield return null;
        }
        c.a = to;
        sr.color = c;
    }

    private void RestoreWeapon()
    {
        var sr = Host.WeaponSr;
        if (sr != null) { Color c = sr.color; c.a = 1f; sr.color = c; }
        Host.transform.localPosition = Vector3.zero;
        Host.transform.rotation      = Quaternion.identity;
        Host.StartTrail();
        _isInSequence      = false;
        _sequenceCoroutine = null;
    }
}
