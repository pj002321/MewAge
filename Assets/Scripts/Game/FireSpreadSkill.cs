using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치 위치에서 상·하·좌·우 4방향으로 각 6칸씩 화염 타일을 깝니다.
/// 화염 타일에 적이 닿으면 tickInterval 마다 mainValue 피해를 입힙니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 이 스크립트를 부착하세요(StructureController와 함께).
///   2. fireTilePrefab : 화염 타일 프리팹 (SpriteRenderer + Collider2D IsTrigger=true).
///      - 화염 프리팹에 별도 스크립트는 필요 없습니다. FireSpreadSkill이 직접 관리합니다.
///   3. spreadDelay : 한 칸씩 깔리는 간격(초) — 0이면 즉시 전개.
///   4. cellSize    : 타일 격자 크기. TileRecycler 타일 크기와 맞추세요.
///   5. fireDuration: 0이면 반영구(웨이브 종료까지 유지). 양수면 N초 후 자동 소멸.
///   6. mainValue   : 틱 당 DoT 피해량.
///   7. tickInterval : DoT 판정 주기(초).
/// </summary>
public class FireSpreadSkill : StructureSkillBase
{
    [Header("화염 타일")]
    [SerializeField] private GameObject fireTilePrefab;   // 화염 이펙트 프리팹
    [SerializeField] private int        spreadLength = 6; // 각 방향 칸 수
    [SerializeField] private float      cellSize     = 1f;// 칸 크기 (TileRecycler 타일 크기에 맞춤)
    [SerializeField] private float      spreadDelay  = 0.05f; // 칸마다 깔리는 연출 딜레이(초)
    [SerializeField] private float      fireDuration = 0f;    // 0 = 웨이브 종료까지 유지



    // 활성화된 화염 타일 목록 (적 DoT 판정 + 정리에 사용)
    private readonly List<GameObject> fireTiles = new();

    private static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    public override void Activate()
    {
        if (fireTilePrefab == null)
        {
            this.DError("[FireSpreadSkill] 작동 실패! Unity Inspector에서 'Fire Tile Prefab'이 연결되지 않았습니다. 화염 프리팹을 만들어서 할당해주세요.");
            return;
        }
        StartCoroutine(SpreadRoutine());
    }

    // ── 화염 전개 코루틴 ─────────────────────────────────────────────
    private IEnumerator SpreadRoutine()
    {
        var waits = spreadDelay > 0f
            ? new WaitForSeconds(spreadDelay)
            : null;

        // 중심 타일
        SpawnTile(transform.position);

        // 4방향 × spreadLength 칸
        for (int step = 1; step <= spreadLength; step++)
        {
            foreach (var dir in Directions)
            {
                Vector3 pos = transform.position + (Vector3)(dir * cellSize * step);
                SpawnTile(pos);
            }

            if (waits != null) yield return waits;
        }

        this.DLog($"[FireSpreadSkill] {fireTiles.Count}개 화염 타일 배치 완료");

        // DoT 틱 루프 시작
        StartCoroutine(DotTickRoutine());

        // 지속 시간이 있으면 N초 후 소멸
        if (fireDuration > 0f)
        {
            yield return new WaitForSeconds(fireDuration);
            ClearTiles();
        }
    }

    // ── DoT 판정 코루틴 ──────────────────────────────────────────────
    private IEnumerator DotTickRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            DealDotDamage();
        }
    }

    private void DealDotDamage()
    {
        // 각 화염 타일 위치 기준 OverlapBox로 적 탐색
        foreach (var tile in fireTiles)
        {
            if (tile == null) continue;
            var hits = Physics2D.OverlapBoxAll(tile.transform.position,
                                               Vector2.one * cellSize * 0.9f, 0f);
            foreach (var col in hits)
                if (col.TryGetComponent<EnemyController>(out var enemy))
                    enemy.TakeDamage(mainValue);
        }
    }

    // ── 타일 스폰/정리 ───────────────────────────────────────────────
    private void SpawnTile(Vector3 pos)
    {
        if (fireTilePrefab == null) return;
        var tile = Instantiate(fireTilePrefab, pos, Quaternion.identity);
        fireTiles.Add(tile);
    }

    private void ClearTiles()
    {
        foreach (var tile in fireTiles)
            if (tile != null) Destroy(tile);
        fireTiles.Clear();
    }

    private void OnDestroy() => ClearTiles(); // 기물이 파괴되면 화염도 제거

    // ── 에디터 기즈모 ────────────────────────────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        foreach (var dir in Directions)
        {
            for (int i = 1; i <= spreadLength; i++)
            {
                Vector3 pos = transform.position + (Vector3)(dir * cellSize * i);
                Gizmos.DrawWireCube(pos, Vector3.one * cellSize * 0.9f);
            }
        }
        // 중심
        Gizmos.DrawWireCube(transform.position, Vector3.one * cellSize * 0.9f);
    }
}
