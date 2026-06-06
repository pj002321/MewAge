using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyInfoPopup : MonoBehaviour
{
    [SerializeField] Image           iconImage;
    [SerializeField] GameObject      bossIndicator;
    [SerializeField] float           iconMaxSize = 100f;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI hpText;
    [SerializeField] TextMeshProUGUI dmgText;
    [SerializeField] TextMeshProUGUI speedText;

    RectTransform _rect;
    ScalablePopup _popup;

    void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _popup = GetComponent<ScalablePopup>();
    }

    public void Show(DB_유닛 unit, Sprite icon)
    {
        if (unit == null) return;
        bossIndicator?.SetActive(false);
        SetIcon(icon);
        if (nameText)  nameText.text  = unit.f_이름;
        if (hpText)    hpText.text    = $"{unit.f_체력}";
        if (dmgText)   dmgText.text   = $"{unit.f_데미지}";
        if (speedText) speedText.text = $"{unit.f_U이속:0.##}";
        _rect.anchoredPosition = Vector2.zero;
        _popup.Show();
    }

    public void ShowBoss(DB_보스 boss, Sprite icon)
    {
        if (boss == null) return;
        bossIndicator?.SetActive(true);
        SetIcon(icon);
        if (nameText)  nameText.text  = boss.f_보스이름;
        if (hpText)    hpText.text    = $"{boss.f_B체력}";
        if (dmgText)   dmgText.text   = $"{boss.f_B접촉데미지}";
        if (speedText) speedText.text = $"{boss.f_B이속}";
        _rect.anchoredPosition = Vector2.zero;
        _popup.Show();
    }

    public void Hide()
    {
        _popup?.HideImmediate();
    }

    void SetIcon(Sprite icon)
    {
        if (iconImage == null) return;
        iconImage.sprite  = icon;
        iconImage.enabled = icon != null;
        if (icon == null) return;

        iconImage.SetNativeSize();
        var rt    = iconImage.rectTransform;
        float max = Mathf.Max(rt.sizeDelta.x, rt.sizeDelta.y);
        if (max > 0f)
            rt.sizeDelta *= iconMaxSize / max;
    }
}
