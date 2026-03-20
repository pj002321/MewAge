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
    public Vector2 GetWorld(int i) => (Vector2)transform.position + localPoints[i];

    // ── AABB (ChargeBehaviour / HordeBehaviour 호환) ─────────────────
    /// <summary>폴리곤 AABB 중심 (월드 좌표)</summary>
    public Vector2 BoundsCenter
    {
        get
        {
            Vector2 mn = localPoints[0], mx = localPoints[0];
            foreach (var p in localPoints) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            return (mn + mx) * 0.5f + (Vector2)transform.position;
        }
    }

    /// <summary>폴리곤 AABB 크기</summary>
    public Vector2 BoundsSize
    {
        get
        {
            Vector2 mn = localPoints[0], mx = localPoints[0];
            foreach (var p in localPoints) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            return mx - mn;
        }
    }

    // ── 스폰 위치 ─────────────────────────────────────────────────────
    /// <summary>폴리곤 경계 위 랜덤 위치에서 edgeOffset 만큼 바깥으로 밀어낸 좌표를 반환합니다.</summary>
    public Vector2 GetRandomEdgePosition()
    {
        if (localPoints.Count < 2) return transform.position;

        Vector2 offset   = transform.position;
        int     count    = localPoints.Count;
        Vector2 centroid = Centroid() + offset;

        // 엣지 길이를 가중치로 랜덤 엣지 선택 → 균등 분포
        float total = 0f;
        for (int i = 0, j = count - 1; i < count; j = i++)
            total += Vector2.Distance(localPoints[j], localPoints[i]);

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        int ai = count - 1, bi = 0;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            float len = Vector2.Distance(localPoints[j], localPoints[i]);
            acc += len;
            if (roll <= acc) { ai = j; bi = i; break; }
        }

        Vector2 a     = localPoints[ai] + offset;
        Vector2 b     = localPoints[bi] + offset;
        Vector2 point = Vector2.Lerp(a, b, Random.value);

        // 다각형 중심 기준 외곽 법선
        Vector2 edge   = b - a;
        Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
        if (Vector2.Dot(normal, (a + b) * 0.5f - centroid) < 0f)
            normal = -normal;

        return point + normal * edgeOffset;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────
    private Vector2 Centroid()
    {
        Vector2 c = Vector2.zero;
        foreach (var p in localPoints) c += p;
        return c / localPoints.Count;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (localPoints == null || localPoints.Count < 2) return;

        Vector2 offset   = transform.position;
        int     count    = localPoints.Count;
        Vector2 centroid = Centroid() + offset;

        // 내부 경계 (주황)
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        for (int i = 0, j = count - 1; i < count; j = i++)
            Gizmos.DrawLine(localPoints[j] + offset, localPoints[i] + offset);

        // 포인트 구체
        Gizmos.color = Color.yellow;
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(localPoints[i] + offset, 0.12f);

        // 외부 edgeOffset 경계 (빨강)
        if (edgeOffset > 0f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.55f);
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a    = localPoints[j] + offset;
                Vector2 b    = localPoints[i] + offset;
                Vector2 edge = b - a;
                Vector2 n    = new Vector2(edge.y, -edge.x).normalized;
                if (Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f) n = -n;
                Gizmos.DrawLine(a + n * edgeOffset, b + n * edgeOffset);
            }
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.6f, 0.1f);
        UnityEditor.Handles.Label(
            centroid + Vector2.up * (BoundsSize.y * 0.5f + edgeOffset + 0.2f),
            "[ Spawn Zone ]");
#endif
    }
}
