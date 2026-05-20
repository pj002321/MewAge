using System;
using UnityEngine;

/// <summary>
/// DB_상점 f_보상ID 문자열 → 아이콘 매핑 ScriptableObject.
/// Inspector의 entries에 f_보상ID 값(RWD_GOLD 등)을 그대로 입력하면
/// DB와 별도 enum 수정 없이 자동 연동됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Reward Visual Config", fileName = "RewardVisualConfig")]
public class RewardVisualConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string rewardId; // DB f_보상ID 값 (예: RWD_GOLD, RWD_RUBY)
        public Sprite icon;
    }

    public Entry[] entries;

    /// <summary>f_보상ID 문자열로 아이콘 조회.</summary>
    public Sprite GetIcon(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return null;
        foreach (var e in entries)
            if (e.rewardId == rewardId) return e.icon;
        Debug.LogWarning($"[RewardVisualConfig] '{rewardId}' 매핑 없음. 등록된 IDs: {string.Join(", ", System.Array.ConvertAll(entries, e => e.rewardId))}");
        return null;
    }

    /// <summary>RewardType enum으로 조회 (기존 호환 — RewardType → rewardId 변환 후 탐색).</summary>
    public Sprite GetIcon(RewardType type) => GetIcon(TypeToId(type));

    public static string TypeToId(RewardType type) => type switch
    {
        RewardType.Gold           => "COIN",
        RewardType.Ruby           => "RUBY",
        RewardType.Exp            => "EXP",
        RewardType.CharMat        => "MAT_HERO",
        RewardType.RelicScroll    => "MAT_RELIC",
        RewardType.EquipTopMat    => "MAT_EQ_TOP",
        RewardType.EquipHeadMat   => "MAT_EQ_HEAD",
        RewardType.EquipRingMat   => "MAT_EQ_RING",
        RewardType.EquipNeckMat   => "MAT_EQ_NECK",
        RewardType.EquipcloakMat  => "MAT_EQ_CLOAK",
        RewardType.EquipFootMat   => "MAT_EQ_FOOT",
        RewardType.TicketEqNormal => "KEY_NORMAL",
        RewardType.TicketEqRare   => "KEY_SPECIAL",
        RewardType.TicketEqEpic   => "KEY_EPIC",
        RewardType.TicketRfNormal => "KEY_RELIC_NORMAL",
        RewardType.TicketRfEpic   => "KEY_RELIC_EPIC",
        _                         => "",
    };

    private static RewardVisualConfig _shared;
    public static RewardVisualConfig Shared
        => _shared ??= Resources.Load<RewardVisualConfig>("Reward/RewardVisualConfig");
}
