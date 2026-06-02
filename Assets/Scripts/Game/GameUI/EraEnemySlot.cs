using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시대별 적 정보 슬롯 1개.
/// StageInfoPopup이 런타임에 생성하고 Bind()로 데이터를 채웁니다.
/// </summary>
public class EraEnemySlot : MonoBehaviour
{
    public Image           iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI typeText;

    public void Bind(Sprite icon, string displayName, string typeId)
    {
        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
            if (icon != null) iconImage.SetNativeSize();
        }
        if (nameText != null) nameText.text = displayName;
        if (typeText != null) typeText.text = TypeIdToKorean(typeId);
    }

    static string TypeIdToKorean(string typeId) => typeId?.ToUpper() switch
    {
        "MELEE"      => "근접",
        "RANGED"     => "원거리",
        "CHARGE"     => "돌진",
        "MAGIC"      => "주술",
        "HEALER"     => "힐러",
        "TANK"       => "방어",
        "SLAM"       => "충격",
        "HORDE"      => "군집",
        "SWARM"      => "군집",
        "DASH_COMBO" => "대시콤보",
        "TELEPORT"   => "순간이동",
        _            => typeId ?? "",
    };
}
