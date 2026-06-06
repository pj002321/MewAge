using UnityEngine;

// 6개 장착 슬롯이 전부 채워지면 최고 무기를 한 단계 올리고 나머지를 판매한다.
public class AugmentWildFusion : AugmentEffectBase
{
    private bool _triggered;

    public override void OnGain()
    {
        var inv = PlayerInventory.Shared;
        if (inv != null) inv.OnChanged += OnInventoryChanged;
    }

    public override void OnRemove()
    {
        var inv = PlayerInventory.Shared;
        if (inv != null) inv.OnChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged()
    {
        if (_triggered) return;

        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        if (inv.Equipped.Count < PlayerInventory.MaxEquipped) return;
        for (int i = 0; i < inv.Equipped.Count; i++)
            if (inv.Equipped[i] == null) return;

        _triggered = true;
        inv.OnChanged -= OnInventoryChanged;

        WeaponData best = null;
        foreach (var w in inv.Equipped)
        {
            if (best == null || (int)w.grade > (int)best.grade ||
                ((int)w.grade == (int)best.grade && w.star > best.star))
                best = w;
        }

        var result = best.UpgradedTo(Mathf.Min(3, best.star + 1));

        for (int i = inv.Equipped.Count - 1; i >= 0; i--)
            if (inv.Equipped[i] != null) inv.SellFromEquipped(i);

        inv.ReplaceEquipped(0, result);
    }
}
