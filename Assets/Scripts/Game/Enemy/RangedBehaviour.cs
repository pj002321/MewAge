using System.Collections;
using UnityEngine;

/// <summary>
/// 원거리 공격형 — 적정 거리를 유지하면서 투사체를 발사합니다.
/// </summary>
public class RangedBehaviour : EnemyBehaviourBase
{
    [Header("원거리 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float preferredRange  = 4f;
    [SerializeField] private float shootInterval   = 2f;
    [SerializeField] private float rangedDamage    = 0f;  // 0이면 ContactDamage 사용

    [Header("발사 지점")]
    [Tooltip("null이면 적 중심에서 발사")]
    [SerializeField] private Transform firePoint;

    [Header("경고 라인")]
    [SerializeField] private bool     useWarningLines   = true;
    [SerializeField] private float    warningDuration   = 0.4f;
    [SerializeField] private float    warningLineLength = 8f;
    [SerializeField] private float    warningLineWidth  = 0.05f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfx;
    [SerializeField] private float      attackVfxDuration = 0.3f;

    [Header("애니메이션 이벤트")]
    [Tooltip("true: tAttack 애니메이션 이벤트 OnRangedAttackFire()로 발사. false: 경고 후 즉시 발사")]
    [SerializeField] private bool  useAnimationEvent  = true;
    [Tooltip("애니메이션 이벤트가 이 시간 내에 오지 않으면 직접 발사 (0이면 폴백 없음)")]
    [SerializeField] private float animEventTimeout   = 0.6f;

    [Header("이동 상태")]
    [SerializeField] private float retreatDuration = 1.4f;

    private enum MoveState { Approaching, Idle, Retreating }
    private MoveState _moveState = MoveState.Approaching;
    private float     _retreatTimer;

    private float        _projSpeed;
    private float        _projBonusDamage;
    private bool         _projCanReflect;
    private float        shootTimer;
    private bool         _isWarning;
    private bool         _pendingShot;
    private Vector2      _pendingShotDir;
    private float        _pendingShotDmg;
    private LineRenderer _activeWarnLine;
    private Collider2D   _col;

    public override void ApplyWaveScaling(float multiplier)
    {
        if (rangedDamage > 0f) rangedDamage *= multiplier;
    }

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null) return;
        if (_isWarning) return;

        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            shootTimer = shootInterval;
            StartCoroutine(WarnAndShoot());
        }
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;

        float   dist     = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;
        Owner.FlipToward(toPlayer);

