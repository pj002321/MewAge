using UnityEngine;

public enum ImprovGrade
{
    Common   = 0, // 일반 - 회색
    Uncommon = 1, // 고급 - 녹색
    Rare     = 2, // 희귀 - 파랑
    Epic     = 3, // 에픽 - 보라
    Legend   = 4, // 전설 - 금색
}

public static class ImprovGradeColors
{
    public static Color Get(ImprovGrade grade) => grade switch
    {
        ImprovGrade.Common   => new Color(0.75f, 0.75f, 0.75f), // 회색
        ImprovGrade.Uncommon => new Color(0.3f,  0.85f, 0.3f),  // 녹색
        ImprovGrade.Rare     => new Color(0.3f,  0.6f,  1.0f),  // 파랑
        ImprovGrade.Epic     => new Color(0.7f,  0.2f,  1.0f),  // 보라
        ImprovGrade.Legend   => new Color(1.0f,  0.85f, 0.1f),  // 금색
        _                    => Color.white,
    };

    /// <summary>등급별 기본 가중치 (테이블 기본값 참고용)</summary>
    public static int DefaultWeight(ImprovGrade grade) => grade switch
    {
        ImprovGrade.Common   => 100,
        ImprovGrade.Uncommon => 60,
        ImprovGrade.Rare     => 30,
        ImprovGrade.Epic     => 12,
        ImprovGrade.Legend   => 4,
        _                    => 10,
    };
}
