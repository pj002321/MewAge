using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 위치에서 방사형으로 투사체를 발사합니다.
/// ★레벨에 따라 방향 수가 변합니다: ★1=4방향, ★2=8방향, ★3=10방향.
/// 투사체에 맞은 적은 피해 + 넉백을 받습니다.
///
/// mainValue    : 투사체 피해량
/// subValue     : 넉백 강도
/// radius       : 투사체 최대 사거리
/// tickInterval : 반복 발사 주기 (초) — repeatFire=false이면 무시
///
/// # Unity Setup:
///   1. c1_w8_s 프리팹에 StructureController + 이 스크립트 부착.
///   2. projectilePrefab : c1_w8_proj 프리팹 연결.
///   3. projectileSpeed  : 투사체 이동 속도.
///   4. repeatFire       : true=주기적 발사 / false=활성화 시 1회만 발사.
/// </summary>
public class RadialShotSkill : StructureSkillBase
{
    [Header("방사형 발사 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed  = 5f;
    [Tooltip("투사체가 적에게 닿았다고 판정하는 반경 (월드 단위). 0.2~0.3 권장.")]
    [SerializeField] private float      projectileRadius = 0.2f;   // 충돌 감지 반경
    // subValue = 넉백 강도. KnockbackRoutine은 force * fixedDeltaTime(0.02) 스케일이므로 200~500 권장.

    [Tooltip("true = tickInterval마다 반복 발사\nfalse = Activate 시 1회만 발사")]
    [SerializeField] private bool repeatFire = true;

    // ★레벨별 방향 수
    private static readonly int[] DirectionsByLevel = { 4, 8, 10 };

    private readonly List<GameObject> _activeProjectiles = new();

    public override void Activate()
    {
        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        FireVolley();
        if (!repeatFire) yield break;

        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            FireVolley();
        }
    }

    private void FireVolley()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        Vector3 origin = player.transform.position;

        int level      = Mathf.Clamp(StructureLevel, 1, DirectionsByLevel.Length);
        int dirCount   = DirectionsByLevel[level - 1];
        float angleStep = 360f / dirCount;

        for (int i = 0; i < dirCount; i++)
        {
            float   angle = angleStep * i;
            float   rad   = angle * Mathf.Deg2Rad;
            Vector2 dir   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            StartCoroutine(ProjectileRoutine(origin, dir));
        }
    }

    private IEnumerator ProjectileRoutine(Vector3 origin, Vector2 direction)
    {
        if (projectilePrefab == null) yield break;

        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        go.SetActive(true);
        // 스프라이트 위쪽이 진행 방향을 향하도록 초기 회전 (이후 BaseAction이 스핀 가능)
        go.transform.up = (Vector3)direction;
        _activeProjectiles.Add(go);

        float travel = 0f;
        var hitSet   = new HashSet<EnemyController>();
        // 월드 방향을 미리 저장 — BaseAction이 transform을 회전시켜도 이동 방향은 고정
        Vector3 worldDir = (Vector3)direction;

        while (travel < radius)
        {
            if (go == null) yield break;

            float step = projectileSpeed * Time.deltaTime;
            go.transform.position += worldDir * step;
            travel += step;

            // 충돌 판정 (관통 — 동일 적에게 1회만 피해)
            var cols = Physics2D.OverlapCircleAll(go.transform.position, projectileRadius);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<EnemyController>(out var enemy)) continue;
                if (hitSet.Contains(enemy)) continue;
                hitSet.Add(enemy);
                enemy.TakeDamage(mainValue, false, direction, subValue);
            }

            yield return null;
        }

        if (go != null)
        {
            _activeProjectiles.Remove(go);
            Destroy(go);
        }
    }

    private void OnDestroy()
    {
        foreach (var p in _activeProjectiles)
            if (p != null) Destroy(p);
        _activeProjectiles.Clear();
    }

    protected override void OnDrawGizmosSelected()
    {
        var player = Application.isPlaying && PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(player, radius);

        // 방향선 미리보기 (현재 레벨 기준)
        int level    = Mathf.Clamp(StructureLevel, 1, DirectionsByLevel.Length);
        int dirCount = DirectionsByLevel[level - 1];
        float step   = 360f / dirCount;
        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
        for (int i = 0; i < dirCount; i++)
        {
            float   rad = step * i * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Gizmos.DrawLine(player, player + dir * radius);
        }
    }
}
