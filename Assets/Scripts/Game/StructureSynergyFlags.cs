/// <summary>
/// 기물 시너지가 활성화되었을 때 켜지는 플래그 저장소.
/// WareSynergySystem.ApplySynergies() 에서 설정되며,
/// 전투 로직(WeaponPoint, BossController 등)에서 읽습니다.
///
/// 사용 예시 (BossController/WeaponPoint):
///   if (StructureSynergyFlags.CCSynergyActive &amp;&amp; Random.value &lt; 0.15f)
///       bossController.ApplyGroggy(duration);
/// </summary>
public static class StructureSynergyFlags
{
    /// <summary>CC 기물 3개 이상 프리셋 → 공격 시 15% 확률 보스 그로기</summary>
    public static bool  CCSynergyActive   { get; set; }

    /// <summary>둔화 기물 3개 이상 프리셋 → 둔화 효과 배율 추가 (0 = 비활성)</summary>
    public static float SlowSynergyBonus  { get; set; }

    /// <summary>게임 씬 시작 전 반드시 초기화</summary>
    public static void Reset()
    {
        CCSynergyActive  = false;
        SlowSynergyBonus = 0f;
    }
}
