// 단순 스탯 조정 증강 모음. 로직 없이 OnGain 한 줄짜리만 여기에 둔다.

public class AugmentHpHeal : AugmentEffectBase
{
    public override void OnGain()
    {
        var player = PlayerController.Instance;
        if (player != null)
            player.HealHP(player.MaxHPValue * (Data.f_주수치 / 100f));
    }
}

public class AugmentHpPotion : AugmentEffectBase
{
    public override void OnGain()
    {
        PlayerController.Instance?.IncreaseHealOrbChance(
            chance:     Data.f_주수치  / 100f,
            healFactor: Data.f_보조수치 / 100f);
    }
}

public class AugmentMovDownAtkUp : AugmentEffectBase
{
    public override void OnGain()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;
        sm.AddPctMod(StatType.MoveSpeed, -Data.f_주수치);
        sm.AddPctMod(StatType.AtkSpeed,   Data.f_보조수치);
    }

    public override void OnRemove()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;
        sm.AddPctMod(StatType.MoveSpeed,  Data.f_주수치);
        sm.AddPctMod(StatType.AtkSpeed,  -Data.f_보조수치);
    }
}
