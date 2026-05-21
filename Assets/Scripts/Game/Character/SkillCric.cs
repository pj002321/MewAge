using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크릭 캐릭터 고유 스킬.
///
/// [발동 즉시] 소환체들이 좌→우로 맵을 가로지르며 적을 오른쪽으로 밀어냄.
/// [발동 즉시] 적 밀집 지점을 향해 1회 돌진 + 피해 + 넉백. 적 없으면 이동방향으로 돌진.
/// </summary>
public class SkillCric : CharacterSkillBase
{
    [Header("소환체 스윕")]
    [SerializeField] private GameObject sweepPrefab;
    [SerializeField] private float sweepSpeed          = 25f;
    [SerializeField] private float sweepKnockbackForce = 15f;

    [Header("대쉬")]
    [SerializeField] private GameObject dashWavePrefab;
    [SerializeField] private float dashDistance  = 5f;
    [SerializeField] private float dashStepTime  = 0.15f;
    [SerializeField] private float knockbackForce  = 22f;
    [SerializeField] private float knockbackRadius = 1.8f;
    [SerializeField] private float clusterRadius   = 3f;
    [SerializeField] private float dashHitShakeDuration  = 0.12f;
    [SerializeField] private float dashHitShakeMagnitude = 0.18f;

    public override string SkillId => "SKL_CRIC";

    private Coroutine   _dashRoutine;
    private Rigidbody2D _rb;
    private Vector2     _lastMoveDir = Vector2.right;

    // ── 초기화 ────────────────────────────────────────────────────────

    private void Start()
    {
        var player = PlayerController.Instance;
        if (player != null) _rb = player.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        var d = PlayerController.Instance?.MoveDirection ?? Vector2.zero;
        if (d.sqrMagnitude > 0.01f) _lastMoveDir = d.normalized;
    }

    // ── 발동 / 종료 ───────────────────────────────────────────────────

    public override void Activate()
    {
        if (_dashRoutine != null) StopCoroutine(_dashRoutine);
        _dashRoutine = StartCoroutine(SweepThenDash());
    }

    private IEnumerator SweepThenDash()
    {
        var sweeps = SpawnSweep();

        // 모든 스윕 엔티티가 화면을 벗어날 때까지 대기
        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            foreach (var s in sweeps)
                if (s != null) { anyAlive = true; break; }
            yield return null;
        }

        yield return StartCoroutine(DashOnceRoutine());
        _dashRoutine = null;
    }

    public override void Deactivate()
    {
        if (_dashRoutine != null) { StopCoroutine(_dashRoutine); _dashRoutine = null; }

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetSkillInvincible(false);
            PlayerController.Instance.InputLocked = false;
        }
    }

    // ── 소환체 스윕 ───────────────────────────────────────────────────

    private List<CricSweepEntity> SpawnSweep()
    {
        var result = new List<CricSweepEntity>();
        if (sweepPrefab == null) return result;

        var cam = Camera.main;
        if (cam == null) return result;

        float camZ   = Mathf.Abs(cam.transform.position.z);
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(-0.05f, 0.5f, camZ));
        Vector3 right= cam.ViewportToWorldPoint(new Vector3( 1.10f, 0.5f, camZ));
        float botY   = cam.ViewportToWorldPoint(new Vector3(0.5f, -0.05f, camZ)).y;
        float topY   = cam.ViewportToWorldPoint(new Vector3(0.5f,  1.05f, camZ)).y;
        left.z = right.z = 0f;

        float entityH = sweepPrefab.GetComponent<BoxCollider2D>()?.size.y ?? 1f;
        float damage  = CalcSweepDamage();

        for (float y = botY; y <= topY + entityH; y += entityH)
        {
            var go = Instantiate(sweepPrefab, new Vector3(left.x, y, 0f), Quaternion.identity);
            if (go.TryGetComponent<CricSweepEntity>(out var sweep))
            {
                sweep.Init(right.x, sweepSpeed, sweepKnockbackForce, damage);
                result.Add(sweep);
            }
        }
        return result;
    }

    // ── 1회 돌진 ──────────────────────────────────────────────────────

    private IEnumerator DashOnceRoutine()
    {
        var player = PlayerController.Instance;
        if (player == null || _rb == null) yield break;

        Vector2 from          = _rb.position;
        bool    foundEnemy    = EnemyController.All.Count > 0;
        Vector2 target        = FindDashTarget(from);
        Vector2 dir           = (target - from);
        // 적이 있으면 해당 위치까지, 없으면 dashDistance만큼만 이동
        float   dist  = foundEnemy ? dir.magnitude : Mathf.Min(dir.magnitude, dashDistance);
        Vector2 endPos = from + dir.normalized * dist;

        // 파동 VFX
        if (dashWavePrefab != null)
        {
            var col = player.GetComponentInChildren<Collider2D>();
            float groundY = col != null ? col.bounds.min.y : player.transform.position.y;
            Instantiate(dashWavePrefab, new Vector3(from.x, groundY, 0f), Quaternion.identity);
        }

        player.InputLocked = true;
        player.SetSkillInvincible(true);

        float damage    = CalcSweepDamage();
        var   hitEnemies = new HashSet<EnemyController>();

        int steps = Mathf.Max(2, Mathf.RoundToInt(dashStepTime / Time.fixedDeltaTime));
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            _rb.MovePosition(Vector2.Lerp(from, endPos, t));
            ApplyHitAt(_rb.position, dir.normalized, damage, hitEnemies);
            yield return new WaitForFixedUpdate();
        }

        player.SetSkillInvincible(false);
        player.InputLocked = false;
        _dashRoutine = null;
    }

    // ── 밀집 지점 탐색 ────────────────────────────────────────────────

    private Vector2 FindDashTarget(Vector2 from)
    {
        var enemies = EnemyController.All;
        if (enemies.Count == 0)
            return from + _lastMoveDir * dashDistance;

        EnemyController best     = null;
        int             maxCount = -1;

        foreach (var e in enemies)
        {
            if (e == null) continue;
            int count = 0;
            foreach (var other in enemies)
            {
                if (other == null) continue;
                if (Vector2.Distance(e.transform.position, other.transform.position) < clusterRadius)
                    count++;
            }
            if (count > maxCount) { maxCount = count; best = e; }
        }

        return best != null ? (Vector2)best.transform.position : from + _lastMoveDir * dashDistance;
    }

    // ── 대쉬 피해 처리 ────────────────────────────────────────────────

    private void ApplyHitAt(Vector2 center, Vector2 dashDir, float damage,
                             HashSet<EnemyController> hitSet)
    {
        foreach (var enemy in EnemyController.All)
        {
            if (enemy == null) continue;
            if (Vector2.Distance(enemy.transform.position, center) > knockbackRadius) continue;

            Vector2 kbDir = (Vector2)enemy.transform.position - center;
            if (kbDir.sqrMagnitude < 0.01f) kbDir = dashDir;
            enemy.Knockback(kbDir.normalized, knockbackForce);

            if (hitSet.Add(enemy))
            {
                enemy.TakeDamage(damage);
                CameraFollow.Shared?.Shake(dashHitShakeDuration, dashHitShakeMagnitude);
            }
        }
    }

    // ── 데미지 계산 ───────────────────────────────────────────────────

    private static float CalcSweepDamage()
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (wps == null || wps.Count == 0) return 0f;

        float total = 0f;
        foreach (var wp in wps) total += wp.AttackDamage;
        return total / wps.Count / 3f;
    }
}
