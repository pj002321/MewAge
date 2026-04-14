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

    // ── 가산 스탯: pct가 직접 더해지므로 base=0이어도 표시 의미 있음
    private static bool IsAdditiveStat(StatType t) =>
        t == StatType.CritChance || t == StatType.Evasion || t == StatType.HealOnKill;

    /// <summary>PlayerStatManager 값을 읽어 valueText를 갱신합니다. 무기 기여치도 합산합니다.</summary>
    public void Refresh(PlayerStatManager sm)
    {
        if (sm == null || valueText == null) return;

        float pct      = sm.GetPctMod(statType);
        float finalVal = sm.GetFinal(statType);
        float baseVal  = sm.GetBase(statType) + sm.GetEquipFlat(statType);

        // 무기 기여치 합산 (ATK, CritChance, BleedPower, HealOnKill)
        float weaponBonus = WeaponStatHelper.GetAverageBonus(statType);
        finalVal += weaponBonus;
        baseVal  += weaponBonus;

        // 곱산 스탯에서 base=0이면 pct가 결과에 영향을 주지 않음 → 표기 생략
        bool showPct = !Mathf.Approximately(pct, 0f)
                    && (IsAdditiveStat(statType) || !Mathf.Approximately(baseVal, 0f));

        if (showPct)
        {
            string sign     = pct > 0f ? "+" : "";
            string pctColor = pct < 0f ? "#FF8888" : "#88FF88";
            valueText.text = $"{PlayerStatManager.FormatValue(statType, finalVal)} " +
                             $"<color={pctColor}>({sign}{pct:0.#}%)</color>";
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
            float baseVal     = sm.GetBase(statType) + sm.GetEquipFlat(statType);
            float pct         = sm.GetPctMod(statType);
            float weaponBonus = WeaponStatHelper.GetAverageBonus(statType);

            // 곱산 스탯에서 base=0(무기 기여 포함)이면 미리보기도 의미 없음 → 일반 표시로 폴백
            if (!IsAdditiveStat(statType) && Mathf.Approximately(baseVal + weaponBonus, 0f))
            {
                Refresh(sm);
                return;
            }

            float previewFinal = IsAdditiveStat(statType)
                ? baseVal + pct + previewPct + weaponBonus
                : baseVal * (1f + (pct + previewPct) / 100f) + weaponBonus;

            string previewSign = previewPct > 0f ? "+" : "";
            valueText.text = $"<color=#88CCFF>{PlayerStatManager.FormatValue(statType, previewFinal)}</color> " +
                             $"<color=#88CCFF>({previewSign}{previewPct:0.#}%)</color>";
        }
        else
        {
            Refresh(sm);
        }
    }
}
