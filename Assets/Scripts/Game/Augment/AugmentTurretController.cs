using DG.Tweening;
using UnityEngine;

public class AugmentTurretController : MonoBehaviour
{
    public enum AttackType { Direct, Parabolic }

    [SerializeField] private AttackType attackType      = AttackType.Parabolic;
    [SerializeField] private float      attackInterval  = 2f;
    [SerializeField] private float      attackRange     = 5f;
    [SerializeField] private float      projectileSpeed = 6f;
    [SerializeField] private float      arcHeight       = 2f;
    [SerializeField] private float      lifetime        = 8f;
    [SerializeField] private Transform  firePoint;
    [SerializeField] private Transform  visualRoot;     // 펄스 연출 대상, null이면 transform

    private float           _damage;
    private float           _timer;
    private GameObject      _projectilePrefab;
    private Vector3         _originScale;
    private SpriteRenderer  _sr;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        Transform vis = visualRoot != null ? visualRoot : transform;
        _originScale = vis.localScale;
        EnemyFX.SpawnBounce(vis, _originScale);
    }

    public void Init(float damage, GameObject projectilePrefab)
    {
        _damage           = damage;
        _projectilePrefab = projectilePrefab;
        _timer            = attackInterval; // 스폰 직후 즉시 발사 방지
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = attackInterval;

        EnemyController nearest = null;
        float minDist = attackRange;
        foreach (var e in EnemyController.All)
        {
            if (e == null) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < minDist) { minDist = d; nearest = e; }
        }

        if (nearest == null) return;

        Fire(nearest);
    }

    private void Fire(EnemyController target)
    {
        // 플립
        if (_sr != null)
            _sr.flipX = target.transform.position.x < transform.position.x;

        // 펄스 연출
        Transform vis = visualRoot != null ? visualRoot : transform;
        vis.DOKill();
        vis.localScale = _originScale;
        vis.DOPunchScale(Vector3.one * 0.35f, 0.2f, 6, 0.4f).SetLink(gameObject);

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        if (_projectilePrefab == null)
        {
            target.TakeDamage(_damage);
            return;
        }

        var proj       = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        var projectile = proj.GetComponent<Projectile>();
        if (projectile == null) return;

        if (attackType == AttackType.Parabolic)
            projectile.InitParabolic(_damage, target.transform.position, projectileSpeed, arcHeight);
        else
        {
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)spawnPos).normalized;
            projectile.Init(_damage, dir, projectileSpeed);
        }
    }
}
