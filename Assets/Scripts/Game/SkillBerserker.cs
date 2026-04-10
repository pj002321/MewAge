using UnityEngine;

/// <summary>
/// 버서커 스킬 — 발동 시 공격속도를 대폭 증가시킵니다.
/// 프리팹: Resources/Skills/skl_berserker
/// Inspector: duration=6, pointsRequired=10, atkSpeedBonus=50 (%)
/// </summary>
public class SkillBerserker : CharacterSkillBase
{
    [Header("버서커 설정")]
    [Tooltip("스킬 발동 중 추가 공격속도 (%)")]
    [SerializeField] private float atkSpeedBonus = 50f;

    public override string SkillId => "SKL_BERSERKER";

    public override void Activate()
    {
        PlayerStatManager.Shared?.AddPctMod(StatType.AtkSpeed, atkSpeedBonus);
        ApplyToWeapons();
        Debug.Log($"[Berserker] 스킬 발동 — 공격속도 +{atkSpeedBonus}%");
    }

    public override void Deactivate()
    {
        PlayerStatManager.Shared?.AddPctMod(StatType.AtkSpeed, -atkSpeedBonus);
        ApplyToWeapons();
        Debug.Log("[Berserker] 스킬 종료");
    }

    private static void ApplyToWeapons()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;
        float pct = sm.GetFinal(StatType.AtkSpeed);
        var weapons = WeaponManager.Instance?.GetActiveWeaponPoints();
        if (weapons == null) return;
        foreach (var wp in weapons)
            wp.SetStatSpeedPct(pct);
    }
}
