using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EquipSortMode { None, ByGrade, ByCivilization }

/// <summary>
/// 장비 아이템 인벤토리 싱글톤.
/// 장착 슬롯 6개 — 카테고리별 고정 슬롯:
///   0=RING, 1=NECK, 2=FOOT, 3=TOP, 4=CLOAK, 5=HEAD
/// 보관함 무제한.
/// DontDestroy=true: 홈→게임 씬 유지.
/// 장착/해제 시 PlayerStatManager에 플랫 스탯 자동 적용.
/// </summary>
public class EquipmentInventory : MonoBehaviourSingleton<EquipmentInventory>
{
    public const int MaxEquipped = 6;

    // 인덱스 = (int)EquipCategory  (RING=0 … HEAD=5)
    private readonly EquipmentData[] equipped = new EquipmentData[MaxEquipped];
    private readonly List<EquipmentData> bag  = new();

    public System.Action OnChanged;

    protected override bool DontDestroy => true;

    public IReadOnlyList<EquipmentData> Equipped => equipped;
    public IReadOnlyList<EquipmentData> Bag      => bag;

    // ── 카테고리 → 슬롯 인덱스 ────────────────────────────────────────
    public static int CategoryToSlot(EquipCategory cat) => (int)cat;

    // ── 보관함에 추가 ────────────────────────────────────────────────
    public bool TryAdd(EquipmentData data)
    {
        if (data == null) return false;
        bag.Add(data);
        OnChanged?.Invoke();
        return true;
    }

    // ── 보관함 → 고정 슬롯 장착 ──────────────────────────────────────
    /// <summary>
    /// 카테고리에 맞는 슬롯에 장착합니다.
    /// 슬롯에 이미 아이템이 있으면 기존 아이템이 보관함으로 이동합니다.
    /// </summary>
    public void EquipFromBag(EquipmentData data)
    {
        int bagIndex = bag.IndexOf(data);
        if (bagIndex < 0) return;

        int slot      = CategoryToSlot(data.category);
        var displaced = equipped[slot];   // 기존 착용 아이템 (없으면 null)

        equipped[slot] = data;
        bag.RemoveAt(bagIndex);

        if (displaced != null)
            bag.Add(displaced);

        RecalculateEquipStats();
        OnChanged?.Invoke();
    }

    // ── 장착 해제 → 보관함 ───────────────────────────────────────────
    public void UnequipToBag(EquipmentData data)
    {
        int slot = CategoryToSlot(data.category);
        if (equipped[slot] != data) return;

        bag.Add(equipped[slot]);
        equipped[slot] = null;

        RecalculateEquipStats();
        OnChanged?.Invoke();
    }

    // ── 보관함에서 버리기 ────────────────────────────────────────────
    public void RemoveFromBag(EquipmentData data)
    {
        if (bag.Remove(data))
            OnChanged?.Invoke();
    }

    // ── 정렬된 보관함 반환 ───────────────────────────────────────────
    public List<EquipmentData> GetSortedBag(EquipSortMode mode)
    {
        var sorted = new List<EquipmentData>(bag);
        switch (mode)
        {
            case EquipSortMode.ByGrade:
                sorted.Sort((a, b) => b.grade.CompareTo(a.grade));
                break;
            case EquipSortMode.ByCivilization:
                sorted.Sort((a, b) => a.civilization.CompareTo(b.civilization));
                break;
        }
        return sorted;
    }

    // ── 장착 스탯 → PlayerStatManager 적용 ────────────────────────────
    public void RecalculateEquipStats()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;

        sm.ResetEquipFlat();

        for (int i = 0; i < MaxEquipped; i++)
        {
            var e = equipped[i];
            if (e == null || string.IsNullOrEmpty(e.statId)) continue;
            var statType = EquipmentData.StatIdToType(e.statId);
            if (statType.HasValue)
                sm.SetEquipFlat(statType.Value,
                    sm.GetEquipFlat(statType.Value) + e.statValue);
        }
    }
}
