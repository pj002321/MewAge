using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기물 설치 가능 영역.
/// BoxCollider2D로 영역을 정의하고, 그 안에서 그리드 스냅으로 설치됩니다.
/// 런타임에는 아무것도 표시되지 않으며 씬 뷰에서만 기즈모로 확인합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StructureZone : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;

    private BoxCollider2D box;
    private readonly HashSet<Vector2Int> occupiedCells = new();

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    /// <summary>from 위치에서 가장 가까운 빈 그리드 셀을 찾습니다.</summary>
    public bool TryGetNearestCell(Vector3 from, out Vector3 worldPos)
    {
        worldPos = from;
        if (box == null) return false;

        Bounds b = box.bounds;

        // from을 그리드에 스냅
        Vector2Int best  = WorldToCell(from);
        float      bestD = float.MaxValue;

        // 스냅한 위치가 비어있으면 바로 사용, 아니면 주변 탐색
        int searchRange = Mathf.CeilToInt(Mathf.Max(b.size.x, b.size.y) / cellSize);
        for (int dx = -searchRange; dx <= searchRange; dx++)
        for (int dy = -searchRange; dy <= searchRange; dy++)
        {
            var cell = new Vector2Int(best.x + dx, best.y + dy);
            if (occupiedCells.Contains(cell)) continue;

            Vector3 wp = CellToWorld(cell);
            if (!b.Contains(wp)) continue;

            float d = Vector3.Distance(from, wp);
            if (d < bestD) { bestD = d; worldPos = wp; best = cell; }
        }

        if (bestD == float.MaxValue) return false;   // 빈 셀 없음
        worldPos = CellToWorld(best);
        return true;
    }

    public void OccupyCell(Vector3 worldPos)   => occupiedCells.Add(WorldToCell(worldPos));
    public void ReleaseCell(Vector3 worldPos)  => occupiedCells.Remove(WorldToCell(worldPos));

    private Vector2Int WorldToCell(Vector3 w) =>
        new(Mathf.RoundToInt(w.x / cellSize), Mathf.RoundToInt(w.y / cellSize));

    private Vector3 CellToWorld(Vector2Int c) =>
        new(c.x * cellSize, c.y * cellSize, transform.position.z);

    // ── 씬 뷰 전용 기즈모 ────────────────────────────────────────
    private void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider2D>();
        if (b == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawCube(b.bounds.center, b.bounds.size);

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
    }
}
