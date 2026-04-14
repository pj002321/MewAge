using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치 위치에서 화염 타일을 깝니다.
///
/// 레벨별 확장:
///   Lv. 1~ 9 : 상·하·좌·우 4방향 (십자)
///   Lv.10~14 : 대각선 4방향 추가 → 8방향 (별)
///   Lv.15~19 : 8방향 + spreadLength +2 보너스
///   Lv.20    : 8방향 + spreadLength +4 보너스
///
/// spreadLength 는 레벨마다 +0.25 씩 자동 증가합니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 이 스크립트를 부착하세요 (StructureController와 함께).
///   2. fireTilePrefab : 화염 타일 프리팹 (SpriteRenderer + Collider2D IsTrigger=true).
///   3. spreadDelay : 한 칸씩 깔리는 간격(초). 0이면 즉시.
///   4. cellSize    : 타일 격자 크기. TileRecycler 타일 크기와 맞추세요.
///   5. fireDuration: 0이면 반영구(웨이브 종료까지). 양수면 N초 뒤 자동 소멸.
/// </summary>
public class FireSpreadSkill : StructureSkillBase
{
    [Header("화염 타일")]
    [SerializeField] private GameObject fireTilePrefab;
    [SerializeField] private int        spreadLength = 4;
    [SerializeField] private float      cellSize     = 1f;
    [SerializeField] private float      spreadDelay  = 0.05f;
    [SerializeField] private float      fireDuration = 0f;

    private readonly List<GameObject> fireTiles = new();

    // 기본 4방향
    private static readonly Vector2[] CrossDirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    // 추가 대각 4방향
    private static readonly Vector2[] DiagDirs =
    {
        new Vector2( 1f,  1f).normalized,
        new Vector2(-1f,  1f).normalized,
        new Vector2( 1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized,
    };

    public override void Activate()
    {
        if (fireTilePrefab == null)
        {
            this.DError("[FireSpreadSkill] Fire Tile Prefab이 연결되지 않았습니다.");
            return;
        }
        StartCoroutine(SpreadRoutine());
    }

    // ── 화염 전개 코루틴 ─────────────────────────────────────────────

    private IEnumerator SpreadRoutine()
    {
        int   level          = StructureLevel;
        int   effectiveLen   = CalcEffectiveLength(level);
        bool  addDiagonals   = level >= 6;

        var waits = spreadDelay > 0f ? new WaitForSeconds(spreadDelay) : null;

        // 중심 타일
        SpawnTile(transform.position);

        for (int step = 1; step <= effectiveLen; step++)
        {
            // 십자 방향
            foreach (var dir in CrossDirs)
                SpawnTile(transform.position + (Vector3)(dir * cellSize * step));

            // 별 방향 (Lv.10+)
            if (addDiagonals)
                foreach (var dir in DiagDirs)
                    SpawnTile(transform.position + (Vector3)(dir * cellSize * step));

            if (waits != null) yield return waits;
        }

        this.DLog($"[FireSpreadSkill] Lv.{level} — {fireTiles.Count}개 화염 타일 배치 완료 (8방향={addDiagonals})");

        StartCoroutine(DotTickRoutine());

        if (fireDuration > 0f)
        {
            yield return new WaitForSeconds(fireDuration);
            ClearTiles();
        }
    }

    /// <summary>레벨에 따른 실제 확산 거리 계산 (Lv.2마다 +1칸)</summary>
    private int CalcEffectiveLength(int level)
    {
        // Lv.2=+1, Lv.4=+2, Lv.6=+3 … (2레벨마다 1칸 증가, 레벨 1부터 즉시 체감)
        int bonus = level / 2;
        return Mathf.Max(1, spreadLength + bonus);
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
        foreach (var tile in fireTiles)
        {
            if (tile == null) continue;
            var hits = Physics2D.OverlapBoxAll(tile.transform.position,
                                               Vector2.one * cellSize * 0.9f, 0f);
            bool bossHit = false;
            foreach (var col in hits)
            {
                if (col.GetComponentInParent<EnemyController>() is EnemyController enemy)
                    enemy.TakeDamage(mainValue);
                else if (!bossHit && col.GetComponentInParent<BossController>() is BossController boss)
                {
                    boss.TakeDamage(mainValue);
                    bossHit = true;
                }
            }
        }
    }

    // ── 타일 스폰/정리 ───────────────────────────────────────────────

    private void SpawnTile(Vector3 pos)
    {
        if (fireTilePrefab == null) return;
        fireTiles.Add(Instantiate(fireTilePrefab, pos, Quaternion.identity));
    }

    private void ClearTiles()
    {
        foreach (var tile in fireTiles)
            if (tile != null) Destroy(tile);
        fireTiles.Clear();
    }

    private void OnDestroy() => ClearTiles();

    // ── 에디터 기즈모 ────────────────────────────────────────────────

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);

        int previewLen = CalcEffectiveLength(StructureLevel > 0 ? StructureLevel : 1);

        foreach (var dir in CrossDirs)
            for (int i = 1; i <= previewLen; i++)
                Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                    Vector3.one * cellSize * 0.9f);

        if (StructureLevel >= 6)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            foreach (var dir in DiagDirs)
                for (int i = 1; i <= previewLen; i++)
                    Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                        Vector3.one * cellSize * 0.9f);
        }

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * cellSize * 0.9f);
    }
}
