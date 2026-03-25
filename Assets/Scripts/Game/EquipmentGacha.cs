using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>장비 상자 종류.</summary>
public enum EquipBoxType
{
    /// <summary>일반 장비 상자 — 노말 80% / 레어 20%</summary>
    Normal,
    /// <summary>레어 장비 상자 — 에픽 80% / S급 20%</summary>
    Rare,
}

/// <summary>
/// 장비 뽑기 로직.
/// DB_장비.f_등급 값: "노말" / "레어" / "에픽" / "S급"
/// </summary>
public static class EquipmentGacha
{
    // ── 뽑기 ───────────────────────────────────────────────────────
    public static EquipmentData Draw(EquipBoxType boxType)
    {
        string grade = RollGrade(boxType);
        return DrawByGrade(grade);
    }

    // ── 등급 확률 ───────────────────────────────────────────────────
    static string RollGrade(EquipBoxType boxType) => boxType switch
    {
        EquipBoxType.Normal => Random.value < 0.80f ? "노말" : "레어",
        EquipBoxType.Rare   => Random.value < 0.80f ? "에픽" : "S급",
        _                   => "노말",
    };

    // ── DB에서 해당 등급 아이템 랜덤 선택 ────────────────────────────
    static EquipmentData DrawByGrade(string grade)
    {
        var candidates = DB_장비.FindEntities(e => e.f_등급 == grade);

        if (candidates == null || candidates.Count == 0)
        {
            // 해당 등급 데이터 없으면 전체에서 랜덤
            candidates = DB_장비.FindEntities(_ => true);
        }

        if (candidates == null || candidates.Count == 0)
        {
            Debug.LogWarning($"[EquipmentGacha] DB_장비 데이터 없음 (등급: {grade})");
            return null;
        }

        var row = candidates[Random.Range(0, candidates.Count)];
        return EquipmentData.FromDB(row);
    }

    // ── 등급 색상 ───────────────────────────────────────────────────
    public static UnityEngine.Color GradeColor(string grade) => grade switch
    {
        "S급"  => new UnityEngine.Color(0.90f, 0.70f, 0.10f, 1f), // 금
        "에픽" => new UnityEngine.Color(0.55f, 0.20f, 0.85f, 1f), // 보라
        "레어" => new UnityEngine.Color(0.15f, 0.45f, 0.90f, 1f), // 파랑
        "노말" => new UnityEngine.Color(0.20f, 0.55f, 0.25f, 1f), // 초록
        _      => new UnityEngine.Color(0.60f, 0.60f, 0.60f, 1f), // 회색
    };
}
