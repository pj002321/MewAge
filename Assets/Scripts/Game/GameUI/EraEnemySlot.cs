using UnityEngine;
using UnityEngine.UI;

public class EraEnemySlot : MonoBehaviour
{
    [SerializeField] Image frameImage;
    [SerializeField] Image iconImage;
    [SerializeField] Image bossIconImage;

    public void Bind(Sprite icon, bool isBoss)
    {
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
}
