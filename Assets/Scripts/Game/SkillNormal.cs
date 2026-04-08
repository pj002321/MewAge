/// <summary>
/// SKL_NORMAL 스킬 — 발동 시 3초 동안 공격속도 50% 증가.
/// 프리팹: Resources/Skills/skl_normal
/// Inspector: duration=3, pointsRequired=10
/// </summary>
public class SkillNormal : CharacterSkillBase
{
    public override string SkillId => "SKL_NORMAL";

    private const float SpeedMultiplier = 1.5f; // 50% 증가

    public override void Activate()
    {
        foreach (var wp in GetWeaponPoints())
            wp.MultiplyAttackSpeed(SpeedMultiplier);
    }

    public override void Deactivate()
    {
        // 활성화 때 곱한 배율을 역수로 나눠서 원복
        foreach (var wp in GetWeaponPoints())
            wp.MultiplyAttackSpeed(1f / SpeedMultiplier);
    }

    private static System.Collections.Generic.List<WeaponPoint> GetWeaponPoints()
        => WeaponManager.Instance?.GetActiveWeaponPoints()
           ?? new System.Collections.Generic.List<WeaponPoint>();
}
