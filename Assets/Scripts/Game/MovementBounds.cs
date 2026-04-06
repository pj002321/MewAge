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

    [Tooltip("카메라 제한 영역 — 이동 영역 AABB에서 각 방향으로 얼마나 더 넓게 허용할지 (Left, Right, Bottom, Top, 월드 단위)")]
    [SerializeField] public Vector4 cameraPadding = new(1f, 1f, 1f, 1f); // x=Left, y=Right, z=Bottom, w=Top

    private void OnEnable()  => Shared = this;
    public  void Register()  => Shared = this; // WaveEnvironmentSwitcher에서 강제 등록용

    // ── 외부에서 읽기 ──────────────────────────────────────────
    public int   PointCount               => localPoints.Count;
    public Vector2 GetLocal(int i)        => localPoints[i];
    public Vector2 GetWorld(int i)        => transform.TransformPoint(localPoints[i]);

    /// <summary>world 좌표가 폴리곤 안에 있는지 (Ray Casting)</summary>
    public bool IsInside(Vector2 world)
    {
        // 월드 좌표를 로컬 좌표로 변환 (스케일 반영)
        Vector2 p     = transform.InverseTransformPoint(world);
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
        Vector2 p       = transform.InverseTransformPoint(world);
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
        return transform.TransformPoint(closest);
    }

    /// <summary>폴리곤을 감싸는 직사각형의 min/max를 반환합니다. cameraPadding 적용 포함.</summary>
    public void GetBoundsMinMax(out Vector2 min, out Vector2 max)
    {
        if (localPoints == null || localPoints.Count == 0)
        {
            min = max = Vector2.zero;
            return;
        }

        min = transform.TransformPoint(localPoints[0]);
        max = min;

        for (int i = 1; i < localPoints.Count; i++)
        {
            Vector2 worldPoint = transform.TransformPoint(localPoints[i]);
            min.x = Mathf.Min(min.x, worldPoint.x);
            min.y = Mathf.Min(min.y, worldPoint.y);
            max.x = Mathf.Max(max.x, worldPoint.x);
            max.y = Mathf.Max(max.y, worldPoint.y);
        }

        min.x -= cameraPadding.x; // Left
        max.x += cameraPadding.y; // Right
        min.y -= cameraPadding.z; // Bottom
        max.y += cameraPadding.w; // Top
    }

    // ── Gizmo ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        int count = localPoints.Count;

        // 이동 영역 경계선 (초록)
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(transform.TransformPoint(localPoints[j]),
                            transform.TransformPoint(localPoints[i]));

        // 포인트 구체
        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(transform.TransformPoint(localPoints[i]), 0.12f);

        // 이동 영역 AABB 계산
        Vector2 aabbMin = transform.TransformPoint(localPoints[0]);
        Vector2 aabbMax = aabbMin;
        for (int i = 1; i < count; i++)
        {
            Vector2 wp = transform.TransformPoint(localPoints[i]);
            aabbMin.x = Mathf.Min(aabbMin.x, wp.x); aabbMin.y = Mathf.Min(aabbMin.y, wp.y);
            aabbMax.x = Mathf.Max(aabbMax.x, wp.x); aabbMax.y = Mathf.Max(aabbMax.y, wp.y);
        }

        // 이동 영역 AABB (흰색 점선 느낌으로 구분)
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Vector3 aabbCenter = new((aabbMin.x + aabbMax.x) * 0.5f, (aabbMin.y + aabbMax.y) * 0.5f, 0f);
        Vector3 aabbSize   = new(aabbMax.x - aabbMin.x, aabbMax.y - aabbMin.y, 0f);
        Gizmos.DrawWireCube(aabbCenter, aabbSize);

        // 카메라 패딩 영역 — 반드시 AABB 밖 (하늘색)
        Vector2 camMin = new(aabbMin.x - Mathf.Max(0f, cameraPadding.x),
                             aabbMin.y - Mathf.Max(0f, cameraPadding.z));
        Vector2 camMax = new(aabbMax.x + Mathf.Max(0f, cameraPadding.y),
                             aabbMax.y + Mathf.Max(0f, cameraPadding.w));
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Vector3 camCenter = new((camMin.x + camMax.x) * 0.5f, (camMin.y + camMax.y) * 0.5f, 0f);
        Vector3 camSize   = new(camMax.x - camMin.x, camMax.y - camMin.y, 0f);
        Gizmos.DrawWireCube(camCenter, camSize);
    }
}
