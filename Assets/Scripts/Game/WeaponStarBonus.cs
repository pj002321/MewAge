/// <summary>
/// 특정 성 단계에서 발동하는 단일 강화 보너스.
/// WeaponData.bonus2Star / bonus3Star / bonus4Star 로 보관됩니다.
/// </summary>
[System.Serializable]
public struct WeaponStarBonus
{
    /// <summary>속성 ID (예: "ATK_ADD", "CD_RED"). 빈 문자열이면 해당 성에 보너스 없음.</summary>
    public string statId;

    /// <summary>수치 (정수). 예: ATK_ADD 25 → 공격력 +25%.</summary>
    public int value;

    /// <summary>포맷된 설명 (예: "공격력 +25%"). WeaponStatRegistry에서 자동 생성.</summary>
    public string description;

    public bool IsEmpty => string.IsNullOrEmpty(statId);
}
