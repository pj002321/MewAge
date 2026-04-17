using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치 위치에서 화염 타일을 깝니다.
///
/// 방향별 프리팹:
///   - sidePrefab      : 좌·우 (양옆) 방향 타일. 대각선 중 |X| >= |Y| 인 경우에도 사용.
///   - topBottomPrefab : 상·하 (위아래) 방향 타일. 대각선 중 |Y| >  |X| 인 경우에도 사용.
///   둘 다 연결해야 8방향이 올바르게 표시됩니다.
///   중심 타일은 sidePrefab을 사용합니다.
///
/// 레벨별 확장:
///   Lv. 1~ 9 : 상·하·좌·우 4방향 (십자)
///   Lv.10~14 : 대각선 4방향 추가 → 8방향 (별)
///   Lv.15~19 : 8방향 + spreadLength +2 보너스
///   Lv.20    : 8방향 + spreadLength +4 보너스
///
/// spreadLength 는 Lv.2마다 +1 씩 자동 증가합니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 이 스크립트를 부착하세요 (StructureController와 함께).
///   2. sidePrefab      : 좌·우 화염 타일 프리팹
///   3. topBottomPrefab : 상·하 화염 타일 프리팹
///   4. spreadDelay     : 한 칸씩 깔리는 간격(초). 0이면 즉시.
///   5. cellSize        : 타일 격자 크기. TileRecycler 타일 크기와 맞추세요.
///   6. fireDuration    : 0이면 반영구(웨이브 종료까지). 양수면 N초 뒤 자동 소멸.
/// </summary>
public class FireSpreadSkill : StructureSkillBase
{
    [Header("화염 타일 프리팹")]
    [Tooltip("좌·우(양옆) 방향 및 대각선(|X|≥|Y|) 에 사용할 프리팹")]
    [SerializeField] private GameObject sidePrefab;
    [Tooltip("상·하(위아래) 방향 및 대각선(|Y|>|X|) 에 사용할 프리팹")]
    [SerializeField] private GameObject topBottomPrefab;

    [Header("사운드")]
    [SerializeField] private AudioClip activateSound;

    [Header("확산 설정")]
    [SerializeField] private int   spreadLength = 4;
    [SerializeField] private float cellSize     = 1f;
    [SerializeField] private float spreadDelay  = 0.05f;
    [SerializeField] private float fireDuration = 0f;

    [Header("화염 출혈")]
    [SerializeField] private float bleedDamage   = 5f;   // 틱당 출혈 피해
    [SerializeField] private float bleedDuration = 3f;   // 출혈 지속 시간(초)
    [SerializeField] private float bleedInterval = 0.5f; // 출혈 틱 간격(초)

    private readonly List<GameObject> fireTiles = new();

    // 4방향 (십자)
    private static readonly Vector2[] CrossDirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    // 4방향 (대각)
    private static readonly Vector2[] DiagDirs =
    {
        new Vector2( 1f,  1f).normalized,
        new Vector2(-1f,  1f).normalized,
        new Vector2( 1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized,
    };

    public override void Activate()
    {
        if (sidePrefab == null && topBottomPrefab == null)
        {
            this.DError("[FireSpreadSkill] sidePrefab 또는 topBottomPrefab 중 하나 이상 연결하세요.");
            return;
        }
        SoundManager.Shared?.PlayClip(activateSound);
        StartCoroutine(SpreadRoutine());
    }

    // ── 화염 전개 코루틴 ─────────────────────────────────────────────

    private IEnumerator SpreadRoutine()
    {
        int  level        = StructureLevel;
        int  effectiveLen = CalcEffectiveLength(level);
        bool addDiagonals = level >= 6;

        var waits = spreadDelay > 0f ? new WaitForSeconds(spreadDelay) : null;

        // 중심 타일 (sidePrefab 우선)
        SpawnTile(transform.position, Vector2.zero);

        for (int step = 1; step <= effectiveLen; step++)
        {
            foreach (var dir in CrossDirs)
                SpawnTile(transform.position + (Vector3)(dir * cellSize * step), dir);

            if (addDiagonals)
                foreach (var dir in DiagDirs)
                    SpawnTile(transform.position + (Vector3)(dir * cellSize * step), dir);

            if (waits != null) yield return waits;
        }

        this.DLog($"[FireSpreadSkill] Lv.{level} — {fireTiles.Count}개 화염 타일 배치 완료 (8방향={addDiagonals})");

        if (fireDuration > 0f)
        {
            yield return new WaitForSeconds(fireDuration);
            ClearTiles();
        }
    }

    /// <summary>레벨에 따른 실제 확산 거리 계산 (Lv.2마다 +1칸)</summary>
    private int CalcEffectiveLength(int level)
    {
        int bonus = level / 2;
        return Mathf.Max(1, spreadLength + bonus);
    }

    // ── 타일 스폰/정리 ───────────────────────────────────────────────

    /// <summary>
    /// 방향에 따라 적절한 프리팹을 선택해 타일을 스폰합니다.
    /// dir == zero 이면 중심 타일로 sidePrefab 우선 사용합니다.
    /// |Y| > |X| → topBottomPrefab (상·하·수직 대각)
    /// |X| >= |Y| → sidePrefab     (좌·우·수평 대각)
    /// </summary>
    private void SpawnTile(Vector3 pos, Vector2 dir)
    {
        var prefab = PickPrefab(dir);
        if (prefab == null) return;

        var go = Instantiate(prefab, pos, Quaternion.identity);

        // sidePrefab을 오른쪽 방향(dir.x > 0)으로 사용할 때 flipX
        bool usesSide = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
        if (usesSide && dir.x > 0f)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = true;
        }

        // 트리거 콜라이더가 없으면 동적 추가
        var col = go.GetComponent<BoxCollider2D>();
        if (col == null) col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = Vector2.one * cellSize * 0.9f;

        // 피해 컴포넌트 부착
        var tile = go.AddComponent<FireTile>();
        tile.Init(mainValue, bleedDamage, bleedDuration, bleedInterval, tickInterval);

        fireTiles.Add(go);
    }

    private GameObject PickPrefab(Vector2 dir)
    {
        // 중심 또는 수평 우위
        bool useVertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x);

        if (useVertical)
            return topBottomPrefab != null ? topBottomPrefab : sidePrefab;
        else
            return sidePrefab != null ? sidePrefab : topBottomPrefab;
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
        int previewLen = CalcEffectiveLength(StructureLevel > 0 ? StructureLevel : 1);

        // 십자 방향
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * cellSize * 0.9f);
        foreach (var dir in CrossDirs)
            for (int i = 1; i <= previewLen; i++)
                Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                    Vector3.one * cellSize * 0.9f);

        // 대각 방향 (Lv.6+)
        if (StructureLevel >= 6)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            foreach (var dir in DiagDirs)
                for (int i = 1; i <= previewLen; i++)
                    Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                        Vector3.one * cellSize * 0.9f);
        }
    }
}
