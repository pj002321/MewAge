using Coffee.UIExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시대별 무기 정보 팝업의 스크롤 슬롯 1개.
/// EraWeaponInfoPopup이 런타임에 생성하고 Bind()로 데이터를 채웁니다.
/// </summary>
public class EraWeaponSlot : MonoBehaviour
{
    [Header("무기 정보")]
    public Image           iconImage;
    public Image           gradeFrameImage;
    public Image           weaponTypeIcon;
    public TextMeshProUGUI weaponTypeText;
    public TextMeshProUGUI weaponNameText;

    [Header("신화 등급 전용")]
    public GameObject backlightEffect;

    // ── 바인딩 ────────────────────────────────────────────────────────
    public void Bind(WeaponData data)
    {
        if (data == null) return;

        var cfg = WeaponVisualConfig.Shared;

        // 무기 아이콘
        if (iconImage != null)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }

        // 등급 프레임 스프라이트 + 색상
        if (gradeFrameImage != null)
        {
            var gradeSprite = cfg?.GetGradeSprite(data.grade);
            if (gradeSprite != null)
            {
                gradeFrameImage.sprite = gradeSprite;
                gradeFrameImage.color  = Color.white;
            }
            else
            {
                gradeFrameImage.color = WeaponGradeColors.Get(data.grade);
            }

            // gradeFrameImage에 붙은 UIShiny — 신화 등급만 활성화
            var shiny = gradeFrameImage.GetComponent<UIShiny>();
            if (shiny != null) shiny.enabled = data.grade == WeaponGrade.Mythic;
        }

        // 무기 타입 아이콘
        if (weaponTypeIcon != null)
        {
            var typeSprite = cfg?.GetTypeSprite(data.weaponType);
            weaponTypeIcon.sprite  = typeSprite;
            weaponTypeIcon.enabled = typeSprite != null;
        }

        // 무기 타입 텍스트
        if (weaponTypeText != null)
            weaponTypeText.text = WeaponTypeToKorean(data.weaponType);

        // 무기 이름 텍스트
        if (weaponNameText != null)
            weaponNameText.text = data.weaponName;

        // 신화 등급일 때만 백라이트
        if (backlightEffect != null)
            backlightEffect.SetActive(data.grade == WeaponGrade.Mythic);
    }

    static string WeaponTypeToKorean(WeaponType t) => t switch
    {
        WeaponType.Melee     => "근거리",
        WeaponType.Ranged    => "원거리",
        WeaponType.Magic     => "마법",
        WeaponType.Boomerang => "부메랑",
        WeaponType.Spin      => "돌리기",
        WeaponType.Slash     => "슬래시",
        WeaponType.Charge    => "차징",
        WeaponType.Hammer    => "해머",
        _                    => t.ToString(),
    };
}
