using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GachaDrawButton : MonoBehaviour
{
    public int          drawCount = 1;
    public EquipBoxType boxType   = EquipBoxType.Normal;

    [SerializeField] HomeShopPanel   panel;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI label;

    int        _totalCost;
    RewardType _ticketType;
    string     _ticketRewardId;

    void Awake()
    {
        if (panel == null) panel = GetComponentInParent<HomeShopPanel>(true);
        if (panel == null) panel = FindObjectOfType<HomeShopPanel>(true);
        if (panel == null) { Debug.LogWarning($"[GachaDrawButton] HomeShopPanel 없음: {gameObject.name}"); return; }

        var row    = FindRow(boxType);
        if (row == null) Debug.LogWarning($"[GachaDrawButton] DB에서 뽑기 행을 찾지 못했습니다. boxType={boxType}, gameObject={gameObject.name}");
        int unit   = row?.f_뽑기비용 ?? DefaultCost(boxType);
        _totalCost = drawCount <= 1 ? unit : unit * (drawCount - 1);

        if (row != null && !string.IsNullOrEmpty(row.f_뽑기권ID))
        {
            _ticketRewardId = row.f_뽑기권ID;
            _ticketType     = RewardItem.ParseId(row.f_뽑기권ID);
        }

        GetComponent<Button>().onClick.AddListener(() => OnClick(panel));
    }

    void OnEnable()  { TicketManager.OnChanged += Refresh; Refresh(); }
    void OnDisable() { TicketManager.OnChanged -= Refresh; }

    void Refresh()
    {
        var cfg      = RewardVisualConfig.Shared;
        int count    = string.IsNullOrEmpty(_ticketRewardId) ? 0 : TicketManager.GetCount(_ticketType);
        bool hasTicket = count > 0;

        if (icon)  icon.sprite = hasTicket ? cfg?.GetIcon(_ticketRewardId) : cfg?.GetIcon(RewardType.Ruby);
        if (label)
        {
            if (hasTicket)
            {
                label.text  = $"<color={(count >= drawCount ? "#00FF00" : "#FF4444")}>{count}</color>/{drawCount}";
            }
            else
            {
                label.text  = _totalCost.ToString();
            }
        }
    }

    void OnClick(HomeShopPanel panel)
    {
        int count = string.IsNullOrEmpty(_ticketRewardId) ? 0 : TicketManager.GetCount(_ticketType);
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
        panel.TryDrawMulti(_totalCost, drawCount, boxType);
        Refresh();
    }

    static DB_상점 FindRow(EquipBoxType boxType)
    {
        string id = boxType switch
        {
            EquipBoxType.Rare => "EQ_EPIC",
            EquipBoxType.Epic => "EQ_SPECIAL",
            _                 => "EQ_NORMAL",
        };
        return DB_상점.FindEntity(e => e.f_뽑기ID == id);
    }

    static int DefaultCost(EquipBoxType boxType) => boxType switch
    {
        EquipBoxType.Rare => 200,
        EquipBoxType.Epic => 500,
        _                 => 80,
    };
}
