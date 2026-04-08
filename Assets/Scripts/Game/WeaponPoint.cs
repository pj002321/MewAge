using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 자식 오브젝트(weaponPoint)에 부착합니다.
/// 공격 흐름: Idle → Pullback(당김) → Striking(확! 돌진) → Overshooting(팍!) → Returning
/// 회전 우선순위: 적 존재 → 적 방향 / 없으면 → 이동 방향 / 없으면 → 위쪽
/// </summary>
public class WeaponPoint : MonoBehaviour
{
    [Header("무기 타입")]
    [SerializeField] private WeaponType weaponType = WeaponType.Melee;

    // ── DB_무기에서 런타임 로드 (Inspector 설정 불필요) ─────────────
    // f_기본공격력, f_공격속도, f_탐지범위, f_주변범위
    private float attackDamage;
    private float attackCooldown;
    private float attackRange;
    private float areaHitRadius;
    // ────────────────────────────────────────────────────────────────

    [Header("무기 고유 스탯 (프리팹별 Inspector 설정)")]
    [Tooltip("크리티컬 발동 확률 (0~1, 예: 0.1 = 10%)")]
    [SerializeField] private float criticalRate       = 0f;
    [Tooltip("크리티컬 데미지 배율 (예: 2 = 2배)")]
    [SerializeField] private float criticalMultiplier = 2f;
    [Tooltip("출혈 데미지 (0 = 없음)")]
    [SerializeField] private float dotDamage   = 0f;
    [Tooltip("흡혈 (공격 시 HP 회복 %, 0 = 없음)")]
    [SerializeField] private float lifeSteal   = 0f;

    // ── 읽기 전용 프로퍼티 (WeaponInfoPanel용) ───────────────────
    public float AttackDamage   => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float AttackRange    => attackRange;
    public float CriticalRate   => criticalRate;
    public float BleedDamage    => dotDamage;
    public float LifeSteal      => lifeSteal;

    [Header("근거리 이동 속도")]
    [SerializeField] private float pullbackDist     = 0.5f;  // 뒤로 빼는 거리 (로컬)
    [SerializeField] private float anticipationTime = 0.08f; // 백스윙 후 멈추는 시간
    [SerializeField] private float strikeSpeed   = 80f;
    [SerializeField] private float returnSpeed   = 25f;

    [Header("근거리 타격 판정")]
    [SerializeField] private float hitDistance = 0.4f;

    [Header("범위 타격 설정")]
    [SerializeField] private float areaHitDamageRatio = 0.5f; // 주변 적 데미지 배율 (0~1)

    [Header("오버슛 (타격 후 더 나아감)")]
    [SerializeField] private float overshootDistance = 0.5f;
    [SerializeField] private float overshootSpeed    = 60f;

    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 8f;

    [Header("발사 스케일 펄스")]
    [SerializeField] private bool  shootScalePulse    = true;
    [SerializeField] private float scalePulseMax      = 1.3f;   // 최대 배율
    [SerializeField] private float scalePulseDuration = 0.12f;  // 총 지속 시간 (초)

    [Header("부메랑 설정")]
    [SerializeField] private int   boomerangMaxBounces  = 3;
    [SerializeField] private float boomerangBounceRange = 5f;

    [Header("돌리기 설정 (WeaponType.Spin)")]
    [SerializeField] private float orbitRadius     = 1.2f;
    [SerializeField] private float orbitSpeed      = 200f;   // deg/s (Spin 타입은 시계방향으로 변경)
    [SerializeField] private float spinHitRadius   = 0.35f;  // 타격 판정 반경
    [SerializeField] private float spinHitCooldown = 0.5f;   // 동일 적 재타격 쿨타임
    [SerializeField] private float spinKnockback   = 7f;

    private enum State { Idle, Pullback, Anticipation, Striking, Overshooting, Returning }
    private State state = State.Idle;

    private Vector3 originLocalPos;
    private Vector3 pullbackLocalPos;  // 뒤로 뺀 로컬 목표
    private EnemyController targetEnemy;
    private Vector3 overshootTarget;
    private float cooldownTimer;
    private float anticipationTimer;

