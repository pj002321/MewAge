using UnityEngine;

/// <summary>성 단계(star 1~5) 또는 희귀도(WeaponGrade)에 따른 프레임 색상.</summary>
public static class WeaponGradeColors
{
    /// <summary>인벤토리/장착 슬롯 — 합성 성 단계 기준</summary>
    public static Color Get(int star) => star switch
    {
        1 => new Color(0.75f, 0.75f, 0.75f), // 1성 — 회색  (Common)
        2 => new Color(0.3f,  0.6f,  1f),    // 2성 — 파랑  (Rare)
        3 => new Color(0.7f,  0.2f,  1f),    // 3성 — 보라  (Epic)
        4 => new Color(1f,    0.9f,  0.1f),  // 4성 — 노랑  (Legendary)
        5 => new Color(1f,    0.2f,  0.2f),  // 5성 — 빨강  (Mythic)
        _ => Color.white,
    };

    /// <summary>상점 카드 — 희귀도 등급 기준</summary>
    public static Color Get(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Common    => new Color(0.75f, 0.75f, 0.75f), // 일반 — 회색
        WeaponGrade.Rare      => new Color(0.3f,  0.6f,  1f),    // 고급 — 파랑
        WeaponGrade.Epic      => new Color(0.7f,  0.2f,  1f),    // 희귀 — 보라
        WeaponGrade.Legendary => new Color(1f,    0.9f,  0.1f),  // 전설 — 노랑
        WeaponGrade.Mythic    => new Color(1f,    0.2f,  0.2f),  // 신화 — 빨강
        _                     => Color.white,
    };
}
