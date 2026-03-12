using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동 가능 영역을 폴리곤으로 정의합니다.
/// Inspector에서 포인트를 추가하고, 씬 뷰에서 노란 핸들을 드래그해 편집하세요.
/// </summary>
public class MovementBounds : MonoBehaviour
{
    public static MovementBounds Shared { get; private set; }

    [SerializeField] private List<Vector2> localPoints = new()
    {
        new(-5f, -9f), new( 5f, -9f),
        new( 5f,  9f), new(-5f,  9f),
    };

    private void Awake() => Shared = this;

    // ── 외부에서 읽기 ──────────────────────────────────────────
    public int   PointCount               => localPoints.Count;
    public Vector2 GetLocal(int i)        => localPoints[i];
    public Vector2 GetWorld(int i)        => (Vector2)transform.position + localPoints[i];

    /// <summary>world 좌표가 폴리곤 안에 있는지 (Ray Casting)</summary>
    public bool IsInside(Vector2 world)
    {
        Vector2 p     = world - (Vector2)transform.position;
        int     count = localPoints.Count;
        bool    inside = false;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = localPoints[i];
            Vector2 pj = localPoints[j];
            if ((pi.y > p.y) != (pj.y > p.y) &&
                p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>폴리곤 경계 위에서 world 좌표와 가장 가까운 점</summary>
    public Vector2 ClosestPointOnBoundary(Vector2 world)
    {
        Vector2 offset  = transform.position;
        Vector2 p       = world - offset;
        int     count   = localPoints.Count;
        float   minDist = float.MaxValue;
        Vector2 closest = p;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a  = localPoints[j];
            Vector2 b  = localPoints[i];
            Vector2 ab = b - a;
            float   t  = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector2 pr = a + t * ab;
            float   d  = Vector2.SqrMagnitude(p - pr);
            if (d < minDist) { minDist = d; closest = pr; }
        }
        return closest + offset;
    }

    // ── Gizmo ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        Vector2 offset = transform.position;
        int     count  = localPoints.Count;

        // 경계선
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(localPoints[j] + offset, localPoints[i] + offset);

        // 포인트 구체
        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(localPoints[i] + offset, 0.12f);
    }
}
