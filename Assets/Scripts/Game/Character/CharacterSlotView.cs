using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class CharacterSlotView : MonoBehaviour
{
    [Header("슬롯 UI")]
    public DB_캐릭터        data;
    public Image            slotBackground;
    public Image            thumbnailImage;
    public TextMeshProUGUI  gradeText;

    [Header("잠금 상태 (미보유)")]
    public GameObject       lockImage;
    public GameObject       lockedOverlay;

    [Header("장착중 뱃지")]
    public GameObject       equippedBadge;

    [Header("영웅조각 표시 (비선택 + 미장착 시)")]
    public GameObject       fragGroup;      // fragIcon + fragCountText를 묶은 부모 오브젝트
    public Image            fragIcon;
    public TextMeshProUGUI  fragCountText;

    [Header("선택 시 포트레이트")]
    public RawImage         portraitImg;

    [Header("액션 버튼 (장착 / 레벨업 / 구매 겸용)")]
    public Button           actionButton;

    HomeMemberPanel         _panel;
    TextMeshProUGUI         _actionLabel;
    bool                    _selected;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnSlotClick);
        actionButton?.onClick.AddListener(OnActionClicked);
        _actionLabel = actionButton?.GetComponentInChildren<TextMeshProUGUI>();
        ApplyDeselectedState();
    }

    public void Init(HomeMemberPanel panel) => _panel = panel;

    // ── 데이터 바인딩 ─────────────────────────────────────────────
    public void Bind(DB_캐릭터 row)
    {
        data = row;
        if (row == null) return;

        if (slotBackground)
        {
            slotBackground.sprite = MemberVisualConfig.Shared?.GetSlotSprite(row.f_등급, selected: _selected);
            slotBackground.color  = Color.white;
        }

        if (thumbnailImage)
        {
            var spr = MemberVisualConfig.Shared?.GetThumbnail(row.f_캐릭ID);
            thumbnailImage.enabled = spr != null;
            if (spr != null) { thumbnailImage.sprite = spr; thumbnailImage.SetNativeSize(); }
        }

        if (gradeText) gradeText.text = row.f_등급;

        bool owned = row.f_보유여부;
        if (lockImage)     lockImage    .SetActive(!owned);
        if (lockedOverlay) lockedOverlay.SetActive(!owned);

        Refresh();
    }

    // ── 선택 상태 ────────────────────────────────────────────────
    public void Select()
    {
        _selected = true;
        if (slotBackground && data != null)
            slotBackground.sprite = MemberVisualConfig.Shared?.GetSlotSprite(data.f_등급, selected: true);
        SetChildrenScale(1.1f);
        Refresh();
    }

    public void Deselect()
    {
        _selected = false;
        if (slotBackground && data != null)
            slotBackground.sprite = MemberVisualConfig.Shared?.GetSlotSprite(data.f_등급, selected: false);
        SetChildrenScale(1f);
        Refresh();
    }

    // ── 액션 버튼 ────────────────────────────────────────────────
    void OnSlotClick() => _panel?.SelectSlot(this);

    void OnActionClicked()
    {
        if (data == null) return;

        bool owned      = data.f_보유여부;
        bool isEquipped = IsEquipped();

        if (!owned)
        {
            Debug.Log($"[Member] {data.f_이름} 구매 시도 | 가격: {data.f_가격} 보석");
            // TODO: 보석 차감 → data.f_보유여부 = true → Bind(data)
        }
        else if (isEquipped)
        {
            _panel?.ToggleLevelUpPanel(data);
        }
        else
        {
            PlayerCharacterManager.Shared?.EquipCharacter(data);
            _panel?.SetPortrait(data);
            _panel?.RefreshAllSlots();
        }
    }

    public void HidePortrait()
    {
        if (portraitImg)    portraitImg   .gameObject.SetActive(false);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(true);
    }

    // ── 전체 상태 갱신 ───────────────────────────────────────────
    public void RefreshState() => Refresh();

    void Refresh()
    {
        if (data == null) return;

        bool owned      = data.f_보유여부;
        bool isEquipped = IsEquipped();

        // 장착중 뱃지
        if (equippedBadge) equippedBadge.SetActive(isEquipped);

        if (_selected)
            ApplySelectedState(owned, isEquipped);
        else
            ApplyDeselectedState(owned, isEquipped);
    }

    void ApplySelectedState(bool owned, bool isEquipped)
    {
        if (portraitImg)    portraitImg   .gameObject.SetActive(true);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(false);
        if (fragGroup)      fragGroup     .SetActive(false);

        actionButton?.gameObject.SetActive(true);
        if (_actionLabel)
            _actionLabel.text = !owned     ? $"x{data.f_가격}"
                              : isEquipped ? "레벨업"
                                           : "장착";
    }

    void ApplyDeselectedState(bool owned = false, bool isEquipped = false)
    {
        if (portraitImg)    portraitImg   .gameObject.SetActive(false);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(true);
        actionButton?.gameObject.SetActive(false);

        // 영웅조각: 비선택 + 보유 + 미장착일 때만 표시
        bool showFrag = owned && !isEquipped;
        if (fragGroup) fragGroup.SetActive(showFrag);

        if (showFrag)
        {
            if (fragIcon)
            {
                var spr = MemberVisualConfig.Shared?.GetFragmentIcon(data?.f_캐릭ID);
                fragIcon.gameObject.SetActive(spr != null);
                if (spr != null) { fragIcon.sprite = spr; fragIcon.SetNativeSize(); }
            }

            if (fragCountText)
            {
                int have = CharacterLevelUpPanel.GetHeroFragCount(data.f_캐릭ID);
                int need = CharacterLevelUpPanel.FragCost;
                string col = have >= need ? "#00C800" : "#FF3232";
                fragCountText.text = $"<color={col}>{have}</color>/{need}";
            }
        }
    }

    bool IsEquipped() =>
        data != null && PlayerCharacterManager.Shared?.EquippedCharID == data.f_캐릭ID;

    void SetChildrenScale(float scale)
    {
        foreach (Transform child in transform)
            child.localScale = Vector3.one * scale;
    }
}
