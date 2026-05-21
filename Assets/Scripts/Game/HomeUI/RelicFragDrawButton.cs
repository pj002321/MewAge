using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물 조각 뽑기 버튼.
/// KEY_RELIC_NORMAL / KEY_RELIC_EPIC 뽑기권 보유 시 이미지·텍스트 교체.
/// 뽑기권 부족 시 루비 차감.
/// Inspector: boxType, drawCount, resultPopup, icon, label 연결.
/// </summary>
[RequireComponent(typeof(Button))]
public class RelicFragDrawButton : MonoBehaviour
{
    [SerializeField] RelicFragBoxType     boxType     = RelicFragBoxType.Normal;
    [SerializeField] int                  drawCount   = 1;
    [SerializeField] RelicFragResultPopup resultPopup;

    [Header("아이콘 / 비용 텍스트")]
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI label;

    int        _totalCost;
    RewardType _ticketType;
    string     _ticketRewardId;

    void Awake()
    {
        _ticketRewardId = boxType == RelicFragBoxType.Epic ? "KEY_RELIC_EPIC" : "KEY_RELIC_NORMAL";
        _ticketType     = RewardItem.ParseId(_ticketRewardId);

        int unitCost = RelicFragGacha.GetCost(boxType);
        _totalCost   = drawCount <= 1 ? unitCost : unitCost * drawCount;

        if (resultPopup == null)
            resultPopup = GetComponentInParent<RelicFragResultPopup>();

        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnEnable()  { TicketManager.OnChanged += Refresh; Refresh(); }
    void OnDisable() { TicketManager.OnChanged -= Refresh; }

    void Refresh()
    {
        var cfg      = RewardVisualConfig.Shared;
        int count    = TicketManager.GetCount(_ticketType);
        bool hasTicket = count > 0;

        if (icon)
            icon.sprite = hasTicket ? cfg?.GetIcon(_ticketRewardId) : cfg?.GetIcon(RewardType.Ruby);

        if (label)
        {
            if (hasTicket)
                label.text = $"<color={(count >= drawCount ? "#00FF00" : "#FF4444")}>{count}</color>/{drawCount}";
            else
                label.text = _totalCost.ToString();
        }
    }

    void OnClick()
    {
        if (resultPopup != null && resultPopup.gameObject.activeInHierarchy) return;

        int count = TicketManager.GetCount(_ticketType);
        if (count >= drawCount)
        {
            for (int i = 0; i < drawCount; i++) TicketManager.TrySpend(_ticketType);
        }
        else
        {
            if (!(RewardManager.Shared?.SpendRuby(_totalCost) ?? false))
            {
                PopupBaseNoti.Shared?.Show("루비가 부족합니다");
                return;
            }
        }

        if (drawCount <= 1)
        {
            var entry = RelicFragGacha.Draw(boxType);
            if (entry.data != null)
                resultPopup?.ShowSingle(entry.data, entry.count);
        }
        else
        {
            var results = RelicFragGacha.DrawMulti(boxType, drawCount);
            resultPopup?.ShowMulti(results);
        }

        Refresh();
    }
}
