/// <summary>
/// UI 전용 단방향 이벤트 버스.
/// 재화/재료 변경 시 모든 데이터 소스가 BroadcastCurrencyChanged()를 호출합니다.
/// UI 컴포넌트는 OnCurrencyChanged 하나만 구독하면 됩니다.
/// </summary>
public static class UIDataBus
{
    public static event System.Action OnCurrencyChanged;

    public static void BroadcastCurrencyChanged() => OnCurrencyChanged?.Invoke();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnCurrencyChanged = null;
}
