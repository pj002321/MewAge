using UnityEngine;

/// <summary>성 단계(star 1~5) 또는 희귀도(WeaponGrade)에 따른 프레임 색상.</summary>
public static class WeaponGradeColors
{
    /// <summary>인벤토리/장착 슬롯 — 합성 성 단계 기준</summary>
    public static Color Get(int star) => star switch
    {
        1 => new Color(0.75f, 0.75f, 0.75f), // 1성 — 회색
        2 => new Color(0.3f,  0.6f,  1f),    // 2성 — 파랑
        3 => new Color(0.1f,  0.75f, 1f),    // 3성 — 파랑 (엘리트)
        4 => new Color(0.7f,  0.2f,  1f),    // 4성 — 보라
        5 => new Color(1f,    0.9f,  0.1f),  // 5성 — 노랑
        _ => Color.white,
    };

    /// <summary>상점 카드 — 희귀도 등급 기준</summary>
    public static Color Get(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Normal => new Color(0.75f, 0.75f, 0.75f), // 노멀 — 회색
        WeaponGrade.Rare   => new Color(0.3f,  0.6f,  1f),    // 레어  — 파랑
        WeaponGrade.Elite  => new Color(0.1f,  0.75f, 1f),    // 엘리트 — 파랑
        WeaponGrade.Epic   => new Color(0.7f,  0.2f,  1f),    // 에픽  — 보라
        WeaponGrade.Legend => new Color(1f,    0.9f,  0.1f),  // 신화  — 노랑
        _                  => Color.white,
    };
}
