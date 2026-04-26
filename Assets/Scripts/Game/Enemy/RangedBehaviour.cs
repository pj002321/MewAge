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
    [SerializeField] private float    warningDuration   = 0.4f;
    [SerializeField] private float    warningLineLength = 8f;
    [SerializeField] private float    warningLineWidth  = 0.05f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;

    private float        _projSpeed;
    private float        _projBonusDamage;
    private bool         _projCanReflect;
    private float        shootTimer;
    private bool         _isWarning;
    private LineRenderer _activeWarnLine;

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

        Vector2 moveDir;
        if (dist < preferredRange * 0.75f)      moveDir = -toPlayer;  // 너무 가까우면 후퇴
        else if (dist > preferredRange * 1.3f)  moveDir = toPlayer;   // 너무 멀면 접근
        else                                    moveDir = Vector2.zero;

        if (moveDir != Vector2.zero)
            Owner.MoveInDirection(moveDir, Owner.MoveSpeed);

        Owner.FlipToward(toPlayer);
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

    private IEnumerator WarnAndShoot()
    {
        _isWarning = true;

        Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        Vector2 dir      = Owner.PlayerTransform != null
            ? ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized
            : Vector2.right;

        var lr      = SpawnWarningLine(spawnPos, dir);
        _activeWarnLine = lr;
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (Owner == null) { if (lr != null) Destroy(lr.gameObject); _activeWarnLine = null; _isWarning = false; yield break; }
            // 발사 지점이 이동할 수 있으므로 매 프레임 갱신
            spawnPos = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
            if (lr != null) lr.SetPosition(0, spawnPos);
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / warningDuration);
            if (lr != null) lr.SetPosition(1, spawnPos + dir * (warningLineLength * t));
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);
        _activeWarnLine = null;

        if (Owner != null && Owner.PlayerTransform != null)
            ShootProjectile();

        _isWarning = false;
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null) return;

        Owner.EnemyAnimator?.SetTrigger("tAttack");
        if (attackVfxPrefab != null)
            Instantiate(attackVfxPrefab, transform.position, Quaternion.identity);

        float   dmg      = rangedDamage > 0f ? rangedDamage : Owner.ContactDamage;
        Vector2 spawnPos = firePoint != null ? (Vector2)firePoint.position : Owner.Rb.position;
        Vector2 dir      = ((Vector2)Owner.PlayerTransform.position - spawnPos).normalized;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            float spd = _projSpeed > 0f ? _projSpeed : 6f;
            p.Init(dmg, dir, spd);
        }
    }

    private void OnDisable()
    {
        if (_activeWarnLine != null)
        {
            Destroy(_activeWarnLine.gameObject);
            _activeWarnLine = null;
        }
        _isWarning = false;
    }

    private LineRenderer SpawnWarningLine(Vector2 start, Vector2 dir)
    {
        var go = new GameObject("WarnLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, start);
        lr.startWidth   = warningLineWidth;
        lr.endWidth     = warningLineWidth;
        lr.material     = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor   = warningColor;
        lr.endColor     = warningColor;
        lr.sortingOrder = 10;
        return lr;
    }
}
