using System.Collections.Generic;

/// <summary>
/// 장비 아이템 인벤토리 싱글톤.
/// 장착 슬롯 6개 + 보관함 (무제한).
/// </summary>
public class EquipmentInventory : MonoBehaviourSingleton<EquipmentInventory>
{
    public const int MaxEquipped = 6;

    private readonly List<EquipmentData> equipped = new();
    private readonly List<EquipmentData> bag      = new();

    public System.Action OnChanged;

    protected override bool DontDestroy => false;

    public IReadOnlyList<EquipmentData> Equipped => equipped;
    public IReadOnlyList<EquipmentData> Bag      => bag;

    // ── 보관함에 추가 ────────────────────────────────────────────────
    public bool TryAdd(EquipmentData data)
    {
        if (data == null) return false;
        bag.Add(data);
        OnChanged?.Invoke();
        return true;
    }

    // ── 보관함 → 장착 슬롯 (빈 슬롯이 없으면 마지막 슬롯과 교체) ──
    public void EquipFromBag(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= bag.Count) return;
        var item = bag[bagIndex];

        int empty = -1;
        for (int i = 0; i < equipped.Count; i++)
            if (equipped[i] == null) { empty = i; break; }
        if (empty < 0 && equipped.Count < MaxEquipped)
            empty = equipped.Count;

        if (empty >= 0)
        {
            if (empty == equipped.Count) equipped.Add(item);
            else equipped[empty] = item;
            bag.RemoveAt(bagIndex);
        }
        else
        {
            var displaced = equipped[equipped.Count - 1];
            equipped[equipped.Count - 1] = item;
            bag[bagIndex] = displaced;
        }
        OnChanged?.Invoke();
    }

    // ── 장착 해제 → 보관함 ──────────────────────────────────────────
    public void UnequipToBag(int equipIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return;
        bag.Add(equipped[equipIndex]);
        equipped.RemoveAt(equipIndex);
        OnChanged?.Invoke();
    }

    // ── 보관함에서 버리기 ────────────────────────────────────────────
    public void RemoveFromBag(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= bag.Count) return;
        bag.RemoveAt(bagIndex);
        OnChanged?.Invoke();
    }
}
