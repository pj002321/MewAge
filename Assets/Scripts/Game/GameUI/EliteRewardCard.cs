using UnityEngine;
using TMPro;

/// <summary>
/// 엘리트 처치 보상 카드 UI.
/// Inspector 연결: titleText, buffText (녹색), debuffText (빨간색)
/// </summary>
public class EliteRewardCard : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text buffText;
    [SerializeField] private TMP_Text debuffText;

    public void Setup(EliteRewardEntry entry)
    {
        if (entry == null) return;
        if (titleText)  titleText.text  = entry.displayName;
        if (buffText)   buffText.text   = $"{PlayerStatManager.GetStatName(entry.buffStat)}  +{entry.buffPct:0.#}%";
        if (debuffText) debuffText.text = $"{PlayerStatManager.GetStatName(entry.debuffStat)}  -{entry.debuffPct:0.#}%";
    }
}
