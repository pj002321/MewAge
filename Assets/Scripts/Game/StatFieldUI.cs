using TMPro;
using UnityEngine;

/// <summary>
/// stat_field GameObject에 붙이는 스크립트.
/// · Inspector에서 statType과 valueText를 지정합니다.
/// · stat_icon / stat_info_text는 에디터에서 직접 설정한 값을 그대로 사용합니다.
/// </summary>
public class StatFieldUI : MonoBehaviour
{
    [SerializeField] public StatType statType;
    [SerializeField] private TextMeshProUGUI valueText;

    /// <summary>PlayerStatManager 값을 읽어 valueText를 갱신합니다.</summary>
    public void Refresh(PlayerStatManager sm)
    {
        if (sm == null || valueText == null) return;

        float pct      = sm.GetPctMod(statType);
        float finalVal = sm.GetFinal(statType);

        // 증강(%) 있으면 "최종값 (+pct%)" 형태로 표시
        if (!Mathf.Approximately(pct, 0f))
        {
            string sign = pct > 0f ? "+" : "";
            valueText.text = $"{PlayerStatManager.FormatValue(statType, finalVal)} <color=#88FF88>({sign}{pct:0.#}%)</color>";
        }
        else
        {
            valueText.text = PlayerStatManager.FormatValue(statType, finalVal);
        }
    }
}

// ── Unity 구현 가이드 ───────────────────────────────────────────────
// 1. 각 stat_field에 Add Component → StatFieldUI
// 2. Inspector에서 Stat Type 선택 (ATK, DEF, MaxHP …)
// 3. 끝. stat_value 텍스트는 자동으로 찾아서 업데이트합니다.