        switch (_moveState)
        {
            case MoveState.Retreating:
                _retreatTimer -= Time.fixedDeltaTime;
                Owner.MoveInDirection(-toPlayer, Owner.MoveSpeed);
                SetWalk(true);
                if (_retreatTimer <= 0f)
                    _moveState = MoveState.Approaching;
                break;

            case MoveState.Idle:
                // 경고 중 제자리. 경고가 끝나면 Approaching으로 전환
                SetWalk(false);
                if (!_isWarning)
                    _moveState = MoveState.Approaching;
                break;

            case MoveState.Approaching:
                if (dist > preferredRange)
                {
                    Owner.MoveInDirection(toPlayer, Owner.MoveSpeed);
                    SetWalk(true);
                }
                else
                {
                    SetWalk(false);
                }
                break;
        }
    }

    private void SetWalk(bool walking)
        => Owner.EnemyAnimator?.SetBool(EnemyAnim.BMove, walking);

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        shootTimer    = shootInterval;
        _isWarning    = false;
        _pendingShot  = false;
        _moveState    = MoveState.Approaching;
        _retreatTimer = 0f;
        _col         = host.SelfTransform.GetComponentInChildren<Collider2D>();
        attackVfx?.SetActive(false);
        if (_activeWarnLine != null)
        {
            Destroy(_activeWarnLine.gameObject);
            _activeWarnLine = null;
        }
    }

    /// <summary>DB_유닛 투사체 데이터를 적용합니다.</summary>
    public void ApplyProjectileData(ProjectileData data)
    {
        if (data == null) return;
        projectilePrefab = data.prefab;
        if (data.damage > 0f) rangedDamage = data.damage;
        if (data.speed  > 0f) _projSpeed   = data.speed;
        _projBonusDamage = data.bonusDamage;
        _projCanReflect  = data.canReflect;
    }

    private Vector2 GetWarningOrigin()
    {
        if (firePoint != null) return (Vector2)firePoint.position;
        if (_col != null) return new Vector2(Owner.Rb.position.x, _col.bounds.min.y);
        return Owner.Rb.position;
    }

    private IEnumerator WarnAndShoot()
    {
        _isWarning = true;
        _moveState = MoveState.Idle;

        Vector2 spawnPos = GetWarningOrigin();
        Vector2 dir      = Owner.PlayerTransform != null
            ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
            : Vector2.right;

        var lr = SpawnWarningLine(spawnPos, dir);
        _activeWarnLine = lr;
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (Owner == null) { if (lr != null) Destroy(lr.gameObject); _activeWarnLine = null; _isWarning = false; yield break; }
            spawnPos = GetWarningOrigin();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);
            if (lr != null)
            {
                lr.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
                lr.SetPosition(0, Vector3.zero);
                Vector3 worldEnd = new Vector3(spawnPos.x + dir.x * warningLineLength * t,
                                               spawnPos.y + dir.y * warningLineLength * t, 0f);
                lr.SetPosition(1, lr.transform.InverseTransformPoint(worldEnd));
            }
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);
        _activeWarnLine = null;

        if (Owner != null && Owner.PlayerTransform != null)
            ShootProjectile();

        // 애니메이션 이벤트 발사가 올 때까지 Idle 유지 (이동 중 발사 방지)
        if (useAnimationEvent && _pendingShot)
        {
            float timeout = animEventTimeout > 0f ? animEventTimeout : 0.6f;
            float t2 = 0f;
            while (_pendingShot && t2 < timeout)
            {
                if (Owner == null) break;
                t2 += Time.deltaTime;
                yield return null;
            }
            if (_pendingShot)   // 타임아웃: 폴백 발사
            {
                _pendingShot = false;
                if (Owner != null) FireProjectile();
            }
        }

        _isWarning    = false;
        _moveState    = MoveState.Retreating;
        _retreatTimer = retreatDuration;
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null) return;

        Vector2 spawnPos     = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        _pendingShotDir      = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;
        _pendingShotDmg      = rangedDamage > 0f ? rangedDamage : Owner.ContactDamage;

        if (attackVfx != null) StartCoroutine(ShowVfx());

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        if (useAnimationEvent)
            _pendingShot = true;
        else
            FireProjectile();
    }

    private IEnumerator ShowVfx()
    {
        attackVfx.SetActive(true);
        yield return new WaitForSeconds(attackVfxDuration);
        if (attackVfx != null) attackVfx.SetActive(false);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || Owner == null) return;
        Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        var proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj.TryGetComponent<Projectile>(out var p))
            p.Init(_pendingShotDmg, _pendingShotDir, _projSpeed > 0f ? _projSpeed : 6f);
    }

    /// <summary>tAttack 애니메이션의 발사 프레임에 Animation Event로 호출됩니다.</summary>
    public void OnRangedAttackFire()
    {
        _pendingShot = false;
        if (Owner == null) return;
        FireProjectile();
    }

    private void OnDisable()
    {
        if (_activeWarnLine != null)
        {
            Destroy(_activeWarnLine.gameObject);
            _activeWarnLine = null;
        }
        _isWarning = false;
        SetWalk(false);
    }

    private LineRenderer SpawnWarningLine(Vector2 start, Vector2 dir)
    {
        if (!useWarningLines || !WarningLineSettings.Enabled) return null;
        var go = new GameObject("WarnLine");
        go.transform.position = new Vector3(start.x, start.y, 0f);
        go.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.zero);
        lr.startWidth   = warningLineWidth;
        lr.endWidth     = warningLineWidth;
        lr.material     = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor   = warningColor;
        lr.endColor     = warningColor;
        lr.sortingOrder = 10;
        Destroy(go, warningDuration + 0.5f);
        return lr;
    }
}
