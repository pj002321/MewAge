using UnityEngine;

/// <summary>
/// 엘리트 처치 보상 테이블. Assets/Data/ 에 생성.
/// MuAge/Elite Reward Table 메뉴로 생성합니다.
/// </summary>
[CreateAssetMenu(fileName = "EliteRewardTable", menuName = "MuAge/Elite Reward Table")]
public class EliteRewardTableSO : ScriptableObject
{
    [SerializeField] private EliteRewardEntry[] entries;

    public EliteRewardEntry GetRandom()
    {
        if (entries == null || entries.Length == 0) return null;
        return entries[Random.Range(0, entries.Length)];
    }
}
