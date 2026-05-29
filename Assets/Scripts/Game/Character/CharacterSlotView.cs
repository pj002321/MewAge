using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class CharacterSlotView : MonoBehaviour
{
    [Header("캐릭터 데이터")]
    public DB_캐릭터 data;

    [Header("미선택 패널 (기본 상태)")]
    public GameObject      deselectPanel;
    public GameObject      fragGroup;
    public Image           fragIcon;
    public TextMeshProUGUI fragCountText;
    public Button          deselectLevelUpBtn;

    [Header("선택 패널")]
    public GameObject      selectPanel;
    public Button          equipButton;
    public TextMeshProUGUI equipLabel;
    public Button          selectLevelUpBtn;

    [Header("캐릭터 비주얼 (Info)")]
    public Image      thumbnailImage;
    public RawImage   portraitImg;
    public Image      gradeImage;
    public GameObject lockImage;
    public GameObject lockedOverlay;

    [Header("장착중 뱃지")]
    public GameObject equippedBadge;

    HomeMemberPanel _panel;
    bool            _selected;
    Canvas          _canvas;
    TextMeshProUGUI _deselectBtnLabel;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _deselectBtnLabel = deselectLevelUpBtn?.GetComponentInChildren<TextMeshProUGUI>();

        GetComponent<Button>().onClick.AddListener(OnSlotClick);
        deselectLevelUpBtn?.onClick.AddListener(OnLevelUpClicked);
        selectLevelUpBtn  ?.onClick.AddListener(OnLevelUpClicked);
        equipButton       ?.onClick.AddListener(OnEquipClicked);

        ApplyDeselectedState();
    }

    public void Init(HomeMemberPanel panel) => _panel = panel;

    public void Bind(DB_캐릭터 row)
    {
        data = row;
        if (row == null) return;

        if (thumbnailImage)
        {
            var spr = MemberVisualConfig.Shared?.GetThumbnail(row.f_캐릭ID);
            thumbnailImage.enabled = spr != null;
            if (spr != null) { thumbnailImage.sprite = spr; thumbnailImage.SetNativeSize(); }
        }

        bool owned = row.f_보유여부;
        if (lockImage)     lockImage    .SetActive(!owned);
        if (lockedOverlay) lockedOverlay.SetActive(!owned);

        Refresh();
    }

    public void Select()
    {
        _selected = true;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder    = 1;
        SetChildrenScale(1.1f);
        Refresh();
    }

    public void Deselect()
    {
        _selected = false;
        _canvas.overrideSorting = false;
        SetChildrenScale(1f);
        Refresh();
    }

    public void OnScrollBegin()
    {
        if (portraitImg)    portraitImg   .gameObject.SetActive(false);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(true);
    }

    public void OnScrollEnd()
    {
        if (_selected) Refresh();
    }

    void OnSlotClick() => _panel?.SelectSlot(this);

    void OnEquipClicked()
    {
        if (data == null || !data.f_보유여부 || IsEquipped()) return;
        PlayerCharacterManager.Shared?.EquipCharacter(data);
        _panel?.SetPortrait(data);
        _panel?.RefreshAllSlots();
    }

    void OnLevelUpClicked()
    {
        if (data == null || !data.f_보유여부) return;
        _panel?.ToggleLevelUpPanel(data);
    }

    public void RefreshState() => Refresh();

    void Refresh()
    {
        if (data == null) return;

        bool owned      = data.f_보유여부;
        bool isEquipped = IsEquipped();

        if (equippedBadge) equippedBadge.SetActive(isEquipped);

        if (_selected) ApplySelectedState(owned, isEquipped);
        else           ApplyDeselectedState(owned, isEquipped);
    }

    void ApplySelectedState(bool owned, bool isEquipped)
    {
        deselectPanel?.SetActive(!owned);
        selectPanel  ?.SetActive(owned);

        if (!owned)
        {
            fragGroup?.SetActive(false);
            deselectLevelUpBtn?.gameObject.SetActive(true);
            if (_deselectBtnLabel) _deselectBtnLabel.text = $"{data?.f_가격}";
            return;
        }

        if (portraitImg)    portraitImg   .gameObject.SetActive(true);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(false);

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(true);
            equipButton.interactable = owned && !isEquipped;
            if (equipLabel) equipLabel.text = !owned     ? "미보유"
                                            : isEquipped ? "장착중"
                                                         : "장착";
        }
        selectLevelUpBtn?.gameObject.SetActive(owned);
    }

    void ApplyDeselectedState(bool owned = false, bool isEquipped = false)
    {
        deselectPanel?.SetActive(true);
        selectPanel  ?.SetActive(false);

        if (portraitImg)    portraitImg   .gameObject.SetActive(false);
        if (thumbnailImage) thumbnailImage.gameObject.SetActive(true);

        deselectLevelUpBtn?.gameObject.SetActive(isEquipped && owned);

        bool showFrag = owned && !isEquipped;
        fragGroup?.SetActive(showFrag);
        if (showFrag) RefreshFragCount();
    }

    void RefreshFragCount()
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

    bool IsEquipped() =>
        data != null && PlayerCharacterManager.Shared?.EquippedCharID == data.f_캐릭ID;

    public void SetCanvasOverride(bool enabled)
    {
        if (_canvas != null) _canvas.overrideSorting = enabled;
    }

    void SetChildrenScale(float scale)
    {
        foreach (Transform child in transform)
            child.localScale = Vector3.one * scale;
    }
}
