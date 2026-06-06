// 첫 구매 무기를 2성으로 업그레이드한다.
public class AugmentFirstHunt : AugmentEffectBase
{
    public override void OnGain()   => UpgradeManager.OnModifyPurchasedWeapon += Upgrade;
    public override void OnRemove() => UpgradeManager.OnModifyPurchasedWeapon -= Upgrade;

    private WeaponData Upgrade(WeaponData weapon)
    {
        UpgradeManager.OnModifyPurchasedWeapon -= Upgrade;
        return weapon.UpgradedTo(2);
    }
}
