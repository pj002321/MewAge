using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 포메이션 형태별 스폰 위치 목록을 생성합니다.
/// EnemySpawner.SpawnFormation()에서 사용.
///
/// count = 팔/열/변당 적 수 (형태마다 총 스폰 수가 다름)
/// spacing = 인접한 적 사이 거리 (유닛)
/// </summary>
public static class FormationLayout
{
    public enum Shape
    {
        X형,    // 4방향 대각선 팔 (총 count×4)
        사각형,  // 사각형 둘레 (총 (count-1)×4)
        H형,    // 양 열 + 가로 연결부
        십자형,  // 상하좌우 팔 (총 count×4)
        직선,   // 가로 한 줄 (총 count)
        원형,   // 원 둘레 (총 count)
    }

    public static List<Vector2> GetPositions(Shape shape, Vector2 center, float spacing, int count)
    {
        count = Mathf.Max(1, count);
        return shape switch
        {
            Shape.X형   => XShape(center, spacing, count),
            Shape.사각형 => SquareShape(center, spacing, count),
            Shape.H형   => HShape(center, spacing, count),
            Shape.십자형 => PlusShape(center, spacing, count),
            Shape.직선  => LineShape(center, spacing, count),
            Shape.원형  => CircleShape(center, spacing, count),
            _           => new List<Vector2>(),
        };
    }

    // ── X형: 45° 대각선 4방향 ─────────────────────────────────────
    // count=3 → 12명 (팔당 3개)
    static List<Vector2> XShape(Vector2 c, float sp, int n)
    {
        var pts = new List<Vector2>();
        float d = sp / Mathf.Sqrt(2f); // 45° 대각선 한 칸 거리
        Vector2[] arms = { new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };
        foreach (var arm in arms)
            for (int i = 1; i <= n; i++)
                pts.Add(c + arm * (d * i));
        return pts;
    }

    // ── 사각형: 둘레를 따라 배치 ──────────────────────────────────
    // count=4 → 한 변당 4개, 코너 공유 → 총 (4-1)×4 = 12명
    static List<Vector2> SquareShape(Vector2 c, float sp, int n)
    {
        var pts = new List<Vector2>();
        float half = sp * (n - 1) * 0.5f;
        // 위 (왼→오)
        for (int i = 0; i < n; i++) pts.Add(c + new Vector2(-half + sp * i,  half));
        // 오른쪽 (위→아래, 코너 제외)
        for (int i = 1; i < n; i++) pts.Add(c + new Vector2( half,  half - sp * i));
        // 아래 (오→왼, 코너 제외)
        for (int i = 1; i < n; i++) pts.Add(c + new Vector2( half - sp * i, -half));
        // 왼쪽 (아래→위, 양쪽 코너 제외)
        for (int i = 1; i < n - 1; i++) pts.Add(c + new Vector2(-half, -half + sp * i));
        return pts;
    }

    // ── H형: 양 열 + 중간 가로 연결 ──────────────────────────────
    // count=5 → 열당 5개 + 연결부
    static List<Vector2> HShape(Vector2 c, float sp, int n)
    {
        var pts = new List<Vector2>();
        float colX  = sp * 2f;
        float halfH = sp * (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float y = -halfH + sp * i;
            pts.Add(c + new Vector2(-colX, y));
            pts.Add(c + new Vector2( colX, y));
        }

        // 중간 가로 연결부 (-colX+sp ~ colX-sp)
        for (float x = -colX + sp; x < colX - sp * 0.5f; x += sp)
            pts.Add(c + new Vector2(x, 0f));

        return pts;
    }

    // ── 십자형: 상하좌우 팔 ───────────────────────────────────────
    // count=3 → 팔당 3개 → 총 12명 (중심 없음)
    static List<Vector2> PlusShape(Vector2 c, float sp, int n)
    {
        var pts = new List<Vector2>();
        for (int i = 1; i <= n; i++)
        {
            pts.Add(c + new Vector2( sp * i,    0));
            pts.Add(c + new Vector2(-sp * i,    0));
            pts.Add(c + new Vector2(   0,  sp * i));
            pts.Add(c + new Vector2(   0, -sp * i));
        }
        return pts;
    }

    // ── 직선: 가로 한 줄 ─────────────────────────────────────────
    static List<Vector2> LineShape(Vector2 c, float sp, int n)
    {
        var pts  = new List<Vector2>();
        float half = sp * (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
            pts.Add(c + new Vector2(-half + sp * i, 0f));
        return pts;
    }

    // ── 원형: 원 둘레 균등 배치 ──────────────────────────────────
    // count=8 → 45° 간격 8명. radius = count*spacing / (2π)
    static List<Vector2> CircleShape(Vector2 c, float sp, int n)
    {
        var pts    = new List<Vector2>();
        float r    = n * sp / (2f * Mathf.PI);
        for (int i = 0; i < n; i++)
        {
            float angle = 2f * Mathf.PI * i / n;
            pts.Add(c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
        }
        return pts;
    }
}
