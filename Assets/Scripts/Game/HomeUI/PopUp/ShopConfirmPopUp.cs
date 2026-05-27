using System;
using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 상점 슬롯 선택 시 표시되는 구매 확인 팝업.
/// 같은 GameObject에 ScalablePopup 컴포넌트를 추가하세요.
/// ※ 파일명을 DailyShopConfirmPopUp.cs 로 리네임하세요.
/// </summary>
public class DailyShopConfirmPopUp : MonoBehaviour
{
    [Header("아이템 정보")]
    [SerializeField] private Image           itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image           currencyIcon;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("버튼")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private ScalablePopup _popup;
    private Action        _onConfirm;
    private Action        _onCancel;

    private ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    private void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    public void Show(DB_상점 row, Action onConfirm, Action onCancel = null)
    {
        if (Popup == null) { this.DError("ScalablePopup 컴포넌트가 같은 GameObject에 없습니다."); return; }

        _onConfirm = onConfirm;
        _onCancel  = onCancel;

        RefreshUI(row);
        Popup.Show();
    }

    private void RefreshUI(DB_상점 row)
    {
        var cfg = RewardVisualConfig.Shared;

        if (itemIcon)
        {
            itemIcon.sprite = cfg?.GetIcon(row.f_D보상);
            itemIcon.SetNativeSize();
        }

        if (itemNameText)
        {
            string name = DB_상점.FindEntity(e => e.f_보상ID == row.f_D보상)?.f_보상이름 ?? row.f_D보상;
            itemNameText.text = name;
        }

        if (amountText) amountText.text = "×1";

        bool useGold = row.f_D골드 > 0;
        int  price   = useGold ? row.f_D골드 : row.f_D루비;

        if (currencyIcon) currencyIcon.sprite = cfg?.GetIcon(useGold ? RewardType.Gold : RewardType.Ruby);
        if (priceText)    priceText.text      = price.ToString();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Buy);
        Popup.Hide();
        _onConfirm?.Invoke();
    }

    private void OnCancelClicked()
    {
        Popup.Hide();
        _onCancel?.Invoke();
    }
}
