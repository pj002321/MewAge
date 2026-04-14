using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 패널에서 해당 시너지를 보유한 무기를 미리보기로 보여주는 슬롯.
/// 프리팹 구조: Image(synergyIcon) / Image(frameImage) / Image(weaponTypeIcon) / Image(weaponIcon)
/// </summary>
public class SynergyRefSlot : MonoBehaviour
{
    [SerializeField] public Image synergyIcon;
    [SerializeField] public Image frameImage;
    [SerializeField] public Image weaponTypeIcon;
    [SerializeField] public Image weaponIcon;

    public void Bind(WeaponData weapon, Sprite synergySprite)
    {
        var cfg = WeaponVisualConfig.Shared;

        if (synergyIcon != null)
        {
            synergyIcon.sprite  = synergySprite;
            synergyIcon.enabled = synergySprite != null;
        }

        if (frameImage != null)
        {
            var gradeSprite = weapon != null ? cfg?.GetGradeSprite(weapon.grade) : null;
            frameImage.sprite  = gradeSprite;
            frameImage.enabled = gradeSprite != null;
        }

        if (weaponTypeIcon != null)
        {
            var typeSprite = weapon != null ? cfg?.GetTypeSprite(weapon.weaponType) : null;
            weaponTypeIcon.sprite  = typeSprite;
            weaponTypeIcon.enabled = typeSprite != null;
        }

        if (weaponIcon != null)
        {
            weaponIcon.sprite  = weapon?.icon;
            weaponIcon.enabled = weapon?.icon != null;
        }
    }
}
