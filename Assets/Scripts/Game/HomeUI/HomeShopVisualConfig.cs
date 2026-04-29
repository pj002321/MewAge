using System;
using UnityEngine;

public enum GachaBoxType
{
    EquipNormal,
    EquipRare,
    EquipEpic,
    RelicNormal,
    RelicEpic,
}

[CreateAssetMenu(menuName = "Mewage/Home Shop Visual Config", fileName = "HomeShopVisualConfig")]
public class HomeShopVisualConfig : ScriptableObject
{
    [Serializable]
    public struct GachaVisualEntry
    {
        public GachaBoxType              boxType;
        public RuntimeAnimatorController animatorController;
        public Sprite                    backgroundImage;
    }

    public GachaVisualEntry[] entries;

    public bool TryGetEntry(GachaBoxType boxType, out GachaVisualEntry entry)
    {
        foreach (var e in entries)
            if (e.boxType == boxType) { entry = e; return true; }
        entry = default;
        return false;
    }

    private static HomeShopVisualConfig _shared;
    public static HomeShopVisualConfig Shared
        => _shared ??= Resources.Load<HomeShopVisualConfig>("HomeShopVisualConfig");
}
