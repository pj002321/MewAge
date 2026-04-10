using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines player movement polygon AND camera behavior independently.
/// - Player movement: polygon (localPoints)
/// - Camera: FixedView (centered, no follow) or DynamicFollow (follows player, clamped)
/// Attach as a child of the Background prefab alongside FitSpriteToScreen.
/// </summary>
public class MovementBounds : MonoBehaviour
{
    public static MovementBounds Shared { get; private set; }

    // ── Camera View Mode ─────────────────────────────────────────
    public enum CameraViewMode
    {
        DynamicFollow,  // Camera follows player, clamped within camera bounds (normal maps)
        FixedView       // Camera fixed at center of camera bounds, no follow (boss maps)
    }

    [Header("카메라 뷰 모드")]
    [Tooltip("DynamicFollow: 카메라가 플레이어를 따라감 (일반맵)\nFixedView: 카메라 고정, 지정 영역 중심 (보스맵)")]
    [SerializeField] private CameraViewMode viewMode = CameraViewMode.DynamicFollow;
    public CameraViewMode ViewMode => viewMode;

    // ── Player Movement Polygon ──────────────────────────────────
    [Header("플레이어 이동 영역 (폴리곤)")]
    [SerializeField] private List<Vector2> localPoints = new()
    {
        new(-5f, -9f), new( 5f, -9f),
        new( 5f,  9f), new(-5f,  9f),
    };

    // ── Camera Bounds ────────────────────────────────────────────
    [Header("카메라 제한 영역")]
    [Tooltip("true: cameraBoundsRect를 직접 사용 / false: 이동 영역 AABB + cameraPadding 자동 계산\nFixedView 모드에서는 항상 cameraBoundsRect 사용")]
    [SerializeField] private bool useCustomCameraBounds = false;

    [Tooltip("직접 지정 카메라 영역 (로컬 좌표). FixedView 또는 useCustomCameraBounds=true일 때 적용")]
    [SerializeField] private Rect cameraBoundsRect = new(-5f, -9f, 10f, 18f);

    [Tooltip("자동 카메라 영역 패딩 (Left, Right, Bottom, Top). DynamicFollow + useCustomCameraBounds=false일 때만 적용")]
    [SerializeField] public Vector4 cameraPadding = new(1f, 1f, 1f, 1f);

    private void OnEnable()  => Shared = this;
    public  void Register()  => Shared = this;

    // ── Read-only API ────────────────────────────────────────────
    public int     PointCount                => localPoints.Count;
    public Vector2 GetLocal(int i)           => localPoints[i];
    public Vector2 GetWorld(int i)           => transform.TransformPoint(localPoints[i]);
    public bool    UseCustomCameraBounds     => useCustomCameraBounds;
    public Rect    CameraBoundsRect          => cameraBoundsRect;

