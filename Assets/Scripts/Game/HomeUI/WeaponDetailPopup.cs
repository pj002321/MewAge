using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponDetailPopup : MonoBehaviour
{
    [SerializeField] Image           iconImage;
    [SerializeField] Image           gradeFrameImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI gradeText;
    [SerializeField] TextMeshProUGUI typeText;
    [SerializeField] Image           typeIconImage;
    [SerializeField] TextMeshProUGUI atkText;
    [SerializeField] TextMeshProUGUI atkSpeedText;
    [SerializeField] TextMeshProUGUI lifeStealText;
    [SerializeField] TextMeshProUGUI knockbackText;
    [SerializeField] TextMeshProUGUI traitText;

    RectTransform _rect;
    ScalablePopup _popup;

    void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _popup = GetComponent<ScalablePopup>();
    }

    public void Show(WeaponData weapon)
    {
        if (weapon == null) return;

        var cfg = WeaponVisualConfig.Shared;

        if (iconImage != null)
        {
            iconImage.sprite  = weapon.icon;
            iconImage.enabled = weapon.icon != null;
            if (weapon.icon != null) iconImage.SetNativeSize();
        }

        if (gradeFrameImage != null)
        {
            var gradeSprite = cfg?.GetGradeSprite(weapon.grade);
            if (gradeSprite != null)
            {
                gradeFrameImage.sprite = gradeSprite;
                gradeFrameImage.color  = Color.white;
            }
            else
            {
                gradeFrameImage.color = WeaponGradeColors.Get(weapon.grade);
            }
        }

        if (nameText)      nameText.text      = weapon.weaponName;
        if (gradeText)     gradeText.text     = GradeName(weapon.grade);
        if (typeText)      typeText.text      = TypeName(weapon.weaponType);

        if (typeIconImage != null)
        {
            var typeSprite = cfg?.GetTypeSprite(weapon.weaponType);
            typeIconImage.sprite  = typeSprite;
            typeIconImage.enabled = typeSprite != null;
            if (typeSprite != null) typeIconImage.SetNativeSize();
        }
        if (atkText)       atkText.text       = $"{weapon.baseAttackDamage:0.##}";
        if (atkSpeedText)  atkSpeedText.text  = $"{weapon.baseAttackCooldown:0.##}";
        if (lifeStealText) lifeStealText.text = $"{weapon.baseLifeSteal:0.##}";
        if (knockbackText) knockbackText.text = $"{weapon.baseKnockbackPower:0.##}";

        if (traitText != null)
        {
            string desc       = weapon.specialEffectDesc;
            traitText.text    = desc;
            traitText.enabled = !string.IsNullOrEmpty(desc);
        }

        _rect.anchoredPosition = Vector2.zero;
        _popup.Show();
    }

    public void Hide()
    {
        _popup?.HideImmediate();
    }

    static string GradeName(WeaponGrade g) => g switch
    {
        WeaponGrade.Common    => "일반",
        WeaponGrade.Rare      => "고급",
        WeaponGrade.Epic      => "희귀",
        WeaponGrade.Legendary => "전설",
        WeaponGrade.Mythic    => "신화",
        _                     => g.ToString(),
    };

    static string TypeName(WeaponType t) => t switch
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
