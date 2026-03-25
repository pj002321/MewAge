using UnityEngine;

/// <summary>
/// 캐릭터 등급 → 슬롯 배경 색상 변환 헬퍼.
/// DB_캐릭터.f_등급 값("S","A","B","C")을 받아 Color를 반환합니다.
/// </summary>
public static class CharacterGradeColor
{
    public static Color Get(string grade) => grade switch
    {
        "S" => new Color(0.90f, 0.70f, 0.10f, 1f),   // 금
        "A" => new Color(0.55f, 0.20f, 0.85f, 1f),   // 보라
        "B" => new Color(0.15f, 0.45f, 0.90f, 1f),   // 파랑
        "C" => new Color(0.20f, 0.55f, 0.25f, 1f),   // 초록
        _   => new Color(0.25f, 0.25f, 0.30f, 1f),   // 기본(회색)
    };
}
