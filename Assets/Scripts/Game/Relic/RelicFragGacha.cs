using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 유물 조각 뽑기 상자 종류.
/// DB_확률의 f_가챠타입과 대응합니다. (RF_NORMAL / RF_EPIC)
/// </summary>
public enum RelicFragBoxType
{
    Normal,
    Epic,
}

/// <summary>
/// 유물 조각 뽑기 로직.
/// 뽑기 결과로 특정 유물의 조각을 획득하며, RelicInventory에 자동 추가합니다.
///
/// 확률 구조 (DB_확률 RF_NORMAL / RF_EPIC 행):
///   f_G일반  — 일반 등급 유물 조각 확률
///   f_G레어  — 고급 등급 유물 조각 확률
///   f_G에픽  — 희귀 등급 유물 조각 확률
///   f_G전설  — 전설 등급 유물 조각 확률
/// </summary>
public static class RelicFragGacha
{
    // ── 코드 기본 확률 fallback ─────────────────────────────────────
    static readonly Dictionary<RelicFragBoxType, float[]> DefaultWeights = new()
    {
        { RelicFragBoxType.Normal, new[] { 60f, 35f,  5f,  0f } },
        { RelicFragBoxType.Epic,   new[] {  0f, 40f, 50f, 10f } },
    };

    // 등급별 획득 조각 수 (min, max)
    static readonly Dictionary<string, (int min, int max)> FragCounts = new()
    {
        { "일반",     (10, 15) },
        { "고급",     ( 5, 10) },
        { "희귀",     ( 3,  5) },
        { "전설",     ( 1,  3) },
        { "신화",     ( 1,  2) },
    };

    static readonly string[] GradeKeys   = { "일반", "고급", "희귀", "전설" };
    static readonly string[] GradeDbKeys = { "NORMAL", "RARE", "EPIC", "LEGENDARY" };

    // ── 비용 (DB_상점 미연결 시 기본값) ────────────────────────────
    static readonly Dictionary<RelicFragBoxType, int> DefaultCosts = new()
    {
        { RelicFragBoxType.Normal,  100 },
        { RelicFragBoxType.Epic,    300 },
    };

    // ── 공개 API ────────────────────────────────────────────────────

    public static int GetCost(RelicFragBoxType boxType) =>
        DefaultCosts.TryGetValue(boxType, out var c) ? c : 100;

    public static RelicFragResultPopup.Entry Draw(RelicFragBoxType boxType)
    {
        string gradeKr   = RollGrade(boxType);
        var    data      = DrawByGrade(gradeKr);
        if (data == null) return default;

        var (min, max) = FragCounts.TryGetValue(gradeKr, out var r) ? r : (5, 10);
        int count = Random.Range(min, max + 1);

        RelicInventory.Shared?.AddFragments(data.relicId, count);

        return new RelicFragResultPopup.Entry(data, count);
    }

    public static List<RelicFragResultPopup.Entry> DrawMulti(RelicFragBoxType boxType, int count)
    {
        var results = new List<RelicFragResultPopup.Entry>(count);
        for (int i = 0; i < count; i++)
        {
            var entry = Draw(boxType);
            if (entry.data != null) results.Add(entry);
        }
        return results;
    }

    // ── 내부 ────────────────────────────────────────────────────────

    static string RollGrade(RelicFragBoxType boxType)
    {
        float[] w    = GetWeights(boxType);
        float   roll = Random.value * 100f;
        float   cum  = 0f;
        for (int i = 0; i < GradeKeys.Length; i++)
        {
            cum += w[i];
            if (roll < cum) return GradeKeys[i];
        }
        return GradeKeys[0];
    }

    static float[] GetWeights(RelicFragBoxType boxType)
    {
        try
        {
            string key  = BoxTypeToDbKey(boxType);
            var    rows = DB_확률.FindEntities(e => e.f_가챠타입 == key);
            if (rows != null && rows.Count > 0)
            {
                var row = rows[0];
                return new[] { (float)row.f_G일반, (float)row.f_G레어, (float)row.f_G에픽, (float)row.f_G전설 };
            }
        }
        catch { }

        return DefaultWeights.TryGetValue(boxType, out var w) ? w : DefaultWeights[RelicFragBoxType.Normal];
    }

    static string BoxTypeToDbKey(RelicFragBoxType boxType) => boxType switch
    {
        RelicFragBoxType.Normal => "RF_NORMAL",
        RelicFragBoxType.Epic   => "RF_EPIC",
        _                       => "RF_NORMAL",
    };

    static RelicData DrawByGrade(string gradeKr)
    {
        var inv = RelicInventory.Shared;
        if (inv == null) return null;

        var all        = inv.GetAll();
        var candidates = new List<RelicData>();
        foreach (var d in all)
            if (d.grade == gradeKr) candidates.Add(d);

        if (candidates.Count == 0)
        {
            // 해당 등급 없으면 전체에서 랜덤
            foreach (var d in all) candidates.Add(d);
        }

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : null;
    }
}
