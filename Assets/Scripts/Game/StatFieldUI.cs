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

        if (!Mathf.Approximately(pct, 0f))
        {
            string sign     = pct > 0f ? "+" : "";
            string pctColor = pct < 0f ? "#FF8888" : "#88FF88";
            valueText.text = $"{PlayerStatManager.FormatValue(statType, finalVal)} <color={pctColor}>({sign}{pct:0.#}%)</color>";
        }
        else
        {
            valueText.text = PlayerStatManager.FormatValue(statType, finalVal);
        }
    }

    /// <summary>증강 미리보기 — 해당 스탯이면 하늘색으로 예상 수치를 표시합니다.</summary>
    public void RefreshWithPreview(PlayerStatManager sm, StatType previewStat, float previewPct)
    {
        if (sm == null || valueText == null) return;

        if (statType == previewStat && !Mathf.Approximately(previewPct, 0f))
        {
            float b            = sm.GetBase(statType) + sm.GetEquipFlat(statType);
            float pct          = sm.GetPctMod(statType);

            float previewFinal = (statType == StatType.CritChance || statType == StatType.Evasion || statType == StatType.HealOnKill)
                ? b + pct + previewPct
                : b * (1f + (pct + previewPct) / 100f);

            string previewSign = previewPct > 0f ? "+" : "";
            valueText.text = $"<color=#88CCFF>{PlayerStatManager.FormatValue(statType, previewFinal)}</color> <color=#88CCFF>({previewSign}{previewPct:0.#}%)</color>";
        }
        else
        {
            Refresh(sm);
        }
    }
}

// ── Unity 구현 가이드 ───────────────────────────────────────────────
// 1. 각 stat_field에 Add Component → StatFieldUI
// 2. Inspector에서 Stat Type 선택 (ATK, DEF, MaxHP …)
// 3. 끝. stat_value 텍스트는 자동으로 찾아서 업데이트합니다.
