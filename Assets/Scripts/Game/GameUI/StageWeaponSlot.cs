using UnityEngine;
using UnityEngine.UI;

public class StageWeaponSlot : MonoBehaviour
{
    [SerializeField] Image frameImage;
    [SerializeField] Image iconImage;

    public void Bind(WeaponData weapon)
    {
        var cfg = WeaponVisualConfig.Shared;
        if (frameImage != null && cfg != null)
            frameImage.sprite = cfg.GetGradeSprite(weapon.grade);

        if (iconImage != null)
        {
            iconImage.sprite  = weapon.icon;
            iconImage.enabled = weapon.icon != null;
            if (weapon.icon != null) iconImage.SetNativeSize();
        }
    }
}
