using System;
using UnityEngine;
using UnityEngine.UI;

public class StageWeaponSlot : MonoBehaviour
{
    [SerializeField] Image frameImage;
    [SerializeField] Image iconImage;

    Button     _button;
    Action<WeaponData, RectTransform> _onClick;
    WeaponData _weapon;

    void Awake()
    {
        _button = GetComponentInChildren<Button>();
        _button?.onClick.AddListener(OnClick);
    }

    public void Bind(WeaponData weapon, Action<WeaponData, RectTransform> onClick = null)
    {
        _weapon  = weapon;
        _onClick = onClick;

        if (_button != null)
            _button.interactable = onClick != null;

        var cfg = WeaponVisualConfig.Shared;
        if (frameImage != null && cfg != null)
            frameImage.sprite = cfg.GetStageSlotFrameSprite(weapon.grade);

        if (iconImage != null)
        {
            iconImage.sprite  = weapon.icon;
            iconImage.enabled = weapon.icon != null;
            if (weapon.icon != null) iconImage.SetNativeSize();
        }
    }

    void OnClick()
    {
        _onClick?.Invoke(_weapon, GetComponent<RectTransform>());
    }
}
