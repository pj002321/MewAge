using System.Collections.Generic;

/// <summary>
/// 런 중 보물 상자 오브에서 획득한 보상을 타입별로 누적 추적합니다.
/// 게임 시작 시 Reset(), 오브 수집 시 Track(), 결과 패널에서 GetAll() 호출.
/// </summary>
public static class RunChestRewardTracker
{
    private static readonly Dictionary<RewardType, int> _accumulated = new();

    public static void Reset() => _accumulated.Clear();

    public static void Track(RewardItem item)
    {
        if (_accumulated.ContainsKey(item.type))
            _accumulated[item.type] += item.amount;
        else
            _accumulated[item.type] = item.amount;
    }

    public static bool HasAny() => _accumulated.Count > 0;

    public static IEnumerable<KeyValuePair<RewardType, int>> GetAll() => _accumulated;
}
