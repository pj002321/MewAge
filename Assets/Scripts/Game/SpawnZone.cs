using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 스폰 영역. 자체 폴리곤으로 영역을 지정합니다.
/// 부모 오브젝트의 스케일(FitSpriteToScreen 등)을 TransformPoint로 자동 반영합니다.
/// </summary>
public class SpawnZone : MonoBehaviour
{
    public static SpawnZone Shared { get; private set; }

    [SerializeField] private List<Vector2> localPoints = new()
    {
        new(-4f, -6f), new( 4f, -6f),
        new( 4f,  6f), new(-4f,  6f),
    };

    [Tooltip("폴리곤 경계 바깥으로 스폰 위치를 밀어낼 거리 (0 = 경계 위에 스폰)")]
    [SerializeField] private float edgeOffset = 0f;

    private void OnEnable() => Shared = this;
    public  void Register() => Shared = this;

    public int     PointCount      => localPoints.Count;
    public Vector2 GetLocal(int i) => localPoints[i];
    public Vector2 GetWorld(int i) => transform.TransformPoint(localPoints[i]);

    // ── AABB ─────────────────────────────────────────────────────────
    public Vector2 BoundsCenter
    {
        get { GetAABB(out var mn, out var mx); return (mn + mx) * 0.5f; }
    }
    public Vector2 BoundsSize
    {
        get { GetAABB(out var mn, out var mx); return mx - mn; }
    }

    private void GetAABB(out Vector2 mn, out Vector2 mx)
    {
        mn = GetWorld(0); mx = mn;
        for (int i = 1; i < localPoints.Count; i++)
        {
            Vector2 wp = GetWorld(i);
            mn = Vector2.Min(mn, wp); mx = Vector2.Max(mx, wp);
        }
    }

    // ── 스폰 위치 ────────────────────────────────────────────────────
    public Vector2 GetRandomEdgePosition()
    {
        if (localPoints.Count < 2) return transform.position;

        int     count    = localPoints.Count;
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < count; i++) centroid += GetWorld(i);
        centroid /= count;

        // 엣지 길이 가중치 랜덤 선택
        float total = 0f;
        for (int i = 0, j = count - 1; i < count; j = i++)
            total += Vector2.Distance(GetWorld(j), GetWorld(i));

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        int ai = count - 1, bi = 0;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            float len = Vector2.Distance(GetWorld(j), GetWorld(i));
            acc += len;
            if (roll <= acc) { ai = j; bi = i; break; }
        }

        Vector2 a     = GetWorld(ai);
        Vector2 b     = GetWorld(bi);
        Vector2 point = Vector2.Lerp(a, b, Random.value);

        if (edgeOffset > 0f)
        {
            Vector2 n = new Vector2((b - a).y, -(b - a).x).normalized;
            if (Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f) n = -n;
            point += n * edgeOffset;
        }

        return point;
    }

    // ── Gizmo ────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        int     count    = localPoints.Count;
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < count; i++) centroid += GetWorld(i);
        centroid /= count;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(GetWorld(j), GetWorld(i));

        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(GetWorld(i), 0.12f);

        if (edgeOffset > 0f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = GetWorld(j), b = GetWorld(i);
                Vector2 n = new Vector2((b - a).y, -(b - a).x).normalized;
                if (Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f) n = -n;
                Gizmos.DrawLine(a + n * edgeOffset, b + n * edgeOffset);
            }
        }
    }
}