    private float _baseCooldown;      // Inspector 원본값 (변경 안 함)
    private float _statMultiplier  = 1f; // PlayerStatManager.AtkSpeed에서 오는 배율
    private float _bonusMultiplier = 1f; // MultiplyAttackSpeed 등 일회성 보너스 누적

    private Coroutine _scalePulseCoroutine;
    private Vector3   _originalScale;

    // Physics NonAlloc 버퍼 (매 프레임 배열 재할당 방지)
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    // Spin 전용
    private float currentAngle;
    private readonly Dictionary<EnemyController, float> spinHitTimers = new Dictionary<EnemyController, float>();

    // 보스 타겟
    private BossController _targetBoss;
    private float _bossSpinHitTimer;

    // 부메랑 필드 (직접 이동 모드)
    private int _boomerangBouncesLeft;
    private object _boomerangTarget; // EnemyController or BossController
    private Vector3 _boomerangOvershootTarget;
    private readonly HashSet<EnemyController> _boomerangHitEnemies = new();
    private readonly HashSet<BossController> _boomerangHitBosses = new();

    private void Start()
    {
        _baseCooldown  = attackCooldown;
        originLocalPos = transform.localPosition;
        _originalScale = transform.localScale;
        cooldownTimer  = attackCooldown;
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;

        if (weaponType == WeaponType.Spin)
        {
            UpdateSpin();
            return;
        }

        cooldownTimer -= Time.deltaTime;

        UpdateRotation();

        switch (state)
        {
            case State.Idle:         UpdateIdle();         break;
            case State.Pullback:     UpdatePullback();     break;
            case State.Anticipation: UpdateAnticipation(); break;
            case State.Striking:     UpdateStriking();     break;
            case State.Overshooting: UpdateOvershooting(); break;
            case State.Returning:    UpdateReturning();    break;
        }
    }

    // ─────────────────────────────────────────
    // Spin: 플레이어 주위를 공전하며 닿는 적 타격
    // ─────────────────────────────────────────
    private void UpdateSpin()
    {
        // 공전 각도 증가 → localPosition 갱신
        currentAngle += orbitSpeed * Time.deltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;

        // 무기 스프라이트를 공전 방향으로 자전
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);

        // 피타격 쿨타임 감소 (Keys 복사 후 수정)
        if (_bossSpinHitTimer > 0f) _bossSpinHitTimer -= Time.deltaTime;
        var keys = new List<EnemyController>(spinHitTimers.Keys);
        foreach (var k in keys)
        {
            spinHitTimers[k] -= Time.deltaTime;
            if (spinHitTimers[k] <= 0f) spinHitTimers.Remove(k);
        }

        // 타격 판정
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, spinHitRadius, _hitBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _hitBuffer[i];

