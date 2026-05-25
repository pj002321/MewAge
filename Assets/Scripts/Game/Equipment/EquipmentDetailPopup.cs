using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentDetailPopup : MonoBehaviour
{
    [Header("아이콘 & 이름")]
    public Image           itemIcon;
    public Image           frameIcon;
    public Image           bannerImage;
    public Image           categoryIcon;
    public TextMeshProUGUI nameText;

    [Header("등급 / 문명")]
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI civText;

    [Header("스탯 & 설명")]
    public TextMeshProUGUI statText;
    public TextMeshProUGUI descText;

    [Header("태그 이미지")]
    public Image           tagImage;
    public Image           textIndexImg;

    [Header("버튼")]
    public Button          equipButton;    // 장착 / 교체
    public Button          unequipButton;  // 장착 해제
    public Button          levelUpButton;  // 레벨업
    public Button          closeButton;
    public Button          backdrop;

    [Header("레벨업 정보")]
    public TextMeshProUGUI levelText;      // "Lv. X / 100"
    public TextMeshProUGUI goldCostText;   // "보유/100"
    public TextMeshProUGUI matCostText;    // "보유/10"
    public Image           matIcon;

    EquipmentData _current;
    Animator      _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        closeButton?.onClick.AddListener(Close);
        backdrop?.onClick.AddListener(Close);
        equipButton?.onClick.AddListener(OnEquip);
        unequipButton?.onClick.AddListener(OnUnequip);
        levelUpButton?.onClick.AddListener(OnLevelUp);
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        UIDataBus.OnCurrencyChanged += RefreshLevelUI;
        RefreshLevelUI();
    }

    void OnDisable()
    {
        UIDataBus.OnCurrencyChanged -= RefreshLevelUI;
    }

    /// <summary>보관함 슬롯 클릭 — 장착(교체) 버튼만 표시.</summary>
    public void ShowForBagItem(EquipmentData data)
    {
        _current = data;
        gameObject.SetActive(true);  // TMP material 초기화 후 텍스트 설정

        Populate(data, isEquipped: false);

        var equipped = EquipmentInventory.Shared?.Equipped;
        bool hasSlot = equipped != null && equipped[(int)data.category] != null;
        SetLabel(equipButton,   hasSlot ? "교체" : "장착");
        equipButton?.gameObject.SetActive(true);
        unequipButton?.gameObject.SetActive(false);
        levelUpButton?.gameObject.SetActive(false);

        RefreshLevelUI();
    }

    /// <summary>장착 슬롯 클릭 — 해제 + 레벨업 버튼 표시.</summary>
    public void ShowForEquippedItem(EquipmentData data)
    {
        _current = data;
        gameObject.SetActive(true);  // TMP material 초기화 후 텍스트 설정

        Populate(data, isEquipped: true);

        equipButton?.gameObject.SetActive(false);
        unequipButton?.gameObject.SetActive(true);
        levelUpButton?.gameObject.SetActive(true);

        RefreshLevelUI();
    }

    void Populate(EquipmentData e, bool isEquipped)
    {
        if (e == null) return;

        if (itemIcon)
        {
            itemIcon.enabled = e.Icon != null;
            if (e.Icon != null) itemIcon.sprite = e.Icon;
        }

        if (frameIcon)
        {
            var frameSprite = HomeEquipVisualConfig.Shared?.GetFrame(e.grade);
            if (frameSprite != null) { frameIcon.sprite = frameSprite; frameIcon.color = Color.white; }
            else                       frameIcon.color = EquipmentData.GradeColor(e.grade);
        }

        if (bannerImage)
        {
            var s = HomeEquipVisualConfig.Shared?.GetBanner(e.grade);
            bannerImage.sprite  = s;
            bannerImage.enabled = s != null;
        }

        if (categoryIcon)
        {
            string key = $"{e.category.ToString().ToLower()}_{e.grade.ToString().ToLower()}";
            var    s   = Resources.Load<Sprite>($"Equip/Category/{key}");
            categoryIcon.enabled = s != null;
            if (s != null) categoryIcon.sprite = s;
        }

        if (tagImage)
        {
            var s = HomeEquipVisualConfig.Shared?.GetTag(e.grade);
            tagImage.sprite  = s;
            tagImage.enabled = s != null;
        }

        if (textIndexImg)
        {
            var s = HomeEquipVisualConfig.Shared?.GetIndex(e.grade, isEquipped);
            textIndexImg.sprite  = s;
            textIndexImg.enabled = s != null;
        }

        if (nameText)  nameText.text  = e.itemName;
        if (gradeText) gradeText.text = e.GradeLabel();
        if (civText)   civText.text   = $"{e.civilization}  ·  {e.CategoryLabel()}";
        if (statText)  statText.text  = e.StatDescription();
        if (descText)  descText.text  = e.description;
    }

    void OnEquip()
    {
        if (_current == null) { Close(); return; }
        EquipmentInventory.Shared?.EquipFromBag(_current);
        SoundManager.Shared?.Play(SoundKey.Weapon_Equip);
        Close();
    }

    void OnUnequip()
    {
        if (_current == null) { Close(); return; }
        EquipmentInventory.Shared?.UnequipToBag(_current);
        Close();
    }

    void OnLevelUp()
    {
        var inv = EquipmentInventory.Shared;
        if (inv == null || _current == null) return;
        if (!inv.TryLevelUp(_current))
        {
            PopupBaseNoti.Shared?.Show("장비 강화 재료가 부족합니다");
            return;
        }

        if (statText) statText.text = _current.StatDescription();
        if (_animator != null)
        {
            _animator.enabled = false;
            _animator.enabled = true;
            _animator.SetTrigger("tLevelUp");
        }
        // RefreshLevelUI는 TryLevelUp → UIDataBus.BroadcastCurrencyChanged → OnCurrencyChanged 이벤트로 자동 호출됨
    }

    void RefreshLevelUI()
    {
        if (_current == null) return;
        var inv = EquipmentInventory.Shared;

        if (levelText)    levelText.text    = $"Lv. {_current.level} / {EquipmentData.MaxLevel}";

        if (matIcon)
        {
            var s = RewardVisualConfig.Shared?.GetIcon(EquipmentInventory.CategoryToMatType(_current.category));
            matIcon.sprite  = s;
            matIcon.enabled = s != null;
        }

        int ownGold = RewardManager.Shared?.Gold ?? 0;
        int ownMat  = inv?.GetMatCount(_current.category) ?? 0;

        string goldColor = ownGold >= EquipmentInventory.GoldCostPerLevel ? "#00C853" : "#FF3D00";
        string matColor  = ownMat  >= EquipmentInventory.MatCostPerLevel  ? "#00C853" : "#FF3D00";
        if (goldCostText) goldCostText.text = $"<color={goldColor}>{FormatNumber(ownGold)}</color>/{FormatNumber(EquipmentInventory.GoldCostPerLevel)}";
        if (matCostText)  matCostText.text  = $"<color={matColor}>{ownMat}</color>/{EquipmentInventory.MatCostPerLevel}";

    }

    public void Close() => gameObject.SetActive(false);

    static string FormatNumber(int v)
    {
        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.#") + "M";
        if (v >= 1_000)     return (v / 1_000f).ToString("0.#") + "K";
        return v.ToString();
    }

    static void SetLabel(Button btn, string text)
    {
        if (btn == null) return;
        var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl) lbl.text = text;
    }
}
