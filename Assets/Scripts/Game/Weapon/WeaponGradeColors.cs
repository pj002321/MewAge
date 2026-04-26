using UnityEngine;

/// <summary>성 단계(star 1~5) 또는 희귀도(WeaponGrade)에 따른 프레임 색상.</summary>
public static class WeaponGradeColors
{
    /// <summary>인벤토리/장착 슬롯 — 합성 성 단계 기준</summary>
    /// 
    private static Color gray = new Color(0.75f, 0.75f, 0.75f);
    private static Color blue => new Color(0.3f, 0.6f, 1f);    // 2성 — 파랑  (Rare)
    private static Color pupple => new Color(0.7f, 0.2f, 1f);    // 3성 — 보라  (Epic)
    private static Color yellow => new Color(1f, 0.9f, 0.1f);  // 4성 — 노랑  (Legendary)
    private static Color red => new Color(1f, 0.2f, 0.2f);  // 5성 — 빨강  (Mythic)

    /// <summary>등급 한국어 표시명.</summary>
    public static string ToKorean(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Common    => "일반",
        WeaponGrade.Rare      => "고급",
        WeaponGrade.Epic      => "희귀",
        WeaponGrade.Legendary => "전설",
        WeaponGrade.Mythic    => "신화",
        _                     => grade.ToString(),
    };

    /// <summary>상점 카드 — 희귀도 등급 기준</summary>
    public static Color Get(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Common => gray, // 일반 — 회색
        WeaponGrade.Rare => blue,   // 고급 — 파랑
        WeaponGrade.Epic => pupple,   // 희귀 — 보라
        WeaponGrade.Legendary => yellow,   // 전설 — 노랑
        WeaponGrade.Mythic => red,   // 신화 — 빨강
        _ => Color.white,
    };
}
