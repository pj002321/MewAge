using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장비 상세 팝업.
/// 인벤토리 슬롯 또는 장착 슬롯 클릭 시 표시.
/// Inspector 구성:
///   - itemIcon    : 장비 아이콘 (Image)
///   - nameText    : 장비 이름
///   - gradeText   : 등급 (등급 색상 적용)
///   - civText     : 문명 · 카테고리
///   - statText    : 스탯 설명 (예: "공격력 +40")
///   - descText    : 장비 설명
///   - actionButton: 장착 / 장착 해제 버튼
///   - actionLabel : actionButton의 텍스트
///   - closeButton : 팝업 닫기 버튼
///   - backdrop    : 배경 클릭 닫기용 투명 버튼
/// </summary>
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
    public Button          actionButton;
    public TextMeshProUGUI actionLabel;
    public Button          closeButton;
    public Button          backdrop;

    EquipmentData _current;
    bool          _isEquipped;

    void Awake()
    {
        closeButton?.onClick.AddListener(Close);
        backdrop?.onClick.AddListener(Close);
        actionButton?.onClick.AddListener(OnAction);
        gameObject.SetActive(false);
    }

    /// <summary>보관함 아이템 선택 시 표시 (장착 버튼).</summary>
    public void ShowForBagItem(EquipmentData data)
    {
        _current    = data;
        _isEquipped = false;
        Populate(data);
        if (actionLabel) actionLabel.text = "장착";
        gameObject.SetActive(true);
    }

    /// <summary>장착 슬롯 선택 시 표시 (장착 해제 버튼).</summary>
    public void ShowForEquippedItem(EquipmentData data)
    {
        _current    = data;
        _isEquipped = true;
        Populate(data);
        if (actionLabel) actionLabel.text = "장착 해제";
        gameObject.SetActive(true);
    }

    void Populate(EquipmentData e)
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
            if (frameSprite != null)
            {
                frameIcon.sprite = frameSprite;
                frameIcon.color  = Color.white;
            }
            else
                frameIcon.color = EquipmentData.GradeColor(e.grade);
        }

        if (bannerImage)
        {
            var bannerSprite = HomeEquipVisualConfig.Shared?.GetBanner(e.grade);
            bannerImage.sprite  = bannerSprite;
            bannerImage.enabled = bannerSprite != null;
        }

        if (categoryIcon)
        {
            string key       = $"{e.category.ToString().ToLower()}_{e.grade.ToString().ToLower()}";
            var    catSprite = Resources.Load<Sprite>($"Equip/Category/{key}");
            categoryIcon.enabled = catSprite != null;
            if (catSprite != null) categoryIcon.sprite = catSprite;
        }

        if (tagImage)
        {
            var tagSprite = HomeEquipVisualConfig.Shared?.GetTag(e.grade);
            tagImage.sprite  = tagSprite;
            tagImage.enabled = tagSprite != null;
        }

        if (textIndexImg)
        {
            var indexSprite = HomeEquipVisualConfig.Shared?.GetIndex(e.grade, _isEquipped);
            textIndexImg.sprite  = indexSprite;
            textIndexImg.enabled = indexSprite != null;
        }

        if (nameText) nameText.text = e.itemName;

        if (gradeText)
            gradeText.text = e.GradeLabel();

        if (civText)
            civText.text = $"{e.civilization}  ·  {e.CategoryLabel()}";

        if (statText)
            statText.text = e.StatDescription();

        if (descText)
            descText.text = e.description;
    }

    void OnAction()
    {
        var inv = EquipmentInventory.Shared;
        if (inv == null || _current == null) { Close(); return; }

        if (_isEquipped)
            inv.UnequipToBag(_current);
        else
            inv.EquipFromBag(_current);

        Close();
    }

    public void Close() => gameObject.SetActive(false);
}
