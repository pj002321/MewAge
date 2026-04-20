/// <summary>
/// 엘리트 처치 보상 1개 항목. EliteRewardTableSO 배열 요소.
/// buffPct: 양수 % 버프 (예: 30 = +30%)
/// debuffPct: 양수로 저장, 적용 시 음수 % (예: 20 → -20%)
/// </summary>
[System.Serializable]
public class EliteRewardEntry
{
    public string   displayName;
    public StatType buffStat;
    [UnityEngine.Tooltip("+% value, e.g. 30 = +30%")]
    public float    buffPct;
    public StatType debuffStat;
    [UnityEngine.Tooltip("positive number, applied as negative pct, e.g. 20 = -20%")]
    public float    debuffPct;
}
