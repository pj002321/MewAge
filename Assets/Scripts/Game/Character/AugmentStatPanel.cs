using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 증강 능력치 패널 한 칸.
/// HomeMemberPanel에서 3개를 배열로 꽂아 사용합니다.
///
/// Inspector 연결:
///   panelImg — 패널 배경 Image (등급별 활성/비활성 스프라이트로 교체됨)
///   indexImg — 인덱스 아이콘 Image (등급별 해금/잠금 스프라이트)
///   statText — 속성 설명 TextMeshProUGUI (예: "공격력 +4%")
/// </summary>
public class AugmentStatPanel : MonoBehaviour
{
    [Header("패널 구성 요소")]
    public Image            panelImg;
    public Image            indexImg;
    public TextMeshProUGUI  statText;

    /// <summary>
    /// 표시할 데이터를 설정합니다.
    /// </summary>
    /// <param name="statId">DB 속성 ID (예: "STAT_ATK")</param>
    /// <param name="value">수치 문자열 (예: "4")</param>
    /// <param name="unlocked">true = 해금(활성), false = 잠금(비활성)</param>
    /// <param name="grade">캐릭터 등급 (예: "C", "B", "A", "S", "SS")</param>
    public void Refresh(string statId, string value, bool unlocked, string grade)
    {
        gameObject.SetActive(true);

        var config = MemberVisualConfig.Shared;

        if (panelImg)
        {
            var spr = config?.GetPanelSprite(grade, unlocked);
            if (spr != null)
                panelImg.sprite = spr;
            panelImg.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        if (indexImg)
        {
            var spr = config?.GetIndexSprite(grade, unlocked);
            indexImg.sprite  = spr;
            indexImg.enabled = spr != null;
            indexImg.color   = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        if (statText)
        {
            statText.text  = unlocked ? BuildStatLine(statId, value) : "???";
            statText.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        }
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
        "STAT_SKL_CD"     => "기물 쿨타임",
        "STAT_STUN"       => "적 둔화",
        "STAT_DOT_DMG"    => "출혈 데미지",
        "STAT_DOT"        => "지속피해",
        _                 => statId,
    };
}
