using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

public enum WareBoxType
{
    Normal,
    Epic,
}

/// <summary>
/// 기물 캡슐 뽑기.
///
/// 공통 풀: 챕터 1 ~ 현재 챕터까지 해금된 모든 기물
/// 레어 뽑기 한정: 5% 확률로 다음 챕터 기물
///
/// DB_확률 f_가챠타입: SKL_NORMAL, SKL_EPIC
/// DB_상점 f_상품ID : SKL_NORMAL, SKL_EPIC
/// </summary>
public static class StructureGacha
{
    static string ToKey(WareBoxType t) => t switch
    {
        WareBoxType.Epic => "SKL_EPIC",
        _                => "SKL_NORMAL",
    };

    const int FallbackCostNormal = 300;
    const int FallbackCostEpic   = 800;
    const float NextChapterChance = 0.05f;   // 레어 뽑기 전용

    // ── 현재 챕터 ────────────────────────────────────────────────────

    public static int CurrentChapter => PlayerPrefs.GetInt("SelectedChapter", 1);

    public static void SetChapter(int chapter)
    {
        PlayerPrefs.SetInt("SelectedChapter", Mathf.Max(1, chapter));
        PlayerPrefs.Save();
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    public static DB_기물 Draw(WareBoxType boxType)
    {
        if (!SpendHomeGold(GetCost(boxType))) return null;

        var result = RollOne(boxType);
        if (result != null) StructureWareManager.Shared?.AddWare(result.f_기물ID, 1);
        return result;
    }

    public static List<DB_기물> DrawMulti(WareBoxType boxType, int count)
    {
        if (!SpendHomeGold(GetCost(boxType) * count)) return null;

        var results = new List<DB_기물>(count);
        for (int i = 0; i < count; i++)
        {
            var r = RollOne(boxType);
            if (r != null) { results.Add(r); StructureWareManager.Shared?.AddWare(r.f_기물ID, 1); }
        }
        return results;
    }

    /// <summary>홈씬 골드 차감. HomeGoldManager 미구현 시 항상 성공.</summary>
    static bool SpendHomeGold(int cost)
    {
        // TODO: 홈씬 골드 시스템 구현 후 연결
        return true;
    }

    public static int GetCost(WareBoxType boxType)
    {
        string key = ToKey(boxType);
        var row = DB_상점.FindEntity(r => r.f_상품ID == key);
        if (row != null) return row.f_가격;
        return boxType == WareBoxType.Epic ? FallbackCostEpic : FallbackCostNormal;
    }

    // ── 내부 로직 ─────────────────────────────────────────────────────

    static DB_기물 RollOne(WareBoxType boxType)
    {
        bool isEpic = boxType == WareBoxType.Epic;
        string gachaKey = ToKey(boxType);

        // 등급 결정
        int wNormal = 70, wRare = 30, wEpic = 0;
        if (isEpic) { wNormal = 0; wRare = 70; wEpic = 30; }

        var prob = DB_확률.FindEntity(r => r.f_가챠타입 == gachaKey);
        if (prob != null) { wNormal = prob.f_G일반; wRare = prob.f_G레어; wEpic = prob.f_G에픽; }

        string grade = RollGrade(wNormal, wRare, wEpic);

        // 챕터 풀 결정
        int cur = CurrentChapter;
        int poolChapter = cur;

        // 레어 뽑기: 5% 확률로 다음 챕터
        if (isEpic && Random.value < NextChapterChance)
            poolChapter = cur + 1;

        // 현재 챕터 + 이전 챕터 (이전이 없으면 현재만)
        int minChapter = Mathf.Max(1, cur - 1);
        var pool = BuildPool(minChapter, poolChapter, grade);

        // 해당 등급 없으면 등급 제한 해제
        if (pool.Count == 0)
            pool = BuildPool(minChapter, poolChapter, null);

        if (pool.Count == 0)
        {
            Debug.LogWarning($"[StructureGacha] 뽑을 기물이 없습니다. (챕터 1~{poolChapter})");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>챕터 minChapter ~ maxChapter, 지정 등급(null=전체) 기물 목록</summary>
    static List<DB_기물> BuildPool(int minChapter, int maxChapter, string grade)
    {
        return DB_기물.FindEntities(r =>
        {
            string era = StructureWareManager.EraOf(r.f_기물ID);
            if (!era.StartsWith("C")) return false;
            if (!int.TryParse(era.Substring(1), out int c)) return false;
            if (c < minChapter || c > maxChapter) return false;
            if (grade != null && r.f_등급 != grade) return false;
            return true;
        });
    }

    static string RollGrade(int wNormal, int wRare, int wEpic)
    {
        int total = wNormal + wRare + wEpic;
        if (total <= 0) return "NORMAL";
        int roll = Random.Range(0, total);
        if (roll < wEpic)         return "EPIC";
        if (roll < wEpic + wRare) return "RARE";
        return "NORMAL";
    }
}
