using UnityEngine;

/// <summary>
/// 뽑기권(티켓) 보유 수량 관리. PlayerPrefs 기반 영구 저장.
/// 씬 인스턴스 불필요 — 모든 메서드가 static.
/// </summary>
public static class TicketManager
{
    public static event System.Action OnChanged;

    private static string Key(RewardType type) => $"Ticket_{type}_Count";

    public static int GetCount(RewardType type) =>
        PlayerPrefs.GetInt(Key(type), 0);

    public static void Add(RewardType type, int count)
    {
        if (count <= 0) return;
        PlayerPrefs.SetInt(Key(type), GetCount(type) + count);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static bool TrySpend(RewardType type)
    {
        int cur = GetCount(type);
        if (cur <= 0) return false;
        PlayerPrefs.SetInt(Key(type), cur - 1);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }
}
