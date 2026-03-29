using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 장비 상자 종류.
/// DB_상점의 상품ID(EQ_NORMAL/EQ_RARE/EQ_EPIC)와 대응합니다.
/// </summary>
public enum EquipBoxType
{
    /// <summary>일반 장비 상자 — EQ_NORMAL (기본 일반70/레어30)</summary>
    Normal,
    /// <summary>레어 장비 상자 — EQ_RARE (일반40/레어50/에픽10)</summary>
    Rare,
    /// <summary>에픽 장비 상자 — EQ_EPIC (레어45/에픽50/전설5, 필중 8/48회)</summary>
    Epic,
}

/// <summary>
/// 장비 뽑기 로직.
///
/// [DB_확률 테이블에 추가할 컬럼]
///   f_가챠타입  (String, 행 키) : EQ_NORMAL / EQ_RARE / EQ_EPIC
///   f_G일반     (Float 0~100)
///   f_G레어     (Float 0~100)
///   f_G에픽     (Float 0~100)
///   f_G전설     (Float 0~100)
///   f_필중횟수  (String, "8|48" 형식 — 에픽필중|전설필중, 0이면 없음)
///
/// DB 컬럼 추가 전까지는 코드 기본값으로 동작합니다.
/// DB 준비 후 GetWeights/GetPity 내부 주석 해제하세요.
/// </summary>
public static class EquipmentGacha
{
    // ── 코드 기본 확률 fallback ─────────────────────────────────────
    // 순서: [NORMAL, RARE, EPIC, LEGENDARY], 합계 100
    static readonly Dictionary<EquipBoxType, float[]> DefaultWeights = new()
    {
        { EquipBoxType.Normal, new[] { 70f, 30f,  0f,  0f } },
        { EquipBoxType.Rare,   new[] { 40f, 50f, 10f,  0f } },
        { EquipBoxType.Epic,   new[] {  0f, 45f, 50f,  5f } },
    };

    // ── 필중(천장) 기본값 ─────────────────────────────────────────
    // [에픽 필중횟수, 전설 필중횟수] — 0이면 필중 없음
    static readonly int[][] DefaultPity =
    {
        new[] {  0,  0 },  // Normal
        new[] {  0,  0 },  // Rare
        new[] {  8, 48 },  // Epic: 8회→에픽 보장, 48회→전설 보장
    };

    static readonly string[] GradeKeys = { "NORMAL", "RARE", "EPIC", "LEGENDARY" };

    // ── 필중 카운터 (세션 유지, 상자 타입별 독립) ──────────────────
    // [타입인덱스][0=에픽미달횟수, 1=전설미달횟수]
    private static readonly int[][] _pityCount =
    {
        new[] { 0, 0 },  // Normal
        new[] { 0, 0 },  // Rare
        new[] { 0, 0 },  // Epic
    };

    // ── 공개 API ────────────────────────────────────────────────────

    public static EquipmentData Draw(EquipBoxType boxType)
    {
        string grade = RollGrade(boxType);
        UpdatePity(boxType, grade);
        var result = DrawByGrade(grade);
        Debug.Log($"[Gacha] {boxType} → {grade} | 에픽카운터:{_pityCount[(int)boxType][0]} 전설카운터:{_pityCount[(int)boxType][1]}");
        return result;
    }

    public static List<EquipmentData> DrawMulti(int count, EquipBoxType boxType)
    {
        var results = new List<EquipmentData>(count);
        for (int i = 0; i < count; i++)
            results.Add(Draw(boxType));
        return results;
    }

    /// <summary>필중 카운터를 초기화합니다 (디버그/치트 용도).</summary>
    public static void ResetPity(EquipBoxType boxType)
    {
        var c = _pityCount[(int)boxType];
        c[0] = 0; c[1] = 0;
    }

    // ── 내부 ────────────────────────────────────────────────────────

    static string RollGrade(EquipBoxType boxType)
    {
        int   idx  = (int)boxType;
        int[] pity = GetPity(boxType);

        // 전설 필중 체크
        if (pity[1] > 0 && _pityCount[idx][1] >= pity[1])
            return "LEGENDARY";

        // 에픽 필중 체크
        if (pity[0] > 0 && _pityCount[idx][0] >= pity[0])
            return RollEpicOrBetter(boxType);

        // 일반 확률 롤
        float[] w    = GetWeights(boxType);
        float   roll = Random.value * 100f;
        float   cum  = 0f;
        for (int i = 0; i < GradeKeys.Length; i++)
        {
            cum += w[i];
            if (roll < cum) return GradeKeys[i];
        }
        return GradeKeys[GradeKeys.Length - 1];
    }

    /// <summary>에픽 필중 시: 에픽·전설 가중치만으로 재롤.</summary>
    static string RollEpicOrBetter(EquipBoxType boxType)
    {
        float[] w     = GetWeights(boxType);
        float   epicW = w[2];
        float   legW  = w[3];
        float   total = epicW + legW;
        if (total <= 0f) return "EPIC";
        return Random.value * total < epicW ? "EPIC" : "LEGENDARY";
    }

    static void UpdatePity(EquipBoxType boxType, string grade)
    {
        int  idx           = (int)boxType;
        bool isEpicOrBetter = grade == "EPIC" || grade == "LEGENDARY";
        bool isLegendary    = grade == "LEGENDARY";

        _pityCount[idx][0] = isEpicOrBetter ? 0 : _pityCount[idx][0] + 1;
        _pityCount[idx][1] = isLegendary    ? 0 : _pityCount[idx][1] + 1;
    }

    static float[] GetWeights(EquipBoxType boxType)
    {
        try
        {
            string key  = BoxTypeToDbKey(boxType);
            var    rows = DB_확률.FindEntities(e => e.f_가챠타입 == key);
            if (rows != null && rows.Count > 0)
            {
                var r = rows[0];
                return new[] { (float)r.f_G일반, (float)r.f_G레어, (float)r.f_G에픽, (float)r.f_G전설 };
            }
        }
        catch { }

        return DefaultWeights.TryGetValue(boxType, out var w) ? w : DefaultWeights[EquipBoxType.Normal];
    }

    static int[] GetPity(EquipBoxType boxType)
    {
        try
        {
            string key  = BoxTypeToDbKey(boxType);
            var    rows = DB_확률.FindEntities(e => e.f_가챠타입 == key);
            if (rows != null && rows.Count > 0)
            {
                var list = rows[0].f_필중횟수;  // List<int>
                if (list != null && list.Count >= 2)
                    return new[] { list[0], list[1] };
            }
        }
        catch { }

        int idx = (int)boxType;
        return idx < DefaultPity.Length ? DefaultPity[idx] : new[] { 0, 0 };
    }

    static string BoxTypeToDbKey(EquipBoxType boxType) => boxType switch
    {
        EquipBoxType.Normal => "EQ_NORMAL",
        EquipBoxType.Rare   => "EQ_RARE",
        EquipBoxType.Epic   => "EQ_EPIC",
        _                   => "EQ_NORMAL",
    };

    static EquipmentData DrawByGrade(string grade)
    {
        var candidates = DB_장비.FindEntities(e => e.f_등급 == grade);
        if (candidates == null || candidates.Count == 0)
        {
            Debug.LogWarning($"[EquipmentGacha] DB_장비에 '{grade}' 등급 아이템 없음");
            return null;
        }
        return EquipmentData.FromDB(candidates[Random.Range(0, candidates.Count)]);
    }
}
