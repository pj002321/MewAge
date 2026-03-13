using UnityEngine;

public static class WeaponGradeColors
{
    public static Color Get(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Normal => new Color(0.75f, 0.75f, 0.75f), // 회색
        WeaponGrade.Rare   => new Color(0.3f,  0.6f,  1f),    // 파랑
        WeaponGrade.Elite  => new Color(1f,    0.25f, 0.25f), // 빨강
        WeaponGrade.Epic   => new Color(0.7f,  0.2f,  1f),    // 보라
        WeaponGrade.Legend => new Color(1f,    0.9f,  0.1f),  // 노랑
        _                  => Color.white,
    };
}
