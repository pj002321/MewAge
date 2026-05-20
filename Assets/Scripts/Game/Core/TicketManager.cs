using UnityEngine;

/// <summary>
/// 뽑기권(티켓) 보유 수량 관리. PlayerPrefs 기반 영구 저장.
/// 키 형식: "Ticket_{RewardType}_Count"
/// </summary>
public class TicketManager : MonoBehaviourSingleton<TicketManager>
{
    protected override bool DontDestroy => false;

    private static string Key(RewardType type) => $"Ticket_{type}_Count";

    public int GetCount(RewardType type) =>
        PlayerPrefs.GetInt(Key(type), 0);

    public void Add(RewardType type, int count)
    {
        if (count <= 0) return;
        int cur = GetCount(type);
        PlayerPrefs.SetInt(Key(type), cur + count);
        PlayerPrefs.Save();
    }

    /// <summary>티켓 1장 소모. 보유 중이면 true, 없으면 false.</summary>
    public bool TrySpend(RewardType type)
    {
        int cur = GetCount(type);
        if (cur <= 0) return false;
        PlayerPrefs.SetInt(Key(type), cur - 1);
        PlayerPrefs.Save();
        return true;
    }
}
