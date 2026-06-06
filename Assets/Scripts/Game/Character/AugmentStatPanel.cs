using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AugmentStatPanel : MonoBehaviour
{
    [Header("패널 구성 요소")]
    public Image            panelImg;
    public TextMeshProUGUI  statText;

    public void Refresh(string statId, string value, bool unlocked, string grade)
    {
        gameObject.SetActive(true);

        var config = MemberVisualConfig.Shared;

        if (panelImg != null)
        {
            var spr = config?.GetPanelSprite(grade, unlocked);
            if (spr != null) panelImg.sprite = spr;
            panelImg.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        if (statText != null)
            statText.text = BuildStatLine(statId, value);
    }

    static string BuildStatLine(string statId, string value)
    {
        if (string.IsNullOrEmpty(statId)) return string.Empty;
        string label = StatIdToLabel(statId);
        if (string.IsNullOrEmpty(value)) return label;
        return $"{label} +{value}%";
    }

    static string StatIdToLabel(string statId) => statId switch
    {
        "STAT_ATK"        => "공격력",
        "STAT_DEF"        => "방어력",
        "STAT_HP"         => "최대체력",
        "STAT_SPD_ATK"    => "공격속도",
        "STAT_SPD_MOV"    => "이동속도",
        "STAT_CRI_RATE"   => "치명타 확률",
        "STAT_CRI_DMG"    => "치명타 데미지",
        "STAT_EVA_RATE"   => "회피 확률",
        "STAT_ATK_RNG"    => "공격사거리",
        "STAT_CUR_REWARD" => "추가 골드",
        "STAT_CUR_EXP"    => "추가 경험치",
        "STAT_STUN"       => "적 둔화",
        "STAT_DOT_DMG"    => "출혈 데미지",
        "STAT_DOT"        => "지속피해",
        _                 => statId,
    };
}