    /// <summary>Is a world position inside the movement polygon? (Ray Casting)</summary>
    public bool IsInside(Vector2 world)
    {
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

    /// <summary>Closest point on polygon boundary to a world position.</summary>
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

    // ── Player Movement AABB (no padding) ────────────────────────

    /// <summary>Returns player movement polygon AABB in world coords.</summary>
    public void GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max)
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
            Vector2 wp = transform.TransformPoint(localPoints[i]);
            min.x = Mathf.Min(min.x, wp.x);
            min.y = Mathf.Min(min.y, wp.y);
            max.x = Mathf.Max(max.x, wp.x);
            max.y = Mathf.Max(max.y, wp.y);
        }
    }

    // ── Camera Bounds ────────────────────────────────────────────

    /// <summary>
    /// Returns camera bounds min/max in world coords.
    /// FixedView always uses cameraBoundsRect.
    /// DynamicFollow uses cameraBoundsRect if useCustomCameraBounds, else AABB + padding.
    /// </summary>
    public void GetCameraBoundsMinMax(out Vector2 min, out Vector2 max)
    {
        // FixedView always uses the custom rect
        if (viewMode == CameraViewMode.FixedView || useCustomCameraBounds)
        {
            Vector2 localMin = new(cameraBoundsRect.xMin, cameraBoundsRect.yMin);
            Vector2 localMax = new(cameraBoundsRect.xMax, cameraBoundsRect.yMax);
            min = (Vector2)transform.TransformPoint(localMin);
            max = (Vector2)transform.TransformPoint(localMax);
            return;
        }

        // DynamicFollow fallback: movement AABB + padding
        GetPlayerBoundsMinMax(out min, out max);
        min.x -= cameraPadding.x;
        max.x += cameraPadding.y;
        min.y -= cameraPadding.z;
        max.y += cameraPadding.w;
    }

    /// <summary>Legacy API — returns camera bounds.</summary>
    public void GetBoundsMinMax(out Vector2 min, out Vector2 max)
    {
        GetCameraBoundsMinMax(out min, out max);
    }

    // ── Gizmo ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        int count = localPoints.Count;

        // Movement polygon boundary (green)
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(LocalToWorldNoScale(localPoints[j]),
                            LocalToWorldNoScale(localPoints[i]));

        // Point spheres (yellow)
        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(LocalToWorldNoScale(localPoints[i]), 0.12f);

        // Movement AABB (white, faint)
        Vector2 aabbMin = LocalToWorldNoScale(localPoints[0]);
        Vector2 aabbMax = aabbMin;
        for (int i = 1; i < count; i++)
        {
            Vector2 wp = LocalToWorldNoScale(localPoints[i]);
            aabbMin.x = Mathf.Min(aabbMin.x, wp.x); aabbMin.y = Mathf.Min(aabbMin.y, wp.y);
            aabbMax.x = Mathf.Max(aabbMax.x, wp.x); aabbMax.y = Mathf.Max(aabbMax.y, wp.y);
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Vector3 aabbCenter = new((aabbMin.x + aabbMax.x) * 0.5f, (aabbMin.y + aabbMax.y) * 0.5f, 0f);
        Vector3 aabbSize   = new(aabbMax.x - aabbMin.x, aabbMax.y - aabbMin.y, 0f);
        Gizmos.DrawWireCube(aabbCenter, aabbSize);

        // Camera bounds (cyan)
        GetCameraBoundsMinMax(out Vector2 camMin, out Vector2 camMax);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Vector3 camCenter = new((camMin.x + camMax.x) * 0.5f, (camMin.y + camMax.y) * 0.5f, 0f);
        Vector3 camSize   = new(camMax.x - camMin.x, camMax.y - camMin.y, 0f);
        Gizmos.DrawWireCube(camCenter, camSize);

        // FixedView: draw actual camera view rect (magenta) at bounds center
        if (viewMode == CameraViewMode.FixedView)
        {
            float boundsW = camMax.x - camMin.x;
            float boundsH = camMax.y - camMin.y;

            if (boundsW > 0f && boundsH > 0f)
            {
                // Determine aspect ratio: use main camera if available, else estimate 9:19.5
                Camera cam = Camera.main;
                float aspect = cam != null ? cam.aspect : (9f / 19.5f);

                // Same calculation as CameraFollow.ApplyFixedView
                float orthoByH = boundsH * 0.5f;
                float orthoByW = boundsW * 0.5f / aspect;
                float ortho    = Mathf.Min(orthoByH, orthoByW);
                float halfH    = ortho;
                float halfW    = ortho * aspect;

                Gizmos.color = new Color(1f, 0.3f, 1f, 0.8f);
                Gizmos.DrawWireCube(camCenter, new Vector3(halfW * 2f, halfH * 2f, 0f));

                // Label
#if UNITY_EDITOR
                UnityEditor.Handles.color = new Color(1f, 0.3f, 1f, 1f);
                UnityEditor.Handles.Label(
                    camCenter + Vector3.up * (halfH + 0.3f),
                    $"📷 Camera View (ortho={ortho:F2})",
                    UnityEditor.EditorStyles.boldLabel);
#endif
            }
        }
    }

    private Vector3 LocalToWorldNoScale(Vector2 local)
    {
        return transform.position + (Vector3)local;
    }
}
