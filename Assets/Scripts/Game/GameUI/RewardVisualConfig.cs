using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Mewage/Reward Visual Config", fileName = "RewardVisualConfig")]
public class RewardVisualConfig : ScriptableObject
{
    public enum RewardType { Gold, Exp, Ruby, CharScroll, EquipScroll, RelicScroll }

    [Serializable]
    public struct Entry
    {
        public RewardType type;
        public Sprite     icon;
    }

    public Entry[] entries;

    public Sprite GetIcon(RewardType type)
    {
        foreach (var e in entries)
            if (e.type == type) return e.icon;
        return null;
    }

    private static RewardVisualConfig _shared;
    public static RewardVisualConfig Shared
        => _shared ??= Resources.Load<RewardVisualConfig>("Reward/RewardVisualConfig");
}