            // 보스 타격
            var boss = hit.GetComponentInParent<BossController>();
            if (boss != null && _bossSpinHitTimer <= 0f)
            {
                SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
                var (bossDmg, bossCrit) = RollCrit(attackDamage);
                Vector2 bossKnock = (boss.transform.position - transform.position).normalized;
                boss.TakeDamage(bossDmg, bossCrit, bossKnock, spinKnockback);
                ApplyLifeSteal(bossDmg);
                _bossSpinHitTimer = spinHitCooldown;
                continue;
            }

            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || spinHitTimers.ContainsKey(enemy)) continue;

            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            var (finalDmg, isCrit) = RollCrit(attackDamage);
            Vector2 knockDir = (enemy.transform.position - transform.position).normalized;
            enemy.TakeDamage(finalDmg, isCrit, knockDir, spinKnockback);
            ApplyLifeSteal(finalDmg);
            if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);

            spinHitTimers[enemy] = spinHitCooldown;
        }
    }

    // ─────────────────────────────────────────
    // Idle: 쿨타임 후 미예약 적 탐색 → Pullback 시작
    // ─────────────────────────────────────────
    private void UpdateIdle()
    {
        if (cooldownTimer > 0f) return;

        Vector2 origin = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;

        // 보스 인식
        var boss = BossController.Current;

        EnemyController nearest = FindNearestUnclaimed();

        // 보스가 사정거리 내에 있고, 더 가깝거나 일반 적이 없으면 보스 우선
        bool bossInRange = boss != null &&
            ((Vector2)boss.transform.position - origin).sqrMagnitude <= attackRange * attackRange;
        bool preferBoss = bossInRange && (nearest == null ||
            ((Vector2)boss.transform.position - origin).sqrMagnitude <
            ((Vector2)nearest.transform.position - origin).sqrMagnitude);

        if (preferBoss)
        {
            if (weaponType == WeaponType.Ranged)
            {
                ShootAtBoss(boss);
                cooldownTimer = attackCooldown;
                return;
            }
            if (weaponType == WeaponType.Boomerang)
            {
                _boomerangTarget = boss;
                _boomerangBouncesLeft = boomerangMaxBounces;
                _boomerangHitEnemies.Clear();
                _boomerangHitBosses.Clear();
                pullbackLocalPos = -(Vector3)(GetTargetDir(_boomerangTarget) * pullbackDist);
                state = State.Pullback;
                return;
            }
            // Melee
            _targetBoss = boss;
            Vector2 toBoss = ((Vector2)boss.transform.position - origin).normalized;
            pullbackLocalPos = -(Vector3)toBoss * pullbackDist;
            state = State.Pullback;
            return;
        }

        if (weaponType == WeaponType.Ranged && nearest != null)
        {
            ShootAt(nearest);
            cooldownTimer = attackCooldown;
            return;
        }

        if (weaponType == WeaponType.Boomerang && nearest != null)
        {
            _boomerangTarget = nearest;
            _boomerangBouncesLeft = boomerangMaxBounces;
            _boomerangHitEnemies.Clear();
            _boomerangHitBosses.Clear();
            pullbackLocalPos = -(Vector3)(GetTargetDir(_boomerangTarget) * pullbackDist);
            state = State.Pullback;
            return;
        }

        if (weaponType == WeaponType.Boomerang)
        {
            _boomerangTarget = preferBoss ? (object)boss : nearest;
            if (_boomerangTarget == null) return;
            _boomerangBouncesLeft = boomerangMaxBounces;
            _boomerangHitEnemies.Clear();
            _boomerangHitBosses.Clear();
            pullbackLocalPos = -(Vector3)(GetTargetDir(_boomerangTarget) * pullbackDist);
            state = State.Pullback;
            return;
        }

        if (WeaponManager.Instance.TryClaim(nearest))
        {
            targetEnemy = nearest;
            Vector2 toTarget = (nearest.transform.position - transform.parent.position).normalized;
            pullbackLocalPos = -(Vector3)toTarget * pullbackDist;
            state = State.Pullback;
        }
    }

    private void ShootAtBoss(BossController boss)
    {
        if (projectilePrefab == null) return;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        var (finalDmg, isCrit) = RollCrit(attackDamage);
        Vector2 dir = ((Vector2)boss.transform.position - (Vector2)transform.position).normalized;
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        go.GetComponent<Projectile>()?.Init(finalDmg, dir, projectileSpeed, isPrimaryCritical: isCrit,
            lifeSteal: lifeSteal, dotDamage: dotDamage, areaHitRadius: areaHitRadius,
            areaHitDamageRatio: areaHitDamageRatio, criticalRate: criticalRate,
            criticalMultiplier: criticalMultiplier, baseDamage: attackDamage);
        TriggerScalePulse();
    }

    private void ShootAt(EnemyController target)
    {
        if (projectilePrefab == null) return;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        var (finalDmg, isCrit) = RollCrit(attackDamage);
        Vector2 dir = (target.transform.position - transform.position).normalized;
        var go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        go.GetComponent<Projectile>()?.Init(finalDmg, dir, projectileSpeed, isPrimaryCritical: isCrit, lifeSteal: lifeSteal, dotDamage: dotDamage, areaHitRadius: areaHitRadius, areaHitDamageRatio: areaHitDamageRatio, criticalRate: criticalRate, criticalMultiplier: criticalMultiplier, baseDamage: attackDamage);
        TriggerScalePulse();
    }



    private void TriggerScalePulse()
    {
        if (!shootScalePulse) return;
        if (_scalePulseCoroutine != null) StopCoroutine(_scalePulseCoroutine);
        _scalePulseCoroutine = StartCoroutine(ScalePulseRoutine());
    }

    private System.Collections.IEnumerator ScalePulseRoutine()
    {
        Vector3 peak = _originalScale * scalePulseMax;
        float   half = scalePulseDuration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(_originalScale, peak, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(peak, _originalScale, t / half);
            yield return null;
        }
        transform.localScale = _originalScale;
        _scalePulseCoroutine = null;
    }

    // ─────────────────────────────────────────
    // Pullback: 플레이어 중앙으로 땡겨짐
    // ─────────────────────────────────────────
    private void UpdatePullback()
    {
        // 타겟 반대 방향으로 뺌
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, pullbackLocalPos, returnSpeed * Time.deltaTime);

        if ((transform.localPosition - pullbackLocalPos).sqrMagnitude <= 0.001f)
        {
            transform.localPosition = pullbackLocalPos;
            anticipationTimer = anticipationTime;
            state = State.Anticipation;
        }
    }

    private void UpdateAnticipation()
    {
        // 제자리에서 잠깐 멈춤 → 시간 다 되면 팍! Strike
        anticipationTimer -= Time.deltaTime;
        if (anticipationTimer <= 0f)
            BeginStrike();
    }

    // ─────────────────────────────────────────
    // BeginStrike: 무기를 플레이어 중심에 스냅 후 즉시 돌진 시작
    // ─────────────────────────────────────────
    private void BeginStrike()
    {
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        state = State.Striking;
    }

    // ─────────────────────────────────────────
    // Striking: 빠르게 돌진 → 범위 타격 → Overshooting으로 전환
    // ─────────────────────────────────────────
    private void UpdateStriking()
    {
        // 부메랑 모드 처리
        if (weaponType == WeaponType.Boomerang && _boomerangTarget != null)
        {
            // 타겟이 파괴된 경우 즉시 복귀 (Unity fake-null 방어)
            if (IsBoomerangTargetGone())
            {
                _boomerangTarget = null;
                BeginReturn();
                return;
            }

            Vector3 boomerangTargetPos = GetTargetPos(_boomerangTarget);
            transform.position = Vector3.MoveTowards(transform.position, boomerangTargetPos, strikeSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, boomerangTargetPos) <= hitDistance)
            {
                Vector2 hitDir = (boomerangTargetPos - transform.parent.position).normalized;
                HitAreaBoomerang(hitDir);
                _boomerangBouncesLeft--;
                if (_boomerangBouncesLeft <= 0)
                {
                    _boomerangTarget = null;
                    BeginReturn();
                    return;
                }
                object next = FindNextBoomerangTarget(boomerangTargetPos);
                if (next != null)
                {
                    _boomerangTarget = next;
                    _boomerangOvershootTarget = boomerangTargetPos + (Vector3)(GetTargetPos(next) - boomerangTargetPos).normalized * overshootDistance;
                    state = State.Overshooting;
                }
                else
                {
                    _boomerangTarget = null;
                    BeginReturn();
                }
            }
            return;
        }

        // 보스 타겟 처리
        if (_targetBoss != null)
        {
            if (!_targetBoss.gameObject.activeInHierarchy)
            {
                _targetBoss = null;
                BeginReturn();
                return;
            }
            Vector3 bossPos = _targetBoss.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, bossPos, strikeSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, bossPos) <= hitDistance)
            {
                Vector2 hitDir = (bossPos - transform.parent.position).normalized;
                HitArea(hitDir, null);
                overshootTarget = bossPos + (Vector3)(hitDir * overshootDistance);
                _targetBoss = null;
                state = State.Overshooting;
            }
            return;
        }

        if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
        {
            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            BeginReturn();
            return;
        }

        Vector3 targetPos = targetEnemy.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            Vector2 hitDir = (targetPos - transform.parent.position).normalized;
            HitArea(hitDir, targetEnemy);

            overshootTarget = targetPos + (Vector3)(hitDir * overshootDistance);

            WeaponManager.Instance?.Release(targetEnemy);
            targetEnemy = null;
            state = State.Overshooting;
        }
    }

    // ─────────────────────────────────────────
    // Overshooting: 타격 후 조금 더 나아갔다가 복귀
    // ─────────────────────────────────────────
    private void UpdateOvershooting()
    {
        Vector3 target = weaponType == WeaponType.Boomerang ? _boomerangOvershootTarget : overshootTarget;
        transform.position = Vector3.MoveTowards(transform.position, target, overshootSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) <= 0.05f)
        {
            if (weaponType == WeaponType.Boomerang && _boomerangTarget != null)
                state = State.Striking; // 튕김 계속
            else
                BeginReturn();
        }
    }

    // ─────────────────────────────────────────
    // HitArea: 현재 위치 반경 내 모든 적 타격
    // ─────────────────────────────────────────
    private void HitArea(Vector2 hitDir, EnemyController primaryTarget)
    {
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, Mathf.Max(areaHitRadius, hitDistance), _hitBuffer);
        bool bossHit = false;
        for (int i = 0; i < colCount; i++)
        {
            var hit = _hitBuffer[i];

            // 보스 타격
            if (!bossHit)
            {
                var boss = hit.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    var (bossDmg, bossCrit) = RollCrit(attackDamage);
                    Vector2 bossKnock = ((Vector2)boss.transform.position - (Vector2)transform.position);
                    if (bossKnock.sqrMagnitude < 0.001f) bossKnock = hitDir;
                    boss.TakeDamage(bossDmg, bossCrit, bossKnock.normalized, 8f);
                    ApplyLifeSteal(bossDmg);
                    bossHit = true;
                    continue;
                }
            }

            if (!hit.TryGetComponent<EnemyController>(out var enemy)) continue;
            bool isPrimary = enemy == primaryTarget;
            float baseDmg = isPrimary ? attackDamage : attackDamage * areaHitDamageRatio;
            var (finalDmg, isCrit) = RollCrit(baseDmg);
            Vector2 knockDir = ((Vector2)enemy.transform.position - (Vector2)transform.position);
            if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
            enemy.TakeDamage(finalDmg, isCrit, knockDir.normalized, 8f);
            ApplyLifeSteal(finalDmg);
            if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
        }
    }

    private void HitAreaBoomerang(Vector2 hitDir)
    {
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, Mathf.Max(areaHitRadius, hitDistance), _hitBuffer);
        for (int i = 0; i < colCount; i++)
        {
            var hit = _hitBuffer[i];
            EnemyController enemy = null;
            BossController boss = null;
            enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && !_boomerangHitEnemies.Contains(enemy))
            {
                _boomerangHitEnemies.Add(enemy);
                bool isPrimary = enemy == _boomerangTarget;
                float baseDmg = isPrimary ? attackDamage : attackDamage * areaHitDamageRatio;
                var (finalDmg, isCrit) = RollCrit(baseDmg);
                Vector2 knockDir = ((Vector2)enemy.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
                enemy.TakeDamage(finalDmg, isCrit, knockDir.normalized, 8f);
                ApplyLifeSteal(finalDmg);
                if (dotDamage > 0f) enemy.ApplyBleed(dotDamage);
            }
            boss = hit.GetComponentInParent<BossController>();
            if (boss != null && !_boomerangHitBosses.Contains(boss))
            {
                _boomerangHitBosses.Add(boss);
                bool isPrimary = boss == _boomerangTarget;
                float baseDmg = isPrimary ? attackDamage : attackDamage * areaHitDamageRatio;
                var (finalDmg, isCrit) = RollCrit(baseDmg);
                Vector2 knockDir = ((Vector2)boss.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = hitDir;
                boss.TakeDamage(finalDmg, isCrit, knockDir.normalized, 8f);
                ApplyLifeSteal(finalDmg);
                if (dotDamage > 0f) boss.ApplyBleed(dotDamage);
            }
        }
    }

    private void ApplyLifeSteal(float dealtDamage)
    {
        if (lifeSteal <= 0f) return;
        PlayerController.Instance?.HealHP(dealtDamage * lifeSteal);
    }

    // ─────────────────────────────────────────
    // Returning: 원위치(부모 위치)로 복귀
    // ─────────────────────────────────────────
    private void UpdateReturning()
    {
        // 플레이어 중앙(0,0,0)으로 복귀
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, Vector3.zero, returnSpeed * Time.deltaTime);

        if (transform.localPosition.sqrMagnitude <= 0.001f)
        {
            transform.localPosition = Vector3.zero;
            cooldownTimer = attackCooldown;
            state = State.Idle;
        }
    }

    private void BeginReturn() => state = State.Returning;

    // ─────────────────────────────────────────
    // 회전: 적 > 이동방향 > 위쪽 우선순위
    // ─────────────────────────────────────────
    private void UpdateRotation()
    {
        Vector2 dir = GetLookDirection();
        if (dir.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetLookDirection()
    {
        // Striking 또는 Pullback 중: 타겟 방향 고정
        if (state == State.Striking || state == State.Pullback)
        {
            if (weaponType == WeaponType.Boomerang && _boomerangTarget != null)
                return (GetTargetPos(_boomerangTarget) - transform.position).normalized;
            if (_targetBoss != null)
                return ((Vector2)_targetBoss.transform.position - (Vector2)transform.position).normalized;
            if (targetEnemy != null)
                return (targetEnemy.transform.position - transform.position).normalized;
        }

        // 적이 있으면 가장 가까운 적 방향
        EnemyController nearest = FindNearestAny();
        var boss = BossController.Current;
        if (nearest != null && boss != null)
        {
            float eDist = ((Vector2)nearest.transform.position - (Vector2)transform.position).sqrMagnitude;
            float bDist = ((Vector2)boss.transform.position - (Vector2)transform.position).sqrMagnitude;
            return bDist < eDist
                ? ((Vector2)boss.transform.position - (Vector2)transform.position).normalized
                : (nearest.transform.position - transform.position).normalized;
        }
        if (boss != null)
            return ((Vector2)boss.transform.position - (Vector2)transform.position).normalized;
        if (nearest != null)
            return (nearest.transform.position - transform.position).normalized;

        // 적 없으면 플레이어 이동 방향
        if (PlayerController.Instance != null)
        {
            Vector2 moveDir = PlayerController.Instance.MoveDirection;
            if (moveDir.sqrMagnitude > 0.001f)
                return moveDir;
        }

        // 이동도 없으면 위쪽
        return Vector2.up;
    }

    private Vector3 GetTargetPos(object target)
    {
        if (target is EnemyController ec && ec != null) return ec.transform.position;
        if (target is BossController bc && bc != null) return bc.transform.position;
        return Vector3.zero;
    }

    /// <summary>부메랑 타겟이 파괴/비활성화됐는지 확인. Unity fake-null 방어용.</summary>
    private bool IsBoomerangTargetGone()
    {
        if (_boomerangTarget is EnemyController ec)
            return ec == null || !ec.gameObject.activeInHierarchy;
        if (_boomerangTarget is BossController bc)
            return bc == null || !bc.gameObject.activeInHierarchy;
        return true;
    }

    private Vector2 GetTargetDir(object target)
    {
        return (GetTargetPos(target) - transform.parent.position).normalized;
    }

    private object FindNextBoomerangTarget(Vector2 fromPos)
    {
        EnemyController nearestEnemy = null;
        BossController nearestBoss = null;
        float minDist = boomerangBounceRange;

        foreach (var e in EnemyController.All)
        {
            if (_boomerangHitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist) { minDist = d; nearestEnemy = e; }
        }

        if (BossController.Current != null && !_boomerangHitBosses.Contains(BossController.Current))
        {
            float d = Vector2.Distance(fromPos, BossController.Current.transform.position);
            if (d < minDist) { minDist = d; nearestBoss = BossController.Current; }
        }

        if (nearestEnemy != null && nearestBoss != null)
        {
            float dE = Vector2.Distance(fromPos, nearestEnemy.transform.position);
            float dB = Vector2.Distance(fromPos, nearestBoss.transform.position);
            return dE < dB ? nearestEnemy : nearestBoss;
        }
        else if (nearestEnemy != null) return nearestEnemy;
        else if (nearestBoss != null) return nearestBoss;
        else return null;
    }

    // ─────────────────────────────────────────
    // 적 탐색 유틸
    // ─────────────────────────────────────────
    private EnemyController FindNearestUnclaimed()
    {
        EnemyController nearest  = null;
        float           minSqDist = attackRange * attackRange;
        Vector2         origin   = transform.parent.position;

        foreach (var enemy in EnemyController.All)
        {
            if (enemy == null || enemy.transform == null || !enemy.gameObject.activeInHierarchy) continue;
            if (WeaponManager.Instance.IsClaimed(enemy)) continue;

            float sqDist = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqDist < minSqDist) { minSqDist = sqDist; nearest = enemy; }
        }
        return nearest;
    }

    // 거리 제한 없이 씬 전체에서 가장 가까운 적 (회전용)
    private EnemyController FindNearestAny()
    {
        EnemyController nearest   = null;
        float           minSqDist = float.MaxValue;
        Vector2         origin    = transform.parent.position;

        foreach (var enemy in EnemyController.All)
        {
            if (enemy == null || enemy.transform == null || !enemy.gameObject.activeInHierarchy) continue;
            float sqDist = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqDist < minSqDist) { minSqDist = sqDist; nearest = enemy; }
        }
        return nearest;
    }

    /// <summary>WeaponManager가 장착 시 호출 — DB 스탯 및 플레이어 보너스 적용.</summary>
    public void Init(WeaponData data)
    {
        weaponType    = data.weaponType;
        criticalRate  = 0.05f + data.criticalRate;  // 기본 5% + 성 보너스 포함 크리티컬 확률
        lifeSteal     = data.lifeSteal;

        // DB 기반 스탯 적용 (성 보너스 포함된 최종값)
        attackCooldown = data.attackCooldown;
        _baseCooldown  = data.attackCooldown;       // RecalcCooldown 기준값
        attackRange    = data.attackRange;
        areaHitRadius  = data.areaHitRadius;

        // Spin 타입은 시계방향으로 회전
        if (weaponType == WeaponType.Spin)
            orbitSpeed = -Mathf.Abs(orbitSpeed);

        // 부메랑 설정 적용
        if (weaponType == WeaponType.Boomerang)
        {
            boomerangMaxBounces = data.maxBounces;
            boomerangBounceRange = data.bounceRange;
        }

        var player = PlayerController.Instance;
        if (player != null)
        {
            // 공통 보너스 + 무기 타입별 보너스 적용 (플랫 수치)
            // data.attackDamage에는 성 보너스가 포함된 최종 기본값이 들어있음
            attackDamage = data.attackDamage + player.Attack;
            attackDamage += weaponType == WeaponType.Melee    ? player.MeleeAttackBonus  : 0f;
            attackDamage += weaponType == WeaponType.Ranged   ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Boomerang ? player.RangedAttackBonus : 0f;
            attackDamage += weaponType == WeaponType.Spin      ? player.MeleeAttackBonus  : 0f;

            // 시너지 효과는 퍼센트(%)로 증가 (예: 25%면 1.25 곱셈)
            if (player.SynergyAtkPct > 0f)
            {
                attackDamage *= (1f + player.SynergyAtkPct / 100f);
            }
        }

        // 성 등급에 맞는 외곽선 적용
        GetComponentInChildren<SpriteOutline>()?.SetStar(data.star);
        // projectilePrefab은 무기 프리팹 Inspector에서 직접 설정합니다.
    }

    /// <summary>크리티컬 판정. isCritical=true이면 데미지에 multiplier를 곱한 값 반환.</summary>
    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    /// <summary>PlayerStatManager AtkSpeed 값(초 감산량)으로 쿨타임을 재계산합니다.
    /// 예: AtkSpeed=0.2 → 기본 쿨타임에서 0.2초 차감. 최소 0.1초 보장.</summary>
    public void SetStatSpeedPct(float reduction)
    {
        _statMultiplier = reduction; // 감산값(초)으로 재사용
        RecalcCooldown();
    }

    /// <summary>공격속도를 multiplier배 빠르게 합니다 (일회성 보너스 누적).</summary>
    public void MultiplyAttackSpeed(float multiplier)
    {
        _bonusMultiplier *= multiplier;
        RecalcCooldown();
    }

    private void RecalcCooldown()
    {
        // _statMultiplier = AtkSpeed 감산량(초), _bonusMultiplier = 배율 보너스
        float reduced = _baseCooldown - _statMultiplier;
        attackCooldown = Mathf.Max(0.1f, reduced / _bonusMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(center, attackRange);

        if (weaponType == WeaponType.Spin)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, orbitRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, spinHitRadius);
        }
    }
}
