using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폴리곤으로 정의한 적 스폰 영역.
/// NormalEnvironment / BossEnvironment 하위에 배치하고,
/// WaveEnvironmentSwitcher가 환경 전환 시 Register()를 호출합니다.
///
/// Inspector에서 포인트를 추가하고, 씬 뷰에서 핸들을 드래그해 편집하세요.
/// </summary>
public class SpawnZone : MonoBehaviour
{
    public static SpawnZone Shared { get; private set; }

    [SerializeField] private List<Vector2> localPoints = new()
    {
        new(-6f, -10f), new( 6f, -10f),
        new( 6f,  10f), new(-6f,  10f),
    };

    [Tooltip("폴리곤 경계 바깥으로 스폰 위치를 밀어낼 거리")]
    [SerializeField] private float edgeOffset = 0.5f;

    private void OnEnable() => Shared = this;
    public  void Register() => Shared = this;

    public int     PointCount      => localPoints.Count;
    public Vector2 GetLocal(int i) => localPoints[i];
    public Vector2 GetWorld(int i) => transform.TransformPoint(localPoints[i]);

    // ── AABB (ChargeBehaviour / HordeBehaviour 호환) ─────────────────
    /// <summary>폴리곤 AABB 중심 (월드 좌표)</summary>
    public Vector2 BoundsCenter
    {
        get
        {
            if (localPoints.Count == 0) return transform.position;
            Vector2 mn = transform.TransformPoint(localPoints[0]);
            Vector2 mx = mn;
            foreach (var p in localPoints)
            {
                Vector2 wp = transform.TransformPoint(p);
                mn = Vector2.Min(mn, wp);
                mx = Vector2.Max(mx, wp);
            }
            return (mn + mx) * 0.5f;
        }
    }

    /// <summary>폴리곤 AABB 크기</summary>
    public Vector2 BoundsSize
    {
        get
        {
            if (localPoints.Count == 0) return Vector2.zero;
            Vector2 mn = transform.TransformPoint(localPoints[0]);
            Vector2 mx = mn;
            foreach (var p in localPoints)
            {
                Vector2 wp = transform.TransformPoint(p);
                mn = Vector2.Min(mn, wp);
                mx = Vector2.Max(mx, wp);
            }
            return mx - mn;
        }
    }

    // ── 스폰 위치 ─────────────────────────────────────────────────────
    /// <summary>폴리곤 경계 위 랜덤 위치에서 edgeOffset 만큼 바깥으로 밀어낸 좌표를 반환합니다.</summary>
    public Vector2 GetRandomEdgePosition()
    {
        if (localPoints.Count < 2) return transform.position;

        int     count    = localPoints.Count;
        Vector2 centroid = CentroidWorld();

        // 엣지 길이를 가중치로 랜덤 엣지 선택 → 균등 분포 (월드 좌표 기준)
        float total = 0f;
        for (int i = 0, j = count - 1; i < count; j = i++)
            total += Vector2.Distance(transform.TransformPoint(localPoints[j]), transform.TransformPoint(localPoints[i]));

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        int ai = count - 1, bi = 0;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            float len = Vector2.Distance(transform.TransformPoint(localPoints[j]), transform.TransformPoint(localPoints[i]));
            acc += len;
            if (roll <= acc) { ai = j; bi = i; break; }
        }

        Vector2 a     = transform.TransformPoint(localPoints[ai]);
        Vector2 b     = transform.TransformPoint(localPoints[bi]);
        Vector2 point = Vector2.Lerp(a, b, Random.value);

        // 다각형 중심 기준 외곽 법선
        Vector2 edge   = b - a;
        Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
        if (Vector2.Dot(normal, (a + b) * 0.5f - centroid) < 0f)
            normal = -normal;

        return point + normal * edgeOffset;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────
    private Vector2 CentroidWorld()
    {
        if (localPoints.Count == 0) return transform.position;
        Vector2 c = Vector2.zero;
        foreach (var p in localPoints) c += (Vector2)transform.TransformPoint(p);
        return c / localPoints.Count;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        int     count    = localPoints.Count;
        Vector2 centroid = Vector2.zero;
        foreach (var p in localPoints) centroid += (Vector2)LocalToWorldNoScale(p);
        centroid /= count;

        // 내부 경계 (주황)
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(LocalToWorldNoScale(localPoints[j]), LocalToWorldNoScale(localPoints[i]));

        // 포인트 구체
        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(LocalToWorldNoScale(localPoints[i]), 0.12f);

        // 외부 edgeOffset 경계 (빨강)
        if (edgeOffset > 0f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a    = LocalToWorldNoScale(localPoints[j]);
                Vector2 b    = LocalToWorldNoScale(localPoints[i]);
                Vector2 edge = b - a;
                Vector2 n    = new Vector2(edge.y, -edge.x).normalized;
                if (Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f) n = -n;
                Gizmos.DrawLine(a + n * edgeOffset, b + n * edgeOffset);
            }
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0.1f);
        
        // BoundsSize를 NoScale 기준으로 근사 계산하여 라벨 띄움
        Vector2 mn = LocalToWorldNoScale(localPoints[0]);
        Vector2 mx = mn;
        foreach (var p in localPoints)
        {
            Vector2 wp = LocalToWorldNoScale(p);
            mn = Vector2.Min(mn, wp);
            mx = Vector2.Max(mx, wp);
        }
        
        UnityEditor.Handles.Label(
            centroid + Vector2.up * ((mx.y - mn.y) * 0.5f + edgeOffset + 0.2f),
            "[ Spawn Zone ]");
#endif
    }
    
    // MovementBounds.cs의 렌더링 유지 코드와 동일한 기능
    private Vector3 LocalToWorldNoScale(Vector2 local)
    {
        return transform.position + (Vector3)local;
    }
}
