using System;
using UnityEngine;
using UnityEngine.UI;

public class EraEnemySlot : MonoBehaviour
{
    [SerializeField] Image frameImage;
    [SerializeField] Image iconImage;
    [SerializeField] Image bossIconImage;

    Button _button;
    Action<string, bool, Sprite, RectTransform> _onClick;
    string _entityId;
    bool   _isBoss;

    void Awake()
    {
        _button = GetComponentInChildren<Button>();
        _button?.onClick.AddListener(OnClick);
    }

    public void Bind(Sprite icon, bool isBoss, string entityId = null, Action<string, bool, Sprite, RectTransform> onClick = null)
    {
        _entityId = entityId;
        _isBoss   = isBoss;
        _onClick  = onClick;

        if (_button != null)
            _button.interactable = onClick != null;

        if (frameImage != null)
        {
            var cfg = UnitVisualConfig.Shared;
            if (cfg == null)
            {
                this.DError("UnitVisualConfig.Shared 로드 실패");
            }
            else
            {
                var frame = isBoss ? cfg.bossEnemyFrame : cfg.normalEnemyFrame;
                if (frame == null) this.DError($"UnitVisualConfig 프레임 스프라이트 미설정: isBoss={isBoss}");
                frameImage.sprite  = frame;
                frameImage.enabled = frame != null;
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
            if (icon != null) iconImage.SetNativeSize();
        }

        if (bossIconImage != null)
            bossIconImage.enabled = isBoss;
    }

    void OnClick()
    {
        _onClick?.Invoke(_entityId, _isBoss, iconImage?.sprite, GetComponent<RectTransform>());
    }
}
